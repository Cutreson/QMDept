using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Khối HTML dùng chung nhiều màn hình — port 1:1 các hàm render của artifact (taskRow, extraRow, projectCard, planTable,
/// planMeta, supCmt, stepList, weekChart, pjTable, donut, issueList, weekReport). Gọi trong Razor: <c>@D.TaskRow(t)</c>.
/// Khối chỉ hiển thị; nút thao tác do từng màn hình tự thêm.
/// </summary>
public static class Blocks
{
    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    static HtmlString H(string s) => new(s);
    static string Src(TaskSource s) => Labels.Of(s);

    /// <summary>taskRow(t). showEmployee: thêm tên NV ở dòng phụ (dùng ở Theo dõi → theo dự án). tail: HTML thêm vào cột giờ (vd nút xóa).
    /// Luôn có dòng "Leader dự án: …" (yêu cầu 2026-10-14) — hiện ở lịch của NV, lịch nhóm của Leader, Theo dõi, Báo cáo.</summary>
    /// <summary>Nhãn mức độ ưu tiên của công việc (Bình thường không hiện nhãn) — yêu cầu 2026-10-24.</summary>
    public static string PriorityTag(TaskPriority p) => p switch
    {
        TaskPriority.Urgent => " <span class=\"pill p-solid-bad\">Khẩn cấp</span>",
        TaskPriority.High => " <span class=\"pill p-warn\">Ưu tiên</span>",
        _ => "",
    };

    /// <summary>Render một partial thành chuỗi HTML (để nhét vào cột của TaskRow).</summary>
    public static async Task<string> PartialStr(this IHtmlHelper html, string name, object model)
    {
        using var w = new StringWriter();
        (await html.PartialAsync(name, model)).WriteTo(w, HtmlEncoder.Default);
        return w.ToString();
    }

    public static IHtmlContent TaskRow(this Dept d, WorkTask t, bool showEmployee = false, string? tail = null, bool leadOnly = false, string? actions = null)
    {
        var st = t.Status;
        var ck = st == TaskState.Done ? "on" : st == TaskState.InProgress ? "half" : st == TaskState.Late ? "bad" : "";
        var src = Src(t.Source);
        var lead = d.ProjectLead(t.ProjectId);
        // leadOnly (lịch nhóm của Leader): nhiều người cùng một việc → chỉ dòng của người được chọn làm Leader dự án mới hiện
        // "Leader dự án"; người hỗ trợ không hiện.
        var leadLine = d.PJ(t.ProjectId) is { IsCommon: true } ? "" : leadOnly && (lead is null || lead.Id != t.EmployeeId) ? ""
            : lead is null ? "<div class=\"s\">Leader dự án: chưa chọn</div>" : $"<div class=\"s\">Leader dự án: <b>{E(lead.Name)}</b></div>";
        // Trạng thái RIÊNG từng việc ở cuối dòng + lý do chưa xong (yêu cầu 2026-10-22)
        var res = QMDept.ViewModels.StaffCalendar.TaskResult(t, d.Scope.Today);
        // Số lần chuyển tiếp (delay) + giải thích trạng thái (yêu cầu 2026-10-24)
        var times = t.Source == TaskSource.CarryOver ? d.CarryTimes(t) : 0;
        if (t.Source == TaskSource.CarryOver) src = times >= 2 ? $"<b style=\"color:var(--bad)\">Chuyển tiếp lần {times}</b>" : $"Chuyển tiếp lần {Math.Max(times, 1)}";
        var stNote = "";
        if (t.CarriedToId is not null)
        {
            var nx = d.Task(t.CarriedToId);
            stNote = $"Chuyển tiếp lần {d.CarryTimes(t) + 1}" + (nx is { CarryOk: false } && !(d.P(nx.EmployeeId) is { Role: Role.Leader or Role.Manager or Role.Admin }) ? " · chờ Leader duyệt" : "");
        }
        else if (t.Status != TaskState.Done && t.Date < d.Scope.Today) stNote = "Chưa có kế hoạch chuyển";
        var noteLine = t.Status != TaskState.Done && !string.IsNullOrWhiteSpace(t.Note) ? $"<div class=\"s\">Ghi chú: {E(t.Note)}</div>" : "";
        // Việc do cấp quản lý giao: hiện người giao + trạng thái xác nhận (yêu cầu 2026-10-21)
        var ackLine = t.GivenById is null ? ""
            : t.AckAt is DateTime ak ? $"<div class=\"s\">Giao bởi {E(t.GivenBy)} · <b style=\"color:var(--ok)\">✓ Đã xác nhận {VnTime.Hm(ak)} {Fmt.Dm(VnTime.DateOf(ak))}</b></div>"
            : $"<div class=\"s\">Giao bởi {E(t.GivenBy)} · <b style=\"color:var(--warn,#b45309)\">Chờ xác nhận</b></div>";
        return H($"<div class=\"trow trow-st{(actions is null ? "" : " trow-act")}\"><span class=\"ck {ck}\" aria-hidden=\"true\">{(st == TaskState.Done ? "✓" : "")}</span><div><div class=\"t\">{E(t.Title)}{PriorityTag(t.Priority)}</div>"
            + $"<div class=\"s\">{(showEmployee ? E(d.PName(t.EmployeeId)) + " · " : "")}{E(d.PjName(t.ProjectId))}{(src.Length > 0 ? " · " + src : "")}</div>{leadLine}{ackLine}{noteLine}</div>"
            + $"<span class=\"tm\">{E(t.Start)} – {E(t.End)}{tail}</span><span class=\"st\"><span class=\"pill {res.Css}\">{E(res.Text)}</span>{(stNote.Length > 0 ? $"<span class=\"st-note{(t.CarriedToId is null ? " bad" : "")}\">{E(stNote)}</span>" : "")}</span>{(actions is null ? "" : $"<span class=\"act\">{actions}</span>")}</div>");
    }

    /// <summary>extraRow(x)</summary>
    public static IHtmlContent ExtraRow(this Dept d, Extra x)
    {
        var st = d.ExSt(x);
        var sb = new StringBuilder($"<div class=\"issue\"><span class=\"ic\" style=\"--ic:{Labels.Color(st)}\">!</span><div><div class=\"t\">{E(x.Title)}</div>");
        sb.Append($"<div class=\"m\">{E(d.PjName(x.ProjectId))} · {E(d.PName(x.EmployeeId))} · <span class=\"mono\">{E(x.Start ?? VnTime.Hm(x.CreatedAt))}{(x.End is null ? "" : " – " + E(x.End))}</span>{(x.Date != d.Scope.Date ? " · " + Fmt.Dm(x.Date) : "")}</div>");
        if (!string.IsNullOrEmpty(x.Description)) sb.Append($"<div class=\"m\">{E(x.Description)}</div>");
        if (!string.IsNullOrEmpty(x.Reason)) sb.Append($"<div class=\"m\" style=\"color:var(--bad)\">Lý do từ chối: {E(x.Reason)}</div>");
        sb.Append($"</div>{Labels.PillHtml(st)}</div>");
        return H(sb.ToString());
    }

    /// <summary>projectCard(p, mine): thẻ dự án (link). mine: nhãn vai trò của NV ("Leader dự án" / "Hỗ trợ dự án" / "Bạn đang tham gia") hoặc null.</summary>
    public static IHtmlContent ProjectCard(this Dept d, Project p, string href, string? mine = null)
    {
        var st = d.PjStatus(p.Id, d.Scope.Date);
        var place = Dept.SubLine(p);
        return H($"<a class=\"card pcard\" href=\"{E(href)}\">{Ui.Ptile(d.Tile(p.Id), p.Name).ToHtml()}"
            + $"<span style=\"min-width:0\"><span class=\"t\"><b>{E(p.Name)}</b>{Labels.PillHtml(st)}</span>"
            + $"<span class=\"note\" style=\"display:block\">{E(place)}{(mine is null ? "" : $" · <b style=\"color:var(--brand)\">{E(mine)}</b>")}</span>"
            + Ui.Bar(d.PjPct(p.Id), Labels.Color(st)).ToHtml() + $"<span class=\"note mono\">{Fmt.Dm(p.StartDate)} – {Fmt.Dm(p.EndDate)}</span></span>"
            + "<span class=\"chev\" aria-hidden=\"true\">›</span></a>");
    }

    /// <summary>supCmt(p): ô "Cần hỗ trợ · …".</summary>
    public static string SupCmt(DayPlan? p, string style = "")
    {
        var sc = Dept.SupScope(p);
        if (p is null || sc == SupportScope.None) return "";
        return $"<div class=\"cmt\"{(style.Length > 0 ? $" style=\"{style}\"" : "")}><b>Cần hỗ trợ · {Labels.Of(sc)}{(Dept.NeedsManager(p) ? " (TP duyệt)" : "")}:</b> {E(p.SupportText)}</div>";
    }


    /// <summary>Mô tả ngắn một bản ghi nghỉ trên lịch: "Nghỉ phép · Cả ngày" / "Nghỉ không lương · 10:00–12:00" / "Nghỉ" (kèm lý do nếu có).</summary>
    public static string LeaveBrief(Leave? l)
    {
        if (l is null) return "";
        var kind = l.LeaveType == LeaveType.Paid ? "Nghỉ phép" + (l.LeaveSession is LeaveSession s ? " · " + LeaveRules.Session(s).Label : "")
            : l.LeaveType == LeaveType.Unpaid ? $"Nghỉ không lương · {l.LeaveFrom}–{l.LeaveTo}" : "Nghỉ";
        return string.IsNullOrEmpty(l.Reason) ? kind : $"{kind} — {l.Reason}";
    }
    /// <summary>leaveText(l): "Buổi sáng 08:00–12:00 · 0,5 ngày phép".</summary>
    public static string LeaveText(DayPlan p) =>
        $"{(p.LeaveType == LeaveType.Paid && p.LeaveSession is LeaveSession s ? LeaveRules.Session(s).Label + " " : "")}<b class=\"mono\">{E(p.LeaveFrom)}–{E(p.LeaveTo)}</b> · {LeaveRules.Qty(p)}";

    /// <summary>planMeta(p): trạng thái làm việc, nghỉ, công tác, báo cơm + supCmt.</summary>
    public static IHtmlContent PlanMeta(DayPlan p)
    {
        var a = new List<string> { $"<span class=\"pill {(LeaveRules.IsLeave(p.Status) ? "p-warn" : "p-info")}\">{Labels.Of(p.Status)}</span>" };
        if (LeaveRules.IsLeave(p.Status) && p.LeaveType is not null) a.Add($"<span>{LeaveText(p)}</span>");
        if (LeaveRules.IsTrip(p.Status) && (p.TripProvince is not null || p.TripCustomer is not null)) a.Add($"<span>Công tác: <b>{E(p.TripProvince ?? "—")}</b> · {E(p.TripCustomer ?? "—")}</span>");
        a.Add($"<span>Báo cơm: <b>{Labels.Of(p.Meal)}</b></span>");
        return H($"<div class=\"pmeta\">{string.Concat(a)}</div>{SupCmt(p)}");
    }

    /// <summary>planTable(p): bảng việc trong kế hoạch ngày.</summary>
    public static IHtmlContent PlanTable(this Dept d, DayPlan p)
    {
        var ts = d.PlanTasks(p);
        if (ts.Count == 0) return H("<p class=\"note\">Không có việc nào được chọn.</p>");
        var sb = new StringBuilder("<div class=\"tbl-wrap\"><table><thead><tr><th class=\"n\">STT</th><th>Dự án</th><th>Công việc</th><th class=\"n\">Thời gian</th></tr></thead><tbody>");
        for (int k = 0; k < ts.Count; k++)
        {
            var t = ts[k]; var src = Src(t.Source);
            sb.Append($"<tr><td class=\"n\">{k + 1}</td><td>{E(d.PjName(t.ProjectId))}</td><td>{E(t.Title)}{(src.Length > 0 ? $" <span class=\"pill p-info\">{src}</span>" : "")}</td><td class=\"n\" style=\"white-space:nowrap\">{E(t.Start)}–{E(t.End)}</td></tr>");
        }
        return H(sb.Append("</tbody></table></div>").ToString());
    }

    /// <summary>stepList(it): danh sách đầu mục theo giai đoạn.</summary>
    public static IHtmlContent StepList(this Dept d, List<PlanItem> it)
    {
        if (it.Count == 0) return H("<p class=\"note\">Chưa có đầu mục công việc.</p>");
        var today = d.Scope.Date;
        var sb = new StringBuilder("<ol class=\"steps\">");
        for (int k = 0; k < it.Count; k++)
        {
            var i = it[k]; var st = i.Status;
            var cls = st == TaskState.Done ? "done" : st == TaskState.InProgress ? "cur" : "";
            var late = st != TaskState.Done && i.EndDate is DateOnly e && e < today;
            sb.Append($"<li class=\"{cls}\"><span class=\"node\">{(st == TaskState.Done ? "✓" : "")}</span><div><div class=\"h\"><span class=\"title\"><span class=\"num\">{k + 1}.</span>{E(i.Title)}</span>{(late ? "<span class=\"pill p-bad\">Trễ</span>" : Labels.PillHtml(st))}</div>");
            var extra = new List<string>();
            if (!string.IsNullOrEmpty(i.Pic)) extra.Add("PIC: " + E(i.Pic));
            if (i.PlanStart is not null || i.PlanEnd is not null) extra.Add($"KH {Fmt.Dm(i.PlanStart)} – {Fmt.Dm(i.PlanEnd)}");
            sb.Append($"<div class=\"meta mono\">{Fmt.Dm(i.StartDate)} – {Fmt.Dm(i.EndDate)}{(i.DoneDate is DateOnly dd ? $" · xong {Fmt.Dm(dd)}{(string.IsNullOrEmpty(i.DoneBy) ? "" : " · " + E(i.DoneBy))}" : "")}{(extra.Count > 0 ? " · " + string.Join(" · ", extra) : "")}</div>");
            if (!string.IsNullOrEmpty(i.Note)) sb.Append($"<div class=\"meta\">{E(i.Note)}</div>");
            sb.Append("</div></li>");
        }
        return H(sb.Append("</ol>").ToString());
    }

    /// <summary>weekChart(pid): % đầu mục có hạn kết thúc trong tuần đã hoàn thành, theo từng tuần của dự án.</summary>
    public static IHtmlContent WeekChart(this Dept d, int pid)
    {
        var p = d.PJ(pid); var it = d.ItemsOf(pid);
        if (p?.StartDate is not DateOnly ps || p.EndDate is not DateOnly pe || it.Count == 0)
            return H("<p class=\"note\">Cần ngày bắt đầu/kết thúc và đầu mục để vẽ tiến độ.</p>");
        var today = d.Scope.Date;
        var data = new List<(string Lbl, string Sub, int? Pct, bool Cur, bool Past)>();
        int k = 0;
        for (var m = Fmt.Monday(ps); m <= pe; m = m.AddDays(7), k++)
        {
            var inW = it.Where(i => i.EndDate is DateOnly e && e >= m && e <= m.AddDays(6)).ToList();
            var dn = inW.Count(i => i.Status == TaskState.Done);
            data.Add(($"Tuần {k + 1}", $"{Fmt.Dmy(m < ps ? ps : m)}<br>{Fmt.Dmy(m.AddDays(6) > pe ? pe : m.AddDays(6))}",
                inW.Count > 0 ? (int)Math.Round(dn * 100.0 / inW.Count, MidpointRounding.AwayFromZero) : null,
                today >= m && today <= m.AddDays(6), m.AddDays(6) < today));
        }
        string Col((string, string, int? Pct, bool Cur, bool Past) x) => x.Pct is null ? "var(--plan)" : x.Pct == 100 ? "var(--ok)" : x.Past ? "var(--bad)" : x.Cur ? "var(--brand)" : "var(--warn)";
        var sb = new StringBuilder("<div class=\"legend\"><span><i style=\"background:var(--ok)\"></i>Xong 100%</span><span><i style=\"background:var(--brand)\"></i>Tuần này</span><span><i style=\"background:var(--bad)\"></i>Tuần đã qua chưa xong</span><span><i style=\"background:var(--warn)\"></i>Sắp tới</span></div>");
        sb.Append("<div class=\"cols\" role=\"img\" aria-label=\"Tỷ lệ đầu mục hoàn thành theo tuần\">");
        foreach (var x in data) sb.Append($"<div class=\"col\"><div class=\"c\" style=\"height:{x.Pct ?? 0}%;--cc:{Col(x)}\"><i>{(x.Pct is null ? "–" : x.Pct + "%")}</i></div></div>");
        sb.Append("</div><div class=\"col-lbl\">");
        foreach (var x in data) sb.Append($"<div><b style=\"color:var(--fg)\">{x.Lbl}</b><br><span class=\"mono\">{x.Sub}</span></div>");
        sb.Append("</div><p class=\"note\">% đầu mục có hạn kết thúc trong tuần đã hoàn thành.</p>");
        return H(sb.ToString());
    }

    /// <summary>pjTable(d): bảng tình hình dự án (tiến độ, nhân sự KH/TT, trạng thái).</summary>
    public static IHtmlContent PjTable(this Dept d, DateOnly date)
    {
        var ps = d.TableProjects();
        if (ps.Count == 0) return H($"<p class=\"note\">{(d.Scope.Role == Role.Leader ? "Nhóm chưa được giao dự án nào." : "Chưa có dự án.")}</p>");
        var sb = new StringBuilder("<div class=\"tbl-wrap\"><table><thead><tr><th>Dự án</th><th class=\"n\">Tiến độ</th><th class=\"n\">Nhân sự KH/TT</th><th>Trạng thái</th></tr></thead><tbody>");
        foreach (var p in ps)
        {
            var st = d.PjStatus(p.Id, date); var need = d.Need(date, p.Id); var onSite = d.OnSite(date, p.Id).Count;
            sb.Append($"<tr><td><b>{E(p.Name)}</b></td><td class=\"n\">{Ui.PctPill(d.PjPct(p.Id), st).ToHtml()}</td><td class=\"n {(onSite < need ? "red" : "")}\">{need} / {onSite}</td><td>{Labels.PillHtml(st)}</td></tr>");
        }
        sb.Append($"</tbody></table></div><p class=\"note\"><b>{d.PjCountText(ps, date)}</b>. KH = nhu cầu nhân sự, TT = số người có việc tại dự án trong ngày. Tiến độ = % đầu mục kế hoạch đã xong.</p>");
        return H(sb.ToString());
    }

    /// <summary>donut(c): vòng tròn tỷ lệ trạng thái công việc.</summary>
    public static IHtmlContent Donut(Counts c)
    {
        var total = c.Total;
        var seg = new[] { (TaskState.Done, "var(--ok)"), (TaskState.InProgress, "var(--warn)"), (TaskState.Late, "var(--bad)"), (TaskState.NotStarted, "var(--plan)") };
        string g = "var(--surface-2)";
        if (total > 0)
        {
            double acc = 0; var parts = new List<string>();
            foreach (var (k, col) in seg) { var a = acc; acc += c.Of(k) * 100.0 / total; parts.Add($"{col} {a.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}% {acc.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%"); }
            g = "conic-gradient(" + string.Join(",", parts) + ")";
        }
        return H($"<div class=\"donut-wrap\"><div class=\"donut\" style=\"--g:{g}\" role=\"img\" aria-label=\"{total} công việc\"><b>{total}</b><span>công việc</span></div>"
            + "<ul class=\"dl\">" + string.Concat(seg.Select(s => $"<li><i style=\"--c:{s.Item2}\"></i><b>{c.Of(s.Item1)}</b>{Labels.Of(s.Item1)}</li>")) + "</ul></div>");
    }

    /// <summary>issueList(list)</summary>
    public static IHtmlContent IssueList(IEnumerable<Issue> list)
    {
        var l = list.ToList();
        if (l.Count == 0) return H("<p class=\"note\">Không có vấn đề nổi bật.</p>");
        return H(string.Concat(l.Select(i => $"<div class=\"issue\"><span class=\"ic\" style=\"--ic:{i.Color}\">!</span><div><div class=\"t\">{E(i.Title)}</div>{(string.IsNullOrEmpty(i.Message) ? "" : $"<div class=\"m\">{E(i.Message)}</div>")}</div><span class=\"mono note\">{E(i.At)}</span></div>")));
    }

    /// <summary>weekReport(m): báo cáo tuần (theo ngày, đầu mục xong trong tuần, theo nhân viên). Cần EnsureAsync(m, m+6).</summary>
    public static IHtmlContent WeekReport(this Dept d, DateOnly m)
    {
        var days = Enumerable.Range(0, 7).Select(k => m.AddDays(k)).ToList();
        var sb = new StringBuilder("<section class=\"card sec\"><h2>Công việc theo ngày</h2><div class=\"tbl-wrap\"><table><thead><tr><th>Ngày</th><th class=\"n\">Việc</th><th class=\"n\">Xong</th><th class=\"n\">Chậm</th><th class=\"n\">Phát sinh</th><th class=\"n\">%</th></tr></thead><tbody>");
        foreach (var day in days)
        {
            var c = d.DayCounts(day); var x = d.ExtrasOn(day).Count;
            sb.Append($"<tr><td>{Fmt.Short(day)}</td><td class=\"n\">{c.Total}</td><td class=\"n\">{c.Done}</td><td class=\"n {(c.Late > 0 ? "red" : "")}\">{c.Late}</td><td class=\"n\">{x}</td><td class=\"n\">{(c.Total > 0 ? Math.Round(c.Done * 100.0 / c.Total, MidpointRounding.AwayFromZero) + "%" : "–")}</td></tr>");
        }
        sb.Append("</tbody></table></div></section>");
        sb.Append("<section class=\"card sec\"><h2>Đầu mục hoàn thành trong tuần</h2><div class=\"tbl-wrap\"><table><thead><tr><th>Dự án</th><th class=\"n\">Xong tuần này</th><th class=\"n\">Tiến độ</th><th>Trạng thái</th></tr></thead><tbody>");
        foreach (var p in d.ActivePj())
        {
            var n = d.ItemsOf(p.Id).Count(i => i.DoneDate is DateOnly dd && dd >= m && dd <= m.AddDays(6)); var st = d.PjStatus(p.Id, d.Scope.Date);
            sb.Append($"<tr><td>{E(p.Name)}</td><td class=\"n\">{n}</td><td class=\"n\">{Ui.PctPill(d.PjPct(p.Id), st).ToHtml()}</td><td>{Labels.PillHtml(st)}</td></tr>");
        }
        sb.Append("</tbody></table></div></section>");
        sb.Append("<section class=\"card sec\"><h2>Nhân viên</h2><div class=\"tbl-wrap\"><table><thead><tr><th>Nhân viên</th><th class=\"n\">Việc</th><th class=\"n\">Xong</th><th class=\"n\">Báo cáo</th></tr></thead><tbody>");
        foreach (var p in d.Staff())
        {
            var ts = days.SelectMany(day => d.Chosen(day, p.Id)).ToList();
            var r = days.Count(day => d.ReportOf(day, p.Id) is not null);
            sb.Append($"<tr><td>{E(p.Name)}</td><td class=\"n\">{ts.Count}</td><td class=\"n\">{ts.Count(t => t.Status == TaskState.Done)}</td><td class=\"n\">{r}</td></tr>");
        }
        sb.Append("</tbody></table></div><p class=\"note\">Chỉ tính các việc nhân viên đã chọn làm trong ngày.</p></section>");
        return H(sb.ToString());
    }

    /// <summary>IHtmlContent → string (để ghép vào chuỗi HTML khác).</summary>
    public static string ToHtml(this IHtmlContent c)
    {
        using var w = new StringWriter();
        c.WriteTo(w, System.Text.Encodings.Web.HtmlEncoder.Default);
        return w.ToString();
    }
}
