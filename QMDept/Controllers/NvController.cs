using QMDept.Data;

namespace QMDept.Controllers;

/// <summary>
/// Giao diện Nhân viên. Chia nhiều file partial:
/// - NvController.Lich.cs    : Kế hoạch tháng (Thang, kể cả chế độ "Tất cả nhân viên" + giao thêm việc), Thông tin dự án (DuAn).
/// - NvController.BaoCao.cs  : Báo cáo đầu ngày (DauNgay) và Báo cáo cuối ngày (CuoiNgay) cùng các POST của chúng.
/// Leader/TP xem trang NV ở chế độ chỉ xem (Scope.IsReadOnly); mọi POST tự kiểm quyền.
/// </summary>
[UiRole(Role.Nv)]
public partial class NvController : AppController
{
}
