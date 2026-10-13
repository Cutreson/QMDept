using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Giao diện Trưởng phòng (TRƯỞNG PHÒNG trong artifact): Tổng quan (tpOverview), Phê duyệt (tpApprove + tpDecide),
/// Dự án (tpProjects), Báo cáo (tpReports + weekReport). Tab "Làm thêm" nằm ở LamThemController (?r=tp).
/// Ở giao diện TP: D.Staff() = mọi NV, InScope = tất cả.
/// </summary>
[UiRole(Role.Tp)]
public class TpController : AppController
{
    /// <summary>GET /Tp/TongQuan — tpOverview: KPI, báo cáo Leader đã gửi, tiến độ, biểu đồ, nhân sự, vấn đề.</summary>
    [HttpGet]
    public async Task<IActionResult> TongQuan()
    {
        await D.EnsureAsync(Date, Date.AddDays(1)); // issuesOf(d) cần d và d+1
        // Khác artifact: báo cáo khóa theo (ngày, Leader) → có thể nhiều báo cáo trong một ngày.
        var reports = await Db.LeaderReports.AsNoTracking().Include(r => r.Leader)
            .Where(r => r.Date == Date).OrderBy(r => r.SentAt).ThenBy(r => r.Id).ToListAsync();
        var sent = reports.Select(r => r.LeaderId).ToHashSet();
        return View(new TpTongQuanVm
        {
            Reports = reports,
            NotSent = D.Leaders().Where(l => !sent.Contains(l.Id)).ToList(),
        });
    }

    /// <summary>GET /Tp/PheDuyet?mode=plan|ps&amp;f=pend|ok|no|all — tpApprove.</summary>
    [HttpGet]
    public async Task<IActionResult> PheDuyet(string? mode, string? f)
    {
        mode = mode == "ps" ? "ps" : "plan";
        f = f is "ok" or "no" or "all" ? f : "pend";
        await D.EnsureAsync(Date, Date);
        var pendingTp = D.Plans.Count(p => p.Date == Date && Dept.PlanSt(p) == PlanState.ChoTp);

        var plans = new List<DayPlan>();
        var extras = new List<Extra>();
        if (mode == "plan")
        {
            plans = D.Plans.Where(p => p.Date == Date && Dept.NeedTp(p) && TpPheDuyetVm.PlanMatches(p, f))
                .OrderBy(p => p.SentAt).ThenBy(p => p.Id).ToList();
        }
        else
        {
            // Artifact liệt kê mọi phát sinh (mới nhất trước); bản này giới hạn 200 mục gần nhất.
            IQueryable<Extra> q = Db.Extras.AsNoTracking().Include(x => x.Photos);
            q = f switch
            {
                "pend" => q.Where(x => x.Status == ExtraStatus.ChoDuyet),
                "ok" => q.Where(x => x.Status == ExtraStatus.DaDuyet),
                "no" => q.Where(x => x.Status == ExtraStatus.TuChoi),
                _ => q,
            };
            extras = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(200).ToListAsync();
            // exSt(x) chỉ cần việc đã tạo của phát sinh ĐÃ DUYỆT (việc cùng ngày với phát sinh — approveExtra)
            // → chỉ nạp khoảng ngày của các phát sinh đó (lọc Chờ duyệt / Từ chối không cần nạp thêm).
            var withTask = extras.Where(x => x.Status == ExtraStatus.DaDuyet && x.TaskId is not null).ToList();
            if (withTask.Count > 0) await D.EnsureAsync(withTask.Min(x => x.Date), withTask.Max(x => x.Date));
        }

        return View(new TpPheDuyetVm { Mode = mode, Filter = f, PendingTp = pendingTp, Plans = plans, Extras = extras });
    }

    /// <summary>
    /// POST /Tp/QuyetDinh — tpDecide: TP duyệt / từ chối kế hoạch cần hỗ trợ nhân sự nhóm khác / phòng ban khác.
    /// Chỉ khi Leader đã duyệt và TP chưa quyết định (Chờ TP duyệt). Từ chối bắt buộc lý do.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> QuyetDinh(int id, string? v, string? note)
    {
        var ok = v == "ok";
        return await Do(async () =>
        {
            RequireTp();
            if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
            var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
            if (!Dept.NeedTp(p)) throw new BizException("Kế hoạch này không cần Trưởng phòng duyệt");
            if (Dept.PlanSt(p) != PlanState.ChoTp)
                throw new BizException(p.LeaderDecision is null ? "Đang chờ Leader duyệt trước." : "Kế hoạch không còn ở trạng thái chờ TP duyệt");
            note = ok ? null : note?.Trim(); // artifact: chỉ nút Từ chối mở askText → chỉ khi từ chối mới ghi tpNote
            if (!ok && string.IsNullOrEmpty(note)) throw new BizException("Nhập lý do");
            if (note is { Length: > 2000 }) throw new BizException("Lý do tối đa 2000 ký tự");

            p.TpDecision = ok ? Decision.Ok : Decision.No;
            p.TpBy = Scope.ActorName;
            p.TpAt = VnTime.UtcNow;
            if (!ok) p.TpNote = note;
            await Db.SaveChangesAsync();
        }, ok ? "Đã duyệt" : "Đã từ chối");
    }

    /// <summary>GET /Tp/DuAn?pj=&amp;tab=tq|kh|ns — tpProjects: chip mọi dự án + projectDetail(pid, 'tp').</summary>
    [HttpGet]
    public async Task<IActionResult> DuAn(int? pj, string? tab)
    {
        await D.EnsureAsync(Date, Date);
        var ps = D.AllPj();
        if (ps.Count == 0) return View(new TpDuAnVm());
        var pid = pj is int x && ps.Any(p => p.Id == x) ? x : ps[0].Id;
        var req = Request;
        var detail = await ProjectDetailVm.LoadAsync(D, Db, Date, pid, "tp", tab, null, t => Ui.Here(req, ("tab", t)));
        return View(new TpDuAnVm { Projects = ps, Pid = pid, Detail = detail });
    }

    /// <summary>GET /Tp/BaoCao?tab=ngay|tuan — tpReports: báo cáo ngày Leader gửi / báo cáo tuần (weekReport).</summary>
    [HttpGet]
    public async Task<IActionResult> BaoCao(string? tab)
    {
        tab = tab == "tuan" ? "tuan" : "ngay";
        await D.EnsureAsync(Date, Date);
        if (tab == "tuan")
        {
            var m = Nav.Week(Request, Date);
            await D.EnsureAsync(m, m.AddDays(6));
            return View(new TpBaoCaoVm { Tab = tab, Week = m });
        }
        // Artifact liệt kê mọi báo cáo (mới nhất trước); bản này lấy 60 báo cáo gần nhất.
        var list = await Db.LeaderReports.AsNoTracking().Include(r => r.Leader)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.SentAt).ThenByDescending(r => r.Id)
            .Take(60).ToListAsync();
        return View(new TpBaoCaoVm { Tab = tab, Week = Fmt.Monday(Date), Reports = list.Select(TpLeaderReportItem.From).ToList() });
    }
}
