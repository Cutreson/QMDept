namespace QMDept.Data;

/// <summary>
/// Nhãn tiếng Việt + class CSS "pill" + màu CSS cho mọi enum, port từ các bảng TS/PS/AP/XS/SRC/WST/... của artifact.
/// Dùng: <c>Labels.Of(t.Status)</c>, <c>Labels.Pill(t.Status)</c>, <c>Labels.Color(t.Status)</c>.
/// Trong Razor có sẵn partial <c>_Pill</c> nhưng thường gọi thẳng <c>Html.Raw(Labels.PillHtml(x))</c>.
/// </summary>
public static class Labels
{
    public record Info(string Label, string Pill, string Color);

    static readonly Dictionary<Enum, Info> Map = new()
    {
        // TS — trạng thái công việc
        [TaskState.Done] = new("Hoàn thành", "p-ok", "var(--ok)"),
        [TaskState.InProgress] = new("Đang làm", "p-warn", "var(--warn)"),
        [TaskState.Late] = new("Chậm", "p-bad", "var(--bad)"),
        [TaskState.NotStarted] = new("Chưa làm", "p-idle", "var(--idle)"),
        // PS — trạng thái dự án
        [ProjectStatus.OnTrack] = new("Đạt", "p-ok", "var(--ok)"),
        [ProjectStatus.InProgress] = new("Đang làm", "p-warn", "var(--warn)"),
        [ProjectStatus.Behind] = new("Chậm", "p-bad", "var(--bad)"),
        [ProjectStatus.AtRisk] = new("Nguy cơ", "p-solid-bad", "var(--bad)"),
        [ProjectStatus.Completed] = new("Hoàn thành", "p-info", "var(--brand)"),
        // AP — trạng thái kế hoạch ngày
        [PlanState.NotSent] = new("Chưa gửi", "p-idle", "var(--idle)"),
        [PlanState.Pending] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [PlanState.PendingManager] = new("Chờ TP duyệt", "p-info", "var(--brand)"),
        [PlanState.Approved] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [PlanState.Rejected] = new("Từ chối", "p-bad", "var(--bad)"),
        // EODS — báo cáo cuối ngày
        [EodState.NotSent] = new("Chưa gửi", "p-idle", "var(--idle)"),
        [EodState.Pending] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [EodState.Approved] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [EodState.Rejected] = new("Yêu cầu bổ sung", "p-bad", "var(--bad)"),
        // XS — phát sinh (hiển thị)
        [ExtraView.Pending] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [ExtraView.Approved] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [ExtraView.InProgress] = new("Đang xử lý", "p-info", "var(--brand)"),
        [ExtraView.Done] = new("Hoàn thành", "p-ok", "var(--ok)"),
        [ExtraView.Rejected] = new("Từ chối", "p-bad", "var(--bad)"),
        // phát sinh (lưu)
        [ExtraStatus.Pending] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [ExtraStatus.Approved] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [ExtraStatus.Rejected] = new("Từ chối", "p-bad", "var(--bad)"),
        // OT_ST
        [OtStatus.Pending] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [OtStatus.Approved] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [OtStatus.Rejected] = new("Từ chối", "p-bad", "var(--bad)"),
        // OT_REG
        [OtReg.Early] = new("Dự trù đúng hạn", "p-ok", "var(--ok)"),
        [OtReg.OnTime] = new("Chính thức đúng hạn", "p-info", "var(--brand)"),
        [OtReg.Late] = new("Trễ hạn 14:00", "p-bad", "var(--bad)"),
        // OT_DAY
        [OtDayType.Workday] = new("Ngày làm việc — ngoài giờ hành chính", "p-info", "var(--brand)"),
        [OtDayType.Holiday] = new("Ngày nghỉ / lễ", "p-warn", "var(--warn)"),
        // OT meal
        [OtMeal.None] = new("Không đăng ký", "p-idle", "var(--idle)"),
        [OtMeal.Snack] = new("Ăn nhẹ", "p-warn", "var(--warn)"),
        [OtMeal.Rice] = new("Cơm", "p-ok", "var(--ok)"),
        [OtMeal.Noodle] = new("Bún", "p-ok", "var(--ok)"),
        // SRC — nguồn công việc (Planned không có nhãn)
        [TaskSource.Planned] = new("", "p-info", "var(--brand)"),
        [TaskSource.Unplanned] = new("Ngoài kế hoạch", "p-info", "var(--brand)"),
        [TaskSource.Extra] = new("Phát sinh", "p-info", "var(--brand)"),
        [TaskSource.CarryOver] = new("Chuyển tiếp", "p-info", "var(--brand)"),
        [TaskSource.Assigned] = new("Leader giao", "p-info", "var(--brand)"),
        // WST — trạng thái làm việc đầu ngày
        [WorkStatus.AtOffice] = new("Làm việc tại công ty", "p-info", "var(--brand)"),
        [WorkStatus.DayTrip] = new("Công tác trong ngày", "p-info", "var(--brand)"),
        [WorkStatus.LongTrip] = new("Công tác dài ngày", "p-info", "var(--brand)"),
        [WorkStatus.PaidLeave] = new("Xin nghỉ phép", "p-warn", "var(--warn)"),
        [WorkStatus.UnpaidLeave] = new("Xin nghỉ không lương", "p-warn", "var(--warn)"),
        // MEAL
        [Meal.None] = new("Cắt cơm", "p-info", "var(--brand)"),
        [Meal.Rice] = new("Cơm", "p-info", "var(--brand)"),
        [Meal.Noodle] = new("Bún", "p-info", "var(--brand)"),
        // LS
        [LeaveSession.Morning] = new("Buổi sáng", "p-warn", "var(--warn)"),
        [LeaveSession.Afternoon] = new("Buổi chiều", "p-warn", "var(--warn)"),
        [LeaveSession.FullDay] = new("Cả ngày", "p-warn", "var(--warn)"),
        // SUP
        [SupportScope.None] = new("Không cần hỗ trợ", "p-idle", "var(--idle)"),
        [SupportScope.Team] = new("Nhân sự trong nhóm", "p-info", "var(--brand)"),
        [SupportScope.OtherTeam] = new("Nhân sự nhóm khác", "p-info", "var(--brand)"),
        [SupportScope.OtherDept] = new("Nhân sự phòng ban khác", "p-info", "var(--brand)"),
        // Role (ROLE_L) và TITLE
        [Role.Staff] = new("Nhân viên", "p-info", "var(--brand)"),
        [Role.Leader] = new("Leader", "p-info", "var(--brand)"),
        [Role.Manager] = new("Trưởng phòng", "p-mgr", "var(--mgr)"),
        [Role.Admin] = new("Quản trị", "p-idle", "var(--idle)"),
        // LV
        [Level.Tech1] = new("Tech 1", "p-mgr", "var(--mgr)"),
        [Level.Tech2] = new("Tech 2", "p-mgr", "var(--mgr)"),
        [Level.Tech3] = new("Tech 3", "p-mgr", "var(--mgr)"),
        // AROLE
        [ProjectRole.Lead] = new("Leader dự án", "p-mgr", "var(--mgr)"),
        [ProjectRole.Support] = new("Hỗ trợ dự án", "p-info", "var(--brand)"),
        // Decision
        [Decision.Ok] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [Decision.No] = new("Từ chối", "p-bad", "var(--bad)"),
    };

    public static Info Get(Enum e) => Map.TryGetValue(e, out var i) ? i : new(e.ToString(), "p-idle", "var(--idle)");
    public static string Of(Enum e) => Get(e).Label;
    public static string Pill(Enum e) => Get(e).Pill;
    public static string Color(Enum e) => Get(e).Color;

    /// <summary>HTML của một pill (đã escape nhãn).</summary>
    public static string PillHtml(Enum e) => $"<span class=\"pill {Get(e).Pill}\">{System.Net.WebUtility.HtmlEncode(Get(e).Label)}</span>";

    /// <summary>Chức danh theo vai trò (TITLE trong artifact).</summary>
    public static string Title(Role r) => r switch
    {
        Role.Staff => "Nhân viên Setup",
        Role.Leader => "Leader Setup",
        Role.Admin => "Quản trị hệ thống",
        _ => "Trưởng phòng",
    };

    /// <summary>Danh sách giá trị enum theo thứ tự khai báo (dùng cho select).</summary>
    public static IEnumerable<T> All<T>() where T : struct, Enum => Enum.GetValues<T>();
}
