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
        [TaskState.HoanThanh] = new("Hoàn thành", "p-ok", "var(--ok)"),
        [TaskState.DangLam] = new("Đang làm", "p-warn", "var(--warn)"),
        [TaskState.Cham] = new("Chậm", "p-bad", "var(--bad)"),
        [TaskState.ChuaLam] = new("Chưa làm", "p-idle", "var(--idle)"),
        // PS — trạng thái dự án
        [ProjectStatus.Dat] = new("Đạt", "p-ok", "var(--ok)"),
        [ProjectStatus.DangLam] = new("Đang làm", "p-warn", "var(--warn)"),
        [ProjectStatus.Cham] = new("Chậm", "p-bad", "var(--bad)"),
        [ProjectStatus.NguyCo] = new("Nguy cơ", "p-solid-bad", "var(--bad)"),
        [ProjectStatus.HoanThanh] = new("Hoàn thành", "p-info", "var(--brand)"),
        // AP — trạng thái kế hoạch ngày
        [PlanState.ChuaGui] = new("Chưa gửi", "p-idle", "var(--idle)"),
        [PlanState.ChoDuyet] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [PlanState.ChoTp] = new("Chờ TP duyệt", "p-info", "var(--brand)"),
        [PlanState.DaDuyet] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [PlanState.TuChoi] = new("Từ chối", "p-bad", "var(--bad)"),
        // EODS — báo cáo cuối ngày
        [EodState.ChuaGui] = new("Chưa gửi", "p-idle", "var(--idle)"),
        [EodState.ChoDuyet] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [EodState.DaDuyet] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [EodState.TuChoi] = new("Yêu cầu bổ sung", "p-bad", "var(--bad)"),
        // XS — phát sinh (hiển thị)
        [ExtraView.ChoDuyet] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [ExtraView.DaDuyet] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [ExtraView.DangXuLy] = new("Đang xử lý", "p-info", "var(--brand)"),
        [ExtraView.Xong] = new("Hoàn thành", "p-ok", "var(--ok)"),
        [ExtraView.TuChoi] = new("Từ chối", "p-bad", "var(--bad)"),
        // phát sinh (lưu)
        [ExtraStatus.ChoDuyet] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [ExtraStatus.DaDuyet] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [ExtraStatus.TuChoi] = new("Từ chối", "p-bad", "var(--bad)"),
        // OT_ST
        [OtStatus.ChoDuyet] = new("Chờ duyệt", "p-warn", "var(--warn)"),
        [OtStatus.DaDuyet] = new("Đã duyệt", "p-ok", "var(--ok)"),
        [OtStatus.TuChoi] = new("Từ chối", "p-bad", "var(--bad)"),
        // OT_REG
        [OtReg.DuTru] = new("Dự trù đúng hạn", "p-ok", "var(--ok)"),
        [OtReg.ChinhThuc] = new("Chính thức đúng hạn", "p-info", "var(--brand)"),
        [OtReg.TreHan] = new("Trễ hạn 14:00", "p-bad", "var(--bad)"),
        // OT_DAY
        [OtDayType.Lv] = new("Ngày làm việc — ngoài giờ hành chính", "p-info", "var(--brand)"),
        [OtDayType.Nghi] = new("Ngày nghỉ / lễ", "p-warn", "var(--warn)"),
        // OT meal
        [OtMeal.Khong] = new("Không đăng ký", "p-idle", "var(--idle)"),
        [OtMeal.Nhe] = new("Ăn nhẹ", "p-warn", "var(--warn)"),
        [OtMeal.Com] = new("Cơm", "p-ok", "var(--ok)"),
        [OtMeal.Bun] = new("Bún", "p-ok", "var(--ok)"),
        // SRC — nguồn công việc (KeHoach không có nhãn)
        [TaskSource.KeHoach] = new("", "p-info", "var(--brand)"),
        [TaskSource.NgoaiKeHoach] = new("Ngoài kế hoạch", "p-info", "var(--brand)"),
        [TaskSource.PhatSinh] = new("Phát sinh", "p-info", "var(--brand)"),
        [TaskSource.ChuyenTiep] = new("Chuyển tiếp", "p-info", "var(--brand)"),
        [TaskSource.GiaoViec] = new("Leader giao", "p-info", "var(--brand)"),
        // WST — trạng thái làm việc đầu ngày
        [WorkStatus.CongTy] = new("Làm việc tại công ty", "p-info", "var(--brand)"),
        [WorkStatus.CtNgay] = new("Công tác trong ngày", "p-info", "var(--brand)"),
        [WorkStatus.CtDai] = new("Công tác dài ngày", "p-info", "var(--brand)"),
        [WorkStatus.NghiPhep] = new("Xin nghỉ phép", "p-warn", "var(--warn)"),
        [WorkStatus.NghiKl] = new("Xin nghỉ không lương", "p-warn", "var(--warn)"),
        // MEAL
        [Meal.Cat] = new("Cắt cơm", "p-info", "var(--brand)"),
        [Meal.Com] = new("Cơm", "p-info", "var(--brand)"),
        [Meal.Bun] = new("Bún", "p-info", "var(--brand)"),
        // LS
        [LeaveSession.Sang] = new("Buổi sáng", "p-warn", "var(--warn)"),
        [LeaveSession.Chieu] = new("Buổi chiều", "p-warn", "var(--warn)"),
        [LeaveSession.CaNgay] = new("Cả ngày", "p-warn", "var(--warn)"),
        // SUP
        [SupportScope.Khong] = new("Không cần hỗ trợ", "p-idle", "var(--idle)"),
        [SupportScope.Nhom] = new("Nhân sự trong nhóm", "p-info", "var(--brand)"),
        [SupportScope.NhomKhac] = new("Nhân sự nhóm khác", "p-info", "var(--brand)"),
        [SupportScope.PhongKhac] = new("Nhân sự phòng ban khác", "p-info", "var(--brand)"),
        // Role (ROLE_L) và TITLE
        [Role.Nv] = new("Nhân viên", "p-info", "var(--brand)"),
        [Role.Leader] = new("Leader", "p-info", "var(--brand)"),
        [Role.Tp] = new("Trưởng phòng", "p-tp", "var(--tp)"),
        // LV
        [Level.Tech1] = new("Tech 1", "p-tp", "var(--tp)"),
        [Level.Tech2] = new("Tech 2", "p-tp", "var(--tp)"),
        [Level.Tech3] = new("Tech 3", "p-tp", "var(--tp)"),
        // AROLE
        [ProjectRole.Lead] = new("Leader dự án", "p-tp", "var(--tp)"),
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
        Role.Nv => "Nhân viên Setup",
        Role.Leader => "Leader Setup",
        _ => "Trưởng phòng",
    };

    /// <summary>Danh sách giá trị enum theo thứ tự khai báo (dùng cho select).</summary>
    public static IEnumerable<T> All<T>() where T : struct, Enum => Enum.GetValues<T>();
}
