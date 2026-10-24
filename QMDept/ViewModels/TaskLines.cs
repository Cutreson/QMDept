using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>
/// Nhập NHIỀU công việc của cùng một dự án trong một bảng (yêu cầu 2026-10-24): mỗi dòng = nội dung + giờ bắt đầu + giờ kết thúc (field <c>ltitle</c> / <c>lstart</c> / <c>lend</c>,
/// lặp lại theo từng dòng). Dùng ở Giao thêm việc, Đề xuất công việc, Kế hoạch cá nhân (Leader / TP) và Thêm công việc ngoài kế hoạch.
/// Không cho cài hai việc trùng khung giờ: trùng NHAU trong bảng, hoặc trùng việc đã có trong ngày của người thực hiện.
/// </summary>
public static class TaskLines
{
    public sealed record Line(string Title, string Start, string End);

    /// <summary>Đọc các dòng từ form; không có field <c>ltitle</c> thì dùng title / start / end cũ (một dòng).</summary>
    public static List<Line> Read(IFormCollection form, string? legacyTitle = null, string? legacyStart = null, string? legacyEnd = null)
    {
        var t = form["ltitle"]; var s = form["lstart"]; var e = form["lend"];
        if (t.Count == 0) return string.IsNullOrWhiteSpace(legacyTitle) ? [] : [new Line(legacyTitle.Trim(), legacyStart ?? "", legacyEnd ?? "")];
        var list = new List<Line>();
        for (var i = 0; i < t.Count; i++)
            if (!string.IsNullOrWhiteSpace(t[i])) list.Add(new Line(t[i]!.Trim(), i < s.Count ? s[i] ?? "" : "", i < e.Count ? e[i] ?? "" : ""));
        return list;
    }

    /// <summary>Kiểm tra từng dòng (nội dung ≤ 300, giờ hợp lệ, kết thúc sau bắt đầu) và không dòng nào trùng giờ với dòng khác; tối đa 20 dòng.</summary>
    public static List<Line> Validate(List<Line> lines, bool allowNone = false)
    {
        if (lines.Count == 0) { if (allowNone) return lines; throw new BizException("Nhập nội dung công việc"); }
        if (lines.Count > 20) throw new BizException("Mỗi lần nhập tối đa 20 công việc");
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (l.Title.Length > 300) throw new BizException($"Công việc {i + 1}: nội dung tối đa 300 ký tự");
            if (!Fmt.IsHhmm(l.Start) || !Fmt.IsHhmm(l.End) || l.End.Le(l.Start)) throw new BizException($"Công việc {i + 1} “{Short(l.Title)}”: giờ kết thúc phải sau giờ bắt đầu");
        }
        for (var i = 0; i < lines.Count; i++)
            for (var j = i + 1; j < lines.Count; j++)
                if (Overlap(lines[i].Start, lines[i].End, lines[j].Start, lines[j].End))
                    throw new BizException($"Trùng khung giờ: công việc {i + 1} “{Short(lines[i].Title)}” ({lines[i].Start}–{lines[i].End}) và công việc {j + 1} “{Short(lines[j].Title)}” ({lines[j].Start}–{lines[j].End}) — chỉnh lại giờ");
        return lines;
    }

    /// <summary>Hai khoảng giờ [a1,a2) và [b1,b2) có chồng nhau không (liền kề như 08:00–10:00 và 10:00–12:00 là không trùng).</summary>
    public static bool Overlap(string a1, string a2, string b1, string b2) => Fmt.Min(a1) < Fmt.Min(b2) && Fmt.Min(b1) < Fmt.Min(a2);

    /// <summary>Không trùng giờ với việc ĐÃ CÓ của <paramref name="emp"/> trong ngày (bỏ việc đã chuyển sang ngày khác và <paramref name="excludeId"/>).</summary>
    public static async Task EnsureFreeAsync(AppDbContext db, int emp, string? empName, DateOnly date, IEnumerable<Line> lines, int? excludeId = null)
    {
        empName ??= await db.People.AsNoTracking().Where(p => p.Id == emp).Select(p => p.Name).FirstOrDefaultAsync() ?? "Nhân sự";   // không phụ thuộc Dept đã nạp chưa
        var have = await db.WorkTasks.AsNoTracking().Where(t => t.Date == date && t.EmployeeId == emp && t.CarriedToId == null && (excludeId == null || t.Id != excludeId)).ToListAsync();
        foreach (var l in lines)
            if (have.FirstOrDefault(h => Overlap(l.Start, l.End, h.Start, h.End)) is { } h)
                throw new BizException($"{empName} đã có việc “{Short(h.Title)}” ({h.Start}–{h.End}) ngày {Fmt.Dmy(date)} trùng khung giờ {l.Start}–{l.End} của “{Short(l.Title)}” — chọn giờ khác");
    }

    static string Short(string s) => s.Length <= 40 ? s : s[..40] + "…";
}
