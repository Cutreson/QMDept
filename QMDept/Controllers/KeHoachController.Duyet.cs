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
public partial class KeHoachController
{
    // ======================= GET =======================

    /// <summary>GET /KeHoach/Duyet?ap=all|pend&amp;pj={projectId} — ldApprove().</summary>
    [HttpGet]
    public async Task<IActionResult> Duyet(string? ap, string? pj)
    {
        await D.EnsureAsync(Date, Date);
        int? pid = int.TryParse(pj, out var x) ? x : null;
        ViewData["Title"] = "Kế hoạch & duyệt";
        return View(KhDuyetVm.Build(D, Date, ap, pid));
    }

    /// <summary>GET /KeHoach/BaoCao — ldEodApprove() + carrySection(). Nạp thêm 14 ngày sau (ngày chuyển tiếp).</summary>
    [HttpGet]
    public async Task<IActionResult> BaoCao()
    {
        await D.EnsureAsync(Date, Date.AddDays(14));
        ViewData["Title"] = "Kế hoạch & duyệt";
        return View(KhBaoCaoVm.Build(D, Date));
    }

    // ======================= Kế hoạch đầu ngày (ldDecide / ldUndo / ldNote) =======================

    /// <summary>ldDecide: v=ok duyệt; v=no từ chối (bắt buộc lý do / hướng dẫn sửa).</summary>
    [HttpPost]
    public async Task<IActionResult> QuyetDinhKeHoach(int id, string? v, string? note) => await Do(async () =>
    {
        RequireLead();
        var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
        await RequireInScopeAsync(p.EmployeeId);
        var dec = v switch { "ok" => Decision.Ok, "no" => Decision.No, _ => throw new BizException("Thao tác không hợp lệ") };
        if (p.LeaderDecision is not null) throw new BizException("Kế hoạch đã được Leader quyết định");
        var n = KhDuyet.CleanNote(note);
        if (dec == Decision.No && n is null) throw new BizException("Nhập lý do / hướng dẫn sửa");
        p.LeaderDecision = dec;
        p.LeaderBy = Scope.ActorName;
        p.LeaderAt = VnTime.UtcNow;
        if (note is not null) p.LeaderNote = n;   // artifact: chỉ ghi leaderNote khi có hộp nhập (từ chối)
        await Db.SaveChangesAsync();
        Toast(dec == Decision.Ok ? "Đã duyệt kế hoạch" : "Đã từ chối kế hoạch");
    });

    /// <summary>ldUndo: hoàn tác quyết định của Leader (chỉ khi TP chưa quyết định).</summary>
    [HttpPost]
    public async Task<IActionResult> HoanTacKeHoach(int id) => await Do(async () =>
    {
        RequireLead();
        var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
        await RequireInScopeAsync(p.EmployeeId);
        if (p.TpDecision is not null) throw new BizException("Trưởng phòng đã quyết định, không hoàn tác được");
        p.LeaderDecision = null;
        p.LeaderAt = null;
        await Db.SaveChangesAsync();
    }, "Đã hoàn tác");

    /// <summary>ldNote: góp ý cho kế hoạch (không bắt buộc nội dung; rỗng = xóa góp ý).</summary>
    [HttpPost]
    public async Task<IActionResult> GopYKeHoach(int id, string? note) => await Do(async () =>
    {
        RequireLead();
        var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
        await RequireInScopeAsync(p.EmployeeId);
        p.LeaderNote = KhDuyet.CleanNote(note);
        if (string.IsNullOrEmpty(p.LeaderBy)) p.LeaderBy = Scope.ActorName;
        await Db.SaveChangesAsync();
    }, "Đã gửi góp ý");

    // ======================= Phát sinh (xDecide) =======================

    /// <summary>xDecide v=ok: duyệt phát sinh → tạo việc PhatSinh cho NV.</summary>
    [HttpPost]
    public async Task<IActionResult> DuyetPhatSinh(int id) => await Do(async () =>
    {
        RequireLead();
        var x = await Db.Extras.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id) ?? throw new BizException("Không tìm thấy phát sinh");
        await RequireInScopeAsync(x.EmployeeId);
        if (x.Status != ExtraStatus.ChoDuyet) return;   // artifact: đã xử lý → bỏ qua
        await Flow.ApproveExtraAsync(id, Scope.ActorName, Role.Leader);
        Toast("Đã duyệt phát sinh");
    });

    /// <summary>xDecide v=no: từ chối phát sinh (bắt buộc lý do).</summary>
    [HttpPost]
    public async Task<IActionResult> TuChoiPhatSinh(int id, string? reason) => await Do(async () =>
    {
        RequireLead();
        var x = await Db.Extras.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id) ?? throw new BizException("Không tìm thấy phát sinh");
        await RequireInScopeAsync(x.EmployeeId);
        if (x.Status != ExtraStatus.ChoDuyet) return;
        var r = KhDuyet.CleanNote(reason) ?? throw new BizException("Nhập lý do từ chối");
        await Flow.RejectExtraAsync(id, r, Scope.ActorName, Role.Leader);
        Toast("Đã từ chối");
    });

    // ======================= Báo cáo cuối ngày (eodDecide / eodUndo) =======================

    /// <summary>eodDecide: v=ok duyệt báo cáo + duyệt mọi đề xuất chuyển của NV từ ngày đó; v=no yêu cầu bổ sung (bắt buộc nội dung).</summary>
    [HttpPost]
    public async Task<IActionResult> QuyetDinhBaoCao(int id, string? v, string? note) => await Do(async () =>
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
                .Where(t => t.Source == TaskSource.ChuyenTiep && t.FromDate == r.Date && t.EmployeeId == r.EmployeeId && !t.CarryOk)
                .ToListAsync();
            foreach (var t in carries) { t.CarryOk = true; t.CarryBy = actor; t.CarryAt = now; }
            await Db.SaveChangesAsync();
            Toast("Đã duyệt báo cáo cuối ngày và kế hoạch chuyển tiếp");
        }
        else
        {
            var n = KhDuyet.CleanNote(note) ?? throw new BizException("Nhập nội dung cần bổ sung");
            r.LeaderDecision = Decision.No;
            r.LeaderNote = n;
            r.LeaderBy = actor;
            r.LeaderAt = now;
            await Db.SaveChangesAsync();
            Toast("Đã gửi yêu cầu bổ sung");
        }
    });

    /// <summary>eodUndo: hoàn tác quyết định báo cáo cuối ngày.</summary>
    [HttpPost]
    public async Task<IActionResult> HoanTacBaoCao(int id) => await Do(async () =>
    {
        RequireLead();
        var r = await Db.DayReports.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy báo cáo");
        await RequireInScopeAsync(r.EmployeeId);
        r.LeaderDecision = null;
        r.LeaderAt = null;
        await Db.SaveChangesAsync();
    }, "Đã hoàn tác");

    // ======================= Đề xuất chuyển kế hoạch (carryOk / carryAll / carryUndo / eodCarry) =======================

    /// <summary>Việc chuyển tiếp (tracked) trong phạm vi; ném lỗi nếu không có / không phải việc chuyển tiếp.</summary>
    async Task<WorkTask> KhCarryTaskAsync(int id)
    {
        RequireLead();
        var n = await Db.WorkTasks.FirstOrDefaultAsync(t => t.Id == id && t.Source == TaskSource.ChuyenTiep)
            ?? throw new BizException("Không tìm thấy đề xuất chuyển");
        await RequireInScopeAsync(n.EmployeeId);
        return n;
    }

    /// <summary>carryOk: duyệt một đề xuất chuyển → việc vào lịch NV.</summary>
    [HttpPost]
    public async Task<IActionResult> DuyetChuyen(int id) => await Do(async () =>
    {
        var n = await KhCarryTaskAsync(id);
        n.CarryOk = true;
        n.CarryBy = Scope.ActorName;
        n.CarryAt = VnTime.UtcNow;
        await Db.SaveChangesAsync();
    }, "Đã duyệt, việc đã vào lịch nhân viên");

    /// <summary>carryAll: duyệt mọi đề xuất chuyển chờ duyệt từ ngày <paramref name="date"/> trong phạm vi.</summary>
    [HttpPost]
    public async Task<IActionResult> DuyetTatCaChuyen(string? date) => await Do(async () =>
    {
        RequireLead();
        var d = Fmt.ParseKey(date) ?? Date;
        await D.EnsureAsync(d, d);
        var list = await Db.WorkTasks.Where(t => t.Source == TaskSource.ChuyenTiep && t.FromDate == d && !t.CarryOk).ToListAsync();
        var now = VnTime.UtcNow;
        foreach (var n in list.Where(t => D.InScope(t.EmployeeId)))
        {
            n.CarryOk = true;
            n.CarryBy = Scope.ActorName;
            n.CarryAt = now;
        }
        await Db.SaveChangesAsync();
    }, "Đã duyệt tất cả đề xuất chuyển");

    /// <summary>carryUndo: bỏ duyệt đề xuất chuyển → việc rời khỏi lịch NV.</summary>
    [HttpPost]
    public async Task<IActionResult> HoanTacChuyen(int id) => await Do(async () =>
    {
        var n = await KhCarryTaskAsync(id);
        n.CarryOk = false;
        await Db.SaveChangesAsync();
    }, "Đã hoàn tác, việc rời khỏi lịch");

    /// <summary>eodCarry: Leader sửa đề xuất chuyển (ngày, giờ, nội dung) và duyệt luôn.</summary>
    [HttpPost]
    public async Task<IActionResult> SuaChuyen(int id, string? date, string? start, string? end, string? title) => await Do(async () =>
    {
        var n = await KhCarryTaskAsync(id);
        var nt = (title ?? "").Trim();
        if (nt.Length == 0) throw new BizException("Nhập nội dung");
        if (nt.Length > KhDuyet.TitleMax) throw new BizException($"Nội dung quá dài (tối đa {KhDuyet.TitleMax} ký tự)");
        if (start is null || end is null || !Fmt.IsHhmm(start) || !Fmt.IsHhmm(end) || start == "24:00" || end.Le(start))
            throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        var nd = Fmt.ParseKey(date);
        if (nd is not DateOnly day || !KhDuyet.CarryDays(n).Contains(day)) throw new BizException("Chọn ngày chuyển sang");
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
        await Db.SaveChangesAsync();
    }, "Đã chỉnh và duyệt, việc đã vào lịch nhân viên");
}
