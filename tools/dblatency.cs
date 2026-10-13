#:package MySql.Data@9.4.0
// Đo độ trễ DB: dotnet run tools/dblatency.cs (chạy ở thư mục gốc repo).
// Đo độ trễ: truy vấn trên một kết nối mở sẵn vs mở/đóng mỗi lần (pool). Chỉ đọc.
using System.Diagnostics;
using MySql.Data.MySqlClient;
using System.Text.Json;
var cfg = File.Exists("QMDept/appsettings.Local.json") ? "QMDept/appsettings.Local.json" : "QMDept/appsettings.json";
var json = JsonDocument.Parse(File.ReadAllText(cfg));
var cs = json.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString()!;
foreach (var extra in new[] { "", "ConnectionReset=false;", "SslMode=None;", "SslMode=None;ConnectionReset=false;" })
{
    var conn = cs.TrimEnd().TrimEnd(';') + ";" + extra;
    if (extra.Contains("SslMode")) conn = conn.Replace("SslMode=Preferred;", "");
    try
    {
        var sw = Stopwatch.StartNew();
        using (var c = new MySqlConnection(conn)) { c.Open(); Console.WriteLine($"[{extra}] first open {sw.ElapsedMilliseconds}ms");
            var t = new List<long>();
            for (int i = 0; i < 5; i++) { sw.Restart(); using var cmd = new MySqlCommand("SELECT 1", c); cmd.ExecuteScalar(); t.Add(sw.ElapsedMilliseconds); }
            Console.WriteLine($"  same connection SELECT 1: {string.Join(",", t)} ms"); }
        var t2 = new List<long>();
        for (int i = 0; i < 5; i++) { sw.Restart(); using var c = new MySqlConnection(conn); c.Open(); using var cmd = new MySqlCommand("SELECT 1", c); cmd.ExecuteScalar(); t2.Add(sw.ElapsedMilliseconds); }
        Console.WriteLine($"  pooled open+SELECT 1: {string.Join(",", t2)} ms");
    }
    catch (Exception ex) { Console.WriteLine($"[{extra}] ERR {ex.Message}"); }
}
