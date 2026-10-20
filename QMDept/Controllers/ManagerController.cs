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
    public async Task<IActionResult> Overview(string? s)
    {
        await D.EnsureAsync(Date, Date.AddDays(1)); // issuesOf(d) cần d và d+1
        // Khác artifact: báo cáo khóa theo (ngày, Leader) → có thể nhiều báo cáo trong một ngày.
        var reports = await Db.LeaderReports.AsNoTracking().Include(r => r.Leader)
            .Where(r => r.Date == Date).OrderBy(r => r.SentAt).ThenBy(r => r.Id).ToListAsync();
        var sent = reports.Select(r => r.LeaderId).ToHashSet();
        return View(new ManagerOverviewVm
        {
            Section = s == "people" ? "people" : "projects",
            Reports = reports,
            NotSent = D.Leaders().Where(l => !sent.Contains(l.Id)).ToList(),
        });
    }

    /// <summary>GET /Manager/Approvals?mode=plan|extra&amp;f=pending|approved|rejected|all — tpApprove.</summary>
    [HttpGet]
    public async Task<IActionResult> Approvals(string? mode, string? f)
    {
        mode = mode switch { "extra" => "extra", "leader" => "leader", _ => "plan" };
        f = f is "approved" or "rejected" or "all" ? f : "pending";
        await D.EnsureAsync(Date, Date);
        bool IsLeaderPerson(int emp) => D.P(emp) is { Role: Role.Leader };
        // Kế hoạch / báo cáo của chính Leader (Leader tự gửi → Trưởng phòng duyệt, yêu cầu 2026-10-16): mục riêng "Của Leader".
        var leaderPlans = D.Plans.Where(p => p.Date == Date && IsLeaderPerson(p.EmployeeId)).ToList();
        var pendLeaves = D.LeaderLeaves();
        var pendingLeader = leaderPlans.Count(p => Dept.PlanSt(p) == PlanState.Pending) + pendLeaves.Count;
        var pendingManager = D.Plans.Count(p => p.Date == Date && !IsLeaderPerson(p.EmployeeId) && Dept.PlanSt(p) == PlanState.PendingManager);

        var plans = new List<DayPlan>();
        var extras = new List<Extra>();
        if (mode == "plan")
        {
            plans = D.Plans.Where(p => p.Date == Date && !IsLeaderPerson(p.EmployeeId) && Dept.NeedsManager(p) && ManagerApprovalsVm.PlanMatches(p, f))
                .OrderBy(p => p.SentAt).ThenBy(p => p.Id).ToList();
        }
        else if (mode == "leader")
        {
            leaderPlans = leaderPlans.Where(p => ManagerApprovalsVm.PlanMatches(p, f)).OrderBy(p => p.SentAt).ToList();
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

        return View(new ManagerApprovalsVm
        {
            Mode = mode, Filter = f, PendingManager = pendingManager, Plans = plans, Extras = extras, PendingLeader = pendingLeader,
            LeaderPlans = mode == "leader" ? leaderPlans : [],
            LeaderLeaves = mode == "leader" ? (f == "pending" ? pendLeaves : await Db.LeaveRequests.AsNoTracking().Where(r => r.Role == Role.Leader && (f == "approved" ? r.Status == ExtraStatus.Approved : f == "rejected" ? r.Status == ExtraStatus.Rejected : r.Status != ExtraStatus.Pending)).OrderByDescending(r => r.DecidedAt).Take(15).ToListAsync()) : [],
            LeaderNotSentPlan = mode == "leader" ? D.Leaders().Where(l => D.PlanOf(Date, l.Id) is null && D.OnLeave(Date, l.Id) is null).ToList() : [],
        });
    }

    // ======================= Duyệt kế hoạch / báo cáo của Leader (Leader tự gửi — yêu cầu 2026-10-16) =======================
    // Quyết định của Trưởng phòng ghi vào các trường Leader* của kế hoạch / báo cáo (người duyệt duy nhất của Leader là TP);
    // kế hoạch xin nhóm khác / phòng ban khác thì đồng thời ghi Manager* (Ok) để trạng thái thành "Đã duyệt".

    async Task<DayPlan> LoadLeaderPlanAsync(int id)
    {
        RequireManager();
        var p = await Db.DayPlans.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy kế hoạch");
        if (!await Db.People.AnyAsync(x => x.Id == p.EmployeeId && x.Role == Role.Leader)) throw new BizException("Chỉ dùng cho kế hoạch của Leader");
        return p;
    }

    /// <summary>POST /Manager/DecideLeaderPlan — duyệt (v=ok) / từ chối (v=no, bắt buộc lý do) kế hoạch đầu ngày của Leader.</summary>
    [HttpPost]
    public async Task<IActionResult> DecideLeaderPlan(int id, string? v, string? note) => await Do(async () =>
    {
        if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
        var p = await LoadLeaderPlanAsync(id);
        if (p.LeaderDecision is not null) throw new BizException("Kế hoạch đã được quyết định");
        var n = PlanningApproval.CleanNote(note);
        if (v == "no" && n is null) throw new BizException("Nhập lý do");
        var now = VnTime.UtcNow;
        p.LeaderDecision = v == "ok" ? Decision.Ok : Decision.No;
        p.LeaderBy = Scope.ActorName; p.LeaderAt = now; p.LeaderNote = n;
        if (v == "ok" && Dept.NeedsManager(p)) { p.ManagerDecision = Decision.Ok; p.ManagerBy = Scope.ActorName; p.ManagerAt = now; }
        await Db.SaveChangesAsync();
        await Notify.PlanDecidedAsync(p.EmployeeId, p.Date, v == "ok" ? "Trưởng phòng đã duyệt" : "Trưởng phòng từ chối", n);
    }, "Đã quyết định kế hoạch của Leader");

    /// <summary>POST /Manager/DecideLeave — Trưởng phòng duyệt (ghi Nghỉ vào lịch) / từ chối (bắt buộc lý do) đề xuất xin nghỉ của Leader (yêu cầu 2026-10-20).</summary>
    [HttpPost]
    public Task<IActionResult> DecideLeave(int id, string? v, string? note) => Do(async () =>
    {
        RequireManager();
        var x = await Db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id) ?? throw new BizException("Không tìm thấy đề xuất xin nghỉ");
        if (x.Role != Role.Leader) throw new BizException("Trưởng phòng duyệt xin nghỉ của Leader — nhân viên do Leader duyệt");
        var approve = v switch { "ok" => true, "no" => false, _ => throw new BizException("Thao tác không hợp lệ") };
        await Flow.DecideLeaveAsync(id, approve, note, Scope.Account);
        Toast(approve ? "Đã duyệt xin nghỉ — đã ghi vào lịch" : "Đã từ chối xin nghỉ");
    });

    /// <summary>POST /Manager/UndoLeaderPlan — hoàn tác quyết định kế hoạch của Leader.</summary>
    [HttpPost]
    public async Task<IActionResult> UndoLeaderPlan(int id) => await Do(async () =>
    {
        var p = await LoadLeaderPlanAsync(id);
        if (p.LeaderDecision is null) return;
        p.LeaderDecision = null; p.LeaderAt = null; p.LeaderNote = null; p.LeaderBy = null;
        p.ManagerDecision = null; p.ManagerAt = null; p.ManagerBy = null; p.ManagerNote = null;
        await Db.SaveChangesAsync();
        await Notify.PlanDecidedAsync(p.EmployeeId, p.Date, "Trưởng phòng hoàn tác quyết định, chờ duyệt lại");
    }, "Đã hoàn tác");

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
