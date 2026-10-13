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
    /// <summary>lvPill(p): pill cấp bậc (Tech 1/2/3) hoặc rỗng.</summary>
    public static IHtmlContent LvPill(Person? p) => p?.Level is Level l ? Pill(l) : HtmlString.Empty;
    /// <summary>bar(pct, color)</summary>
    public static IHtmlContent Bar(int pct, string? color = null) => new HtmlString(
        $"<div class=\"barrow\"><div class=\"bar\"><span style=\"width:{Math.Clamp(pct, 0, 100)}%;--bc:{color ?? "var(--ok)"}\"></span></div><span class=\"mono\">{pct}%</span></div>");
    /// <summary>pctPill(pct, st)</summary>
    public static IHtmlContent PctPill(int pct, ProjectStatus st) => new HtmlString($"<span class=\"pill {Labels.Pill(st)} mono\">{pct}%</span>");
    /// <summary>Ảnh đại diện chữ cái đầu.</summary>
    public static IHtmlContent Av(string? name, bool tp = false, string? style = null) =>
        new HtmlString($"<span class=\"av{(tp ? " tp" : "")}\"{(style is null ? "" : $" style=\"{style}\"")} aria-hidden=\"true\">{E(Fmt.Initials(name))}</span>");
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
        if (n > 0) sb.Append($"<span class=\"step-n\">{n}</span>");
        sb.Append($"<div style=\"flex:1;min-width:0\"><h1>{E(title)}</h1>");
        if (!string.IsNullOrEmpty(sub)) sb.Append($"<div class=\"sub\">{E(sub)}</div>");
        sb.Append("</div>");
        if (extra is not null) { using var w = new StringWriter(); extra.WriteTo(w, System.Text.Encodings.Web.HtmlEncoder.Default); sb.Append(w); }
        sb.Append("</div>");
        return new HtmlString(sb.ToString());
    }

    /// <summary>tabs(): thanh tab con dạng gạch chân. Mỗi phần tử: (href, nhãn, đang chọn).</summary>
    public static IHtmlContent Subtabs(IEnumerable<(string Href, string Label, bool Active)> items) =>
        new HtmlString("<div class=\"subtabs\" role=\"tablist\">" + string.Concat(items.Select(i =>
            $"<a role=\"tab\" href=\"{E(i.Href)}\" aria-selected=\"{(i.Active ? "true" : "false")}\">{E(i.Label)}</a>")) + "</div>");

    /// <summary>seg(): nút phân đoạn; Count &gt; 0 hiện chấm đỏ đếm.</summary>
    public static IHtmlContent Seg(IEnumerable<(string Href, string Label, bool Active, int Count)> items) =>
        new HtmlString("<div class=\"seg\">" + string.Concat(items.Select(i =>
            $"<a href=\"{E(i.Href)}\" aria-pressed=\"{(i.Active ? "true" : "false")}\">{E(i.Label)}{(i.Count > 0 ? $"<span class=\"cnt\">{i.Count}</span>" : "")}</a>")) + "</div>");

    /// <summary>timeSel(id, val, end, blank): select giờ 00:00–24:00 bước 30 phút (end = bỏ 00:00, ngược lại bỏ 24:00).</summary>
    public static IHtmlContent TimeSelect(string name, string? value, bool end = false, string? blank = null, string? id = null, string? extraAttrs = null)
    {
        var sb = new StringBuilder($"<select class=\"in mono\" name=\"{E(name)}\" id=\"{E(id ?? name)}\"{(extraAttrs is null ? "" : " " + extraAttrs)}>");
        if (blank is not null) sb.Append($"<option value=\"\">{E(blank)}</option>");
        foreach (var x in end ? Fmt.EndTimes : Fmt.StartTimes)
            sb.Append($"<option value=\"{x}\"{(x == value ? " selected" : "")}>{x}</option>");
        return new HtmlString(sb.Append("</select>").ToString());
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

    /// <summary>Hidden input returnUrl = trang hiện tại (AppController.Back dùng để quay lại đúng trang/tham số).</summary>
    public static IHtmlContent ReturnField(HttpRequest r) => new HtmlString($"<input type=\"hidden\" name=\"returnUrl\" value=\"{E(Current(r))}\">");
}
