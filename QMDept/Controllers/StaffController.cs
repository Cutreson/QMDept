using Microsoft.AspNetCore.Mvc;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.Controllers;

/// <summary>
/// Giao diện Nhân viên. Chia nhiều file partial:
/// - StaffController.Calendar.cs : Kế hoạch tháng (Month, kể cả chế độ "Tất cả nhân viên" + giao thêm việc), Thông tin dự án (Projects).
/// - StaffController.Reports.cs  : Báo cáo đầu ngày (Morning) và Báo cáo cuối ngày (Evening) cùng các POST của chúng.
/// Leader/TP xem trang NV ở chế độ chỉ xem (Scope.IsReadOnly); mọi POST tự kiểm quyền.
/// </summary>
[UiRole(Role.Staff)]
public partial class StaffController : AppController
{
    /// <summary>/Controller → trang mặc định của giao diện.</summary>
    [HttpGet] public IActionResult Index() => Redirect(Ui.To(Request, "/Staff/Month"));

}
