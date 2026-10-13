using QMDept.Data;

namespace QMDept.Controllers;

/// <summary>
/// Leader → tab "Kế hoạch &amp; duyệt" (ldPlan). Chia nhiều file partial:
/// - KeHoachController.Duyet.cs   : tab con Kế hoạch (Duyet) + Báo cáo (BaoCao) — duyệt kế hoạch ngày, phát sinh, báo cáo cuối ngày, chuyển tiếp.
/// - KeHoachController.Manage.cs  : tab con Lịch tuần (Tuan), Dự án &amp; đầu mục (DuAn), Nhân sự (NhanSu), Phân công (PhanCong).
/// Đầu mỗi view gọi partial "_KeHoachTabs" để vẽ tiêu đề + tab con.
/// </summary>
[UiRole(Role.Leader)]
public partial class KeHoachController : AppController
{
}
