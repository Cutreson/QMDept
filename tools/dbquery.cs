#:package MySql.Data@9.4.0
// Chạy các câu SQL trong file (mỗi câu một dòng; dòng bắt đầu bằng -- là tiêu đề). Connection string lấy từ QMDept/appsettings.json.
// Dùng (file-based app .NET 10, tự tải MySql.Data): dotnet run tools/dbquery.cs <file.sql>
using MySql.Data.MySqlClient;
using System.Text.Json;
// Chạy ở thư mục gốc repo; appsettings.Local.json (nếu có) ưu tiên như trong Program.cs.
var cfg = File.Exists("QMDept/appsettings.Local.json") ? "QMDept/appsettings.Local.json" : "QMDept/appsettings.json";
var json = JsonDocument.Parse(File.ReadAllText(cfg));
var cs = json.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString();
using var c = new MySqlConnection(cs);
c.Open();
foreach (var line in File.ReadAllLines(args[0]))
{
    if (string.IsNullOrWhiteSpace(line)) continue;
    if (line.StartsWith("--")) { Console.WriteLine("== " + line[2..].Trim()); continue; }
    using var cmd = new MySqlCommand(line, c);
    using var r = cmd.ExecuteReader();
    do
    {
        while (r.Read()) { var vals = new object[r.FieldCount]; r.GetValues(vals); Console.WriteLine(string.Join(" | ", vals)); }
        if (r.RecordsAffected > 0) Console.WriteLine($"({r.RecordsAffected} rows affected)");
    } while (r.NextResult());
}
