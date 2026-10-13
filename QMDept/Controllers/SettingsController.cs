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
public class SettingsController : AppController
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
