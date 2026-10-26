using System.Globalization;

namespace QMDept.Services;

/// <summary>
/// Giờ Việt Nam (UTC+7, không có giờ mùa hè). DB lưu mốc thời gian dạng UTC; mọi "hôm nay", "giờ hiện tại"
/// và hiển thị giờ đều đi qua lớp này (tương đương todayKey()/hm()/nowISO() của artifact).
/// Máy chủ đặt ở nước ngoài (yêu cầu chủ dự án 2026-10-13): KHÔNG dùng DateTime.Now / DateTime.Today / giờ máy chủ
/// cho ngày nghiệp vụ; JS phía trình duyệt cũng không tự tính ngày nghiệp vụ từ đồng hồ máy người dùng (ngày luôn do server đưa xuống).
/// </summary>
public static class VnTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTime UtcNow => DateTime.UtcNow;
    /// <summary>Giờ VN hiện tại (Kind = Unspecified).</summary>
    public static DateTime Now => DateTime.SpecifyKind(DateTime.UtcNow + Offset, DateTimeKind.Unspecified);
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>UTC → giờ VN.</summary>
    public static DateTime ToVn(DateTime utc) => DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + Offset, DateTimeKind.Unspecified);
    /// <summary>Mốc UTC tương ứng giờ VN "HH:mm" của ngày <paramref name="d"/>.</summary>
    public static DateTime UtcAt(DateOnly d, string hhmm) =>
        DateTime.SpecifyKind(d.ToDateTime(TimeOnly.ParseExact(hhmm, "HH:mm", CultureInfo.InvariantCulture)) - Offset, DateTimeKind.Utc);

    /// <summary>hm(iso): "HH:mm" giờ VN của một mốc UTC; null → "".</summary>
    public static string Hm(DateTime? utc) => utc is null ? "" : ToVn(utc.Value).ToString("HH:mm", CultureInfo.InvariantCulture);
    /// <summary>"HH:mm dd/MM/yyyy" (giờ Việt Nam) — hiện giờ + ngày gửi / đề xuất / duyệt ở mọi nội dung (yêu cầu 2026-10-24).</summary>
    public static string Stamp(DateTime? utc) => utc is null ? "" : ToVn(utc.Value).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
    /// <summary>Ngày VN của một mốc UTC.</summary>
    public static DateOnly DateOf(DateTime utc) => DateOnly.FromDateTime(ToVn(utc));
    /// <summary>"HH:mm" giờ VN hiện tại (nowHMs).</summary>
    public static string NowHm => Now.ToString("HH:mm", CultureInfo.InvariantCulture);
}
