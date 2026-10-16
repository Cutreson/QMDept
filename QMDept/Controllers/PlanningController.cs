using Microsoft.AspNetCore.Mvc;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.Controllers;

/// <summary>
/// Leader → tab "Kế hoạch &amp; duyệt" (ldPlan). Chia nhiều file partial:
/// - PlanningController.Approval.cs : tab con Kế hoạch (Approvals) + Báo cáo (Reports) — duyệt kế hoạch ngày, phát sinh, báo cáo cuối ngày, chuyển tiếp.
/// - PlanningController.Manage.cs   : tab con Lịch tuần (Week), Dự án &amp; đầu mục (Projects), Nhân sự (People), Phân công (Assign).
/// Đầu mỗi view gọi partial "_PlanningTabs" để vẽ tiêu đề + tab con.
/// </summary>
[UiRole(Role.Leader)]
public partial class PlanningController : AppController
{
    /// <summary>Nhân sự + thao tác tài khoản / báo nghỉ dùng chung cho giao diện Leader và TP (TP có tab "Nhân sự" riêng —
    /// mô hình xem màn hình cấp dưới không cho TP thao tác trong giao diện Leader như artifact).</summary>
    static readonly HashSet<string> LeaderManagerActions = new(StringComparer.OrdinalIgnoreCase) { "People", "ToggleLeave", "CreateAccount", "EditAccount", "DeleteAccount",
        // TP / Quản trị tạo + sửa dự án và đầu mục (trang /Manager/Projects chỉ xem; nút "＋ Dự án mới" dẫn tới đây)
        "Projects", "SaveProject", "AddItem", "SetItemStatus", "DeleteItem",
        // chọn Leader dự án (Leader: tab con "Leader dự án"; TP / Quản trị: nút ở trang Dự án)
        "ProjectLeads", "SetProjectLead",
        // TP / Quản trị chọn Leader (nhóm) phụ trách từng dự án — nút ở trang Dự án
        "ProjectOwners", "SetProjectOwner" };

    protected override Role[] UiRoles(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext ctx) =>
        LeaderManagerActions.Contains(ctx.RouteData.Values["action"]?.ToString() ?? "") ? [Role.Leader, Role.Manager] : [Role.Leader];

    /// <summary>/Controller → trang mặc định của giao diện.</summary>
    [HttpGet] public IActionResult Index() => Redirect(Ui.To(Request, "/Planning/Approvals"));

}
