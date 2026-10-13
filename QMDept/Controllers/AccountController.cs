using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.Controllers;

/// <summary>
/// Đăng nhập bằng mã nhân viên + mật khẩu (port loginForm / bootForm của artifact):
/// - Chưa có TP nào → form "Thiết lập tài khoản Trưởng phòng" (bootstrap).
/// - Lần đầu đăng nhập (chưa có mật khẩu): mật khẩu nhập vào trở thành mật khẩu mới (tối thiểu 4 ký tự).
/// - Quên mật khẩu: TP đặt lại (xóa PasswordHash) ở Kế hoạch &amp; duyệt → Nhân sự.
/// Mật khẩu băm bằng PasswordHasher (PBKDF2) của ASP.NET Core Identity.
/// </summary>
public class AccountController(AppDbContext db, IPasswordHasher<Person> hasher) : Controller
{
    public const int MinPassword = AccountRules.MinPassword;

    public sealed class LoginVm
    {
        public bool NeedBootstrap { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Error { get; set; }
    }

    // Chưa có Trưởng phòng và chưa có Quản trị → cho tạo TP đầu tiên.
    async Task<bool> NeedBootstrapAsync() => !await db.People.AnyAsync(p => (p.Role == Role.Tp || p.Role == Role.Admin) && !p.IsDeleted && p.Code != "");

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        return View(new LoginVm { NeedBootstrap = await NeedBootstrapAsync() });
    }

    [HttpPost]
    public async Task<IActionResult> Login(string? code, string? password)
    {
        code = AccountRules.NormalizeCode(code);
        password ??= "";
        var vm = new LoginVm { Code = code };
        var p = await db.People.FirstOrDefaultAsync(x => x.Code == code && !x.IsDeleted);
        if (p is null) { vm.Error = "Không tìm thấy mã nhân viên " + code; return View(vm); }
        if (password.Length < MinPassword) { vm.Error = $"Mật khẩu tối thiểu {MinPassword} ký tự"; return View(vm); }
        if (string.IsNullOrEmpty(p.PasswordHash))
        {
            p.PasswordHash = hasher.HashPassword(p, password);
            await db.SaveChangesAsync();
            TempData["toast"] = "Đã đặt mật khẩu cho " + code;
        }
        else
        {
            var r = hasher.VerifyHashedPassword(p, p.PasswordHash, password);
            if (r == PasswordVerificationResult.Failed) { vm.Error = "Sai mật khẩu"; return View(vm); }
            if (r == PasswordVerificationResult.SuccessRehashNeeded) { p.PasswordHash = hasher.HashPassword(p, password); await db.SaveChangesAsync(); }
        }
        await SignInAsync(p);
        return Redirect(ScopeResolver.HomeOf(p.Role));
    }

    [HttpPost]
    public async Task<IActionResult> Bootstrap(string? name, string? code, string? password)
    {
        name = (name ?? "").Trim();
        code = AccountRules.NormalizeCode(code);
        password ??= "";
        var vm = new LoginVm { NeedBootstrap = true, Code = code, Name = name };
        if (!await NeedBootstrapAsync()) return RedirectToAction(nameof(Login));
        if (name.Length == 0 || code.Length == 0) { vm.Error = "Nhập họ tên và mã nhân viên"; return View("Login", vm); }
        if (password.Length < MinPassword) { vm.Error = $"Mật khẩu tối thiểu {MinPassword} ký tự"; return View("Login", vm); }
        try { await AccountRules.ValidateCodeAsync(db, code); }
        catch (BizException ex) { vm.Error = ex.Message; return View("Login", vm); }
        var p = new Person
        {
            Name = name, Code = code, Role = Role.Tp, CreatedAt = VnTime.UtcNow,
            SortOrder = await db.People.CountAsync() + 1,
        };
        p.PasswordHash = hasher.HashPassword(p, password);
        db.People.Add(p);
        await db.SaveChangesAsync();
        TempData["toast"] = "Đã tạo tài khoản Trưởng phòng";
        await SignInAsync(p);
        return Redirect(ScopeResolver.HomeOf(Role.Tp));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        foreach (var c in Request.Cookies.Keys.Where(k => k.StartsWith("qm_as_"))) Response.Cookies.Delete(c);
        return RedirectToAction(nameof(Login));
    }

    async Task SignInAsync(Person p)
    {
        var id = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, p.Id.ToString()),
            new Claim(ClaimTypes.Name, p.Name),
            new Claim(ClaimTypes.Role, p.Role.ToString()),
            new Claim(ScopeResolver.StampClaim, p.SecurityStamp),
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }
}
