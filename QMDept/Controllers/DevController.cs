using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.Controllers;

/// <summary>
/// Đổi nhanh tài khoản khi chạy thử trên máy (xem <see cref="DevSwitch"/>). Môi trường Production: mọi action trả 404.
/// </summary>
[AllowAnonymous]
public class DevController(AppDbContext db, DevSwitch dev) : Controller
{
    /// <summary>GET /Dev/As?id= — đăng nhập luôn với tài khoản id (không cần mật khẩu).</summary>
    [HttpGet]
    public async Task<IActionResult> As(int? id)
    {
        if (!dev.Enabled) return NotFound();
        var p = await db.People.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (p is null) return Redirect("/");
        await SignInAsync(HttpContext, p);
        Response.Cookies.Delete(ScopeResolver.ViewCookie);
        return Redirect(ScopeResolver.HomeOf(p.Role));
    }

    /// <summary>GET /Dev/Reminders — XEM TRƯỚC các thông báo nhắc việc sẽ đẩy (không gửi thật, bỏ qua khung giờ) — chỉ khi bật thanh DEV.</summary>
    [HttpGet]
    public async Task<IActionResult> Reminders([FromServices] ReminderService svc)
    {
        if (!dev.Enabled) return NotFound();
        var list = new List<string>();
        var n = await svc.RunOnceAsync(HttpContext.RequestAborted, dryRun: true, preview: list);
        return Content($"{n} thông báo sẽ được đẩy:\n" + string.Join("\n", list), "text/plain; charset=utf-8");
    }

    /// <summary>Đăng nhập tự động bằng một tài khoản (Trưởng phòng đầu tiên, không có thì Quản trị) — thay màn hình đăng nhập.</summary>
    public static async Task<bool> AutoSignInAsync(HttpContext http, AppDbContext db)
    {
        var p = await db.People.Where(x => !x.IsDeleted && (x.Role == Role.Manager || x.Role == Role.Admin))
            .OrderBy(x => x.Role == Role.Manager ? 0 : 1).ThenBy(x => x.Id).FirstOrDefaultAsync();
        if (p is null) return false;
        await SignInAsync(http, p);
        return true;
    }

    static Task SignInAsync(HttpContext http, Person p)
    {
        var id = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, p.Id.ToString()),
            new Claim(ClaimTypes.Name, p.Name),
            new Claim(ClaimTypes.Role, p.Role.ToString()),
            new Claim(ScopeResolver.StampClaim, p.SecurityStamp),
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }
}
