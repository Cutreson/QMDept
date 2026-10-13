using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>Model trang Leader → Theo dõi (ldTrack). Mode: "nv" (theo nhân viên) | "da" (theo dự án); ProjectId: lọc dự án (ldTrPj).</summary>
public sealed class LeaderTrackVm
{
    public required string Mode { get; init; }
    public int? ProjectId { get; init; }
}

/// <summary>Model trang Leader → Báo cáo TP (ldReport). Tab: "ngay" | "tuan".</summary>
public sealed class LeaderReportVm
{
    public required string Tab { get; init; }
    /// <summary>Thứ 2 của tuần đang xem (tab tuần).</summary>
    public DateOnly Monday { get; init; }
    /// <summary>Báo cáo đã gửi TP cho (ngày, Leader đang xem) — null nếu chưa gửi.</summary>
    public LeaderReport? Sent { get; init; }
}

/// <summary>Tiện ích dùng chung cho các view Leader.</summary>
public static class LeaderUi
{
    /// <summary>Độ dài tối đa của ghi chú / lý do / mô tả (cột MaxLength(2000)).</summary>
    public const int NoteMax = 2000;

    /// <summary>
    /// URL action POST của giao diện Leader: luôn kèm ?d= (ngày đang xem) và ?as= (Leader đang xem) để thao tác
    /// áp đúng ngày / đúng nhóm dù cookie "Xem Leader" đổi ở tab khác.
    /// </summary>
    public static string Post(HttpRequest r, ViewScope s, string path) =>
        Ui.To(r, path, ("d", Fmt.Key(s.Date)), ("as", s.MeId?.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Kiểu ảnh PhotoStore nhận (bản sao danh sách trong PhotoStore — giữ đồng bộ).</summary>
    static readonly HashSet<string> PhotoTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "image/gif", "image/heic" };

    /// <summary>
    /// Kiểm ảnh TRƯỚC khi gọi Flow.CreateExtraAsync (hàm đó lưu phát sinh rồi mới kiểm ảnh) — cùng luật, cùng câu báo lỗi
    /// với PhotoStore.SaveAsync. Bỏ khi WorkflowService tự kiểm ảnh trước lần SaveChanges đầu.
    /// </summary>
    public static void CheckPhotos(IEnumerable<IFormFile>? files)
    {
        if (files is null) return;
        foreach (var f in files.Where(f => f.Length > 0).Take(PhotoStore.MaxFiles))
        {
            if (!PhotoTypes.Contains(f.ContentType ?? "")) throw new BizException("Chỉ nhận file ảnh (jpg, png, webp, gif, heic)");
            if (f.Length > PhotoStore.MaxBytes) throw new BizException("Ảnh vượt quá 10 MB");
        }
    }

    /// <summary>% hoàn thành (Math.round của artifact) — 0 khi không có việc.</summary>
    public static int Pct(int done, int total) =>
        total == 0 ? 0 : (int)Math.Round(done * 100.0 / total, MidpointRounding.AwayFromZero);
}
