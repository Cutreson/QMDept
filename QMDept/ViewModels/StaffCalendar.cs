using Microsoft.Extensions.Primitives;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>Model của /Staff/Month (nvMonth): tháng đang xem, chế độ chỉ xem / "Tất cả nhân sự", danh sách người (poolFor('nv')).</summary>
public sealed class StaffMonthVm
{
    /// <summary>Ngày 1 của tháng đang xem (S.month || đầu tháng của S.date).</summary>
    public required DateOnly MonthStart { get; init; }
    /// <summary>isRO(): Leader/TP đang xem lịch NV.</summary>
    public required bool ReadOnly { get; init; }
    /// <summary>allMode = isRO() &amp;&amp; S.allNv.</summary>
    public required bool AllMode { get; init; }
    /// <summary>NV đang xem (S.me.nv); null khi nhóm trống.</summary>
    public Person? Me { get; init; }
    /// <summary>Đề xuất lịch của chính NV trong ngày đang chọn (nvMonth, chế độ một người, đang ở màn hình của mình).</summary>
    public List<CalendarProposal> Proposals { get; init; } = [];
    /// <summary>Đề xuất xin nghỉ của chính người đang đăng nhập trong ngày đang chọn (Đề xuất công việc → Xin nghỉ).</summary>
    public List<LeaveRequest> LeaveRequests { get; init; } = [];
    /// <summary>ppl = poolFor('nv') (chỉ dùng ở chế độ "Tất cả nhân sự").</summary>
    public List<Person> People { get; init; } = [];

    public int Days => Fmt.DaysInMonth(MonthStart);
    /// <summary>Số ô trống đầu lưới (lead = (wday(ms)+6)%7).</summary>
    public int Lead => (Fmt.Wday(MonthStart) + 6) % 7;
    public bool InMonth(DateOnly d) => d.Year == MonthStart.Year && d.Month == MonthStart.Month;
}

/// <summary>Model của /Staff/Projects (nvProjects): danh sách dự án theo nhóm, hoặc chi tiết một dự án (projectDetail).</summary>
public sealed class StaffProjectsVm
{
    /// <summary>Có ?pj= → hiện chi tiết dự án (partial _ProjectDetail).</summary>
    public ProjectDetailVm? Detail { get; init; }
    /// <summary>Nhóm đang chọn (?pt=): "active" (Đang triển khai) | "done" (Đã hoàn thành).</summary>
    public string Tab { get; init; } = "active";
    public List<Project> Active { get; init; } = [];
    public List<Project> Done { get; init; } = [];
    /// <summary>Nhãn vai trò của NV theo dự án ("Leader dự án" / "Hỗ trợ dự án" / "Bạn đang tham gia").</summary>
    public Dictionary<int, string> Mine { get; init; } = [];

    public List<Project> List => Tab == "done" ? Done : Active;
    public string? MineOf(int pid) => Mine.GetValueOrDefault(pid);
}

/// <summary>Helper cho Kế hoạch tháng / Giao thêm việc (nvMonth, teamDayList, calAssignForm).</summary>
public static class StaffCalendar
{
    /// <summary>
    /// Leader / TP được xóa / chuyển lịch việc nào (yêu cầu 2026-10-20): việc của chính mình; Leader thêm việc của nhân viên trong nhóm mình;
    /// TP / Quản trị thêm việc của Leader (không vượt cấp xuống nhân viên). Không áp dụng khi đang xem màn hình người khác.
    /// </summary>
    public static bool OwnsTask(Person actor, bool isManager, bool viewingOther, Person? emp, WorkTask t)
    {
        if (viewingOther || emp is null || emp.IsDeleted || emp.Id != t.EmployeeId) return false;
        if (emp.Id == actor.Id) return actor.Role is Role.Leader or Role.Manager or Role.Admin;
        return actor.Role == Role.Leader ? emp.Role == Role.Staff && emp.LeaderId == actor.Id : isManager && emp.Role == Role.Leader;
    }

    /// <summary>Hiện nút Sửa / Xóa / Chuyển lịch cho việc này không (quyền + chưa khóa).</summary>
    public static bool CanManage(ViewScope s, Dept d, WorkTask t) => s.CanLead && OwnsTask(s.Account, s.IsManager, s.IsViewingOther, d.P(t.EmployeeId), t) && !TaskLocked(d, t);

    /// <summary>Việc đã có kết quả (Hoàn thành / Chậm) hoặc NV đã gửi báo cáo cuối ngày → khóa, không xóa / chuyển được.</summary>
    public static bool TaskLocked(Dept d, WorkTask t) => t.Status is TaskState.Done or TaskState.Late || d.ReportOf(t.Date, t.EmployeeId) is not null;

    /// <summary>Màu ô ngày ở chế độ một nhân viên (nvMonth): không việc / xong hết / có chậm / hôm nay / còn lại.</summary>
    public static (string Bg, string Fg) CellColor(IReadOnlyCollection<WorkTask> t, bool isToday)
    {
        if (t.Count == 0) return ("var(--surface-2)", "var(--faint)");
        var c = Dept.Count(t);
        if (c.Done == t.Count) return ("var(--ok)", "#fff");
        if (c.Late > 0) return ("var(--bad-soft)", "var(--bad)");
        return isToday ? ("var(--brand)", "#fff") : ("var(--brand-soft)", "var(--brand)");
    }

    /// <summary>Màu ô ngày ở chế độ "Tất cả nhân sự": không ai có việc / có việc chậm / hôm nay / còn lại.</summary>
    public static (string Bg, string Fg) TeamCellColor(int busy, bool late, bool isToday)
    {
        if (busy == 0) return ("var(--surface-2)", "var(--faint)");
        if (late) return ("var(--bad-soft)", "var(--bad)");
        return isToday ? ("var(--brand)", "#fff") : ("var(--brand-soft)", "var(--brand)");
    }

    /// <summary>
    /// pj0 của calAssignForm(d, ''): mainProject('', d) — dự án có nhiều việc nhất trong ngày (mọi người), mặc định dự án đầu tiên.
    /// Nếu dự án đó đã đóng (không có trong danh sách chọn) thì lấy dự án đang chạy đầu tiên.
    /// </summary>
    public static int? DefaultProject(Dept d, DateOnly date)
    {
        var active = d.AllPj();   // gồm cả dự án đã hoàn thành (vẫn phải triển khai khi khách hàng có sự cố)
        var top = d.TasksOn(date).GroupBy(t => t.ProjectId).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
        return top is int p && active.Any(x => x.Id == p) ? p : active.FirstOrDefault()?.Id;
    }

    /// <summary>
    /// Đọc người tham gia của form Giao thêm việc: checkbox "emp" (id). Vai trò do dự án quyết định (Leader dự án đã chọn = Leader dự án,
    /// còn lại = hỗ trợ — WorkflowService.AssignTaskAsync). Mọi người phải thuộc nhóm NV của tài khoản (poolFor('nv')).
    /// </summary>
    public static List<int> ReadPeople(IFormCollection form, IReadOnlyCollection<Person> pool)
    {
        var list = new List<int>();
        foreach (var raw in form["emp"])
        {
            if (!int.TryParse(raw, out var id)) throw new BizException("Không tìm thấy nhân viên");
            if (list.Contains(id)) continue;
            if (!pool.Any(p => p.Id == id)) throw new BizException("Nhân viên không thuộc nhóm bạn quản lý");
            list.Add(id);
        }
        return list;
    }
}
