using System.Globalization;

namespace QMDept.Services;

/// <summary>Định dạng ngày/giờ/tiền và danh sách giờ chọn — port các helper của artifact (dm, dmy, dayLabel, T24, HALF, num, money…).</summary>
public static class Fmt
{
    public static readonly string[] WD = ["Chủ nhật", "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7"];
    public static readonly string[] WDS = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];
    /// <summary>Tiêu đề cột lịch tháng (bắt đầu Thứ 2).</summary>
    public static readonly string[] MonthHeader = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

    /// <summary>Màu ô dự án (TILE), chọn theo vị trí dự án trong danh sách.</summary>
    public static readonly string[] Tile = ["#1747d6", "#0f7a6b", "#c2410c", "#6a3fd1", "#0e7490", "#b91c1c", "#4d7c0f"];

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Khóa ngày "yyyy-MM-dd" (dùng trong URL ?d=).</summary>
    public static string Key(DateOnly d) => d.ToString("yyyy-MM-dd", Inv);
    public static DateOnly? ParseKey(string? s) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d) ? d : null;
    /// <summary>Đọc ngày người dùng nhập: dd/MM/yyyy (chính) hoặc yyyy-MM-dd; sai → null.</summary>
    public static DateOnly? ParseInput(string? s) =>
        DateOnly.TryParseExact(s?.Trim(), new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" }, Inv, DateTimeStyles.None, out var d) ? d : null;

    public static int Wday(DateOnly d) => (int)d.DayOfWeek;                        // 0 = Chủ nhật
    public static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    public static DateOnly MonthStart(DateOnly d) => new(d.Year, d.Month, 1);
    public static int DaysInMonth(DateOnly d) => DateTime.DaysInMonth(d.Year, d.Month);
    public static int DaysBetween(DateOnly a, DateOnly b) => b.DayNumber - a.DayNumber;
    /// <summary>n ngày kế tiếp sau d (nextWDs).</summary>
    public static IEnumerable<DateOnly> NextDays(DateOnly d, int n) => Enumerable.Range(1, n).Select(k => d.AddDays(k));
    /// <summary>Ngày làm việc liền trước (bỏ qua Chủ nhật) — prevWD.</summary>
    public static DateOnly PrevWorkDay(DateOnly d) { var p = d.AddDays(-1); if (p.DayOfWeek == DayOfWeek.Sunday) p = p.AddDays(-1); return p; }

    /// <summary>Mọi ngày hiển thị cho người dùng đều dd/MM/yyyy (yêu cầu 2026-10-16) — Dm giữ lại làm tên cũ của Dmy.</summary>
    public static string Dm(DateOnly? d) => Dmy(d);
    /// <summary>dd/MM/yyyy</summary>
    public static string Dmy(DateOnly? d) => d is null ? "" : d.Value.ToString("dd/MM/yyyy", Inv);
    /// <summary>"Thứ 2, 13/10/2026"</summary>
    public static string DayLabel(DateOnly d) => $"{WD[Wday(d)]}, {Dmy(d)}";
    /// <summary>"T2 13/10/2026"</summary>
    public static string Short(DateOnly d) => $"{WDS[Wday(d)]} {Dm(d)}";

    /// <summary>num(): số làm tròn 2 chữ số, dấu phẩy thập phân.</summary>
    public static string Num(decimal n) => Math.Round(n, 2).ToString("0.##", Inv).Replace('.', ',');
    public static string Num(double n) => Num((decimal)n);
    /// <summary>money(): 35.000đ</summary>
    public static string Money(long n) => n.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";

    /// <summary>"HH:mm" → số phút (hmMin). "24:00" → 1440.</summary>
    public static int Min(string hhmm) => int.Parse(hhmm[..2], Inv) * 60 + int.Parse(hhmm[3..5], Inv);
    public static string Hhmm(int min) => $"{min / 60:00}:{min % 60:00}";
    /// <summary>Đúng dạng "HH:mm" (4 chữ số ASCII + dấu ':'; 00:00–24:00). Không dùng int.TryParse (nhận " 9", "+9" → so sánh chuỗi sai).</summary>
    public static bool IsHhmm(string? s) => s is { Length: 5 } && s[2] == ':' && char.IsAsciiDigit(s[0]) && char.IsAsciiDigit(s[1])
        && char.IsAsciiDigit(s[3]) && char.IsAsciiDigit(s[4]) && Min(s) is var t && t <= 1440 && s[3] <= '5';

    /// <summary>T24: 00:00–24:00 bước 30 phút.</summary>
    public static readonly string[] T24 = Enumerable.Range(0, 49).Select(k => Hhmm(k * 30)).ToArray();
    /// <summary>Giờ bắt đầu hợp lệ (bỏ 24:00).</summary>
    public static IEnumerable<string> StartTimes => T24.Where(x => string.CompareOrdinal(x, "24:00") < 0);
    /// <summary>Giờ kết thúc hợp lệ (bỏ 00:00).</summary>
    public static IEnumerable<string> EndTimes => T24.Where(x => string.CompareOrdinal(x, "00:00") > 0);

    /// <summary>Viết tắt tên (initials): 2 chữ cái đầu của 2 từ cuối.</summary>
    public static string Initials(string? n)
    {
        var w = (n ?? "?").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (w.Length == 0) return "?";
        return (w.Length > 1 ? $"{w[^2][0]}{w[^1][0]}" : w[0][..Math.Min(2, w[0].Length)]).ToUpperInvariant();
    }

    /// <summary>So sánh chuỗi giờ "HH:mm" (ordinal) — tiện viết a.Lt(b).</summary>
    public static bool Lt(this string a, string b) => string.CompareOrdinal(a, b) < 0;
    public static bool Le(this string a, string b) => string.CompareOrdinal(a, b) <= 0;
    public static bool Gt(this string a, string b) => string.CompareOrdinal(a, b) > 0;
    public static bool Ge(this string a, string b) => string.CompareOrdinal(a, b) >= 0;

    /// <summary>34 tỉnh / thành (đơn vị hành chính cấp tỉnh từ 01/7/2025) cho công tác.</summary>
    /// <summary>Phòng ban có thể đề xuất hỗ trợ ở lịch tháng (yêu cầu 2026-10-15).</summary>
    public static readonly string[] SupportDepartments = ["Thiết kế cơ", "Thiết kế điện", "Lắp ráp cơ", "Lắp ráp điện"];
    /// <summary>"Thiết kế cơ=2;Nhóm A=1" → "Thiết kế cơ ×2, Nhóm A ×1".</summary>
    public static string QuotaText(string? q) => string.Join(", ", (q ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Replace("=", " ×")));

    public static readonly string[] Provinces = ["Hà Nội", "TP Hồ Chí Minh", "Hải Phòng", "Đà Nẵng", "Huế", "Cần Thơ", "An Giang", "Bắc Ninh", "Cà Mau", "Cao Bằng", "Đắk Lắk", "Điện Biên", "Đồng Nai", "Đồng Tháp", "Gia Lai", "Hà Tĩnh", "Hưng Yên", "Khánh Hòa", "Lai Châu", "Lạng Sơn", "Lào Cai", "Lâm Đồng", "Nghệ An", "Ninh Bình", "Phú Thọ", "Quảng Ngãi", "Quảng Ninh", "Quảng Trị", "Sơn La", "Tây Ninh", "Thái Nguyên", "Thanh Hóa", "Tuyên Quang", "Vĩnh Long"];

    public static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public static readonly StringComparer ViCompare = StringComparer.Create(Vi, false);
}
