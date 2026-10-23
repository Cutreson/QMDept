using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.WebUtilities;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>
/// Helper HTML nhỏ dùng trong Razor (port các hàm render của artifact: pill, bar, pctPill, lvPill, head, tabs, seg, timeSel…).
/// Mọi chuỗi người dùng nhập đều được HtmlEncode bên trong.
/// </summary>
public static class Ui
{
    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Đường path SVG của icon (IC).</summary>
    public static readonly Dictionary<string, string> Icons = new()
    {
        ["home"] = "<path d=\"M3 11l9-7 9 7v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z\"/>",
        ["week"] = "<rect x=\"3\" y=\"5\" width=\"18\" height=\"16\" rx=\"2\"/><path d=\"M3 10h18M8 3v4M16 3v4\"/>",
        ["sun"] = "<circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4\"/>",
        ["bolt"] = "<path d=\"M13 2L4 14h7l-1 8 9-12h-7z\"/>",
        ["flag"] = "<path d=\"M5 21V4M5 4h11l-2 4 2 4H5\"/>",
        ["check"] = "<path d=\"M9 11l3 3 8-8\"/><path d=\"M20 12v7a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h9\"/>",
        ["eye"] = "<path d=\"M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        ["send"] = "<path d=\"M22 2L11 13M22 2l-7 20-4-9-9-4z\"/>",
        ["grid"] = "<rect x=\"3\" y=\"3\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"3\" y=\"14\" width=\"7\" height=\"7\" rx=\"1\"/><rect x=\"14\" y=\"14\" width=\"7\" height=\"7\" rx=\"1\"/>",
        ["folder"] = "<path d=\"M3 6a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z\"/>",
        ["doc"] = "<path d=\"M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z\"/><path d=\"M14 3v6h6M8 13h8M8 17h6\"/>",
        ["clock"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3 2\"/>",
        ["plus"] = "<rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"4\"/><path d=\"M12 8v8M8 12h8\"/>",
    };

    public static IHtmlContent Icon(string name) =>
        new HtmlString($"<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\">{Icons.GetValueOrDefault(name, "")}</svg>");

    /// <summary>pill(map, k)</summary>
    public static IHtmlContent Pill(Enum e) => new HtmlString(Labels.PillHtml(e));
    public static IHtmlContent Pill(string text, string cls) => new HtmlString($"<span class=\"pill {cls}\">{E(text)}</span>");
    /// <summary>lvPill(p): pill cấp bậc (Tech 1/2/3); Leader nhóm hiện pill "Leader" thay cho cấp bậc (leader = true; yêu cầu 2026-10-16); không có thì rỗng.</summary>
    public static IHtmlContent LevelPill(Person? p, bool leader = true) =>
        p is null ? HtmlString.Empty
        : p.Role == Role.Leader ? (leader ? Pill("Leader", "p-mgr") : HtmlString.Empty)
        : p.Level is Level l ? Pill(l) : HtmlString.Empty;
    /// <summary>bar(pct, color)</summary>
    public static IHtmlContent Bar(int pct, string? color = null) => new HtmlString(
        $"<div class=\"barrow\"><div class=\"bar\"><span style=\"width:{Math.Clamp(pct, 0, 100)}%;--bc:{color ?? "var(--ok)"}\"></span></div><span class=\"mono\">{pct}%</span></div>");
    /// <summary>pctPill(pct, st)</summary>
    public static IHtmlContent PctPill(int pct, ProjectStatus st) => new HtmlString($"<span class=\"pill {Labels.Pill(st)} mono\">{pct}%</span>");
    /// <summary>Ảnh đại diện chữ cái đầu (manager = màu Trưởng phòng).</summary>
    public static IHtmlContent Av(string? name, bool manager = false, string? style = null) =>
        new HtmlString($"<span class=\"av{(manager ? " mgr" : "")}\"{(style is null ? "" : $" style=\"{style}\"")} aria-hidden=\"true\">{E(Fmt.Initials(name))}</span>");
    /// <summary>Ô màu dự án (ptile). size = cạnh px; null = cỡ mặc định 54px.</summary>
    public static IHtmlContent Ptile(string color, string name, int? size = null)
    {
        var st = size is int s ? $";width:{s}px;height:{s}px;font-size:{Math.Max(9, s / 3)}px;border-radius:{Math.Max(6, s / 4)}px" : "";
        return new HtmlString($"<span class=\"ptile\" style=\"--tc:{color}{st}\">{E(Fmt.Initials(name))}</span>");
    }

    /// <summary>head(n, title, sub): tiêu đề màn hình, n = số bước (0 = không hiện).</summary>
    public static IHtmlContent Head(int n, string title, string? sub = null, IHtmlContent? extra = null)
    {
        var sb = new StringBuilder("<div class=\"head\">");
        // Số thứ tự tròn trước tiêu đề đã bỏ (yêu cầu 2026-10-21) — tham số n giữ để các nơi gọi không phải đổi.
        sb.Append($"<div style=\"flex:1;min-width:0\"><h1>{E(title)}</h1>");
        if (!string.IsNullOrEmpty(sub)) sb.Append($"<div class=\"sub\">{E(sub)}</div>");
        sb.Append("</div>");
        if (extra is not null) { using var w = new StringWriter(); extra.WriteTo(w, System.Text.Encodings.Web.HtmlEncoder.Default); sb.Append(w); }
        sb.Append("</div>");
        return new HtmlString(sb.ToString());
    }

    /// <summary>tabs(): thanh tab con dạng gạch chân. Mỗi phần tử: (href, nhãn, đang chọn).</summary>
    /// <summary>
    /// Nhãn mục con có đuôi " (n)": n &gt; 0 thành chấm ĐỎ (trừ mục "Đã …" — danh sách đã xong, không phải việc phải làm), n = 0 giữ chữ thường (yêu cầu 2026-10-23).
    /// </summary>
    static string LabelWithCount(string label)
    {
        var m = System.Text.RegularExpressions.Regex.Match(label, @"^(.*?)\s*\((\d+)\)$");
        if (!m.Success) return E(label);
        var n = int.Parse(m.Groups[2].Value);
        var text = m.Groups[1].Value;
        if (n == 0 || text.StartsWith("Đã ", StringComparison.Ordinal)) return E(label);
        return E(text) + $"<span class=\"cnt\">{n}</span>";
    }

    public static IHtmlContent Subtabs(IEnumerable<(string Href, string Label, bool Active)> items) =>
        new HtmlString("<div class=\"subtabs\" role=\"tablist\">" + string.Concat(items.Select(i =>
            $"<a role=\"tab\" href=\"{E(i.Href)}\" aria-selected=\"{(i.Active ? "true" : "false")}\">{LabelWithCount(i.Label)}</a>")) + "</div>");

    /// <summary>seg(): nút phân đoạn; Count &gt; 0 hiện chấm đỏ đếm.</summary>
    public static IHtmlContent Seg(IEnumerable<(string Href, string Label, bool Active, int Count)> items) =>
        new HtmlString("<div class=\"seg\">" + string.Concat(items.Select(i =>
            $"<a href=\"{E(i.Href)}\" aria-pressed=\"{(i.Active ? "true" : "false")}\">{LabelWithCount(i.Label)}{(i.Count > 0 ? $"<span class=\"cnt\">{i.Count}</span>" : "")}</a>")) + "</div>");

    /// <summary>timeSel(id, val, end, blank): select giờ 00:00–24:00 bước 30 phút (end = bỏ 00:00, ngược lại bỏ 24:00).</summary>
    public static IHtmlContent TimeSelect(string name, string? value, bool end = false, string? blank = null, string? id = null, string? extraAttrs = null)
    {
        var sb = new StringBuilder($"<select class=\"in mono\" name=\"{E(name)}\" id=\"{E(id ?? name)}\"{(extraAttrs is null ? "" : " " + extraAttrs)}>");
        if (blank is not null) sb.Append($"<option value=\"\">{E(blank)}</option>");
        foreach (var x in end ? Fmt.EndTimes : Fmt.StartTimes)
            sb.Append($"<option value=\"{x}\"{(x == value ? " selected" : "")}>{x}</option>");
        return new HtmlString(sb.Append("</select>").ToString());
    }

    /// <summary>Tùy chọn ô chọn dự án: "Mã · Tên" (có mã để phân biệt dự án trùng tên). Dùng với D.AllPj() — dự án đã hoàn thành cũng phải chọn / lọc được (yêu cầu 2026-10-21).</summary>
    public static IEnumerable<(string Value, string Label)> PjOptions(IEnumerable<Project> ps) =>
        ps.Select(p => (p.Id.ToString(), string.IsNullOrEmpty(p.Code) ? p.Name : p.Code + " · " + p.Name));

    /// <summary>Dòng "Leader dự án: …" dưới ô chọn dự án (id = <paramref name="forId"/>): site.js cập nhật theo dự án đang chọn (yêu cầu 2026-10-16).</summary>
    public static IHtmlContent ProjectLeadHint(Dept d, IEnumerable<Project> pjs, string forId, int? current)
    {
        var map = pjs.ToDictionary(p => p.Id.ToString(), p => d.ProjectLead(p.Id)?.Name ?? "");
        var json = System.Text.Json.JsonSerializer.Serialize(map, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var name = current is int c && map.TryGetValue(c.ToString(), out var n) ? n : "";
        return new HtmlString($"<div class=\"pj-lead\" data-pj-lead-for=\"{E(forId)}\" data-map=\"{E(json)}\">{LeadText(name)}</div>");
    }
    static string LeadText(string name) => name.Length == 0 ? "Chưa có Leader dự án" : $"Leader dự án: <b>{E(name)}</b>";

    /// <summary>Ô nhập ngày dd/MM/yyyy (chữ, tự chèn dấu /) + nút lịch; luôn hiển thị theo định dạng này, không phụ thuộc ngôn ngữ trình duyệt (site.js: input[data-date]).</summary>
    public static IHtmlContent DateInput(string name, DateOnly? value, string? id = null)
    {
        var i = E(id ?? name);
        return new HtmlString($"<span class=\"dateinp\"><input class=\"in mono\" type=\"text\" inputmode=\"numeric\" maxlength=\"10\" autocomplete=\"off\" data-date name=\"{E(name)}\" id=\"{i}\" value=\"{E(Fmt.Dmy(value))}\">"
            + $"<button type=\"button\" class=\"dp\" data-date-pick aria-label=\"Chọn ngày\">📅</button><input type=\"date\" class=\"dp-native\" tabindex=\"-1\" aria-hidden=\"true\"></span>");
    }

    /// <summary>select từ danh sách (value, label); placeholder != null → thêm option rỗng disabled (selectField).</summary>
    public static IHtmlContent Select(string name, IEnumerable<(string Value, string Label)> opts, string? current, string? placeholder = null, string? id = null, string? cls = "in", string? extraAttrs = null)
    {
        var list = opts.ToList();
        bool has = current is not null && list.Any(o => o.Value == current);
        var sb = new StringBuilder($"<select class=\"{cls}{(placeholder is not null && !has ? " ph" : "")}\" name=\"{E(name)}\" id=\"{E(id ?? name)}\"{(extraAttrs is null ? "" : " " + extraAttrs)}>");
        if (placeholder is not null) sb.Append($"<option value=\"\" disabled{(has ? "" : " selected")}>{E(placeholder)}</option>");
        foreach (var (v, l) in list) sb.Append($"<option value=\"{E(v)}\"{(v == current ? " selected" : "")}>{E(l)}</option>");
        return new HtmlString(sb.Append("</select>").ToString());
    }

    /// <summary>Các option của một enum với nhãn tiếng Việt.</summary>
    public static IEnumerable<(string Value, string Label)> EnumOpts<T>(IEnumerable<T>? only = null) where T : struct, Enum =>
        (only ?? Enum.GetValues<T>()).Select(v => (v.ToString(), Labels.Of(v)));

    // ======================= URL =======================

    /// <summary>URL trang hiện tại với một số tham số query được đặt / xóa (value null = xóa).</summary>
    public static string Here(HttpRequest r, params (string Key, string? Value)[] set) => Build(r.Path, r.Query.ToDictionary(k => k.Key, v => v.Value.ToString()), set);

    /// <summary>URL tới <paramref name="path"/>, giữ ?d= hiện tại (nếu có), đặt thêm tham số.</summary>
    public static string To(HttpRequest r, string path, params (string Key, string? Value)[] set)
    {
        var q = new Dictionary<string, string>();
        var qi = path.IndexOf('?');
        if (qi >= 0) { foreach (var kv in QueryHelpers.ParseQuery(path[qi..])) q[kv.Key] = kv.Value.ToString(); path = path[..qi]; }
        if (r.Query.TryGetValue("d", out var d) && !q.ContainsKey("d")) q["d"] = d.ToString();
        return Build(path, q, set);
    }

    static string Build(string path, Dictionary<string, string> q, (string Key, string? Value)[] set)
    {
        foreach (var (k, v) in set) { if (v is null) q.Remove(k); else q[k] = v; }
        return q.Count == 0 ? path : QueryHelpers.AddQueryString(path, q.Where(kv => kv.Value.Length > 0).ToDictionary(kv => kv.Key, kv => (string?)kv.Value));
    }

    /// <summary>Đường dẫn hiện tại (để đặt vào hidden returnUrl).</summary>
    public static string Current(HttpRequest r) => r.Path + r.QueryString;

    /// <summary>
    /// Hidden input returnUrl = trang hiện tại (AppController.Back dùng để quay lại đúng trang/tham số)
    /// + hidden "d" = ngày đang chọn (nếu trang có ?d=) để POST thao tác đúng ngày (ScopeResolver đọc field "d" của form).
    /// Vì vậy form POST KHÔNG được có field khác tên "d".
    /// </summary>
    public static IHtmlContent ReturnField(HttpRequest r)
    {
        var d = r.Query["d"].ToString();
        if (string.IsNullOrEmpty(d) && r.HasFormContentType) d = r.Form["d"].ToString();
        var dField = Fmt.ParseKey(d) is DateOnly dd ? $"<input type=\"hidden\" name=\"d\" value=\"{Fmt.Key(dd)}\">" : "";
        return new HtmlString($"<input type=\"hidden\" name=\"returnUrl\" value=\"{E(Current(r))}\">{dField}");
    }
}
