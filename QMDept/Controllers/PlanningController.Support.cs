using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Leader nhóm được xin hỗ trợ nhân sự (yêu cầu 2026-10-23): Leader dự án đề xuất (báo cáo đầu ngày, mục "Nhóm khác") → Leader trực tiếp của họ duyệt kế hoạch
/// (PlanningController.DecidePlan tạo <see cref="SupportRequest"/>) → Leader nhóm được xin XÁC NHẬN + chọn nhân viên hỗ trợ ở đây → thông báo cho người đề xuất,
/// Leader của họ và nhân viên được chọn. Không qua Trưởng phòng.
/// </summary>
public partial class PlanningController
{
    /// <summary>GET /Planning/Support?f=pending|done — yêu cầu hỗ trợ gửi tới nhóm của tôi trong ngày đang chọn.</summary>
    [HttpGet]
    public async Task<IActionResult> Support(string? f)
    {
        var me = Scope.Account.Id;
        await D.EnsureAsync(Date, Date);
        var all = await Db.SupportRequests.AsNoTracking().Where(x => x.GroupLeaderId == me).ToListAsync();
        var pending = f != "done";
        var today = all.Where(x => x.Date == Date).ToList();
        return View(new PlanningSupportVm
        {
            Filter = pending ? "pending" : "done",
            Requests = today.Where(x => (x.Confirmed || x.Declined) != pending).OrderBy(x => x.CreatedAt).ToList(),
            PendingTotal = today.Count(x => !x.Confirmed && !x.Declined),
            DoneTotal = today.Count(x => x.Confirmed || x.Declined),
            OtherPending = all.Where(x => !x.Confirmed && !x.Declined && x.Date != Date).GroupBy(x => x.Date).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToList(),
        });
    }

    /// <summary>POST /Planning/ConfirmSupport — Leader nhóm được xin xác nhận và chọn 1..Qty nhân viên của nhóm mình sẽ hỗ trợ.</summary>
    [HttpPost]
    public async Task<IActionResult> ConfirmSupport(int id) => await Do(async () =>
    {
        RequireLead();
        var me = Scope.Account;
        var x = await Db.SupportRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new BizException("Không tìm thấy yêu cầu hỗ trợ — có thể kế hoạch đã được gửi lại, tải lại trang");
        if (x.GroupLeaderId != me.Id) throw new BizException("Yêu cầu này không gửi tới nhóm của bạn");
        if (x.Confirmed || x.Declined) throw new BizException("Yêu cầu này đã được xử lý");
        var picked = new List<int>();
        foreach (var raw in Request.Form["emp"]) if (int.TryParse(raw, out var e) && !picked.Contains(e)) picked.Add(e);
        if (picked.Count == 0) throw new BizException("Chọn ít nhất một nhân viên sẽ hỗ trợ");
        if (picked.Count > x.Qty) throw new BizException($"Chỉ xin {x.Qty} người — bạn chọn {picked.Count}");
        var team = await Db.People.AsNoTracking().Where(p => p.Role == Role.Staff && p.LeaderId == me.Id && !p.IsDeleted && picked.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        if (team.Count != picked.Count) throw new BizException("Chỉ chọn được nhân viên trong nhóm của bạn");
        x.Confirmed = true; x.StaffIds = string.Join(",", picked); x.ConfirmedBy = Scope.ActorName; x.ConfirmedAt = VnTime.UtcNow;
        await Db.SaveChangesAsync();
        await Notify.SupportConfirmedAsync(x.RequesterId, me.Id, picked, x.Date);
        Toast("Đã xác nhận — thông tin đã gửi cho người đề xuất và nhân viên được chọn");
    });

    /// <summary>POST /Planning/DeclineSupport — Leader nhóm được xin không đáp ứng được (bắt buộc lý do) → báo người đề xuất + Leader trực tiếp của họ (yêu cầu 2026-10-24).</summary>
    [HttpPost]
    public async Task<IActionResult> DeclineSupport(int id, string? reason) => await Do(async () =>
    {
        RequireLead();
        var x = await Db.SupportRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new BizException("Không tìm thấy yêu cầu hỗ trợ — có thể kế hoạch đã được gửi lại, tải lại trang");
        if (x.GroupLeaderId != Scope.Account.Id) throw new BizException("Yêu cầu này không gửi tới nhóm của bạn");
        if (x.Confirmed || x.Declined) throw new BizException("Yêu cầu này đã được xử lý");
        var why = PlanningApproval.CleanNote(reason) ?? throw new BizException("Nhập lý do không đáp ứng được");
        x.Declined = true; x.DeclineReason = why.Length > 500 ? why[..500] : why; x.ConfirmedBy = Scope.ActorName; x.ConfirmedAt = VnTime.UtcNow;
        await Db.SaveChangesAsync();
        await Notify.SupportDeclinedAsync(x.RequesterId, Scope.Account.Id, x.Date, x.DeclineReason!);
        Toast("Đã từ chối — thông tin đã gửi cho người đề xuất");
    });
}
