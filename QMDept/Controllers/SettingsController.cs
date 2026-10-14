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
/// Cài đặt thông báo đẩy (SignalR) của tài khoản đang đăng nhập — bổ sung theo yêu cầu chủ dự án 2026-10-13:
/// bật / tắt thông báo đẩy, chọn các loại thông báo muốn nhận (chỉ hiện loại liên quan tới vai trò),
/// cho phép thông báo của hệ điều hành (trình duyệt hỏi quyền — phía client, site.js).
/// Link luôn kèm ?as=me để trang luôn là của chính mình (không bị chặn khi đang xem màn hình cấp dưới).
/// </summary>
[AnyUiRole]
public class SettingsController(IPasswordHasher<Person> hasher) : AppController
{
    public sealed class SettingsVm
    {
        public bool Enabled { get; init; }
        public required List<(NotifyCatalog.Item Item, bool On)> Types { get; init; }
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var acc = Scope.Account;
        var s = await Db.NotifySettings.AsNoTracking().FirstOrDefaultAsync(x => x.PersonId == acc.Id);
        ViewData["Title"] = "Cài đặt";
        return View(new SettingsVm
        {
            Enabled = s?.Enabled ?? true,
            Types = NotifyCatalog.ForRole(acc.Role)
                .Select(i => (i, s is null || !s.DisabledTypes.Split(',').Contains(i.Type.ToString()))).ToList(),
        });
    }

    /// <summary>Tự đổi mật khẩu: kiểm tra mật khẩu hiện tại; đổi SecurityStamp (đăng xuất các phiên khác) rồi đăng nhập lại phiên này.</summary>
    [HttpPost]
    public Task<IActionResult> ChangePassword(string? current, string? password, string? confirm) => Do(async () =>
    {
        var p = await Db.People.FirstOrDefaultAsync(x => x.Id == Scope.Account.Id && !x.IsDeleted) ?? throw new BizException("Không tìm thấy tài khoản");
        if (string.IsNullOrEmpty(p.PasswordHash) || hasher.VerifyHashedPassword(p, p.PasswordHash, current ?? "") == PasswordVerificationResult.Failed)
            throw new BizException("Mật khẩu hiện tại không đúng");
        AccountRules.ValidatePassword(password);
        if (password != confirm) throw new BizException("Mật khẩu nhập lại không khớp");
        AccountRules.SetPassword(p, password, hasher);
        await Db.SaveChangesAsync();
        var id = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, p.Id.ToString()), new Claim(ClaimTypes.Name, p.Name),
            new Claim(ClaimTypes.Role, p.Role.ToString()), new Claim(ScopeResolver.StampClaim, p.SecurityStamp),
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(id),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }, "Đã đổi mật khẩu", "/Settings?as=me");

    /// <summary>Lưu: enabled + danh sách loại được chọn (types). Loại không hiện cho vai trò này giữ nguyên.</summary>
    [HttpPost]
    public Task<IActionResult> Save(bool enabled, List<string>? types) => Do(async () =>
    {
        var acc = Scope.Account;
        var mine = NotifyCatalog.ForRole(acc.Role).Select(i => i.Type).ToList();
        var chosen = (types ?? []).Select(t => Enum.TryParse<NotifyType>(t, out var v) ? v : (NotifyType?)null).OfType<NotifyType>().ToHashSet();
        var s = await Db.NotifySettings.FirstOrDefaultAsync(x => x.PersonId == acc.Id);
        if (s is null) { s = new NotifySetting { PersonId = acc.Id }; Db.NotifySettings.Add(s); }
        var keep = s.DisabledTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => Enum.TryParse<NotifyType>(t, out var v) && !mine.Contains(v));
        s.DisabledTypes = string.Join(",", keep.Concat(mine.Where(t => !chosen.Contains(t)).Select(t => t.ToString())));
        s.Enabled = enabled;
        s.UpdatedAt = VnTime.UtcNow;
        await Db.SaveChangesAsync();
    }, "Đã lưu cài đặt thông báo", "/Settings?as=me");
}
