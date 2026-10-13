using System.Text.Json;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Ảnh chụp số liệu khi Leader gửi báo cáo ngày cho TP (ldRepForm → leaderReports.summary). Lưu JSON trong LeaderReport.SummaryJson.
/// Build cần Dept đã nạp ngày <c>date</c> và <c>date+1</c>, ở giao diện Leader (phạm vi = nhóm của Leader).
/// </summary>
public sealed class LeaderReportSummary
{
    /// <summary>Một dự án: % đầu mục xong, nhu cầu nhân sự (KH), số người có việc tại dự án (TT), trạng thái.
    /// JSON: {"name","pct","need","onSite","status"} (trước 2026-10-13 là "kh","tt","st" — dữ liệu cũ phải được migration đổi khóa).</summary>
    public sealed record ProjectRow(string Name, int Pct, int Need, int OnSite, ProjectStatus Status);
    public sealed record TomorrowRow(string Name, int Need, int Assigned);

    public int Staff { get; set; }
    public int Working { get; set; }
    public int Done { get; set; }
    public int Doing { get; set; }
    public int Late { get; set; }
    public List<ProjectRow> Projects { get; set; } = [];
    public List<string> Issues { get; set; } = [];
    public List<TomorrowRow> Tomorrow { get; set; } = [];

    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static LeaderReportSummary Build(Dept d, DateOnly date)
    {
        var k = d.Kpis(date);
        var tm = date.AddDays(1);
        return new LeaderReportSummary
        {
            Staff = k.Total, Working = k.Working, Done = k.C.Done, Doing = k.C.InProgress, Late = k.C.Late,
            Projects = d.ActivePj().Select(p => new ProjectRow(p.Name, d.PjPct(p.Id), d.Need(date, p.Id), d.OnSite(date, p.Id).Count, d.PjStatus(p.Id, date))).ToList(),
            Issues = d.IssuesOf(date).Select(i => i.Title + (string.IsNullOrEmpty(i.Message) ? "" : " — " + i.Message)).ToList(),
            Tomorrow = d.ActivePj().Select(p => new TomorrowRow(p.Name, d.Need(tm, p.Id), d.OnSite(tm, p.Id).Count)).ToList(),
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static LeaderReportSummary FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<LeaderReportSummary>(json, Json) ?? new(); } catch (JsonException) { return new(); }
    }
}
