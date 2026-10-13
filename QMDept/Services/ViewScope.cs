using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Ngữ cảnh xem của request hiện tại. Điền bởi <see cref="ScopeResolver"/> (gọi từ AppController) trước mỗi action;
/// inject vào view bằng <c>@inject ViewScope Scope</c>.
///
/// MÔ HÌNH "XEM MÀN HÌNH CẤP DƯỚI" (yêu cầu chủ dự án 2026-10-13, thay cơ chế chọn giao diện của artifact):
/// - Mỗi tài khoản dùng giao diện của chính vai trò mình (NV / Leader / TP) với đầy đủ quyền thao tác;
///   Quản trị dùng giao diện TP (đủ quyền TP) + tab "Tài khoản" (ScopeResolver.UiRoleOf).
/// - Cấp trên có combobox "Xem màn hình" để chọn bất kỳ cấp dưới nào (Quản trị: TP + Leader + NV; TP: Leader + NV;
///   Leader: NV trong nhóm) → thấy đúng các màn hình của người đó, CHỈ XEM (server chặn mọi POST — AppController).
/// - Lựa chọn "Tất cả nhân viên" (AllStaff) = lịch tháng cả nhóm (nvMonth all mode) + form giao thêm việc như artifact.
/// Lựa chọn được nhớ trong cookie qm_view (?as=&lt;id&gt; | ?as=all | ?as=me).
/// </summary>
public sealed class ViewScope
{
    public bool Resolved { get; set; }
    /// <summary>Tài khoản đang đăng nhập (người thật sự thao tác).</summary>
    public Person Account { get; set; } = null!;
    /// <summary>Giao diện đang hiển thị = vai trò của người đang được xem (Staff khi AllStaff; Quản trị → Manager).</summary>
    public Role Role { get; set; }
    /// <summary>Người đang được xem (chính mình, hoặc cấp dưới được chọn). Khi AllStaff: NV đầu tiên của Pool (có thể null).</summary>
    public Person? Me { get; set; }
    public int? MeId => Me?.Id;
    /// <summary>Đang xem lịch tháng cả nhóm ("Tất cả nhân viên").</summary>
    public bool AllStaff { get; set; }
    /// <summary>Ngày đang chọn (?d=), mặc định hôm nay theo giờ VN.</summary>
    public DateOnly Date { get; set; }
    public DateOnly Today { get; set; }
    /// <summary>NV mà tài khoản được xem / giao việc ở lịch cả nhóm (Leader: nhóm mình; TP, Quản trị: mọi NV).</summary>
    public List<Person> Pool { get; set; } = [];
    /// <summary>Cấp dưới tài khoản được chọn để xem màn hình (cho combobox), theo thứ tự TP → Leader → NV.</summary>
    public List<Person> Subordinates { get; set; } = [];

    /// <summary>Đang xem màn hình của một cấp dưới (không phải chính mình, không phải lịch cả nhóm).</summary>
    public bool IsViewingOther => !AllStaff && Me is not null && Me.Id != Account.Id;
    /// <summary>Chỉ xem: đang xem cấp dưới, hoặc lịch cả nhóm (ở đó chỉ form giao việc được phép ghi).</summary>
    public bool IsReadOnly => IsViewingOther || AllStaff;
    /// <summary>Đang ở màn hình của chính mình.</summary>
    public bool IsSelf => !AllStaff && Me is not null && Me.Id == Account.Id;
    /// <summary>Tài khoản có quyền TP (Trưởng phòng, hoặc Quản trị — Quản trị bao trùm mọi chức năng TP).</summary>
    public bool IsManager => Account.Role is Role.Manager or Role.Admin;
    /// <summary>Tài khoản là Quản trị (thêm quyền quản trị tài khoản).</summary>
    public bool IsAdmin => Account.Role == Role.Admin;
    /// <summary>Tài khoản có quyền giao việc ở lịch cả nhóm (Leader, TP, Quản trị).</summary>
    public bool CanLead => Account.Role is Role.Leader or Role.Manager or Role.Admin;
    /// <summary>Tên ghi vào các trường …By (người thật sự thao tác).</summary>
    public string ActorName => Account.Name;
}
