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
/// Tab "Kế hoạch &amp; duyệt" nằm ở KeHoachController; "Làm thêm" ở LamThemController.
/// </summary>
[UiRole(Role.Leader)]
public partial class LeaderController : AppController
{
    // ======================= GET =======================

    /// <summary>GET /Leader/TongQuan — ldOverview.</summary>
    [HttpGet]
    public async Task<IActionResult> TongQuan()
    {
        await D.EnsureAsync(Date, Date.AddDays(1));
        ViewData["Title"] = "Tổng quan";
        return View();
    }

    /// <summary>GET /Leader/TheoDoi?mode=nv|da&amp;pj= — ldTrack.</summary>
    [HttpGet]
    public async Task<IActionResult> TheoDoi(string? mode, string? pj)
    {
        await D.EnsureAsync(Date, Date.AddDays(1));
        ViewData["Title"] = "Theo dõi";
        return View(new LeaderTrackVm
        {
            Mode = mode == "da" ? "da" : "nv",
            ProjectId = int.TryParse(pj, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : null,
        });
    }

    /// <summary>GET /Leader/BaoCao?tab=ngay|tuan — ldReport (ngày) / weekReport (tuần, ?w=).</summary>
    [HttpGet]
    public async Task<IActionResult> BaoCao(string? tab)
    {
        ViewData["Title"] = "Báo cáo TP";
        if (tab == "tuan")
        {
            var m = Nav.Week(Request, Date);
            await D.EnsureAsync(m, m.AddDays(6));
            await D.EnsureAsync(Date, Date);
            return View(new LeaderReportVm { Tab = "tuan", Monday = m });
        }
        await D.EnsureAsync(Date, Date.AddDays(1));
        var sent = Scope.MeId is int me
            ? await Db.LeaderReports.AsNoTracking().FirstOrDefaultAsync(r => r.Date == Date && r.LeaderId == me)
            : null;
        return View(new LeaderReportVm { Tab = "ngay", Monday = Fmt.Monday(Date), Sent = sent });
    }

    // ======================= POST =======================

    /// <summary>POST /Leader/ThemPhatSinh — openExtra(true): Leader thêm phát sinh cho NV trong nhóm, duyệt ngay.</summary>
    [HttpPost]
    public async Task<IActionResult> ThemPhatSinh(ExtraInput input) => await Do(async () =>
    {
        RequireLead();
        RequireViewedLeader();
        await D.EnsureAsync(Date, Date);
        if (input.EmployeeId is not int emp || !D.Staff().Any(p => p.Id == emp)) throw new BizException("Chọn nhân viên thực hiện");
        if (D.PJ(input.ProjectId) is not { Closed: false }) throw new BizException("Chọn dự án");
        if ((input.Title ?? "").Trim().Length > 300) throw new BizException("Tên công việc phát sinh tối đa 300 ký tự");
        if ((input.Desc ?? "").Trim().Length > LeaderUi.NoteMax) throw new BizException($"Mô tả / lý do tối đa {LeaderUi.NoteMax} ký tự");
        // Flow.CreateExtraAsync lưu phát sinh TRƯỚC khi kiểm ảnh → kiểm trước để ảnh sai không để lại phát sinh "Chờ duyệt" mồ côi.
        LeaderUi.CheckPhotos(input.Photos);
        await Flow.CreateExtraAsync(Date, emp, input.ProjectId, input.Title ?? "", input.Desc,
            string.IsNullOrEmpty(input.Start) ? null : input.Start, input.End, input.Photos, Scope.Account.Id,
            byLeader: true, Scope.ActorName, Role.Leader);
    }, "Đã thêm phát sinh");

    /// <summary>POST /Leader/DuyetPhatSinh — xDecide (ok): duyệt phát sinh → tạo việc PhatSinh cho NV.</summary>
    [HttpPost]
    public async Task<IActionResult> DuyetPhatSinh(int id) => await Do(async () =>
    {
        RequireLead();
        RequireViewedLeader();
        var x = await Db.Extras.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (x is null || x.Status != ExtraStatus.ChoDuyet) return; // artifact: đã quyết định rồi → bỏ qua
        await RequireInScopeAsync(x.EmployeeId);
        await Flow.ApproveExtraAsync(id, Scope.ActorName, Role.Leader);
        Toast("Đã duyệt phát sinh");
    });

    /// <summary>POST /Leader/TuChoiPhatSinh — xDecide (no): từ chối phát sinh, bắt buộc lý do (field reason).</summary>
    [HttpPost]
    public async Task<IActionResult> TuChoiPhatSinh(int id, string? reason) => await Do(async () =>
    {
        RequireLead();
        RequireViewedLeader();
        var x = await Db.Extras.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (x is null || x.Status != ExtraStatus.ChoDuyet) return; // artifact: đã quyết định rồi → bỏ qua (trước khi hỏi lý do)
        await RequireInScopeAsync(x.EmployeeId);
        if (string.IsNullOrWhiteSpace(reason)) throw new BizException("Nhập lý do");
        if (reason.Trim().Length > LeaderUi.NoteMax) throw new BizException($"Lý do tối đa {LeaderUi.NoteMax} ký tự");
        await Flow.RejectExtraAsync(id, reason, Scope.ActorName, Role.Leader);
        Toast("Đã từ chối");
    });

    /// <summary>POST /Leader/NhuCau — change "need": sửa nhu cầu nhân sự (ngày, dự án) trực tiếp trong bảng Kế hoạch ngày mai.</summary>
    [HttpPost]
    public async Task<IActionResult> NhuCau(string? date, int projectId, string? need) => await Do(async () =>
    {
        RequireLead();
        if (Fmt.ParseKey(date) is not DateOnly d) throw new BizException("Ngày không hợp lệ");
        if (!await Db.Projects.AnyAsync(p => p.Id == projectId)) throw new BizException("Không tìm thấy dự án");
        // Math.max(0, Math.round(+value || 0))
        var v = double.TryParse(need, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) ? x : 0;
        var n = (int)Math.Clamp(Math.Floor(v + 0.5), 0, int.MaxValue);
        var row = await Db.ProjectNeeds.FirstOrDefaultAsync(r => r.Date == d && r.ProjectId == projectId);
        if (row is null) Db.ProjectNeeds.Add(new ProjectNeed { Date = d, ProjectId = projectId, Need = n });
        else row.Need = n;
        await Db.SaveChangesAsync();
    }, "Đã lưu nhu cầu");

    /// <summary>POST /Leader/GuiBaoCao — ldRepForm: gửi (hoặc gửi lại) báo cáo ngày cho TP kèm ảnh chụp số liệu.</summary>
    [HttpPost]
    public async Task<IActionResult> GuiBaoCao(string? note) => await Do(async () =>
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
