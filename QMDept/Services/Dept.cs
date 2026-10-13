using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>Số công việc theo trạng thái (Hoàn thành / Đang làm / Chậm / Chưa làm).</summary>
public sealed record Counts(int Done, int InProgress, int Late, int NotStarted)
{
    public int Total => Done + InProgress + Late + NotStarted;
    public int Of(TaskState s) => s switch { TaskState.Done => Done, TaskState.InProgress => InProgress, TaskState.Late => Late, _ => NotStarted };
}

/// <summary>Một dòng "Vấn đề nổi bật" (issuesOf).</summary>
public sealed record Issue(string Color, string Title, string? Message, string At);

/// <summary>KPI trong ngày (kpis(date)).</summary>
public sealed record Kpi(Counts C, int Working, int Total, int Projects);

/// <summary>
/// Ảnh chụp dữ liệu phòng cho một request (tương đương các mảng S.people, S.prj, S.tasks… của artifact) và
/// toàn bộ hàm truy vấn của artifact được port sang C# với cùng tên (PascalCase).
///
/// CÁCH DÙNG: controller gọi <c>await D.EnsureAsync(from, to)</c> cho khoảng ngày trang cần, sau đó view dùng
/// <c>@inject Dept D</c> để gọi D.TasksOn(...), D.PjStatus(...)…
/// - Bảng nhỏ (people, projects, items, assignments) luôn nạp toàn bộ.
/// - Bảng theo ngày (tasks, plans, reports, leaves, needs, extras, overtimes) chỉ nạp trong các khoảng đã Ensure;
///   truy vấn ngày ngoài khoảng sẽ ném InvalidOperationException để lộ lỗi sớm.
/// - Thực thể nạp ở chế độ AsNoTracking: CHỈ ĐỌC. Muốn ghi, truy vấn lại qua AppDbContext.
/// </summary>
public sealed class Dept(ViewScope scope, DataCache cache)
{
    readonly List<(DateOnly From, DateOnly To)> _ranges = [];
    bool _base;

    readonly Dictionary<int, WorkTask> _tasks = [];
    readonly Dictionary<int, DayPlan> _plans = [];
    readonly Dictionary<int, DayReport> _reports = [];
    readonly Dictionary<int, Leave> _leaves = [];
    readonly Dictionary<int, ProjectNeed> _needs = [];
    readonly Dictionary<int, Extra> _extras = [];
    readonly Dictionary<int, Overtime> _ots = [];

    public List<Person> People { get; private set; } = [];       // gồm cả người đã xóa (để hiện tên trong lịch sử)
    public List<Project> Projects { get; private set; } = [];
    public List<PlanItem> Items { get; private set; } = [];
    public List<Assignment> Assigns { get; private set; } = [];
    public IEnumerable<WorkTask> Tasks => _tasks.Values;
    public IEnumerable<DayPlan> Plans => _plans.Values;
    public IEnumerable<DayReport> Reports => _reports.Values;
    public IEnumerable<Leave> Leaves => _leaves.Values;
    public IEnumerable<ProjectNeed> Needs => _needs.Values;
    public IEnumerable<Extra> Extras => _extras.Values;
    public IEnumerable<Overtime> Ots => _ots.Values;

    public ViewScope Scope => scope;

    // ======================= Nạp dữ liệu =======================

    /// <summary>Nạp bảng nhỏ (một lần) và dữ liệu theo ngày trong [from, to] (gộp với các khoảng đã nạp).</summary>
    public async Task EnsureAsync(DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        // Dữ liệu lấy từ DataCache (dùng chung giữa các request, tự làm mới khi có lệnh ghi) — chỉ đọc.
        if (!_base)
        {
            var b = await cache.GetBaseAsync();
            People = b.People; Projects = b.Projects; Items = b.Items; Assigns = b.Assigns;
            _base = true;
        }
        if (_ranges.Any(r => r.From <= from && to <= r.To)) return;

        var r = await cache.GetRangeAsync(from, to); // gồm việc chuyển tiếp được tham chiếu nằm ngoài khoảng
        foreach (var x in r.Tasks) _tasks.TryAdd(x.Id, x);
        foreach (var x in r.Plans) _plans.TryAdd(x.Id, x);
        foreach (var x in r.Reports) _reports.TryAdd(x.Id, x);
        foreach (var x in r.Leaves) _leaves.TryAdd(x.Id, x);
        foreach (var x in r.Needs) _needs.TryAdd(x.Id, x);
        foreach (var x in r.Extras) _extras.TryAdd(x.Id, x);
        foreach (var x in r.Ots) _ots.TryAdd(x.Id, x);
        _ranges.Add((from, to));
    }

    public bool Covers(DateOnly d) => _ranges.Any(r => r.From <= d && d <= r.To);
    void Guard(DateOnly d)
    {
        if (!_base) throw new InvalidOperationException("Dept chưa được nạp: gọi await D.EnsureAsync(from, to) trong controller trước khi render.");
        if (!Covers(d)) throw new InvalidOperationException($"Dept chưa nạp ngày {Fmt.Key(d)}: mở rộng khoảng EnsureAsync trong controller.");
    }
    void GuardBase()
    {
        if (!_base) throw new InvalidOperationException("Dept chưa được nạp: gọi await D.EnsureAsync(from, to) trong controller trước khi render.");
    }

    // ======================= Người & dự án =======================

    static int ByOrder(Person a, Person b) { var c = a.SortOrder.CompareTo(b.SortOrder); return c != 0 ? c : Fmt.ViCompare.Compare(a.Name, b.Name); }
    static List<Person> Sorted(IEnumerable<Person> s) { var l = s.ToList(); l.Sort(ByOrder); return l; }

    public Person? P(int? id) { GuardBase(); return id is null ? null : People.Find(p => p.Id == id); }
    public string PName(int? id) => P(id)?.Name ?? "—";
    public Project? PJ(int? id) { GuardBase(); return id is null ? null : Projects.Find(p => p.Id == id); }
    public string PjName(int? id) => PJ(id)?.Name ?? "—";
    public PlanItem? Item(int? id) => id is null ? null : Items.Find(i => i.Id == id);
    public WorkTask? Task(int? id) => id is not null && _tasks.TryGetValue(id.Value, out var t) ? t : null;

    /// <summary>Người còn hoạt động (chưa xóa).</summary>
    public IEnumerable<Person> Active => People.Where(p => !p.IsDeleted);
    public List<Person> AllStaff() { GuardBase(); return Sorted(Active.Where(p => p.Role == Role.Staff)); }
    public List<Person> Leaders() { GuardBase(); return Sorted(Active.Where(p => p.Role == Role.Leader)); }
    /// <summary>team(lid): NV thuộc nhóm của Leader.</summary>
    public List<Person> Team(int leaderId) { GuardBase(); return Sorted(Active.Where(p => p.Role == Role.Staff && p.LeaderId == leaderId)); }
    /// <summary>staff(): ở giao diện Leader → nhóm của Leader đang xem; còn lại → toàn bộ NV.</summary>
    public List<Person> Staff() => scope.Role == Role.Leader ? (scope.MeId is int id ? Team(id) : []) : AllStaff();
    /// <summary>inScope(emp): ở giao diện Leader chỉ tính Leader đang xem và NV trong nhóm; giao diện khác: tất cả.</summary>
    /// Không có Leader nào đang được xem (MeId null) → không ai trong phạm vi; người đã xóa không còn trong phạm vi Leader
    /// (artifact: người bị xóa biến khỏi S.people nên inScope = false).
    public bool InScope(int emp) => scope.Role != Role.Leader
        || (scope.MeId is int id && (emp == id || (P(emp) is { IsDeleted: false } p && p.LeaderId == id)));
    /// <summary>accTeam(a): mô tả nhóm của tài khoản.</summary>
    public string TeamText(Person a) => a.Role switch
    {
        Role.Staff => a.LeaderId is int l ? "Nhóm " + PName(l) : "Chưa xếp nhóm",
        Role.Leader => $"Leader · nhóm {Team(a.Id).Count} người",
        Role.Admin => "Quản trị hệ thống",
        _ => "Trưởng phòng",
    };

    public List<Project> ActivePj() { GuardBase(); return Projects.Where(p => !p.Closed).OrderBy(p => p.SortOrder).ThenBy(p => p.Name, Fmt.ViCompare).ToList(); }
    public List<Project> AllPj() { GuardBase(); return Projects.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, Fmt.ViCompare).ToList(); }
    public List<PlanItem> ItemsOf(int pid) { GuardBase(); return Items.Where(i => i.ProjectId == pid).OrderBy(i => i.SortOrder).ThenBy(i => i.Title, Fmt.ViCompare).ToList(); }
    /// <summary>tile(pid): màu ô dự án.</summary>
    public string Tile(int pid) { GuardBase(); var i = Math.Max(0, Projects.OrderBy(p => p.Id).ToList().FindIndex(p => p.Id == pid)); return Fmt.Tile[i % Fmt.Tile.Length]; }
    /// <summary>Dòng phụ "Nhà máy – Địa điểm".</summary>
    public static string Place(Project p) => string.Join(" – ", new[] { p.Factory, p.Location }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>assignsOf(emp): phân công còn hiệu lực (dự án chưa đóng).</summary>
    public List<Assignment> AssignsOf(int emp) => Assigns.Where(a => a.EmployeeId == emp && PJ(a.ProjectId) is { Closed: false }).ToList();
    public Assignment? AssignOf(int pid, int emp) => Assigns.Find(a => a.ProjectId == pid && a.EmployeeId == emp);
    /// <summary>teamProjects(): dự án (chưa đóng) có NV trong phạm vi được phân công.</summary>
    public List<int> TeamProjects() => Assigns.Where(a => P(a.EmployeeId)?.Role == Role.Staff && InScope(a.EmployeeId)).Select(a => a.ProjectId)
        .Distinct().Where(id => PJ(id) is { Closed: false }).ToList();

    // ======================= Công việc & ngày =======================

    /// <summary>tasksOn(date, emp?): việc trong ngày (của một người nếu có), sắp theo giờ bắt đầu.</summary>
    public List<WorkTask> TasksOn(DateOnly date, int? emp = null)
    {
        Guard(date);
        return _tasks.Values.Where(t => t.Date == date && (emp is null || t.EmployeeId == emp))
            .OrderBy(t => t.Start, StringComparer.Ordinal).ThenBy(t => t.Id).ToList();
    }
    /// <summary>chosen(date, emp): việc NV đã chọn làm trong ngày.</summary>
    public List<WorkTask> Chosen(DateOnly date, int emp) => TasksOn(date, emp).Where(t => t.Selected).ToList();
    public DayPlan? PlanOf(DateOnly date, int emp) { Guard(date); return _plans.Values.FirstOrDefault(p => p.Date == date && p.EmployeeId == emp); }
    public DayReport? ReportOf(DateOnly date, int emp) { Guard(date); return _reports.Values.FirstOrDefault(p => p.Date == date && p.EmployeeId == emp); }
    public Leave? OnLeave(DateOnly date, int emp) { Guard(date); return _leaves.Values.FirstOrDefault(p => p.Date == date && p.EmployeeId == emp); }
    /// <summary>extrasOn(date): phát sinh trong ngày thuộc phạm vi, sắp theo thời điểm tạo.</summary>
    public List<Extra> ExtrasOn(DateOnly date) { Guard(date); return _extras.Values.Where(x => x.Date == date && InScope(x.EmployeeId)).OrderBy(x => x.CreatedAt).ToList(); }
    public Extra? ExtraById(int id) => _extras.GetValueOrDefault(id);
    /// <summary>taskIds của kế hoạch ngày.</summary>
    public static HashSet<int> TaskIdsOf(DayPlan? p) => p is null ? [] : p.PlanTasks.Select(x => x.WorkTaskId).ToHashSet();
    /// <summary>Các việc trong kế hoạch ngày (planTable), sắp theo giờ.</summary>
    public List<WorkTask> PlanTasks(DayPlan p) => p.PlanTasks.Select(x => Task(x.WorkTaskId)).OfType<WorkTask>()
        .OrderBy(t => t.Start, StringComparer.Ordinal).ThenBy(t => t.Id).ToList();

    /// <summary>okCal(t): việc chỉ hiện trên lịch tháng khi đã được duyệt.
    /// Ngoài kế hoạch → Leader đã duyệt kế hoạch đầu ngày chứa việc đó; chuyển tiếp → Leader đã duyệt chuyển.</summary>
    public bool OkCal(WorkTask t)
    {
        if (t.Source == TaskSource.Unplanned)
        {
            var p = PlanOf(t.Date, t.EmployeeId);
            return p?.LeaderDecision == Decision.Ok && TaskIdsOf(p).Contains(t.Id);
        }
        if (t.Source == TaskSource.CarryOver) return t.CarryOk;
        return true;
    }

    public static Counts Count(IEnumerable<WorkTask> list)
    {
        int a = 0, b = 0, c = 0, d = 0;
        foreach (var t in list) switch (t.Status) { case TaskState.Done: a++; break; case TaskState.InProgress: b++; break; case TaskState.Late: c++; break; default: d++; break; }
        return new(a, b, c, d);
    }
    public static Counts CountItems(IEnumerable<PlanItem> list) => Count(list.Select(i => new WorkTask { Status = i.Status }));
    /// <summary>dayCounts(date): đếm các việc ĐÃ CHỌN trong ngày thuộc phạm vi.</summary>
    public Counts DayCounts(DateOnly date) { Guard(date); return Count(_tasks.Values.Where(t => t.Date == date && t.Selected && InScope(t.EmployeeId))); }
    /// <summary>working(date): NV (trong staff()) không nghỉ và có việc trong ngày.</summary>
    public List<Person> Working(DateOnly date) => Staff().Where(p => OnLeave(date, p.Id) is null && TasksOn(date, p.Id).Count > 0).ToList();
    /// <summary>mainProject(emp, date): dự án nhiều việc nhất của NV trong ngày, mặc định dự án đầu tiên.</summary>
    public int? MainProject(int? emp, DateOnly date)
    {
        if (emp is int e)
        {
            var top = TasksOn(date, e).GroupBy(t => t.ProjectId).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (top is not null) return top.Key;
        }
        return ActivePj().FirstOrDefault()?.Id;
    }

    /// <summary>carriesFrom(d): đề xuất chuyển tiếp phát sinh từ ngày d (trong phạm vi).</summary>
    public List<WorkTask> CarriesFrom(DateOnly d) { Guard(d); return _tasks.Values.Where(t => t.Source == TaskSource.CarryOver && t.FromDate == d && InScope(t.EmployeeId)).ToList(); }

    // ======================= Trạng thái =======================

    public static SupportScope SupScope(DayPlan? p) => p?.SupportScope ?? SupportScope.None;
    /// <summary>needTp(p): cần nhân sự nhóm khác / phòng ban khác → thêm bước TP duyệt.</summary>
    public static bool NeedsManager(DayPlan? p) => SupScope(p) is SupportScope.OtherTeam or SupportScope.OtherDept;
    /// <summary>planSt(p)</summary>
    public static PlanState PlanSt(DayPlan? p)
    {
        if (p is null) return PlanState.NotSent;
        if (p.LeaderDecision == Decision.No || p.ManagerDecision == Decision.No) return PlanState.Rejected;
        if (p.LeaderDecision == Decision.Ok) return NeedsManager(p) && p.ManagerDecision != Decision.Ok ? PlanState.PendingManager : PlanState.Approved;
        return PlanState.Pending;
    }
    /// <summary>eodSt(r)</summary>
    public static EodState EodSt(DayReport? r) => r is null ? EodState.NotSent
        : r.LeaderDecision == Decision.Ok ? EodState.Approved : r.LeaderDecision == Decision.No ? EodState.Rejected : EodState.Pending;
    /// <summary>exSt(x): trạng thái hiển thị của phát sinh theo tiến độ việc đã tạo.</summary>
    public ExtraView ExSt(Extra x)
    {
        if (x.Status == ExtraStatus.Pending) return ExtraView.Pending;
        if (x.Status == ExtraStatus.Rejected) return ExtraView.Rejected;
        var t = Task(x.TaskId);
        if (t is null) return ExtraView.Approved;
        return t.Status == TaskState.Done ? ExtraView.Done : t.Status == TaskState.NotStarted ? ExtraView.Approved : ExtraView.InProgress;
    }

    // ======================= Tiến độ dự án =======================

    static int Pct(int part, int total) => total == 0 ? 0 : (int)Math.Round(part * 100.0 / total, MidpointRounding.AwayFromZero);
    /// <summary>pjPct(pid): % đầu mục đã hoàn thành.</summary>
    public int PjPct(int pid) { var it = ItemsOf(pid); return Pct(it.Count(i => i.Status == TaskState.Done), it.Count); }
    /// <summary>pjPlanPct(pid, date): % đầu mục có hạn kết thúc TRƯỚC ngày date (kế hoạch đến hôm nay).</summary>
    public int PjPlanPct(int pid, DateOnly date) { var it = ItemsOf(pid); return Pct(it.Count(i => i.EndDate is DateOnly e && e < date), it.Count); }
    /// <summary>pjStatus(pid, date): Hoàn thành nếu đóng / 100%; override nếu có; ngược lại theo chênh lệch kế hoạch – thực tế
    /// (≤0 Đạt, ≤10 Đang làm, ≤25 Chậm, &gt;25 Nguy cơ).</summary>
    public ProjectStatus PjStatus(int pid, DateOnly date)
    {
        var p = PJ(pid);
        if (p is null) return ProjectStatus.InProgress;
        if (p.Closed || (ItemsOf(pid).Count > 0 && PjPct(pid) == 100)) return ProjectStatus.Completed;
        if (p.StatusOverride is ProjectStatus o) return o;
        var gap = PjPlanPct(pid, date) - PjPct(pid);
        return gap <= 0 ? ProjectStatus.OnTrack : gap <= 10 ? ProjectStatus.InProgress : gap <= 25 ? ProjectStatus.Behind : ProjectStatus.AtRisk;
    }
    /// <summary>need(date, pid): nhu cầu nhân sự trong ngày (ghi đè theo ngày hoặc mặc định của dự án).</summary>
    public int Need(DateOnly date, int pid) { Guard(date); return _needs.Values.FirstOrDefault(n => n.Date == date && n.ProjectId == pid)?.Need ?? PJ(pid)?.Need ?? 0; }
    /// <summary>onSite(date, pid): những người có việc ở dự án trong ngày và không nghỉ.</summary>
    public List<int> OnSite(DateOnly date, int pid) => TasksOn(date).Where(t => t.ProjectId == pid && OnLeave(date, t.EmployeeId) is null)
        .Select(t => t.EmployeeId).Distinct().ToList();

    /// <summary>kpis(date)</summary>
    public Kpi Kpis(DateOnly date) => new(DayCounts(date), Working(date).Count, Staff().Count,
        scope.Role == Role.Leader ? TeamProjects().Count : ActivePj().Count);

    /// <summary>Dự án hiển thị ở bảng tình hình (pjTable): Leader → dự án nhóm quản lý; TP → tất cả dự án đang chạy.</summary>
    public List<Project> TableProjects()
    {
        if (scope.Role != Role.Leader) return ActivePj();
        var teamProjects = TeamProjects();
        return ActivePj().Where(p => teamProjects.Contains(p.Id)).ToList();
    }

    /// <summary>issuesOf(d): phát sinh chờ duyệt, NV báo vướng mắc, dự án nguy cơ, thiếu nhân sự ngày mai.</summary>
    public List<Issue> IssuesOf(DateOnly d)
    {
        var tm = d.AddDays(1);
        Guard(d); Guard(tm);
        var outp = new List<Issue>();
        foreach (var x in ExtrasOn(d).Where(x => x.Status == ExtraStatus.Pending))
            outp.Add(new("var(--bad)", $"{PjName(x.ProjectId)}: {x.Title}", string.IsNullOrEmpty(x.Description) ? "Phát sinh chờ duyệt" : x.Description, x.Start ?? VnTime.Hm(x.CreatedAt)));
        foreach (var r in _reports.Values.Where(r => r.Date == d && r.Blocked && InScope(r.EmployeeId)))
            outp.Add(new("var(--warn)", $"{PName(r.EmployeeId)} báo vướng mắc", r.Note, VnTime.Hm(r.SentAt)));
        foreach (var p in ActivePj().Where(p => PjStatus(p.Id, d) == ProjectStatus.AtRisk))
            outp.Add(new("var(--bad)", $"{p.Name}: nguy cơ trễ tiến độ", $"Kế hoạch {PjPlanPct(p.Id, d)}% · thực tế {PjPct(p.Id)}%", ""));
        foreach (var p in ActivePj())
        {
            var gap = OnSite(tm, p.Id).Count - Need(tm, p.Id);
            if (gap < 0) outp.Add(new("var(--brand)", $"Thiếu {-gap} nhân sự ngày {Fmt.Dm(tm)}", $"Dự án {p.Name}", ""));
        }
        return outp;
    }

    // ======================= Làm thêm =======================

    public Overtime? OtOf(DateOnly d, int personId) { Guard(d); return _ots.Values.FirstOrDefault(x => x.Date == d && x.PersonId == personId); }
    /// <summary>otPending(d): đăng ký cần mình duyệt — Leader duyệt NV trong nhóm; TP duyệt Leader.</summary>
    public List<Overtime> OtPending(DateOnly d)
    {
        Guard(d);
        var r = scope.Role == Role.Leader ? Role.Staff : Role.Leader;
        return _ots.Values.Where(x => x.Date == d && x.Role == r && InScope(x.PersonId)).ToList();
    }

    // ======================= Đếm cho badge thanh tab =======================

    /// <summary>Số mục chờ Leader xử lý trong ngày (badge tab Kế hoạch &amp; duyệt).</summary>
    public int LeaderPending(DateOnly d) =>
        _plans.Values.Count(p => p.Date == d && PlanSt(p) == PlanState.Pending && InScope(p.EmployeeId))
        + ExtrasOn(d).Count(x => x.Status == ExtraStatus.Pending)
        + _reports.Values.Count(r => r.Date == d && EodSt(r) == EodState.Pending && InScope(r.EmployeeId))
        + CarriesFrom(d).Count(t => !t.CarryOk);
}
