using System.Text;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Tổng hợp báo cáo cuối ngày của một nhân viên thành văn bản (yêu cầu 2026-10-20): gom theo dự án để nhân viên sao chép gửi các nhóm dự án trên Zalo
/// sau khi Leader duyệt. Dùng cho khối "Báo cáo tổng hợp" ở Views/Staff/Evening.cshtml (nút "Sao chép" — wwwroot/js/site.js [data-copy]).
/// </summary>
public static class EodText
{
    public sealed record Block(string Project, string Text);

    /// <summary>Danh sách khối theo dự án + phần đầu + phần "Tình hình chung".</summary>
    public sealed record Summary(string Header, List<Block> Projects, string Footer)
    {
        /// <summary>Văn bản của một dự án (kèm dòng tiêu đề) để dán vào nhóm Zalo dự án đó.</summary>
        public string ProjectText(Block b) => Header + "\n\n" + b.Text;
        /// <summary>Toàn bộ báo cáo (mọi dự án + tình hình chung).</summary>
        public string All() => Header + "\n\n" + string.Join("\n\n", Projects.Select(p => p.Text)) + (Footer.Length > 0 ? "\n\n" + Footer : "");
    }

    public static Summary Build(Dept d, DateOnly date, Person me, DayReport rep)
    {
        var own = d.Chosen(date, me.Id);
        var team = d.TeamTasksOf(me.Id, date);
        var extras = d.ExtrasOn(date).Where(x => x.EmployeeId == me.Id && x.Status != ExtraStatus.Rejected).ToList();
        var projectIds = own.Select(t => t.ProjectId).Concat(team.Select(t => t.ProjectId)).Concat(extras.Select(x => x.ProjectId)).Distinct()
            .OrderBy(id => d.PjName(id), Fmt.ViCompare).ToList();

        var blocks = new List<Block>();
        foreach (var pid in projectIds)
        {
            var sb = new StringBuilder("Dự án: " + d.PjName(pid));
            var n = 0;
            foreach (var t in own.Where(t => t.ProjectId == pid))
                AppendTask(sb, d, ++n, t, d.IsSupportTask(t) ? "hỗ trợ" : null);
            foreach (var t in team.Where(t => t.ProjectId == pid))
                AppendTask(sb, d, ++n, t, d.PName(t.EmployeeId));
            foreach (var x in extras.Where(x => x.ProjectId == pid))
                sb.Append($"\n{++n}. Phát sinh: {x.Title}{(string.IsNullOrEmpty(x.Start) ? "" : $" ({x.Start}–{x.End})")}");
            blocks.Add(new Block(d.PjName(pid), sb.ToString()));
        }
        var footer = new StringBuilder("Vướng mắc: " + (rep.Blocked ? "Có" : "Không"));
        if (!string.IsNullOrWhiteSpace(rep.Note)) footer.Append("\nGhi chú: " + rep.Note.Trim());
        return new Summary($"BÁO CÁO CUỐI NGÀY {Fmt.Dmy(date)} - {me.Name}", blocks, footer.ToString());
    }

    static void AppendTask(StringBuilder sb, Dept d, int n, WorkTask t, string? who)
    {
        var status = who == "hỗ trợ" ? (t.Supported ? "Đã hỗ trợ" : "Chưa hỗ trợ") : Labels.Of(t.Status);
        sb.Append($"\n{n}. {(who is null || who == "hỗ trợ" ? "" : who + ": ")}{(who == "hỗ trợ" ? "Hỗ trợ - " : "")}{t.Title} ({t.Start}–{t.End}) - {status}");
        if (who != "hỗ trợ" && !string.IsNullOrWhiteSpace(t.Note)) sb.Append($"\n   {(t.Status == TaskState.Done ? "Kết quả" : "Lý do chưa xong")}: {t.Note.Trim()}");
        if (t.Status != TaskState.Done && d.Task(t.CarriedToId) is { } nx)
            sb.Append($"\n   Kế hoạch tiếp: {Fmt.Dmy(nx.Date)} {nx.Start}–{nx.End} - {nx.Title}");
    }
}
