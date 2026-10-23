using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.Controllers;

/// <summary>Hộp thông báo của tài khoản đang đăng nhập (yêu cầu 2026-10-23): mọi thông báo đẩy được lưu, mở trang này là đánh dấu đã xem.</summary>
[AnyUiRole]
public class InboxController : AppController
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var me = Scope.Account.Id;
        var items = await Db.InboxItems.AsNoTracking().Where(x => x.PersonId == me).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(100).ToListAsync();
        var unreadIds = items.Where(x => x.ReadAt is null).Select(x => x.Id).ToList();
        ViewBag.Unread = unreadIds.ToHashSet();
        if (unreadIds.Count > 0)
            await Db.InboxItems.Where(x => x.PersonId == me && x.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, VnTime.UtcNow));
        return View(items);
    }
}
