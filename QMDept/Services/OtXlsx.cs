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
/// Tự viết SpreadsheetML (không thêm thư viện).
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

    /// <summary>basis = "reg": đăng ký đã duyệt theo giờ đăng ký; "actual": chỉ đăng ký có kết quả đã duyệt, theo giờ thực tế.</summary>
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
            var actual = basis == "actual";
            var a = actual ? o.ActualStart : o.Start; var b = actual ? o.ActualEnd : o.End;
            var off = o.DayType == OtDayType.Holiday || o.Date.DayOfWeek == DayOfWeek.Sunday;
            var (normal, night) = Split(off, Dec(a), Dec(b));
            var content = actual && !string.IsNullOrWhiteSpace(o.DoneContent) ? o.DoneContent! : !string.IsNullOrWhiteSpace(o.Content) ? o.Content : o.ExtraContent ?? "";
            rows.Add(new Row(p.Code ?? "", p.Name, o.Date, Dec(a), Dec(b), content.Trim(), off ? 0 : normal, off ? normal : 0, off ? 0 : night, off ? night : 0, ""));
        }
        return rows;
    }

    static string E(string? s) => SecurityElement.Escape(s ?? "") ?? "";
    static string Col(int i) => ((char)('A' + i)).ToString();
    static string Num(double d) => d.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    // Chỉ số kiểu (cellXfs) — xem Styles()
    const int sTitle = 1, sHead = 2, sHeadPct = 3, sCenter = 4, sLeft = 5, sDate = 6, sWrap = 7, sNum = 8, sBoldItal = 9, sItal = 10, sSign = 11, sTotL = 12, sTotN = 13;

    static string Styles() => """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<numFmts count="1"><numFmt numFmtId="164" formatCode="dd/mm/yyyy"/></numFmts>
<fonts count="6">
<font><sz val="10"/><name val="Times New Roman"/><family val="1"/></font>
<font><b/><sz val="12"/><name val="Times New Roman"/><family val="1"/></font>
<font><b/><sz val="14"/><name val="Times New Roman"/><family val="1"/></font>
<font><b/><i/><sz val="10"/><name val="Times New Roman"/><family val="1"/></font>
<font><i/><sz val="10"/><name val="Times New Roman"/><family val="1"/></font>
<font><b/><sz val="10"/><name val="Times New Roman"/><family val="1"/></font>
</fonts>
<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
<borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color auto="1"/></left><right style="thin"><color auto="1"/></right><top style="thin"><color auto="1"/></top><bottom style="thin"><color auto="1"/></bottom><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="14">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
<xf numFmtId="9" fontId="1" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
<xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
<xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
<xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center" wrapText="1"/></xf>
<xf numFmtId="2" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
<xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="left"/></xf>
<xf numFmtId="0" fontId="4" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="left"/></xf>
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment horizontal="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="5" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="center"/></xf>
<xf numFmtId="2" fontId="5" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyFont="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
</cellXfs>
<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
""";

    sealed class Sheet
    {
        // hàng → (cột → ô): Excel bắt buộc ô trong hàng theo thứ tự cột tăng dần
        readonly SortedDictionary<int, SortedDictionary<int, string>> _byRow = [];
        public readonly List<string> Merges = [];
        public void S(int row, int col, string text, int style) => Add(row, col, $"<c r=\"{Col(col)}{row}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{E(text)}</t></is></c>");
        public void N(int row, int col, double v, int style) => Add(row, col, $"<c r=\"{Col(col)}{row}\" s=\"{style}\"><v>{Num(v)}</v></c>");
        public void Blank(int row, int col, int style) => Add(row, col, $"<c r=\"{Col(col)}{row}\" s=\"{style}\"/>");
        void Add(int row, int col, string cell) { if (!_byRow.TryGetValue(row, out var d)) _byRow[row] = d = []; d[col] = cell; }
        public string Rows(Dictionary<int, double>? heights = null)
        {
            var sb = new StringBuilder();
            foreach (var (r, cells) in _byRow) sb.Append($"<row r=\"{r}\"{(heights != null && heights.TryGetValue(r, out var h) ? $" ht=\"{Num(h)}\" customHeight=\"1\"" : "")}>{string.Concat(cells.Values)}</row>");
            return sb.ToString();
        }
    }

    public static byte[] Build(DateOnly month, List<Row> rows, List<(string Code, string Name)> staff, byte[]? logo)
    {
        // ---- sheet 1: Bảng OT nhập liệu ----
        var s1 = new Sheet(); var heights = new Dictionary<int, double> { [1] = 21, [2] = 21, [3] = 34.5, [4] = 34.5 };
        s1.S(1, 2, $"BẢNG ĐĂNG KÝ LÀM THÊM GIỜ THÁNG {month.Month}/{month.Year}\nBộ phận: Quản lý chất lượng và bảo dưỡng", sTitle);
        string[] heads = ["STT", "ID", "Họ và Tên", "Ngày làm thêm", "", "Nội dung công việc\n(Ghi rõ công việc của từng cá nhân)", "Thời gian làm thêm", "", "", "", "", "", "Tổng giờ làm thêm", "Ghi chú"];
        for (var c = 0; c < heads.Length; c++) if (c is not (8 or 9 or 10 or 11)) s1.S(3, c, heads[c], sHead);
        foreach (var (c, v) in new[] { (8, 1.5), (9, 2.0), (10, 2.1), (11, 2.7) }) { s1.N(3, c, v, sHeadPct); s1.Blank(4, c, sHead); }
        foreach (var c in new[] { 0, 1, 2, 3, 4, 5, 12, 13 }) s1.Blank(4, c, sHead);
        s1.S(4, 6, "Từ", sHead); s1.S(4, 7, "Đến", sHead);
        s1.Merges.AddRange(["C1:N2", "A3:A4", "B3:B4", "C3:C4", "D3:E4", "F3:F4", "G3:H3", "I3:I4", "J3:J4", "K3:K4", "L3:L4", "M3:M4", "N3:N4"]);
        string[] dow = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];
        var r = 5; var i = 0;
        foreach (var x in rows)
        {
            i++;
            s1.N(r, 0, i, sCenter); s1.S(r, 1, x.Code, sLeft); s1.S(r, 2, x.Name, sLeft);
            s1.N(r, 3, (x.Date.ToDateTime(TimeOnly.MinValue) - new DateTime(1899, 12, 30)).TotalDays, sDate);
            s1.S(r, 4, dow[(int)x.Date.DayOfWeek], sCenter); s1.S(r, 5, x.Content, sWrap);
            s1.N(r, 6, x.From, sNum); s1.N(r, 7, x.To, sNum);
            if (x.H150 > 0) s1.N(r, 8, x.H150, sNum); else s1.Blank(r, 8, sNum);
            if (x.H200 > 0) s1.N(r, 9, x.H200, sNum); else s1.Blank(r, 9, sNum);
            if (x.H210 > 0) s1.N(r, 10, x.H210, sNum); else s1.Blank(r, 10, sNum);
            if (x.H270 > 0) s1.N(r, 11, x.H270, sNum); else s1.Blank(r, 11, sNum);
            s1.N(r, 12, x.Total, sNum); s1.S(r, 13, x.Note, sLeft);
            heights[r] = Math.Max(28.5, 14.5 * Math.Ceiling(Math.Max(1, x.Content.Length) / 48.0));
            r++;
        }
        // dòng tổng cộng
        s1.S(r, 0, "", sTotL); s1.S(r, 1, "", sTotL); s1.S(r, 2, "Tổng cộng", sTotL); foreach (var c in new[] { 3, 4, 5, 6, 7 }) s1.Blank(r, c, sTotL);
        s1.N(r, 8, rows.Sum(x => x.H150), sTotN); s1.N(r, 9, rows.Sum(x => x.H200), sTotN); s1.N(r, 10, rows.Sum(x => x.H210), sTotN); s1.N(r, 11, rows.Sum(x => x.H270), sTotN);
        s1.N(r, 12, rows.Sum(x => x.Total), sTotN); s1.Blank(r, 13, sTotL);
        r += 2;
        s1.S(r, 0, "Ghi chú:", sBoldItal); s1.Merges.Add($"A{r}:N{r}"); r += 2;
        s1.S(r, 0, "- Người kiểm tra: TP; Người duyệt: GĐ", sItal); r++;
        s1.S(r, 0, "- Danh sách đăng ký làm thêm phải gửi cho GĐ ký duyệt trước 16h hàng ngày, nếu làm thêm ngày nghỉ thì gửi trước 16h ngày hôm trước.", sItal); r++;
        s1.S(r, 0, "Người lập\n(Ký, ghi họ tên)", sSign); s1.Merges.Add($"A{r}:D{r}");
        s1.S(r, 5, "Người kiểm tra\n(Ký, ghi họ tên)", sSign);
        s1.S(r, 6, "Người duyệt\n(Ký, ghi họ tên)", sSign); s1.Merges.Add($"G{r}:N{r}");
        heights[r] = 40;
        var sheet1 = $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheetPr><pageSetUpPr fitToPage="1"/></sheetPr>
<sheetViews><sheetView tabSelected="1" zoomScale="85" zoomScaleNormal="85" workbookViewId="0"><pane ySplit="4" topLeftCell="A5" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>
<sheetFormatPr defaultRowHeight="15"/>
<cols><col min="1" max="1" width="7.3" customWidth="1"/><col min="2" max="2" width="9.6" customWidth="1"/><col min="3" max="3" width="22.7" customWidth="1"/><col min="4" max="4" width="19.7" customWidth="1"/><col min="5" max="5" width="7.3" customWidth="1"/><col min="6" max="6" width="46" customWidth="1"/><col min="7" max="12" width="9.2" customWidth="1"/><col min="13" max="13" width="10.3" customWidth="1"/><col min="14" max="14" width="20.7" customWidth="1"/></cols>
<sheetData>{s1.Rows(heights)}</sheetData>
<mergeCells count="{s1.Merges.Count}">{string.Concat(s1.Merges.Select(m => $"<mergeCell ref=\"{m}\"/>"))}</mergeCells>
<pageMargins left="0.7087" right="0.7087" top="0.748" bottom="0.748" header="0.315" footer="0.315"/>
<pageSetup paperSize="9" orientation="landscape" fitToWidth="1" fitToHeight="0"/>
{(logo is null ? "" : "<drawing r:id=\"rId1\"/>")}
</worksheet>
""";
        // ---- sheet 2: Danh sách CBCNV ----
        var s2 = new Sheet();
        s2.S(3, 0, "STT", sHead); s2.S(3, 1, "ID", sHead); s2.S(3, 2, "Họ tên", sHead); s2.S(3, 3, "Tên công ty", sHead);
        var k = 4; foreach (var (code, name) in staff) { s2.N(k, 0, k - 3, sCenter); s2.S(k, 1, code, sLeft); s2.S(k, 2, name, sLeft); s2.Blank(k, 3, sLeft); k++; }
        var sheet2 = $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0"/></sheetViews><sheetFormatPr defaultRowHeight="15"/>
<cols><col min="1" max="1" width="7" customWidth="1"/><col min="2" max="2" width="10" customWidth="1"/><col min="3" max="3" width="28" customWidth="1"/><col min="4" max="4" width="24" customWidth="1"/></cols>
<sheetData>{s2.Rows()}</sheetData></worksheet>
""";
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Put(string name, string content) { var e = zip.CreateEntry(name); using var w = new StreamWriter(e.Open(), new UTF8Encoding(false)); w.Write(content); }
            Put("[Content_Types].xml", $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>{(logo is null ? "" : "<Override PartName=\"/xl/drawings/drawing1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.drawing+xml\"/>")}</Types>
""");
            Put("_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Put("xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Bảng OT nhập liệu" sheetId="1" r:id="rId1"/><sheet name="Danh sách CBCNV" sheetId="2" r:id="rId2"/></sheets></workbook>""");
            Put("xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            Put("xl/styles.xml", Styles());
            Put("xl/worksheets/sheet1.xml", sheet1);
            Put("xl/worksheets/sheet2.xml", sheet2);
            if (logo is not null)
            {
                Put("xl/worksheets/_rels/sheet1.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing1.xml"/></Relationships>""");
                Put("xl/drawings/drawing1.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"><xdr:twoCellAnchor editAs="oneCell"><xdr:from><xdr:col>0</xdr:col><xdr:colOff>7620</xdr:colOff><xdr:row>0</xdr:row><xdr:rowOff>53341</xdr:rowOff></xdr:from><xdr:to><xdr:col>2</xdr:col><xdr:colOff>1146</xdr:colOff><xdr:row>1</xdr:row><xdr:rowOff>167640</xdr:rowOff></xdr:to><xdr:pic><xdr:nvPicPr><xdr:cNvPr id="2" name="Logo"/><xdr:cNvPicPr><a:picLocks noChangeAspect="1"/></xdr:cNvPicPr></xdr:nvPicPr><xdr:blipFill><a:blip xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:embed="rId1"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill><xdr:spPr><a:xfrm><a:off x="7620" y="53341"/><a:ext cx="1144146" cy="380999"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></xdr:spPr></xdr:pic><xdr:clientData/></xdr:twoCellAnchor></xdr:wsDr>""");
                Put("xl/drawings/_rels/drawing1.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../media/image1.png"/></Relationships>""");
                var le = zip.CreateEntry("xl/media/image1.png"); using var ls = le.Open(); ls.Write(logo);
            }
        }
        return ms.ToArray();
    }
}
