using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Xuất "BẢNG ĐĂNG KÝ LÀM THÊM GIỜ THÁNG …/…" ra file Excel (.xlsx) đúng mẫu <c>Tháng 9.1.xlsx</c> của phòng (yêu cầu 2026-10-24):
/// sheet "Bảng OT nhập liệu" (STT, ID, Họ và Tên, Ngày làm thêm, Thứ, Nội dung công việc, Từ – Đến, giờ theo hệ số 150% / 200% / 210% / 270%,
/// Tổng giờ làm thêm, Ghi chú + phần ghi chú và chữ ký) và sheet "Danh sách CBCNV". Không gồm Trưởng phòng / Quản trị.
/// Giờ: ngày thường = 150% đến 22:00, sau 22:00 = 210%; Chủ nhật / ngày nghỉ = 200% đến 22:00, sau 22:00 = 270%; trừ nghỉ trưa 12:00–13:00.
/// Dựng từ chính file mẫu Assets/ot-template.xlsx (= Tháng 9.1.xlsx): giữ nguyên font, cỡ chữ, khung, logo, ghi chú, chữ ký; chỉ thay tiêu đề tháng và các dòng dữ liệu (không thêm dòng tổng).
/// </summary>
public static class OtXlsx
{
    public sealed record Row(string Code, string Name, DateOnly Date, double From, double To, string Content, double H150, double H200, double H210, double H270, string Note)
    {
        public double Total => H150 + H200 + H210 + H270;
    }

    /// <summary>Hệ số theo file mẫu: (trong giờ hành chính mở rộng, sau 22:00) cho ngày thường hoặc Chủ nhật / ngày nghỉ.</summary>
    public static (double Normal, double Night) Split(bool off, double from, double to)
    {
        if (to <= from) return (0, 0);
        var lunch = Math.Max(0, Math.Min(to, 13) - Math.Max(from, 12));
        var night = Math.Max(0, to - Math.Max(from, 22));
        var normal = Math.Max(0, Math.Min(to, 22) - from) - lunch;
        return (Math.Max(0, normal), night);
    }

    static double Dec(string? hhmm) => Fmt.IsHhmm(hhmm) ? Fmt.Min(hhmm!) / 60.0 : 0;

    /// <summary>basis = "latest" (mặc định, yêu cầu 2026-10-27): đăng ký đã được Trưởng phòng duyệt, giờ theo PHÊ DUYỆT MỚI NHẤT — có kết quả đã duyệt thì giờ thực tế, chưa thì giờ đăng ký; "reg": luôn giờ đăng ký; "actual": chỉ đăng ký có kết quả đã duyệt, giờ thực tế. Cách tính hệ số giữ nguyên.</summary>
    public static async Task<List<Row>> LoadAsync(IDbContextFactory<AppDbContext> dbf, DataCache cache, DateOnly month, string basis)
    {
        var from = new DateOnly(month.Year, month.Month, 1); var to = from.AddMonths(1).AddDays(-1);
        await using var db = await dbf.CreateDbContextAsync();
        var q = db.Overtimes.AsNoTracking().Where(o => o.Date >= from && o.Date <= to && o.Status == OtStatus.Approved);
        if (basis == "actual") q = q.Where(o => o.Completion == OtCompletion.Approved);
        var ots = await q.ToListAsync();
        var people = (await cache.GetBaseAsync()).People.Where(p => !p.IsDeleted && p.Role is Role.Staff or Role.Leader).ToDictionary(p => p.Id);
        var rows = new List<Row>();
        foreach (var o in ots.Where(o => people.ContainsKey(o.PersonId)).OrderBy(o => o.Date).ThenBy(o => people[o.PersonId].Name, Fmt.ViCompare))
        {
            var p = people[o.PersonId];
            var actual = basis == "actual" || (basis == "latest" && o.Completion == OtCompletion.Approved && !string.IsNullOrEmpty(o.ActualStart) && !string.IsNullOrEmpty(o.ActualEnd));
            var a = actual ? o.ActualStart : o.Start; var b = actual ? o.ActualEnd : o.End;
            var off = o.DayType == OtDayType.Holiday || o.Date.DayOfWeek == DayOfWeek.Sunday;
            var (normal, night) = Split(off, Dec(a), Dec(b));
            var content = actual && !string.IsNullOrWhiteSpace(o.DoneContent) ? o.DoneContent! : !string.IsNullOrWhiteSpace(o.Content) ? o.Content : o.ExtraContent ?? "";
            rows.Add(new Row(p.Code ?? "", p.Name, o.Date, Dec(a), Dec(b), content.Trim(), off ? 0 : normal, off ? normal : 0, off ? 0 : night, off ? night : 0, ""));
        }
        return rows;
    }


    static string Num(double d) => d.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    static readonly System.Xml.Linq.XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    const int FirstData = 5, TemplateLastData = 141, TemplateFooter = 142;

    /// <summary>Xuất file từ mẫu: dòng 5.. là dữ liệu (dòng trống của mẫu vẫn ẩn như file gốc), phần ghi chú / chữ ký dịch xuống nếu có quá 137 dòng.</summary>
    public static byte[] BuildFromTemplate(string templatePath, DateOnly month, List<Row> rows)
    {
        using var src = new MemoryStream(File.ReadAllBytes(templatePath));
        using var inZip = new ZipArchive(src, ZipArchiveMode.Read);
        var parts = new Dictionary<string, byte[]>();
        foreach (var e in inZip.Entries) { using var s = e.Open(); using var m = new MemoryStream(); s.CopyTo(m); parts[e.FullName] = m.ToArray(); }

        var doc = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(parts["xl/worksheets/sheet1.xml"]));
        var data = doc.Root!.Element(Ns + "sheetData")!;
        var all = data.Elements(Ns + "row").ToDictionary(r => (int)r.Attribute("r")!);
        var protoFilled = all[FirstData];                    // dòng dữ liệu mẫu (có chữ, cao 28,5)
        var protoBlank = all[TemplateLastData];              // dòng trống (ẩn) của mẫu
        var footer = all.Where(kv => kv.Key >= TemplateFooter).OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();

        var capacity = Math.Max(TemplateLastData - FirstData + 1, rows.Count);
        var footerStart = FirstData + capacity; var delta = footerStart - TemplateFooter;
        static string ColOf(string cell) => new string(cell.TakeWhile(char.IsLetter).ToArray());

        System.Xml.Linq.XElement CloneRow(System.Xml.Linq.XElement proto, int row, bool filled)
        {
            var r = new System.Xml.Linq.XElement(proto);
            r.SetAttributeValue("r", row);
            if (filled) r.Attribute("hidden")?.Remove();
            foreach (var c in r.Elements(Ns + "c").ToList())
            {
                var col = ColOf((string)c.Attribute("r")!);
                if (col.Length > 1 || col[0] >= 'S') { c.Remove(); continue; }   // ô phụ S..X (định dạng ngày mẫu) bỏ
                c.SetAttributeValue("r", col + row);
                c.Elements().ToList().ForEach(x => x.Remove()); c.Attribute("t")?.Remove();   // bỏ công thức / giá trị mẫu, giữ kiểu (font, khung)
            }
            return r;
        }
        static System.Xml.Linq.XElement Cell(System.Xml.Linq.XElement row, string col) => row.Elements(Ns + "c").First(x => ColOf((string)x.Attribute("r")!) == col);
        static void SetStr(System.Xml.Linq.XElement row, string col, string text)
        {
            var c = Cell(row, col);
            c.SetAttributeValue("t", "inlineStr");
            c.Add(new System.Xml.Linq.XElement(Ns + "is", new System.Xml.Linq.XElement(Ns + "t", new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xml + "space", "preserve"), text)));
        }
        static void SetNum(System.Xml.Linq.XElement row, string col, double v) => Cell(row, col).Add(new System.Xml.Linq.XElement(Ns + "v", Num(v)));

        // dựng lại sheetData: dòng 1-4 giữ nguyên → dữ liệu → dòng trống ẩn → ghi chú / chữ ký
        var built = all.Where(kv => kv.Key < FirstData).OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
        string[] dow = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];
        for (var i = 0; i < capacity; i++)
        {
            var rn = FirstData + i;
            if (i < rows.Count)
            {
                var x = rows[i]; var r = CloneRow(protoFilled, rn, true);
                SetNum(r, "A", i + 1); SetStr(r, "B", x.Code); SetStr(r, "C", x.Name);
                SetNum(r, "D", (x.Date.ToDateTime(TimeOnly.MinValue) - new DateTime(1899, 12, 30)).TotalDays);
                SetStr(r, "E", dow[(int)x.Date.DayOfWeek]); SetStr(r, "F", x.Content);
                SetNum(r, "G", x.From); SetNum(r, "H", x.To);
                if (x.H150 > 0) SetNum(r, "I", x.H150);
                if (x.H200 > 0) SetNum(r, "J", x.H200);
                if (x.H210 > 0) SetNum(r, "K", x.H210);
                if (x.H270 > 0) SetNum(r, "L", x.H270);
                SetNum(r, "M", x.Total);
                if (!string.IsNullOrEmpty(x.Note)) SetStr(r, "N", x.Note);
                var lines = Math.Max(1, (int)Math.Ceiling(Math.Max(1, x.Content.Length) / 52.0));
                if (lines > 2) r.SetAttributeValue("ht", Num(Math.Min(120, 14.5 * lines)));
                built.Add(r);
            }
            else built.Add(CloneRow(protoBlank, rn, false));
        }
        foreach (var fr in footer)
        {
            var nr = new System.Xml.Linq.XElement(fr); var rn = (int)fr.Attribute("r")! + delta;
            nr.SetAttributeValue("r", rn);
            foreach (var c in nr.Elements(Ns + "c")) c.SetAttributeValue("r", ColOf((string)c.Attribute("r")!) + rn);
            built.Add(nr);
        }
        data.RemoveNodes(); foreach (var r in built) data.Add(r);

        // tiêu đề: "THÁNG m/yyyy" (ô C1, chuỗi nhiều dòng)
        var c1 = Cell(built.First(r => (int)r.Attribute("r")! == 1), "C1".TrimEnd('1'));
        c1.SetAttributeValue("t", "inlineStr"); c1.RemoveNodes();
        c1.Add(new System.Xml.Linq.XElement(Ns + "is", new System.Xml.Linq.XElement(Ns + "t", new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xml + "space", "preserve"),
            $"BẢNG ĐĂNG KÝ LÀM THÊM GIỜ THÁNG {month.Month}/{month.Year}\nBộ phận: Quản lý chất lượng và bảo dưỡng")));

        // merge / lọc / vùng in: dịch theo số dòng
        foreach (var m in doc.Root!.Element(Ns + "mergeCells")!.Elements(Ns + "mergeCell"))
        {
            var rf = (string)m.Attribute("ref")!;
            if (System.Text.RegularExpressions.Regex.IsMatch(rf, @"\d{3}"))
                m.SetAttributeValue("ref", System.Text.RegularExpressions.Regex.Replace(rf, @"\d+", mm => (int.Parse(mm.Value) + delta).ToString()));
        }
        doc.Root.Element(Ns + "autoFilter")?.SetAttributeValue("ref", $"A4:X{TemplateFooter + delta}");
        doc.Root.Element(Ns + "dimension")?.SetAttributeValue("ref", $"A1:X{196 + delta}");
        parts["xl/worksheets/sheet1.xml"] = new UTF8Encoding(false).GetBytes(doc.Declaration + "\n" + doc.Root);

        var wb = Encoding.UTF8.GetString(parts["xl/workbook.xml"]).Replace("$A$4:$X$142", $"$A$4:$X${TemplateFooter + delta}").Replace("$A$1:$N$156", $"$A$1:$N${156 + delta}");
        parts["xl/workbook.xml"] = new UTF8Encoding(false).GetBytes(wb);
        // chuỗi tính (calcChain) tham chiếu ô công thức đã bỏ → xóa để Excel tự dựng lại
        parts.Remove("xl/calcChain.xml");
        parts["xl/_rels/workbook.xml.rels"] = new UTF8Encoding(false).GetBytes(System.Text.RegularExpressions.Regex.Replace(Encoding.UTF8.GetString(parts["xl/_rels/workbook.xml.rels"]), "<Relationship [^>]*calcChain[^>]*/>", ""));
        parts["[Content_Types].xml"] = new UTF8Encoding(false).GetBytes(System.Text.RegularExpressions.Regex.Replace(Encoding.UTF8.GetString(parts["[Content_Types].xml"]), "<Override [^>]*calcChain[^>]*/>", ""));

        using var outMs = new MemoryStream();
        using (var z = new ZipArchive(outMs, ZipArchiveMode.Create, true))
            foreach (var (name, bytes) in parts) { var e = z.CreateEntry(name); using var w = e.Open(); w.Write(bytes); }
        return outMs.ToArray();
    }
}
