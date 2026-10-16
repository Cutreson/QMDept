#:package MySql.Data@9.4.0
// Nạp / kiểm tra file "thông tin DA.xlsx" (2 sheet: "Dự án chế tạo", "Dự án demo + Nội bộ") vào DB thật (yêu cầu chủ dự án 2026-10-16).
//   dotnet run tools/import-da.cs gen <file.xlsx> <out.sql> [chetao]   đọc Excel → sinh file SQL (mỗi câu một dòng) để chạy bằng tools/dbquery.cs; "chetao" = chỉ sheet Dự án chế tạo
//   dotnet run tools/import-da.cs verify <file.xlsx> [chetao]  đọc lại Excel và so từng dự án / hạng mục với DB (báo mọi chỗ lệch)
//   dotnet run tools/import-da.cs report <file.xlsx>          in thống kê + các điểm bất thường trong file (ngày lỗi, tên trống…)
// Quy tắc nạp (đã ghi ở docs/nghiep-vu.md §18): sheet 1 → ngày TT = ngày hạng mục; sheet 2 → ngày TT nếu có, không thì KH; dự án trống tên bị bỏ qua.
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using MySql.Data.MySqlClient;
using System.Text.Json;

var mode = args.Length > 0 ? args[0] : "";
var xlsx = args.Length > 1 ? args[1] : "";
var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
var sheets = ReadXlsx(xlsx);
var projects = Parse(sheets, today, out var notes);
// Tham số cuối "chetao": chỉ lấy sheet "Dự án chế tạo", bỏ sheet "Dự án demo + Nội bộ" (yêu cầu 2026-10-16)
if (args.Length > 2 && args[^1] == "chetao") projects = projects.Where(p => p.Category == "Dự án chế tạo").ToList();

if (mode == "gen") { File.WriteAllText(args[2], BuildSql(projects), new UTF8Encoding(false)); Console.WriteLine($"Đã sinh {args[2]}: {projects.Count} dự án, {projects.Sum(p => p.Items.Count)} hạng mục"); }
else if (mode == "report") Report();
else if (mode == "verify") Verify();
else Console.WriteLine("dùng: gen <xlsx> <out.sql> | verify <xlsx> | report <xlsx>");
return;

// ======================= Đọc xlsx =======================
static Dictionary<string, Dictionary<int, Dictionary<string, string>>> ReadXlsx(string path)
{
    var tmp = Path.Combine(Path.GetTempPath(), "qm_da_" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tmp);
    var copy = Path.Combine(tmp, "da.zip"); File.Copy(path, copy);
    using var zip = ZipFile.OpenRead(copy);
    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    XNamespace rn = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    string Read(string name) { using var s = zip.GetEntry(name)!.Open(); using var r = new StreamReader(s, Encoding.UTF8); return r.ReadToEnd(); }
    var ss = XDocument.Parse(Read("xl/sharedStrings.xml")).Root!.Elements(ns + "si")
        .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))).ToList();
    var wb = XDocument.Parse(Read("xl/workbook.xml")).Root!;
    var rels = XDocument.Parse(Read("xl/_rels/workbook.xml.rels")).Root!.Elements().ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
    var res = new Dictionary<string, Dictionary<int, Dictionary<string, string>>>();
    foreach (var sh in wb.Element(ns + "sheets")!.Elements(ns + "sheet"))
    {
        var name = (string)sh.Attribute("name")!; var rid = (string)sh.Attribute(rn + "id")!;
        var target = rels[rid]; if (!target.StartsWith("/")) target = "xl/" + target; else target = target.TrimStart('/');
        var rows = new Dictionary<int, Dictionary<string, string>>();
        foreach (var row in XDocument.Parse(Read(target)).Root!.Element(ns + "sheetData")!.Elements(ns + "row"))
        {
            var cells = new Dictionary<string, string>();
            foreach (var c in row.Elements(ns + "c"))
            {
                var r = (string)c.Attribute("r")!; var col = new string(r.TakeWhile(char.IsLetter).ToArray());
                var t = (string?)c.Attribute("t"); var v = c.Element(ns + "v")?.Value;
                if (t == "s" && v != null) v = ss[int.Parse(v)];
                else if (t == "inlineStr") v = string.Concat(c.Descendants(ns + "t").Select(x => x.Value));
                // ô lỗi công thức (#REF! / #VALUE!) giữ nguyên chữ để báo bất thường; D() / I() không đọc được → coi như trống
                if (!string.IsNullOrWhiteSpace(v)) cells[col] = v.Trim();
            }
            if (cells.Count > 0) rows[(int)row.Attribute("r")!] = cells;
        }
        res[name] = rows;
    }
    zip.Dispose(); try { Directory.Delete(tmp, true); } catch { /* bỏ qua */ }
    return res;
}

// ======================= Phân tích =======================
static DateOnly? D(string? v) => v != null && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 40000 && n < 60000
    ? DateOnly.FromDateTime(new DateTime(1899, 12, 30).AddDays(Math.Floor(n))) : null;
static int? I(string? v) => v != null && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 0 && n < 100000 ? (int)Math.Round(n) : null;
static string? S(Dictionary<string, string> r, string c) => r.TryGetValue(c, out var v) ? System.Text.RegularExpressions.Regex.Replace(v, @"\s+", " ").Trim() : null;
static string Status(DateOnly? s, DateOnly? e, DateOnly today) => e is DateOnly ed && ed < today ? "Done" : s is DateOnly sd && sd <= today ? "InProgress" : "NotStarted";

static List<Proj> Parse(Dictionary<string, Dictionary<int, Dictionary<string, string>>> sheets, DateOnly today, out List<string> notes)
{
    var list = new List<Proj>(); notes = [];
    // ---- Sheet 1: Dự án chế tạo — A=STT B=Dự án C=Mã D=Khách hàng E=Tỉnh thành F=Số lượng G=Hạng mục H=Leader I=Bắt đầu TT J=Kết thúc TT
    Proj? cur = null;
    foreach (var (rn, r) in sheets["Dự án chế tạo"].OrderBy(k => k.Key))
    {
        if (rn < 4) continue;
        if (S(r, "B") != null)
        {
            cur = new Proj(S(r, "B")!, S(r, "C"), S(r, "D"), S(r, "E"), I(S(r, "F")), S(r, "H"), null, null, null, "Dự án chế tạo", false, null, []);
            list.Add(cur);
        }
        if (cur is null || S(r, "G") == null) continue;
        var s = D(S(r, "I")); var e = D(S(r, "J"));
        if (S(r, "I") != null && s is null) notes.Add($"Sheet 1 dòng {rn} «{S(r, "G")}»: ngày bắt đầu không hợp lệ ({S(r, "I")}) → để trống");
        if (s is DateOnly sd && e is DateOnly ed && ed < sd) notes.Add($"Sheet 1 dòng {rn} «{S(r, "G")}»: kết thúc {ed:dd/MM/yyyy} trước bắt đầu {sd:dd/MM/yyyy} (giữ nguyên như file)");
        cur.Items.Add(new Item(S(r, "G")!, null, s, e, null, null, Status(s, e, today), null));
    }
    // ---- Sheet 2: Dự án demo + Nội bộ — A=STT B=Trạng thái C=Dự án D=Cấp độ E=Số lượng F=Ngày PO G=Giao hàng theo PO H=Hạng mục I=PIC J=Tiến độ TT
    //      L/M=Bắt đầu/Kết thúc KH  O/P=Bắt đầu/Kết thúc TT  T=Trạng thái hạng mục  V/W/X=Tình trạng / Nguyên nhân chậm / NOTE
    cur = null; var skipping = false;
    foreach (var (rn, r) in sheets["Dự án demo + Nội bộ"].OrderBy(k => k.Key))
    {
        if (rn < 4) continue;
        if (S(r, "A") != null)
        {
            var name = S(r, "C");
            if (name == null) { skipping = true; cur = null; notes.Add($"Sheet 2 STT {S(r, "A")} (dòng {rn}, trạng thái «{S(r, "B")}»): dự án không có tên → bỏ qua cùng các hạng mục của nó"); continue; }
            skipping = false;
            string? note = null;
            DateOnly? po = D(S(r, "F")), del = D(S(r, "G"));
            var extra = new List<string>();
            if (S(r, "F") != null && po is null) extra.Add($"Ngày PO: {S(r, "F")}");
            if (S(r, "G") != null && del is null) extra.Add($"Ngày giao hàng theo PO: {S(r, "G")}");
            if (extra.Count > 0) note = string.Join(" · ", extra);
            cur = new Proj(name, null, null, null, I(S(r, "E")), null, po, del, S(r, "D"), "Dự án demo + Nội bộ", S(r, "B") == "Đã hoàn thành", note, []);
            list.Add(cur);
        }
        if (skipping || cur is null || S(r, "H") == null) continue;
        var ps = D(S(r, "L")); var pe = D(S(r, "M")); var ts = D(S(r, "O")); var te = D(S(r, "P"));
        var start = ts ?? ps; var end = te ?? pe;
        var done = cur.Closed || S(r, "T") == "Hoàn thành" || ((I(S(r, "J")) ?? 0) >= 1 && S(r, "T") != "Chưa hoàn thành");
        var st = done ? "Done" : (ts is DateOnly tsd && tsd <= today) ? "InProgress" : "NotStarted";
        var comment = string.Join(" · ", new[] { S(r, "V"), S(r, "W"), S(r, "X") }.Where(x => !string.IsNullOrEmpty(x)));
        cur.Items.Add(new Item(S(r, "H")!, S(r, "I"), start, end, ps, pe, st, comment.Length > 0 ? comment : null));
    }
    return list;
}

// ======================= Sinh SQL =======================
static string Q(string? s) => s is null ? "NULL" : "'" + s.Replace("\\", "\\\\").Replace("'", "''") + "'";
static string Dt(DateOnly? d) => d is null ? "NULL" : $"'{d:yyyy-MM-dd}'";
static string N(int? n) => n?.ToString() ?? "NULL";

static string BuildSql(List<Proj> ps)
{
    var sb = new StringBuilder("-- Nạp từ file thông tin DA.xlsx (tools/import-da.cs)\n");
    var order = 0;
    foreach (var p in ps)
    {
        order++;
        var start = p.Items.Select(i => i.Start).Where(d => d != null).Min() ?? p.Po;
        var end = p.Items.Select(i => i.End).Where(d => d != null).Max() ?? p.Delivery;
        var leader = p.Leader is null ? "NULL" : $"(SELECT Id FROM people WHERE Name = {Q(p.Leader)} AND Role = 'Leader' AND IsDeleted = 0 LIMIT 1)";
        sb.Append($"INSERT INTO projects (Name, Factory, Location, StartDate, EndDate, Need, StatusOverride, Closed, SortOrder, CreatedAt, Code, Quantity, OwnerLeaderId, PoDate, DeliveryDate, Level, Category, Note) VALUES ({Q(p.Name)}, {Q(p.Customer)}, {Q(p.Location)}, {Dt(start)}, {Dt(end)}, 2, NULL, {(p.Closed ? 1 : 0)}, {order}, UTC_TIMESTAMP(6), {Q(p.Code)}, {N(p.Qty)}, {leader}, {Dt(p.Po)}, {Dt(p.Delivery)}, {Q(p.Level)}, {Q(p.Category)}, {Q(p.Note)})\n");
        if (p.Items.Count == 0) continue;
        var rows = p.Items.Select((i, k) => $"((SELECT MAX(Id) FROM projects), {Q(i.Title)}, {Dt(i.Start)}, {Dt(i.End)}, {k + 1}, '{i.Status}', {(i.Status == "Done" ? Dt(i.End) : "NULL")}, NULL, {Q(i.Pic)}, {Dt(i.PlanStart)}, {Dt(i.PlanEnd)}, {Q(i.Note)})");
        sb.Append("INSERT INTO plan_items (ProjectId, Title, StartDate, EndDate, SortOrder, Status, DoneDate, DoneBy, Pic, PlanStart, PlanEnd, Note) VALUES " + string.Join(", ", rows) + "\n");
    }
    return sb.ToString();
}

// ======================= Báo cáo / kiểm tra =======================
void Report()
{
    foreach (var cat in projects.GroupBy(p => p.Category))
        Console.WriteLine($"{cat.Key}: {cat.Count()} dự án, {cat.Sum(p => p.Items.Count)} hạng mục");
    Console.WriteLine($"Tổng: {projects.Count} dự án, {projects.Sum(p => p.Items.Count)} hạng mục; tên dài nhất {projects.Max(p => p.Name.Length)} ký tự, hạng mục dài nhất {projects.SelectMany(p => p.Items).Max(i => i.Title.Length)} ký tự");
    Console.WriteLine($"Leader trong file: {string.Join(", ", projects.Select(p => p.Leader).Where(x => x != null).Distinct())}");
    Console.WriteLine($"Số hạng mục theo trạng thái: {string.Join(", ", projects.SelectMany(p => p.Items).GroupBy(i => i.Status).Select(g => g.Key + "=" + g.Count()))}");
    Console.WriteLine("Bất thường:"); foreach (var n in notes) Console.WriteLine("  - " + n);
}

void Verify()
{
    var cfg = File.Exists("QMDept/appsettings.Local.json") ? "QMDept/appsettings.Local.json" : "QMDept/appsettings.json";
    var cs = JsonDocument.Parse(File.ReadAllText(cfg)).RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
    using var c = new MySqlConnection(cs); c.Open();
    var bad = 0;
    void Bad(string m) { bad++; if (bad <= 40) Console.WriteLine("  LỆCH: " + m); }
    string? Str(object o) => o is DBNull ? null : Convert.ToString(o);
    DateOnly? Dd(object o) => o is DBNull ? null : DateOnly.FromDateTime(Convert.ToDateTime(o));
    int? In(object o) => o is DBNull ? null : Convert.ToInt32(o);

    var dbp = new List<(int Id, string Name, string? Code, string? Customer, string? Location, int? Qty, string? Leader, DateOnly? Po, DateOnly? Del, string? Level, string? Cat, bool Closed, string? Note, DateOnly? Start, DateOnly? End, int Order)>();
    using (var cmd = new MySqlCommand("SELECT p.Id, p.Name, p.Code, p.Factory, p.Location, p.Quantity, l.Name, p.PoDate, p.DeliveryDate, p.Level, p.Category, p.Closed, p.Note, p.StartDate, p.EndDate, p.SortOrder FROM projects p LEFT JOIN people l ON l.Id = p.OwnerLeaderId ORDER BY p.SortOrder", c))
    using (var r = cmd.ExecuteReader())
        while (r.Read()) dbp.Add((r.GetInt32(0), r.GetString(1), Str(r[2]), Str(r[3]), Str(r[4]), In(r[5]), Str(r[6]), Dd(r[7]), Dd(r[8]), Str(r[9]), Str(r[10]), r.GetBoolean(11), Str(r[12]), Dd(r[13]), Dd(r[14]), r.GetInt32(15)));
    Console.WriteLine($"File: {projects.Count} dự án / {projects.Sum(p => p.Items.Count)} hạng mục — DB: {dbp.Count} dự án");
    if (dbp.Count != projects.Count) Bad($"số dự án khác nhau (file {projects.Count}, DB {dbp.Count})");
    var itemsChecked = 0; var dbItemsTotal = 0;
    for (var k = 0; k < Math.Min(projects.Count, dbp.Count); k++)
    {
        var f = projects[k]; var d = dbp[k]; var tag = $"[{k + 1}] {f.Name}";
        if (f.Name != d.Name) Bad($"{tag}: tên DB '{d.Name}'");
        if (f.Code != d.Code) Bad($"{tag}: mã file '{f.Code}' DB '{d.Code}'");
        if (f.Customer != d.Customer) Bad($"{tag}: khách hàng file '{f.Customer}' DB '{d.Customer}'");
        if (f.Location != d.Location) Bad($"{tag}: tỉnh thành file '{f.Location}' DB '{d.Location}'");
        if (f.Qty != d.Qty) Bad($"{tag}: số lượng file {f.Qty} DB {d.Qty}");
        if (f.Leader != d.Leader) Bad($"{tag}: leader file '{f.Leader}' DB '{d.Leader}'");
        if (f.Po != d.Po) Bad($"{tag}: PO file {f.Po} DB {d.Po}");
        if (f.Delivery != d.Del) Bad($"{tag}: giao hàng file {f.Delivery} DB {d.Del}");
        if (f.Level != d.Level) Bad($"{tag}: cấp độ file '{f.Level}' DB '{d.Level}'");
        if (f.Category != d.Cat) Bad($"{tag}: loại file '{f.Category}' DB '{d.Cat}'");
        if (f.Closed != d.Closed) Bad($"{tag}: hoàn thành file {f.Closed} DB {d.Closed}");
        if (f.Note != d.Note) Bad($"{tag}: ghi chú file '{f.Note}' DB '{d.Note}'");
        var items = new List<(string Title, string? Pic, DateOnly? S, DateOnly? E, DateOnly? PS, DateOnly? PE, string St, string? Note)>();
        using (var cmd = new MySqlCommand("SELECT Title, Pic, StartDate, EndDate, PlanStart, PlanEnd, Status, Note FROM plan_items WHERE ProjectId = @p ORDER BY SortOrder", c))
        {
            cmd.Parameters.AddWithValue("@p", d.Id);
            using var r = cmd.ExecuteReader();
            while (r.Read()) items.Add((r.GetString(0), Str(r[1]), Dd(r[2]), Dd(r[3]), Dd(r[4]), Dd(r[5]), r.GetString(6), Str(r[7])));
        }
        dbItemsTotal += items.Count;
        if (items.Count != f.Items.Count) { Bad($"{tag}: số hạng mục file {f.Items.Count} DB {items.Count}"); continue; }
        for (var j = 0; j < items.Count; j++)
        {
            var a = f.Items[j]; var b = items[j]; itemsChecked++;
            if (a.Title != b.Title || a.Pic != b.Pic || a.Start != b.S || a.End != b.E || a.PlanStart != b.PS || a.PlanEnd != b.PE || a.Status != b.St || a.Note != b.Note)
                Bad($"{tag} / hạng mục {j + 1} «{a.Title}»: file ({a.Start}–{a.End}, KH {a.PlanStart}–{a.PlanEnd}, {a.Status}, PIC {a.Pic}) ≠ DB «{b.Title}» ({b.S}–{b.E}, KH {b.PS}–{b.PE}, {b.St}, PIC {b.Pic})");
        }
    }
    Console.WriteLine($"Đã so {itemsChecked} hạng mục (DB tổng {dbItemsTotal}). Số chỗ lệch: {bad}");
    Console.WriteLine(bad == 0 ? "KẾT QUẢ: KHỚP HOÀN TOÀN" : "KẾT QUẢ: CÓ LỆCH");
}

record Item(string Title, string? Pic, DateOnly? Start, DateOnly? End, DateOnly? PlanStart, DateOnly? PlanEnd, string Status, string? Note);
record Proj(string Name, string? Code, string? Customer, string? Location, int? Qty, string? Leader, DateOnly? Po, DateOnly? Delivery, string? Level, string Category, bool Closed, string? Note, List<Item> Items);
