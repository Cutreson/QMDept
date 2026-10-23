using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Leader → Kế hoạch &amp; duyệt: tab con "Kế hoạch" (ldApprove) và "Báo cáo" (ldEodApprove + carrySection).
/// POST port từ các handler eodDecide, eodCarry, carryOk, carryAll, carryUndo, eodUndo, ldDecide, ldUndo, ldNote, xDecide.
/// Mọi POST: RequireLead() + RequireInScopeAsync(NV) trước khi ghi.
/// </summary>
public partial class PlanningController
{
    // ======================= GET =======================

    /// <summary>GET /Planning/Approvals — đã gộp vào "Kế hoạch đề xuất" (/Planning/Proposals, yêu cầu 2026-10-19); giữ route cũ cho liên kết thông báo (ap=pending → mục Chờ duyệt).</summary>
    [HttpGet]
    public IActionResult Approvals(string? ap) => Redirect(Ui.To(Request, "/Planning/Proposals", ("f", ap == "pending" ? "pending" : null)));

    /// <summary>GET /Planning/Reports — ldEodApprove() + carrySection(). Nạp thêm 14 ngày sau (ngày chuyển tiếp).</summary>
    [HttpGet]
    public async Task<IActionResult> Reports()
    {
        await D.EnsureAsync(Date, Date.AddDays(14));
        ViewData["Title"] = "Kế hoạch & duyệt";
        return View(PlanningReportsVm.Build(D, Date));
    }

    /// <summary>
    /// Leader trực tiếp đã DUYỆT kế hoạch có xin người hỗ trợ từ nhóm khác (SupportPick.Qg: id Leader nhóm → số lượng) → mỗi nhóm một yêu cầu cho Leader nhóm đó
    /// xác nhận + chọn nhân viên (yêu cầu 2026-10-23). Không qua Trưởng phòng.
    /// </summary>
    async Task CreateSupportRequestsAsync(DayPlan p)
    {
        var pick = StaffSupportPick.Parse(p.SupportPick);
        if (pick is null || pick.Qg.Count == 0) return;
        Db.SupportRequests.RemoveRange(await Db.SupportRequests.Where(x => x.PlanId == p.Id).ToListAsync());
        var made = new List<(int Leader, int Qty)>();
        foreach (var (k, q) in pick.Qg)
        {
            if (!int.TryParse(k, out var gl) || q <= 0) continue;
            if (!await Db.People.AnyAsync(x => x.Id == gl && x.Role == Role.Leader && !x.IsDeleted)) continue;
            Db.SupportRequests.Add(new SupportRequest { PlanId = p.Id, Date = p.Date, RequesterId = p.EmployeeId, GroupLeaderId = gl, Qty = q, Reason = pick.Reason, CreatedAt = VnTime.UtcNow });
            made.Add((gl, q));
        }
        if (made.Count == 0) return;
        await Db.SaveChangesAsync();
        foreach (var (gl, q) in made) await Notify.SupportRequestedAsync(gl, p.EmployeeId, p.Date, q);
    }

    // ======================= Kế hoạch đầu ngày (ldDecide / ldUndo / ldNote) =======================

    /// <summary>ldDecide: v=ok duyệt; v=no từ chối (bắt buộc lý do / hướng dẫn sửa).</summary>
    [HttpPost]
    public async Task<IActionResult> DecidePlan(int id, string? v, string? note) => await Do(async () =>
    {
        RequireLead();
        var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
        await RequireInScopeAsync(p.EmployeeId);
        var dec = v switch { "ok" => Decision.Ok, "no" => Decision.No, _ => throw new BizException("Thao tác không hợp lệ") };
        if (p.LeaderDecision is not null) throw new BizException("Kế hoạch đã được Leader quyết định");
        var n = PlanningApproval.CleanNote(note);
        if (dec == Decision.No && n is null) throw new BizException("Nhập lý do / hướng dẫn sửa");
        p.LeaderDecision = dec;
        p.LeaderBy = Scope.ActorName;
        p.LeaderAt = VnTime.UtcNow;
        if (note is not null) p.LeaderNote = n;   // artifact: chỉ ghi leaderNote khi có hộp nhập (từ chối)
        await Db.SaveChangesAsync();
        await Notify.PlanDecidedAsync(p.EmployeeId, p.Date, dec == Decision.Ok ? "Leader đã duyệt" : "Leader từ chối", p.LeaderNote);
        if (dec == Decision.Ok && Dept.NeedsManager(p)) await Notify.SupportNeedsManagerAsync(p.EmployeeId, p.Date);
        if (dec == Decision.Ok) await CreateSupportRequestsAsync(p);
        Toast(dec == Decision.Ok ? "Đã duyệt kế hoạch" : "Đã từ chối kế hoạch");
    });

    /// <summary>ldUndo: hoàn tác quyết định của Leader (chỉ khi TP chưa quyết định).</summary>
    [HttpPost]
    public async Task<IActionResult> UndoPlanDecision(int id) => await Do(async () =>
    {
        RequireLead();
        var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
        await RequireInScopeAsync(p.EmployeeId);
        if (p.ManagerDecision is not null) throw new BizException("Trưởng phòng đã quyết định, không hoàn tác được");
        if (p.LeaderDecision is null) return;
        p.LeaderDecision = null;
        Db.SupportRequests.RemoveRange(await Db.SupportRequests.Where(x => x.PlanId == p.Id).ToListAsync());   // hoàn tác duyệt → các yêu cầu hỗ trợ nhóm khác bị hủy
        p.LeaderAt = null;
        await Db.SaveChangesAsync();
        await Notify.PlanDecidedAsync(p.EmployeeId, p.Date, "Leader hoàn tác quyết định, chờ duyệt lại");
    }, "Đã hoàn tác");

    /// <summary>ldNote: góp ý cho kế hoạch (không bắt buộc nội dung; rỗng = xóa góp ý).</summary>
    [HttpPost]
    public async Task<IActionResult> CommentPlan(int id, string? note) => await Do(async () =>
    {
        RequireLead();
        var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
        await RequireInScopeAsync(p.EmployeeId);
        p.LeaderNote = PlanningApproval.CleanNote(note);
        if (string.IsNullOrEmpty(p.LeaderBy)) p.LeaderBy = Scope.ActorName;
        await Db.SaveChangesAsync();
        if (p.LeaderNote is not null) await Notify.PlanDecidedAsync(p.EmployeeId, p.Date, "Leader góp ý", p.LeaderNote);
    }, "Đã gửi góp ý");

    // ======================= Phát sinh (xDecide) =======================

    /// <summary>xDecide v=ok: duyệt phát sinh → tạo việc nguồn Extra (Phát sinh) cho NV.</summary>
    [HttpPost]
    public async Task<IActionResult> ApproveExtra(int id) => await Do(async () =>
    {
        RequireLead();
        var x = await Db.Extras.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id) ?? throw new BizException("Không tìm thấy phát sinh");
        await RequireInScopeAsync(x.EmployeeId);
        if (x.Status != ExtraStatus.Pending) return;   // artifact: đã xử lý → bỏ qua
        await Flow.ApproveExtraAsync(id, Scope.ActorName, Role.Leader);
        Toast("Đã duyệt phát sinh");
    });

    /// <summary>xDecide v=no: từ chối phát sinh (bắt buộc lý do).</summary>
    [HttpPost]
    public async Task<IActionResult> RejectExtra(int id, string? reason) => await Do(async () =>
    {
        RequireLead();
        var x = await Db.Extras.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id) ?? throw new BizException("Không tìm thấy phát sinh");
        await RequireInScopeAsync(x.EmployeeId);
        if (x.Status != ExtraStatus.Pending) return;
        var r = PlanningApproval.CleanNote(reason) ?? throw new BizException("Nhập lý do từ chối");
        await Flow.RejectExtraAsync(id, r, Scope.ActorName, Role.Leader);
        Toast("Đã từ chối");
    });

    // ======================= Báo cáo cuối ngày (eodDecide / eodUndo) =======================

    /// <summary>eodDecide: v=ok duyệt báo cáo + duyệt mọi đề xuất chuyển của NV từ ngày đó; v=no yêu cầu bổ sung (bắt buộc nội dung).</summary>
    [HttpPost]
    public async Task<IActionResult> DecideReport(int id, string? v, string? note) => await Do(async () =>
    {
        RequireLead();
        var r = await Db.DayReports.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy báo cáo");
        await RequireInScopeAsync(r.EmployeeId);
        if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
        if (r.LeaderDecision is not null) throw new BizException("Báo cáo đã được Leader quyết định");
        var now = VnTime.UtcNow;
        var actor = Scope.ActorName;
        if (v == "ok")
        {
            r.LeaderDecision = Decision.Ok;
            r.LeaderBy = actor;
            r.LeaderAt = now;
            var carries = await Db.WorkTasks
                .Where(t => t.Source == TaskSource.CarryOver && t.FromDate == r.Date && t.EmployeeId == r.EmployeeId && !t.CarryOk)
                .ToListAsync();
            foreach (var t in carries) { t.CarryOk = true; t.CarryBy = actor; t.CarryAt = now; await Flow.SyncCarryToPlanAsync(t); }
            await Db.SaveChangesAsync();
            await Notify.ReportDecidedAsync(r.EmployeeId, r.Date, true);
            foreach (var t in carries) await Notify.CarryOverAsync(t.EmployeeId, t.Date, t.Title, "đã được duyệt, đã vào lịch");
            Toast("Đã duyệt báo cáo cuối ngày và kế hoạch chuyển tiếp");
        }
        else
        {
            var n = PlanningApproval.CleanNote(note) ?? throw new BizException("Nhập nội dung cần bổ sung");
            r.LeaderDecision = Decision.No;
            r.LeaderNote = n;
            r.LeaderBy = actor;
            r.LeaderAt = now;
            await Db.SaveChangesAsync();
            await Notify.ReportDecidedAsync(r.EmployeeId, r.Date, false, n);
            Toast("Đã gửi yêu cầu bổ sung");
        }
    });

    /// <summary>eodUndo: hoàn tác quyết định báo cáo cuối ngày.</summary>
    [HttpPost]
    public async Task<IActionResult> UndoReportDecision(int id) => await Do(async () =>
    {
        RequireLead();
        var r = await Db.DayReports.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy báo cáo");
        await RequireInScopeAsync(r.EmployeeId);
        if (r.LeaderDecision is null) return;
        r.LeaderDecision = null;
        r.LeaderAt = null;
        await Db.SaveChangesAsync();
        await Notify.ReportUndoneAsync(r.EmployeeId, r.Date);
    }, "Đã hoàn tác");

    // ======================= Đề xuất chuyển kế hoạch (carryOk / carryAll / carryUndo / eodCarry) =======================

    /// <summary>Việc chuyển tiếp (tracked) trong phạm vi; ném lỗi nếu không có / không phải việc chuyển tiếp.</summary>
    async Task<WorkTask> LoadCarryTaskAsync(int id)
    {
        RequireLead();
        var n = await Db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id && t.Source == TaskSource.CarryOver)
            ?? throw new BizException("Không tìm thấy đề xuất chuyển");
        await RequireInScopeAsync(n.EmployeeId);
        return n;
    }

    /// <summary>carryOk: duyệt một đề xuất chuyển → việc vào lịch NV.</summary>
    [HttpPost]
    public async Task<IActionResult> ApproveCarry(int id) => await Do(async () =>
    {
        var n = await LoadCarryTaskAsync(id);
        if (n.CarryOk) return;
        n.CarryOk = true;
        n.CarryBy = Scope.ActorName;
        n.CarryAt = VnTime.UtcNow;
        await Flow.SyncCarryToPlanAsync(n);
        await Db.SaveChangesAsync();
        await Notify.CarryOverAsync(n.EmployeeId, n.Date, n.Title, "đã được duyệt, đã vào lịch");
    }, "Đã duyệt, việc đã vào lịch nhân viên");

    /// <summary>carryAll: duyệt mọi đề xuất chuyển chờ duyệt từ ngày <paramref name="date"/> trong phạm vi.</summary>
    [HttpPost]
    public async Task<IActionResult> ApproveAllCarries(string? date) => await Do(async () =>
    {
        RequireLead();
        var d = Fmt.ParseKey(date) ?? Date;
        await D.EnsureAsync(d, d);
        var list = await Db.WorkTasks.Where(t => t.Source == TaskSource.CarryOver && t.FromDate == d && !t.CarryOk).ToListAsync();
        var now = VnTime.UtcNow;
        var done = list.Where(t => D.InScope(t.EmployeeId)).ToList();
        foreach (var n in done)
        {
            n.CarryOk = true;
            n.CarryBy = Scope.ActorName;
            n.CarryAt = now;
            await Flow.SyncCarryToPlanAsync(n);
        }
        await Db.SaveChangesAsync();
        foreach (var n in done) await Notify.CarryOverAsync(n.EmployeeId, n.Date, n.Title, "đã được duyệt, đã vào lịch");
    }, "Đã duyệt tất cả đề xuất chuyển");

    /// <summary>carryUndo: bỏ duyệt đề xuất chuyển → việc rời khỏi lịch NV.</summary>
    [HttpPost]
    public async Task<IActionResult> UndoCarry(int id) => await Do(async () =>
    {
        var n = await LoadCarryTaskAsync(id);
        if (!n.CarryOk) return;
        n.CarryOk = false;
        await Flow.SyncCarryToPlanAsync(n);
        await Db.SaveChangesAsync();
        await Notify.CarryOverAsync(n.EmployeeId, n.Date, n.Title, "bị hoàn tác, chưa vào lịch");
    }, "Đã hoàn tác, việc rời khỏi lịch");

    /// <summary>eodCarry: Leader sửa đề xuất chuyển (ngày, giờ, nội dung) và duyệt luôn.</summary>
    [HttpPost]
    public async Task<IActionResult> EditCarry(int id, string? date, string? start, string? end, string? title) => await Do(async () =>
    {
        var n = await LoadCarryTaskAsync(id);
        var nt = (title ?? "").Trim();
        if (nt.Length == 0) throw new BizException("Nhập nội dung");
        if (nt.Length > PlanningApproval.TitleMax) throw new BizException($"Nội dung quá dài (tối đa {PlanningApproval.TitleMax} ký tự)");
        if (start is null || end is null || !Fmt.IsHhmm(start) || !Fmt.IsHhmm(end) || start == "24:00" || end.Le(start))
            throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        var nd = Fmt.ParseKey(date);
        if (nd is not DateOnly day || !PlanningApproval.CarryDays(n).Contains(day)) throw new BizException("Chọn ngày chuyển sang");
        var now = VnTime.UtcNow;
        var actor = Scope.ActorName;
        n.Date = day;
        n.Start = start;
        n.End = end;
        n.Title = nt;
        n.EditedBy = actor;
        n.EditedAt = now;
        n.CarryOk = true;
        n.CarryBy = actor;
        n.CarryAt = now;
        if (n.CarriedFromId is int oid && await Db.WorkTasks.FirstOrDefaultAsync(t => t.Id == oid) is { } o) o.CarryDate = day;
        await Flow.SyncCarryToPlanAsync(n);
        await Db.SaveChangesAsync();
        await Notify.CarryOverAsync(n.EmployeeId, n.Date, n.Title, "được Leader chỉnh và duyệt");
    }, "Đã chỉnh và duyệt, việc đã vào lịch nhân viên");
}
