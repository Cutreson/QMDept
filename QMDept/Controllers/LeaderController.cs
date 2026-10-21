using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Giao diện Leader: Tổng quan (ldOverview), Theo dõi (ldTrack), Báo cáo TP (ldReport + weekReport).
/// Phạm vi = Leader đang xem (Scope.Me; TP chọn Leader ở ô "Xem Leader") → D.Staff()/D.InScope() theo nhóm đó.
/// Tab "Kế hoạch &amp; duyệt" nằm ở PlanningController; "Làm thêm" ở OvertimeController.
/// </summary>
[UiRole(Role.Leader)]
public partial class LeaderController : AppController
{
    /// <summary>/Controller → trang mặc định của giao diện.</summary>
    [HttpGet] public IActionResult Index() => Redirect(Ui.To(Request, "/Leader/Overview"));

    // ======================= GET =======================

    /// <summary>GET /Leader/Overview — ldOverview.</summary>
    [HttpGet]
    public async Task<IActionResult> Overview(string? s)
    {
        await D.EnsureAsync(Date, Date.AddDays(1));
        ViewData["Title"] = "Tổng quan";
        ViewData["Section"] = s == "people" ? "people" : "projects";   // Tổng quan có 3 mục Dự án / Nhân sự / Kế hoạch tháng (yêu cầu 2026-10-20)
        return View();
    }

    /// <summary>GET /Leader/Department — "Nhân sự phòng" của Leader được giao quản lý nhân sự / báo cơm / làm thêm (Person.HrDuty): ?s=meal → Báo cơm & làm thêm cả phòng.</summary>
    [HttpGet]
    public async Task<IActionResult> Department(string? s)
    {
        if (!Scope.Account.HrDuty || Scope.Account.Role != Role.Leader) return Redirect("/");
        await D.EnsureAsync(Date, Date.AddDays(1));
        ViewData["Title"] = "Nhân sự phòng";
        if (s != "meal") return View(new OvertimeVm { Tab = "people" });
        var all = D.Ots.Where(x => x.Date == Date && x.Status != OtStatus.Rejected)
            .OrderBy(x => OvertimeUi.SummaryOrder(x.Role)).ThenBy(x => D.PName(x.PersonId), Fmt.ViCompare).ToList();
        return View(new OvertimeVm { Tab = "summary", Summary = all });
    }

    /// <summary>GET /Leader/Tracking?mode=staff|project&amp;pj= — ldTrack.</summary>
    [HttpGet]
    public async Task<IActionResult> Tracking(string? mode, string? pj)
    {
        await D.EnsureAsync(Date, Date.AddDays(1));
        ViewData["Title"] = "Theo dõi";
        return View(new LeaderTrackVm
        {
            Mode = mode == "project" ? "project" : "staff",
            ProjectId = int.TryParse(pj, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : null,
        });
    }

    /// <summary>GET /Leader/Reports?tab=day|week — ldReport (ngày) / weekReport (tuần, ?w=).</summary>
    [HttpGet]
    public async Task<IActionResult> Reports(string? tab)
    {
        ViewData["Title"] = "Báo cáo TP";
        if (tab == "week")
        {
            var m = Nav.Week(Request, Date);
            await D.EnsureAsync(m, m.AddDays(6));
            await D.EnsureAsync(Date, Date);
            return View(new LeaderReportVm { Tab = "week", Monday = m });
        }
        await D.EnsureAsync(Date, Date.AddDays(1));
        var sent = Scope.MeId is int me
            ? await Db.LeaderReports.AsNoTracking().FirstOrDefaultAsync(r => r.Date == Date && r.LeaderId == me)
            : null;
        return View(new LeaderReportVm { Tab = "day", Monday = Fmt.Monday(Date), Sent = sent });
    }

    // ======================= POST =======================

    /// <summary>POST /Leader/AddExtra — openExtra(true): Leader thêm phát sinh cho NV trong nhóm, duyệt ngay.</summary>
    [HttpPost]
    public async Task<IActionResult> AddExtra(ExtraInput input) => await Do(async () =>
    {
        RequireLead();
        RequireViewedLeader();
        await D.EnsureAsync(Date, Date);
        if (input.EmployeeId is not int emp || !D.Staff().Any(p => p.Id == emp)) throw new BizException("Chọn nhân viên thực hiện");
        if (D.PJ(input.ProjectId) is not { Closed: false }) throw new BizException("Chọn dự án");
        if ((input.Title ?? "").Trim().Length > 300) throw new BizException("Tên công việc phát sinh tối đa 300 ký tự");
        if ((input.Desc ?? "").Trim().Length > LeaderUi.NoteMax) throw new BizException($"Mô tả / lý do tối đa {LeaderUi.NoteMax} ký tự");
        await Flow.CreateExtraAsync(Date, emp, input.ProjectId, input.Title ?? "", input.Desc,
            string.IsNullOrEmpty(input.Start) ? null : input.Start, input.End,
            byLeader: true, Scope.ActorName, Role.Leader);
    }, "Đã thêm phát sinh");

    /// <summary>POST /Leader/ApproveExtra — xDecide (ok): duyệt phát sinh → tạo việc nguồn Extra (Phát sinh) cho NV.</summary>
    [HttpPost]
    public async Task<IActionResult> ApproveExtra(int id) => await Do(async () =>
    {
        RequireLead();
        RequireViewedLeader();
        var x = await Db.Extras.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (x is null || x.Status != ExtraStatus.Pending) return; // artifact: đã quyết định rồi → bỏ qua
        await RequireInScopeAsync(x.EmployeeId);
        await Flow.ApproveExtraAsync(id, Scope.ActorName, Role.Leader);
        Toast("Đã duyệt phát sinh");
    });

    /// <summary>POST /Leader/RejectExtra — xDecide (no): từ chối phát sinh, bắt buộc lý do (field reason).</summary>
    [HttpPost]
    public async Task<IActionResult> RejectExtra(int id, string? reason) => await Do(async () =>
    {
        RequireLead();
        RequireViewedLeader();
        var x = await Db.Extras.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (x is null || x.Status != ExtraStatus.Pending) return; // artifact: đã quyết định rồi → bỏ qua (trước khi hỏi lý do)
        await RequireInScopeAsync(x.EmployeeId);
        if (string.IsNullOrWhiteSpace(reason)) throw new BizException("Nhập lý do từ chối");
        if (reason.Trim().Length > LeaderUi.NoteMax) throw new BizException($"Lý do tối đa {LeaderUi.NoteMax} ký tự");
        await Flow.RejectExtraAsync(id, reason, Scope.ActorName, Role.Leader);
        Toast("Đã từ chối");
    });

    /// <summary>POST /Leader/SetNeed — change "need": sửa nhu cầu nhân sự (ngày, dự án) trực tiếp trong bảng Kế hoạch ngày mai.</summary>
    [HttpPost]
    public async Task<IActionResult> SetNeed(string? date, int projectId, string? need) => await Do(async () =>
    {
        RequireLead();
        if (Fmt.ParseKey(date) is not DateOnly d) throw new BizException("Ngày không hợp lệ");
        if (!await Db.Projects.AnyAsync(p => p.Id == projectId && !p.Closed)) throw new BizException("Không tìm thấy dự án đang triển khai");
        // Math.max(0, Math.round(+value || 0))
        var v = double.TryParse(need, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) ? x : 0;
        var n = (int)Math.Clamp(Math.Floor(v + 0.5), 0, int.MaxValue);
        var row = await Db.ProjectNeeds.FirstOrDefaultAsync(r => r.Date == d && r.ProjectId == projectId);
        if (row is null) Db.ProjectNeeds.Add(new ProjectNeed { Date = d, ProjectId = projectId, Need = n });
        else row.Need = n;
        await Db.SaveChangesAsync();
    }, "Đã lưu nhu cầu");

    /// <summary>POST /Leader/SubmitReport — ldRepForm: gửi (hoặc gửi lại) báo cáo ngày cho TP kèm ảnh chụp số liệu.</summary>
    [HttpPost]
    public async Task<IActionResult> SubmitReport(string? note) => await Do(async () =>
    {
        RequireLead();
        var me = RequireViewedLeader();
        note = note?.Trim();
        if (note is { Length: > LeaderUi.NoteMax }) throw new BizException($"Ghi chú tối đa {LeaderUi.NoteMax} ký tự");
        await D.EnsureAsync(Date, Date.AddDays(1));
        var summary = LeaderReportSummary.Build(D, Date).ToJson();
        var r = await Db.LeaderReports.FirstOrDefaultAsync(x => x.Date == Date && x.LeaderId == me.Id);
        if (r is null) { r = new LeaderReport { Date = Date, LeaderId = me.Id }; Db.LeaderReports.Add(r); }
        r.By = Scope.ActorName;
        r.SentAt = VnTime.UtcNow;
        r.Note = string.IsNullOrEmpty(note) ? null : note;
        r.SummaryJson = summary;
        await Db.SaveChangesAsync();
        await Notify.LeaderReportSentAsync(me.Id, Date);
    }, "Đã gửi báo cáo cho TP");

    /// <summary>
    /// Leader đang xem (phạm vi thao tác) phải tồn tại. Khi chưa có Leader nào (TP mở giao diện Leader), Scope.Me = null
    /// và D.InScope() sẽ coi NV chưa xếp nhóm là trong phạm vi → chặn mọi thao tác ghi.
    /// URL POST ghim ?as= (LeaderUi.Post): nếu Leader đó không còn trong danh sách (bị xóa / đổi vai trò) thì ScopeResolver
    /// đã rơi về Leader đầu tiên → chặn, không ghi nhầm sang nhóm / báo cáo của Leader khác.
    /// </summary>
    Person RequireViewedLeader()
    {
        if (Scope.Me is not { Role: Role.Leader } me)
            throw new BizException("Chưa có Leader nào để xem — Trưởng phòng tạo tài khoản Leader ở Nhân sự");
        var pinned = Request.Query["as"].ToString();
        if (pinned.Length > 0 && pinned != me.Id.ToString(CultureInfo.InvariantCulture))
            throw new BizException("Leader đang xem không còn tồn tại — tải lại trang");
        return me;
    }
}
