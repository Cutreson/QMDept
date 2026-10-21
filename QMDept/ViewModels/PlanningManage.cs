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
    /// <summary>"has" = dự án đã có Leader dự án; "none" = chưa chọn.</summary>
    public string Filter { get; init; } = "has";
    public int HasCount { get; init; }
    public int NoneCount { get; init; }
    /// <summary>Mọi dự án Leader được quản lý (cả hai mục) — cho ô chọn dự án.</summary>
    public List<Project> AllProjects { get; init; } = [];
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
    /// <summary>Chuỗi tìm kiếm của dự án (không dấu, chữ thường): tên + mã + khách hàng + địa điểm + Leader phụ trách.</summary>
    public static string SearchKey(Project p, string? leader) => Norm(string.Join(" ", new[] { p.Name, p.Code, p.Factory, p.Location, leader }.Where(s => !string.IsNullOrWhiteSpace(s))));

    /// <summary>Bỏ dấu tiếng Việt + chữ thường (khớp với hàm norm() trong trang).</summary>
    public static string Norm(string s)
    {
        var d = (s ?? "").Normalize(System.Text.NormalizationForm.FormD).Replace("đ", "d").Replace("Đ", "D");
        return new string(d.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
    }

    /// <summary>Trạng thái chọn được cho đầu mục (TS bỏ "Chậm").</summary>
    public static readonly TaskState[] ItemStates = [TaskState.NotStarted, TaskState.InProgress, TaskState.Done];
    /// <summary>Trạng thái ghi đè của dự án (projForm).</summary>
    public static readonly ProjectStatus[] OverrideStates = [ProjectStatus.OnTrack, ProjectStatus.InProgress, ProjectStatus.Behind, ProjectStatus.AtRisk];
    /// <summary>Vai trò tài khoản theo thứ tự của TITLE.</summary>
    public static readonly Role[] Roles = [Role.Staff, Role.Leader, Role.Manager];

    /// <summary>Giá trị cho ô nhập ngày (dd/MM/yyyy; null = rỗng).</summary>
    public static string? Iso(DateOnly? d) => d is DateOnly x ? Fmt.Dmy(x) : null;

    /// <summary>Đọc ngày dd/MM/yyyy (hoặc yyyy-MM-dd) từ form; rỗng / sai → null.</summary>
    public static DateOnly? Date(string? s) => string.IsNullOrWhiteSpace(s) ? null : Fmt.ParseInput(s);

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
    /// <summary>Dự án đang triển khai của mục đang chọn (Đã có Leader / Chưa có Leader).</summary>
    public required List<Project> Projects { get; init; }
    public required List<Person> Leaders { get; init; }
    /// <summary>"has" = đã có Leader phụ trách; "none" = chưa có (?f=).</summary>
    public required string Filter { get; init; }
    public int Total { get; init; }
    public int Unassigned { get; init; }
    public int Assigned => Total - Unassigned;
    /// <summary>Mọi dự án (cả đã hoàn thành) — cho ô tìm kiếm: kiểm tra dự án đã có trên hệ thống chưa và Leader nào phụ trách (yêu cầu 2026-10-21).</summary>
    public List<Project> AllProjects { get; init; } = [];
    /// <summary>Thống kê tổng quan: từng Leader đang phụ trách những dự án nào (yêu cầu 2026-10-20).</summary>
    public required List<(Person Leader, List<Project> Projects)> Stats { get; init; }
}

/// <summary>
/// Leader → Kế hoạch &amp; duyệt → "Kế hoạch đề xuất" (yêu cầu 2026-10-19): gộp kế hoạch đầu ngày của NV + đề xuất lịch + đăng ký làm thêm,
/// chia 2 mục riêng "Chờ duyệt" / "Đã duyệt" (?f=pending|approved).
/// </summary>
public sealed class PlanningInboxVm
{
    /// <summary>Mục chính (?k=): "work" = Đề xuất công việc (kế hoạch đầu ngày + phát sinh + đề xuất lịch); "ot" = Đề xuất làm thêm tại công ty.</summary>
    public required string Kind { get; init; }
    /// <summary>Mục con (?f=): "pending" = Chờ duyệt | "approved" = Đã duyệt.</summary>
    public required string Filter { get; init; }
    /// <summary>Số chờ duyệt của từng mục chính (nhãn nút chuyển mục).</summary>
    public int WorkPending { get; init; }
    public int OtPending { get; init; }
    /// <summary>Kế hoạch đầu ngày của NV trong nhóm (ngày đang xem) theo mục đang chọn.</summary>
    public required List<DayPlan> Plans { get; init; }
    /// <summary>NV chưa gửi kế hoạch (chỉ hiện ở mục Chờ duyệt).</summary>
    public required List<Person> NotSent { get; init; }
    /// <summary>Phát sinh chờ duyệt trong ngày (chỉ mục Chờ duyệt).</summary>
    public required List<Extra> Extras { get; init; }
    /// <summary>Đề xuất lịch: chờ duyệt (mọi ngày) hoặc đã quyết định gần đây.</summary>
    public required List<CalendarProposal> Proposals { get; init; }
    /// <summary>Xin nghỉ của NV trong nhóm: chờ duyệt (mục Chờ duyệt) hoặc đã quyết định gần đây (mục Đã duyệt).</summary>
    /// <summary>Đăng ký làm thêm của NV trong nhóm (ngày đang xem); mục Chờ duyệt = đăng ký / kết quả cần duyệt.</summary>
    public required List<Overtime> Ots { get; init; }
    public int PendingTotal { get; init; }
    public int ApprovedTotal { get; init; }
}

/// <summary>Trang Leader → Xin nghỉ (/Planning/Leave): xin nghỉ của nhân viên trong nhóm, 2 mục Chờ duyệt / Đã duyệt.</summary>
public sealed class PlanningLeaveVm
{
    public string Filter { get; init; } = "pending";
    public List<LeaveRequest> Requests { get; init; } = [];
    public int PendingTotal { get; init; }
    public int ApprovedTotal { get; init; }
}
