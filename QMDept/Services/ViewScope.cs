using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Ngữ cảnh xem của request hiện tại (tương đương S.acc / S.role / S.me / S.allNv / S.date của artifact).
/// Được điền bởi <see cref="QMDept.Controllers.AppController"/> trước mỗi action; inject vào view bằng <c>@inject ViewScope Scope</c>.
///
/// Quyền xem (artifact): NV chỉ xem giao diện NV của mình; Leader: giao diện Leader + lịch NV trong nhóm;
/// TP: mọi giao diện, mọi người (ở giao diện Leader, TP chọn Leader để xem theo phạm vi nhóm đó).
/// </summary>
public sealed class ViewScope
{
    public bool Resolved { get; set; }
    /// <summary>Tài khoản đang đăng nhập.</summary>
    public Person Account { get; set; } = null!;
    /// <summary>Giao diện hiện tại: Nv / Leader / Tp.</summary>
    public Role Role { get; set; }
    /// <summary>Người đang được xem ở giao diện này (S.me[role]); null nếu nhóm trống.</summary>
    public Person? Me { get; set; }
    public int? MeId => Me?.Id;
    /// <summary>Giao diện NV, chế độ "Tất cả nhân viên" (chỉ cho tài khoản Leader/TP).</summary>
    public bool AllNv { get; set; }
    /// <summary>Ngày đang chọn (?d=), mặc định hôm nay theo giờ VN.</summary>
    public DateOnly Date { get; set; }
    public DateOnly Today { get; set; }
    /// <summary>Danh sách người có thể chọn để xem ở giao diện này (poolFor(role)).</summary>
    public List<Person> Pool { get; set; } = [];
    /// <summary>Các giao diện tài khoản được dùng (allowedRoles).</summary>
    public List<Role> AllowedRoles { get; set; } = [];

    /// <summary>Đang xem trang NV của người khác (Leader/TP xem lịch NV) → chỉ xem (isRO).</summary>
    public bool IsReadOnly => Role == Role.Nv && (AllNv || Me is null || Me.Id != Account.Id);
    /// <summary>Đang thao tác trên chính mình.</summary>
    public bool IsSelf => Me is not null && Me.Id == Account.Id;
    /// <summary>Tài khoản là Trưởng phòng.</summary>
    public bool IsTp => Account.Role == Role.Tp;
    /// <summary>Tài khoản có quyền thao tác giao diện Leader (Leader hoặc TP).</summary>
    public bool CanLead => Account.Role is Role.Leader or Role.Tp;
    /// <summary>Tên ghi vào các trường …By (người thật sự thao tác). Khác artifact (dùng tên người đang được xem).</summary>
    public string ActorName => Account.Name;
}
