using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Luật làm thêm (quy định áp dụng từ 01/10/2026) — port 1:1 từ otRule / otMealOpts / regType / otDeadline của artifact.
/// Làm thêm chỉ tại công ty: ngày làm việc → ngoài giờ hành chính (kết thúc ≤ 08:00 hoặc bắt đầu ≥ 17:00);
/// ngày nghỉ / lễ → được làm cả giờ hành chính (trừ nghỉ trưa 12:00–13:00).
/// File wwwroot/js/overtime.js chứa bản JS tương đương để xem trước trực tiếp trên form — sửa luật thì sửa cả hai.
/// </summary>
public static class OtRules
{
    public const int MealCost = 35000, SnackCost = 15000, Allowance4h = 20000;
    public static readonly string[] EndOptions = ["18:00", "19:00", "20:00", "21:00", "22:00", "23:00", "24:00"]; // OT_END (gợi ý)

    public sealed record Result(
        string? Error, string Start, string End, decimal Hours, bool Lunch, OtDayType DayType,
        string Tier, OtMeal MealLevel, int Cost, int Allow, string Break)
    {
        public bool Ok => Error is null;
    }

    static Result Err(string msg, OtDayType dt) => new(msg, "", "", 0, false, dt, "", OtMeal.None, 0, 0, "");

    /// <summary>otRule(dayType, a, b).</summary>
    public static Result Calc(OtDayType dayType, string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || !Fmt.IsHhmm(a) || !Fmt.IsHhmm(b)) return Err("Chọn giờ bắt đầu và giờ kết thúc.", dayType);
        if (Fmt.Min(a) % 30 != 0 || Fmt.Min(b) % 30 != 0) return Err("Giờ làm thêm theo bước 30 phút.", dayType);
        if (b.Le(a)) return Err("Giờ kết thúc phải sau giờ bắt đầu.", dayType);
        if (dayType != OtDayType.Holiday && a.Lt("17:00") && b.Gt("08:00"))
            return Err("Ngày làm việc chỉ đăng ký ngoài giờ hành chính: kết thúc trước 08:00 hoặc bắt đầu từ 17:00.", dayType);
        int lunch = dayType == OtDayType.Holiday ? Math.Max(0, Math.Min(Fmt.Min(b), 780) - Math.Max(Fmt.Min(a), 720)) : 0;
        decimal h = (Fmt.Min(b) - Fmt.Min(a) - lunch) / 60m;
        if (h <= 0) return Err("Khoảng giờ đã chọn nằm trọn trong nghỉ trưa — không có thời gian làm thêm.", dayType);
        string tier; OtMeal meal; int cost, allow;
        if (h < 2) { tier = $"Làm thêm {Fmt.Num(h)} giờ"; meal = OtMeal.None; cost = 0; allow = 0; }
        else if (h < 3) { tier = "Tăng ca 2 giờ"; meal = OtMeal.Snack; cost = SnackCost; allow = 0; }
        else if (h < 4) { tier = "Tăng ca 3 đến dưới 4 giờ"; meal = OtMeal.Rice; cost = MealCost; allow = 0; }
        else { tier = "Tăng ca từ 4 giờ trở lên"; meal = OtMeal.Rice; cost = MealCost; allow = Allowance4h; }
        var br = new List<string>();
        if (lunch > 0) br.Add("12:00–13:00 (nghỉ trưa)");
        if (h >= 3 && a.Le("17:30") && b.Ge("18:00")) br.Add("17:30–18:00 (1 lần)");
        else if (h >= 2 && h < 3)
        {
            if (a == "17:00") br.Add("17:00–17:10");
            else if (a.Le("07:35") && b.Ge("07:45")) br.Add("07:35–07:45");
        }
        return new(null, a, b, h, lunch > 0, dayType, tier, meal, cost, allow, br.Count > 0 ? string.Join(", ", br) : "Theo bố trí của bộ phận");
    }

    /// <summary>otMealOpts(r): từ 3 giờ → Cơm / Không; 2 giờ → Ăn nhẹ / Không; dưới 2 giờ → chỉ Không.</summary>
    public static OtMeal[] MealOptions(Result r) => !r.Ok ? [] : r.MealLevel switch
    {
        OtMeal.Rice => [OtMeal.Rice, OtMeal.None],
        OtMeal.Snack => [OtMeal.Snack, OtMeal.None],
        _ => [OtMeal.None],
    };

    public static bool IsFullMeal(OtMeal m) => m is OtMeal.Rice or OtMeal.Noodle;
    public static int CostOf(OtMeal m) => IsFullMeal(m) ? MealCost : m == OtMeal.Snack ? SnackCost : 0;

    /// <summary>regType(createdAt, d): dự trù trước 14:00 ngày làm việc liền trước; chính thức trước 14:00 cùng ngày.</summary>
    public static OtReg RegType(DateTime createdUtc, DateOnly d)
    {
        if (createdUtc <= VnTime.UtcAt(Fmt.PrevWorkDay(d), "14:00")) return OtReg.Early;
        if (createdUtc <= VnTime.UtcAt(d, "14:00")) return OtReg.OnTime;
        return OtReg.Late;
    }

    /// <summary>otDeadline(d): (tiêu đề, màu chữ, màu nền) của hộp thông báo hạn đăng ký tại thời điểm hiện tại.</summary>
    public static (string Text, string Fg, string Bg) DeadlineState(DateOnly d)
    {
        var now = VnTime.UtcNow;
        if (now <= VnTime.UtcAt(Fmt.PrevWorkDay(d), "14:00")) return ("Đang trong hạn đăng ký dự trù", "var(--ok)", "var(--ok-soft)");
        if (now <= VnTime.UtcAt(d, "14:00")) return ("Đang trong hạn đăng ký chính thức", "var(--warn)", "var(--warn-soft)");
        return ("Đã quá 14:00 — không đảm bảo bố trí suất ăn", "var(--bad)", "var(--bad-soft)");
    }

    /// <summary>mealText(x)</summary>
    public static string MealText(Overtime x) =>
        x.Exempt ? "Không áp dụng (công tác / giao hàng)" : IsFullMeal(x.Meal) ? "Cơm" : x.Meal == OtMeal.Snack ? "Ăn nhẹ" : "Không đăng ký";
}

/// <summary>
/// Luật nghỉ — port từ workMin / readLeave / LS / KL_FROM / KL_TO.
/// Giờ làm 08:00–12:00 và 13:00–17:00 (không tính nghỉ trưa). Nghỉ phép tối thiểu theo buổi; nghỉ không lương tối thiểu 1 giờ.
/// File wwwroot/js/staff.js chứa bản JS tương đương cho hộp thoại chọn giờ nghỉ (nhận UnpaidFrom/UnpaidTo/Session qua <script id="staffCfg">).
/// </summary>
public static class LeaveRules
{
    /// <summary>LS: buổi → (nhãn, từ, đến).</summary>
    public static (string Label, string From, string To) Session(LeaveSession s) => s switch
    {
        LeaveSession.Morning => ("Buổi sáng", "08:00", "12:00"),
        LeaveSession.Afternoon => ("Buổi chiều", "13:00", "17:00"),
        _ => ("Cả ngày", "08:00", "17:00"),
    };

    /// <summary>workMin(a,b): số phút làm việc trong khoảng [a,b], bỏ nghỉ trưa.</summary>
    public static int WorkMin(string a, string b)
    {
        int ma = Fmt.Min(a), mb = Fmt.Min(b);
        int Ov(int x, int y) => Math.Max(0, Math.Min(mb, y) - Math.Max(ma, x));
        return Ov(480, 720) + Ov(780, 1020);
    }

    static readonly string[] Hourly = Enumerable.Range(0, 10).Select(k => Fmt.Hhmm(480 + k * 60)).ToArray(); // 08:00..17:00
    static readonly string[] ToBase = Hourly.Where(x => x.Gt("08:00") && !(x.Gt("12:00") && x.Le("13:00"))).ToArray();
    /// <summary>KL_FROM: giờ bắt đầu nghỉ không lương hợp lệ.</summary>
    public static readonly string[] UnpaidFrom = Hourly.Where(x => x.Lt("17:00") && !(x.Ge("12:00") && x.Lt("13:00")))
        .Where(a => ToBase.Any(b => b.Gt(a) && WorkMin(a, b) >= 60)).ToArray();
    /// <summary>KL_TO: giờ kết thúc nghỉ không lương hợp lệ.</summary>
    public static readonly string[] UnpaidTo = ToBase.Where(b => UnpaidFrom.Any(a => b.Gt(a) && WorkMin(a, b) >= 60)).ToArray();

    public sealed record Leave(LeaveType Type, LeaveSession? Session, string From, string To, decimal Hours, decimal? Days);

    /// <summary>readLeave cho nghỉ phép.</summary>
    public static Leave Paid(LeaveSession s)
    {
        var (_, a, b) = Session(s);
        bool full = s == LeaveSession.FullDay;
        return new(LeaveType.Paid, s, a, b, full ? 8 : 4, full ? 1 : 0.5m);
    }

    /// <summary>readLeave cho nghỉ không lương; trả lỗi nếu không hợp lệ.</summary>
    public static (Leave? Value, string? Error) Unpaid(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || !Fmt.IsHhmm(a) || !Fmt.IsHhmm(b)) return (null, "Chọn giờ bắt đầu và giờ kết thúc.");
        if (b.Le(a)) return (null, "Giờ kết thúc phải sau giờ bắt đầu.");
        int mins = WorkMin(a, b);
        if (mins < 60) return (null, $"Nghỉ không lương tối thiểu 1 giờ (đang chọn {mins} phút).");
        return (new(LeaveType.Unpaid, null, a, b, mins / 60m, null), null);
    }

    /// <summary>Loại nghỉ tương ứng trạng thái làm việc (LT); null nếu không phải nghỉ.</summary>
    public static LeaveType? TypeOf(WorkStatus s) => s switch
    {
        WorkStatus.PaidLeave => LeaveType.Paid,
        WorkStatus.UnpaidLeave => LeaveType.Unpaid,
        _ => null,
    };
    public static bool IsLeave(WorkStatus s) => TypeOf(s) is not null;
    public static bool IsTrip(WorkStatus s) => s is WorkStatus.DayTrip or WorkStatus.LongTrip;

    /// <summary>leaveQty: "0,5 ngày phép" / "2 giờ không lương".</summary>
    public static string Qty(DayPlan p) => p.LeaveType == LeaveType.Paid
        ? $"{Fmt.Num(p.LeaveDays ?? 0)} ngày phép" : $"{Fmt.Num(p.LeaveHours ?? 0)} giờ không lương";

    /// <summary>Việc nằm trọn trong khung nghỉ (dùng để bỏ chọn khi khai báo nghỉ).</summary>
    public static bool Overlaps(WorkTask t, string from, string to) => t.Start.Ge(from) && t.End.Le(to);
}
