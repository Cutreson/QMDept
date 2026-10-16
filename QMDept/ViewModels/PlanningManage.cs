using Microsoft.AspNetCore.WebUtilities;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

// View-model + tiện ích cho các tab con "quản lý" của Leader → Kế hoạch & duyệt
// (PlanningController.Manage.cs: Lịch tuần, Dự án & đầu mục, Nhân sự, Phân công).

/// <summary>ldWeek: lịch tuần của nhóm.</summary>
public sealed class PlanningWeekVm
{
    /// <summary>Thứ 2 của tuần đang xem.</summary>
    public required DateOnly Monday { get; init; }
    public required List<DateOnly> Days { get; init; }
    /// <summary>staff(): NV trong phạm vi.</summary>
    public required List<Person> Staff { get; init; }
    /// <summary>NV đang chọn (?emp=, mặc định NV đầu tiên); null = nhóm trống.</summary>
    public Person? Emp { get; init; }
}

/// <summary>ldItems: dự án &amp; đầu mục.</summary>
public sealed class PlanningProjectsVm
{
    /// <summary>Mọi dự án (kể cả đã bàn giao), theo thứ tự.</summary>
    public required List<Project> Projects { get; init; }
    /// <summary>Khóa chip đang chọn: id dự án hoặc "new".</summary>
    public string? Key { get; init; }
    /// <summary>Dự án đang sửa; null = form "Dự án mới".</summary>
    public Project? Project { get; init; }
}

/// <summary>ldPeople: nhân sự / tài khoản.</summary>
public sealed class PlanningPeopleVm
{
    /// <summary>Tài khoản đang đăng nhập là Trưởng phòng (quản lý tài khoản).</summary>
    public required bool IsManager { get; init; }
    public required List<Person> People { get; init; }
    /// <summary>Leader đang xem (tiêu đề "Nhóm của …" khi không phải TP).</summary>
    public Person? Leader { get; init; }
}

/// <summary>Chọn Leader dự án: dự án đang triển khai + NV trong phạm vi (nhóm của Leader / mọi NV với TP).</summary>
public sealed class PlanningLeadsVm
{
    public required List<Project> Projects { get; init; }
    public required List<Person> Staff { get; init; }
}

/// <summary>ldAssign: phân công.</summary>
public sealed class PlanningAssignVm
{
    public required List<Person> Staff { get; init; }
    /// <summary>Việc "Leader giao" từ 7 ngày trước trở đi trong phạm vi (tối đa 15, mới nhất trước).</summary>
    public required List<WorkTask> Given { get; init; }
}

/// <summary>Tiện ích dùng chung cho PlanningController.Manage.cs và các view của nó.</summary>
public static class PlanningManage
{
    /// <summary>Trạng thái chọn được cho đầu mục (TS bỏ "Chậm").</summary>
    public static readonly TaskState[] ItemStates = [TaskState.NotStarted, TaskState.InProgress, TaskState.Done];
    /// <summary>Trạng thái ghi đè của dự án (projForm).</summary>
    public static readonly ProjectStatus[] OverrideStates = [ProjectStatus.OnTrack, ProjectStatus.InProgress, ProjectStatus.Behind, ProjectStatus.AtRisk];
    /// <summary>Vai trò tài khoản theo thứ tự của TITLE.</summary>
    public static readonly Role[] Roles = [Role.Staff, Role.Leader, Role.Manager];

    /// <summary>Giá trị cho &lt;input type="date"&gt; (null = bỏ thuộc tính).</summary>
    public static string? Iso(DateOnly? d) => d is DateOnly x ? Fmt.Key(x) : null;

    /// <summary>Đọc ngày "yyyy-MM-dd" từ form; rỗng / sai → null.</summary>
    public static DateOnly? Date(string? s) => string.IsNullOrWhiteSpace(s) ? null : Fmt.ParseKey(s.Trim());

    /// <summary>Cắt khoảng trắng; rỗng → null; dài quá → BizException.</summary>
    public static string? Text(string? s, int max, string label)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (t.Length > max) throw new BizException($"{label} tối đa {max} ký tự");
        return t;
    }

    /// <summary>Đọc enum theo tên; giá trị không thuộc <paramref name="allowed"/> → null.</summary>
    public static T? ParseEnum<T>(string? s, IEnumerable<T>? allowed = null) where T : struct, System.Enum =>
        System.Enum.TryParse<T>(s, false, out var v) && System.Enum.IsDefined(v) && (allowed is null || allowed.Contains(v)) ? v : null;

    /// <summary>Sắp người như ldPeople: TP → Leader → NV; NV theo tên Leader; rồi theo thứ tự + tên (byOrder).</summary>
    public static List<Person> SortPeople(Dept d, IEnumerable<Person> src)
    {
        static int Ord(Role r) => r switch { Role.Manager => 0, Role.Leader => 1, _ => 2 };
        var l = src.ToList();
        l.Sort((a, b) =>
        {
            var c = Ord(a.Role).CompareTo(Ord(b.Role));
            if (c == 0 && a.Role == Role.Staff) c = Fmt.ViCompare.Compare(LeaderName(d, a), LeaderName(d, b));
            if (c == 0) c = a.SortOrder.CompareTo(b.SortOrder);
            return c != 0 ? c : Fmt.ViCompare.Compare(a.Name, b.Name);
        });
        return l;
    }

    /// <summary>pname(p.leader): tên Leader của nhóm; "—" nếu chưa xếp nhóm hoặc Leader đã bị xóa (như artifact).</summary>
    public static string LeaderName(Dept d, Person p) => p.LeaderId is int l && d.P(l) is { IsDeleted: false } x ? x.Name : "—";

    /// <summary>Ném lỗi nếu người được thao tác không phải NV còn hoạt động (Dept phải đã nạp).</summary>
    public static Person RequireStaff(Dept d, int emp) =>
        d.P(emp) is { IsDeleted: false, Role: Role.Staff } p ? p : throw new BizException("Không tìm thấy nhân viên");

    /// <summary>Đặt một tham số query trên URL nội bộ (dùng để chuyển sang ?pj= dự án vừa tạo).</summary>
    public static string WithQuery(string url, string key, string value)
    {
        var qi = url.IndexOf('?');
        var path = qi >= 0 ? url[..qi] : url;
        var q = qi >= 0 ? QueryHelpers.ParseQuery(url[qi..]).ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString()) : [];
        q[key] = value;
        return QueryHelpers.AddQueryString(path, q);
    }
}

/// <summary>Đề xuất lịch của NV trong nhóm: chờ duyệt + đã quyết định gần đây (PlanningController.Proposals).</summary>
public sealed class PlanningProposalsVm
{
    public required List<CalendarProposal> Pending { get; init; }
    public required List<CalendarProposal> Recent { get; init; }
}

/// <summary>TP chọn Leader (nhóm) phụ trách dự án: dự án đang triển khai + danh sách Leader + bộ lọc (all | none).</summary>
public sealed class PlanningOwnersVm
{
    public required List<Project> Projects { get; init; }
    public required List<Person> Leaders { get; init; }
    public required string Filter { get; init; }
    public int Total { get; init; }
    public int Unassigned { get; init; }
}
