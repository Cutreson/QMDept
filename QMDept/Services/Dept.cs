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
    /// <summary>Đề xuất lịch của NV đang chờ Leader duyệt (CalendarProposal, kèm People).</summary>
    public List<CalendarProposal> PendingProposals { get; private set; } = [];
    /// <summary>Đề xuất xin nghỉ đang chờ duyệt (mọi ngày).</summary>
    public List<LeaveRequest> PendingLeaves { get; private set; } = [];
    /// <summary>Xin nghỉ chờ Leader <paramref name="leaderId"/> duyệt (NV trong nhóm).</summary>
    public List<LeaveRequest> LeavesFor(int leaderId) => PendingLeaves.Where(x => x.Role == Role.Staff && P(x.PersonId) is { IsDeleted: false } p && p.LeaderId == leaderId).OrderBy(x => x.Date).ThenBy(x => x.CreatedAt).ToList();
    /// <summary>Xin nghỉ của Leader chờ Trưởng phòng duyệt.</summary>
    public List<LeaveRequest> LeaderLeaves() => PendingLeaves.Where(x => x.Role == Role.Leader && P(x.PersonId) is { IsDeleted: false }).OrderBy(x => x.Date).ThenBy(x => x.CreatedAt).ToList();
    /// <summary>Đề xuất chờ duyệt của NV thuộc nhóm của Leader <paramref name="leaderId"/>.</summary>
    public List<CalendarProposal> ProposalsFor(int leaderId) => PendingProposals.Where(x => P(x.ProposerId) is { IsDeleted: false } p && p.LeaderId == leaderId).OrderBy(x => x.Date).ThenBy(x => x.CreatedAt).ToList();
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
            People = b.People; Projects = b.Projects; Items = b.Items; Assigns = b.Assigns; PendingProposals = b.PendingProposals; PendingLeaves = b.PendingLeaves;
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
    public bool InScope(int emp) => P(emp) is { IsDeleted: false } p0 && (scope.Role != Role.Leader
        || (scope.MeId is int id && (emp == id || p0.LeaderId == id)));
    /// <summary>Chỉ NV (không tính Leader) trong phạm vi — dùng cho phần duyệt của Leader (Leader tự báo cáo thì Trưởng phòng duyệt).</summary>
    public bool IsTeamStaff(int emp) => P(emp) is { Role: Role.Staff } && InScope(emp);
    /// <summary>Leader (còn hoạt động, đúng vai trò Leader) của NV; null = "Chưa xếp nhóm".</summary>
    public Person? LeaderOf(Person a) => a.LeaderId is int l && P(l) is { IsDeleted: false, Role: Role.Leader } x ? x : null;
    /// <summary>"Nhóm X" / "Chưa xếp nhóm".</summary>
    public string GroupText(Person a) => LeaderOf(a) is { } l ? "Nhóm " + l.Name : "Chưa xếp nhóm";
    /// <summary>NV chưa xếp nhóm (không có Leader duyệt kế hoạch / báo cáo) — TP cần xếp nhóm.</summary>
    public List<Person> Unassigned() => AllStaff().Where(p => LeaderOf(p) is null).ToList();
    /// <summary>accTeam(a): mô tả nhóm của tài khoản.</summary>
    public string TeamText(Person a) => a.Role switch
    {
        Role.Staff => GroupText(a),
        Role.Leader => $"Leader · nhóm {Team(a.Id).Count} người",
        Role.Admin => "Quản trị hệ thống",
        _ => "Trưởng phòng",
    };

    /// <summary>
    /// Số lượng dự án (yêu cầu 2026-10-16): tổng / đang triển khai (chưa bàn giao và chưa hoàn thành) / đã hoàn thành (đóng hoặc 100%),
    /// theo trạng thái ở ngày <paramref name="date"/>. Hiện ở mọi màn hình "Thông tin dự án".
    /// </summary>
    public (int Total, int Active, int Done) PjCounts(IEnumerable<Project> ps, DateOnly date)
    {
        var l = ps.ToList();
        var done = l.Count(p => p.Closed || PjStatus(p.Id, date) == ProjectStatus.Completed);
        return (l.Count, l.Count - done, done);
    }
    public string PjCountText(IEnumerable<Project> ps, DateOnly date)
    {
        var (t, a, d) = PjCounts(ps, date);
        return $"{t} dự án · {a} đang triển khai · {d} đã hoàn thành";
    }
    /// <summary>Dự án người đang đăng nhập được thấy: Leader chỉ thấy dự án TP đã giao cho nhóm mình phụ trách (yêu cầu 2026-10-20); TP / Quản trị / NV thấy như cũ.</summary>
    IEnumerable<Project> VisiblePj() => scope.Resolved && scope.Account is { Role: Role.Leader } a ? Projects.Where(p => p.OwnerLeaderId == a.Id) : Projects;
    public List<Project> ActivePj() { GuardBase(); return VisiblePj().Where(p => !p.Closed).OrderBy(p => p.SortOrder).ThenBy(p => p.Name, Fmt.ViCompare).ToList(); }
    public List<Project> AllPj() { GuardBase(); return VisiblePj().OrderBy(p => p.SortOrder).ThenBy(p => p.Name, Fmt.ViCompare).ToList(); }
    public List<PlanItem> ItemsOf(int pid) { GuardBase(); return Items.Where(i => i.ProjectId == pid).OrderBy(i => i.SortOrder).ThenBy(i => i.Title, Fmt.ViCompare).ToList(); }
    /// <summary>tile(pid): màu ô dự án.</summary>
    public string Tile(int pid) { GuardBase(); var i = Math.Max(0, Projects.OrderBy(p => p.Id).ToList().FindIndex(p => p.Id == pid)); return Fmt.Tile[i % Fmt.Tile.Length]; }
    /// <summary>Dòng phụ "Khách hàng – Địa điểm".</summary>
    public static string Place(Project p) => string.Join(" – ", new[] { p.Factory, p.Location }.Where(s => !string.IsNullOrWhiteSpace(s)));
    /// <summary>Thông tin dự án nạp từ file "thông tin DA.xlsx": mã, số lượng, Leader phụ trách, loại, cấp độ, PO, giao hàng, ghi chú (chỉ dòng có dữ liệu).</summary>
    public List<(string Label, string Value)> ProjectInfo(Project p)
    {
        var l = new List<(string, string)>();
        if (!string.IsNullOrEmpty(p.Code)) l.Add(("Mã dự án", p.Code));
        if (p.Quantity is int q) l.Add(("Số lượng", q.ToString()));
        if (p.OwnerLeaderId is int o && P(o) is { } ol) l.Add(("Leader phụ trách", ol.Name));
        if (!string.IsNullOrEmpty(p.Category)) l.Add(("Loại", p.Category));
        if (!string.IsNullOrEmpty(p.Level)) l.Add(("Cấp độ", p.Level));
        if (p.PoDate is DateOnly po) l.Add(("Ngày PO", Fmt.Dmy(po)));
        if (p.DeliveryDate is DateOnly dl) l.Add(("Giao hàng theo PO", Fmt.Dmy(dl)));
        if (!string.IsNullOrEmpty(p.Note)) l.Add(("Ghi chú", p.Note));
        return l;
    }
    /// <summary>Dòng phụ của dự án: "Mã · Khách hàng – Địa điểm".</summary>
    public static string SubLine(Project p) => string.Join(" · ", new[] { p.Code, Place(p) }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>assignsOf(emp): phân công còn hiệu lực (dự án chưa đóng).</summary>
    public List<Assignment> AssignsOf(int emp) => Assigns.Where(a => a.EmployeeId == emp && PJ(a.ProjectId) is { Closed: false }).ToList();
    public Assignment? AssignOf(int pid, int emp) => Assigns.Find(a => a.ProjectId == pid && a.EmployeeId == emp);
    /// <summary>Leader dự án (vai trò Lead trong assign; mỗi dự án một người — xem WorkflowService.UpsertAssignmentAsync). Tài khoản đã xóa không tính.</summary>
    public Person? ProjectLead(int pid) => Assigns.Where(a => a.ProjectId == pid && a.Role == ProjectRole.Lead)
        .Select(a => P(a.EmployeeId)).OfType<Person>().FirstOrDefault(p => !p.IsDeleted);
    /// <summary>
    /// Việc của NGƯỜI HỖ TRỢ: dự án đã có Leader dự án và người làm không phải Leader dự án đó (yêu cầu 2026-10-14). Mọi vấn đề / nội dung của
    /// dự án do Leader dự án báo cáo cuối ngày; người hỗ trợ chỉ tích "đã hỗ trợ". Dự án chưa chọn Leader dự án → báo cáo đầy đủ như trước.
    /// </summary>
    public bool IsSupportTask(WorkTask t) => ProjectLead(t.ProjectId) is { } l && l.Id != t.EmployeeId;
    /// <summary>Việc của người khác (đã chọn trong ngày) ở các dự án mà <paramref name="lead"/> là Leader dự án — Leader dự án phải báo cáo kết quả.</summary>
    public List<WorkTask> TeamTasksOf(int lead, DateOnly date) =>
        TasksOn(date).Where(t => t.Selected && t.EmployeeId != lead && ProjectLead(t.ProjectId)?.Id == lead).ToList();
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
    /// <summary>Nghỉ cả ngày (ghi tay không chi tiết, nghỉ phép cả ngày, hoặc nghỉ không lương đủ 8 giờ) — người này không làm việc trong ngày.</summary>
    public static bool IsFullDayLeave(Leave l) => l.LeaveType is null || l.LeaveSession == LeaveSession.FullDay || (l.LeaveType == LeaveType.Unpaid && (l.LeaveHours ?? 8) >= 8);
    Leave? RawLeave(DateOnly date, int emp) { Guard(date); return _leaves.Values.FirstOrDefault(p => p.Date == date && p.EmployeeId == emp); }
    public Leave? OnLeave(DateOnly date, int emp) => RawLeave(date, emp) is { } l && IsFullDayLeave(l) ? l : null;
    /// <summary>Nghỉ một phần ngày (nửa ngày / vài giờ) đã được duyệt — người này vẫn làm việc phần còn lại (yêu cầu 2026-10-20).</summary>
    public Leave? PartLeave(DateOnly date, int emp) => RawLeave(date, emp) is { } l && !IsFullDayLeave(l) ? l : null;
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
    /// <summary>Việc chuyển tiếp từ ngày d của NV trong nhóm Leader (theo tài khoản Leader, không phụ thuộc người đang xem).</summary>
    public List<WorkTask> CarriesFromTeam(DateOnly d, int leaderId) { Guard(d); return _tasks.Values.Where(t => t.Source == TaskSource.CarryOver && t.FromDate == d && P(t.EmployeeId) is { IsDeleted: false, Role: Role.Staff } p && p.LeaderId == leaderId).ToList(); }
    public List<WorkTask> CarriesFrom(DateOnly d) { Guard(d); return _tasks.Values.Where(t => t.Source == TaskSource.CarryOver && t.FromDate == d && InScope(t.EmployeeId)).ToList(); }

    // ======================= Trạng thái =======================

    public static SupportScope SupScope(DayPlan? p) => p?.SupportScope ?? SupportScope.None;
    /// <summary>needTp(p): cần nhân sự nhóm khác / phòng ban khác → thêm bước TP duyệt.</summary>
    public static bool NeedsManager(DayPlan? p) => SupScope(p) is SupportScope.OtherDept;   // nhóm khác trong phòng KHÔNG qua TP (Leader nhóm đó xác nhận — SupportRequest, yêu cầu 2026-10-23)
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
        ActivePj().Count);

    /// <summary>Dự án hiển thị ở bảng tình hình (pjTable): Leader → dự án được giao cho nhóm; TP → tất cả dự án đang chạy (ActivePj đã lọc theo tài khoản).</summary>
    public List<Project> TableProjects() => ActivePj();

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
        if (scope.Role == Role.Leader) return _ots.Values.Where(x => x.Date == d && x.Role == Role.Staff && InScope(x.PersonId)).ToList();
        // TP: đăng ký của Leader + đăng ký của NV mà Leader đã duyệt (kết thúc sau 20:00 thì TP duyệt tiếp; còn lại TP chỉ xem thông tin đã cập nhật lên) — yêu cầu 2026-10-21.
        return _ots.Values.Where(x => x.Date == d && InScope(x.PersonId) && (x.Role == Role.Leader
            || (x.Role == Role.Staff && (x.Status is OtStatus.Approved or OtStatus.PendingManager || (x.Status == OtStatus.Rejected && x.LeaderAt != null))))).ToList();
    }

    // ======================= Đếm cho badge thanh tab =======================

    /// <summary>Số mục chờ duyệt ở "Kế hoạch đề xuất" của Leader <paramref name="lid"/> (tính theo tài khoản, không phụ thuộc người đang xem):
    /// kế hoạch đầu ngày + phát sinh + đăng ký làm thêm (hoặc kết quả chờ duyệt) của NV trong nhóm + đề xuất lịch (xin nghỉ đếm riêng: LeavesFor).</summary>
    public int LeaderInbox(DateOnly d, int lid)
    {
        Guard(d);
        bool Team(int emp) => P(emp) is { IsDeleted: false, Role: Role.Staff } p && p.LeaderId == lid;
        return _plans.Values.Count(p => p.Date == d && p.LeaderDecision is null && Team(p.EmployeeId))
            + _extras.Values.Count(x => x.Date == d && x.Status == ExtraStatus.Pending && Team(x.EmployeeId))
            + _ots.Values.Count(x => x.Date == d && x.Role == Role.Staff && x.NeedsDecision && Team(x.PersonId))
            + ProposalsFor(lid).Count;
    }

    /// <summary>Số mục chờ Leader xử lý trong ngày (badge tab Kế hoạch &amp; duyệt).</summary>
    public int LeaderPending(DateOnly d) =>
        _plans.Values.Count(p => p.Date == d && PlanSt(p) == PlanState.Pending && IsTeamStaff(p.EmployeeId))
        + ExtrasOn(d).Count(x => x.Status == ExtraStatus.Pending)
        + _reports.Values.Count(r => r.Date == d && EodSt(r) == EodState.Pending && IsTeamStaff(r.EmployeeId))
        + CarriesFrom(d).Count(t => !t.CarryOk);

    // ---- Số đỏ trên thanh tab: đếm MỌI ngày đã nạp (không chỉ ngày đang xem) để số không lúc có lúc không (yêu cầu 2026-10-21) ----
    // Tham số tường minh theo TÀI KHOẢN đăng nhập (không theo scope) để số đỏ vẫn đúng khi đang ở trang cá nhân ?as=mine.
    public int LeaderPendingAll(int lid)
    {
        bool Team(int emp) => P(emp) is { IsDeleted: false, Role: Role.Staff } p && p.LeaderId == lid;
        bool Scoped(int emp) => P(emp) is { IsDeleted: false } p && (emp == lid || p.LeaderId == lid);
        return _plans.Values.Count(p => PlanSt(p) == PlanState.Pending && Team(p.EmployeeId))
            + _extras.Values.Count(x => x.Status == ExtraStatus.Pending && Scoped(x.EmployeeId))
            + _reports.Values.Count(r => EodSt(r) == EodState.Pending && Team(r.EmployeeId))
            + _tasks.Values.Count(t => t.Source == TaskSource.CarryOver && t.FromDate != null && !t.CarryOk && Team(t.EmployeeId));
    }
    public IEnumerable<Overtime> OtPendingAll(Role role, int me) => _ots.Values.Where(x => P(x.PersonId) is { IsDeleted: false } p && (role == Role.Leader
        ? x.Role == Role.Staff && p.LeaderId == me
        : x.Role == Role.Leader || (x.Role == Role.Staff && (x.Status is OtStatus.Approved or OtStatus.PendingManager || (x.Status == OtStatus.Rejected && x.LeaderAt != null)))));

    /// <summary>
    /// Các ngày có việc đang chờ tài khoản này xử lý (duyệt / kiểm tra) → số lượng mỗi ngày (yêu cầu 2026-10-22): chấm đỏ trên lịch tháng + thanh ngày.
    /// Tính theo TÀI KHOẢN đăng nhập, mọi ngày đã nạp (±30 ngày quanh hôm nay) + đề xuất / xin nghỉ ở mọi ngày.
    /// </summary>
    public Dictionary<DateOnly, int> PendingDays(Role role, int me)
    {
        var r = new Dictionary<DateOnly, int>();
        void Add(DateOnly d, int n = 1) => r[d] = r.GetValueOrDefault(d) + n;
        if (role == Role.Leader)
        {
            bool Team(int emp) => P(emp) is { IsDeleted: false, Role: Role.Staff } p && p.LeaderId == me;
            bool Scoped(int emp) => P(emp) is { IsDeleted: false } p && (emp == me || p.LeaderId == me);
            foreach (var p in _plans.Values) if (PlanSt(p) == PlanState.Pending && Team(p.EmployeeId)) Add(p.Date);
            foreach (var x in _extras.Values) if (x.Status == ExtraStatus.Pending && Scoped(x.EmployeeId)) Add(x.Date);
            foreach (var x in _reports.Values) if (EodSt(x) == EodState.Pending && Team(x.EmployeeId)) Add(x.Date);
            foreach (var t in _tasks.Values) if (t.Source == TaskSource.CarryOver && t.FromDate is DateOnly fd && !t.CarryOk && Team(t.EmployeeId)) Add(fd);
            foreach (var o in OtPendingAll(Role.Leader, me)) if (o.NeedsDecision) Add(o.Date);
            foreach (var x in ProposalsFor(me)) Add(x.Date);
            foreach (var x in LeavesFor(me)) Add(x.Date);
        }
        else if (role is Role.Manager or Role.Admin)
        {
            foreach (var p in _plans.Values)
            {
                var lead = P(p.EmployeeId) is { Role: Role.Leader };
                if (PlanSt(p) == (lead ? PlanState.Pending : PlanState.PendingManager)) Add(p.Date);
            }
            foreach (var x in LeaderLeaves()) Add(x.Date);
            foreach (var o in OtPendingAll(Role.Manager, me)) if (o.NeedsTpDecision) Add(o.Date);
        }
        else
        {
            foreach (var x in _extras.Values) if (x.EmployeeId == me && x.Status == ExtraStatus.Pending) Add(x.Date);
        }
        return r;
    }
}
