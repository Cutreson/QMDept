using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Giao diện Trưởng phòng (TRƯỞNG PHÒNG trong artifact): Tổng quan (tpOverview), Phê duyệt (tpApprove + tpDecide),
/// Dự án (tpProjects), Báo cáo (tpReports + weekReport). Tab "Làm thêm" nằm ở OvertimeController.
/// Ở giao diện TP: D.Staff() = mọi NV, InScope = tất cả.
/// </summary>
[UiRole(Role.Manager)]
public class ManagerController : AppController
{
    /// <summary>/Controller → trang mặc định của giao diện.</summary>
    [HttpGet] public IActionResult Index() => Redirect(Ui.To(Request, "/Manager/Overview"));

    /// <summary>GET /Manager/Overview — tpOverview: KPI, báo cáo Leader đã gửi, tiến độ, biểu đồ, nhân sự, vấn đề.</summary>
    [HttpGet]
    public async Task<IActionResult> Overview()
    {
        await D.EnsureAsync(Date, Date.AddDays(1)); // issuesOf(d) cần d và d+1
        // Khác artifact: báo cáo khóa theo (ngày, Leader) → có thể nhiều báo cáo trong một ngày.
        var reports = await Db.LeaderReports.AsNoTracking().Include(r => r.Leader)
            .Where(r => r.Date == Date).OrderBy(r => r.SentAt).ThenBy(r => r.Id).ToListAsync();
        var sent = reports.Select(r => r.LeaderId).ToHashSet();
        return View(new ManagerOverviewVm
        {
            Reports = reports,
            NotSent = D.Leaders().Where(l => !sent.Contains(l.Id)).ToList(),
        });
    }

    /// <summary>GET /Manager/Approvals?mode=plan|extra&amp;f=pending|approved|rejected|all — tpApprove.</summary>
    [HttpGet]
    public async Task<IActionResult> Approvals(string? mode, string? f)
    {
        mode = mode == "extra" ? "extra" : "plan";
        f = f is "approved" or "rejected" or "all" ? f : "pending";
        await D.EnsureAsync(Date, Date);
        var pendingManager = D.Plans.Count(p => p.Date == Date && Dept.PlanSt(p) == PlanState.PendingManager);

        var plans = new List<DayPlan>();
        var extras = new List<Extra>();
        if (mode == "plan")
        {
            plans = D.Plans.Where(p => p.Date == Date && Dept.NeedsManager(p) && ManagerApprovalsVm.PlanMatches(p, f))
                .OrderBy(p => p.SentAt).ThenBy(p => p.Id).ToList();
        }
        else
        {
            // Artifact liệt kê mọi phát sinh (mới nhất trước); bản này giới hạn 200 mục gần nhất.
            IQueryable<Extra> q = Db.Extras.AsNoTracking();
            q = f switch
            {
                "pending" => q.Where(x => x.Status == ExtraStatus.Pending),
                "approved" => q.Where(x => x.Status == ExtraStatus.Approved),
                "rejected" => q.Where(x => x.Status == ExtraStatus.Rejected),
                _ => q,
            };
            extras = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(200).ToListAsync();
            // exSt(x) chỉ cần việc đã tạo của phát sinh ĐÃ DUYỆT (việc cùng ngày với phát sinh — approveExtra)
            // → chỉ nạp khoảng ngày của các phát sinh đó (lọc Chờ duyệt / Từ chối không cần nạp thêm).
            var withTask = extras.Where(x => x.Status == ExtraStatus.Approved && x.TaskId is not null).ToList();
            if (withTask.Count > 0) await D.EnsureAsync(withTask.Min(x => x.Date), withTask.Max(x => x.Date));
        }

        return View(new ManagerApprovalsVm { Mode = mode, Filter = f, PendingManager = pendingManager, Plans = plans, Extras = extras });
    }

    /// <summary>
    /// POST /Manager/Decide — tpDecide: TP duyệt / từ chối kế hoạch cần hỗ trợ nhân sự nhóm khác / phòng ban khác.
    /// Chỉ khi Leader đã duyệt và TP chưa quyết định (Chờ TP duyệt). Từ chối bắt buộc lý do.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Decide(int id, string? v, string? note)
    {
        var ok = v == "ok";
        return await Do(async () =>
        {
            RequireManager();
            if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
            var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
            if (!Dept.NeedsManager(p)) throw new BizException("Kế hoạch này không cần Trưởng phòng duyệt");
            if (Dept.PlanSt(p) != PlanState.PendingManager)
                throw new BizException(p.LeaderDecision is null ? "Đang chờ Leader duyệt trước." : "Kế hoạch không còn ở trạng thái chờ TP duyệt");
            note = ok ? null : note?.Trim(); // artifact: chỉ nút Từ chối mở askText → chỉ khi từ chối mới ghi tpNote (ManagerNote)
            if (!ok && string.IsNullOrEmpty(note)) throw new BizException("Nhập lý do");
            if (note is { Length: > 2000 }) throw new BizException("Lý do tối đa 2000 ký tự");

            p.ManagerDecision = ok ? Decision.Ok : Decision.No;
            p.ManagerBy = Scope.ActorName;
            p.ManagerAt = VnTime.UtcNow;
            if (!ok) p.ManagerNote = note;
            await Db.SaveChangesAsync();
            await Notify.PlanDecidedAsync(p.EmployeeId, p.Date, ok ? "Trưởng phòng đã duyệt hỗ trợ nhân sự" : "Trưởng phòng từ chối", note, alsoLeader: true);
        }, ok ? "Đã duyệt" : "Đã từ chối");
    }

    /// <summary>GET /Manager/Projects?pj=&amp;tab=overview|plan|people — tpProjects: chip mọi dự án + projectDetail(pid, 'tp').</summary>
    [HttpGet]
    public async Task<IActionResult> Projects(int? pj, string? tab)
    {
        await D.EnsureAsync(Date, Date);
        var ps = D.AllPj();
        if (ps.Count == 0) return View(new ManagerProjectsVm());
        var pid = pj is int x && ps.Any(p => p.Id == x) ? x : ps[0].Id;
        var req = Request;
        var detail = await ProjectDetailVm.LoadAsync(D, Db, Date, pid, ProjectDetailVm.ManagerView, tab, null, t => Ui.Here(req, ("tab", t)));
        return View(new ManagerProjectsVm { Projects = ps, Pid = pid, Detail = detail });
    }

    /// <summary>GET /Manager/Reports?tab=day|week — tpReports: báo cáo ngày Leader gửi / báo cáo tuần (weekReport).</summary>
    [HttpGet]
    public async Task<IActionResult> Reports(string? tab)
    {
        tab = tab == "week" ? "week" : "day";
        await D.EnsureAsync(Date, Date);
        if (tab == "week")
        {
            var m = Nav.Week(Request, Date);
            await D.EnsureAsync(m, m.AddDays(6));
            return View(new ManagerReportsVm { Tab = tab, Week = m });
        }
        // Artifact liệt kê mọi báo cáo (mới nhất trước); bản này lấy 60 báo cáo gần nhất.
        var list = await Db.LeaderReports.AsNoTracking().Include(r => r.Leader)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.SentAt).ThenByDescending(r => r.Id)
            .Take(60).ToListAsync();
        return View(new ManagerReportsVm { Tab = tab, Week = Fmt.Monday(Date), Reports = list.Select(ManagerLeaderReportItem.From).ToList() });
    }
}
