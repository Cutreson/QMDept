using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.WebUtilities;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>Model trang Làm thêm (otScreen) — dùng chung 3 giao diện (?r=nv|leader|tp).</summary>
public sealed class LamThemVm
{
    /// <summary>Khóa giao diện trong URL: "nv" | "leader" | "tp".</summary>
    public required string R { get; init; }
    /// <summary>Tab con: "me" (Của tôi) | "duyet" | "th" (Tổng hợp suất ăn). Giao diện NV luôn "me".</summary>
    public required string Tab { get; init; }
    /// <summary>S.otEdit: đang sửa đăng ký đã có (?edit=&lt;ngày đang xem yyyy-MM-dd&gt;).</summary>
    public bool Edit { get; init; }
    /// <summary>Số đăng ký chờ mình duyệt (nhãn tab "Duyệt … (n)").</summary>
    public int Pending { get; init; }

    // ---- Của tôi (otMine) ----
    public Overtime? Mine { get; init; }
    /// <summary>Việc trong ngày chưa xong (hoặc đã tích trong đăng ký) để tích "Công việc làm tiếp".</summary>
    public List<WorkTask> Open { get; init; } = [];
    public HashSet<int> Picked { get; init; } = [];

    // ---- Duyệt (otApprove) ----
    public List<Overtime> Approve { get; init; } = [];

    // ---- Tổng hợp suất ăn (otSummary) ----
    public List<Overtime> Summary { get; init; } = [];
    public OtSend? Send { get; init; }
}

/// <summary>Helper riêng cho màn hình Làm thêm: khóa URL, khóa giá trị form (giống artifact), hộp xem trước otLive.</summary>
public static class LamThemUi
{
    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    // ---------- giao diện (?r=) ----------
    public static string RoleKey(Role r) => r switch { Role.Tp => "tp", Role.Leader => "leader", _ => "nv" };
    public static Role? ParseRole(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "nv" => Role.Nv,
        "leader" => Role.Leader,
        "tp" => Role.Tp,
        _ => null,
    };

    // ---------- khóa giá trị form = khóa của artifact ('lv'/'nghi', 'com'/'nhe'/'khong') để ot.js giữ nguyên bản gốc ----------
    public static string DayKey(OtDayType t) => t == OtDayType.Nghi ? "nghi" : "lv";
    public static string MealKey(OtMeal m) => m switch { OtMeal.Com => "com", OtMeal.Nhe => "nhe", OtMeal.Bun => "bun", _ => "khong" };
    public static OtDayType? ParseDay(string? s) => s?.Trim().ToLowerInvariant() switch { "lv" => OtDayType.Lv, "nghi" => OtDayType.Nghi, _ => null };
    public static OtMeal? ParseMeal(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "com" => OtMeal.Com,
        "nhe" => OtMeal.Nhe,
        "bun" => OtMeal.Bun,
        "khong" => OtMeal.Khong,
        _ => null,
    };
    /// <summary>Nhãn option báo cơm (otMealOpts).</summary>
    public static string MealOptLabel(OtMeal m) => m switch { OtMeal.Com => "Cơm", OtMeal.Nhe => "Ăn nhẹ", OtMeal.Bun => "Bún", _ => "Không đăng ký" };

    // ---------- URL ----------
    /// <summary>URL POST /LamThem/{action}?r=…&amp;d=…[&amp;as=…] — ScopeResolver đọc giao diện, ngày và người đang xem từ query,
    /// nên luôn ghi rõ ngày đang xem (tránh lệch ngày khi gửi form qua nửa đêm).</summary>
    public static string PostUrl(HttpRequest req, string action, string r, DateOnly d)
    {
        var q = new Dictionary<string, string?> { ["r"] = r, ["d"] = Fmt.Key(d) };
        if (req.Query.TryGetValue("as", out var v) && !string.IsNullOrEmpty(v.ToString())) q["as"] = v.ToString();
        return QueryHelpers.AddQueryString("/LamThem/" + action, q);
    }

    /// <summary>Bỏ một tham số query khỏi URL nội bộ (vd bỏ edit=1 sau khi lưu — S.otEdit=false).</summary>
    public static string WithoutParam(string url, string key)
    {
        var qi = url.IndexOf('?');
        if (qi < 0) return url;
        var path = url[..qi];
        var q = QueryHelpers.ParseQuery(url[qi..]).Where(kv => !string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            .Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString()));
        return QueryHelpers.AddQueryString(path, q);
    }

    // ---------- otDeadline ----------
    public static IHtmlContent Deadline(DateOnly d)
    {
        var (text, fg, bg) = OtRules.DeadlineState(d);
        return new HtmlString($"<div class=\"notice\" style=\"--nbg:{bg};--nfg:{fg}\"><span class=\"ic\">i</span><div><b>{E(text)}</b><div class=\"note\">Dự trù trước 14:00 {Fmt.DayLabel(Fmt.PrevWorkDay(d))} · Chính thức trước 14:00 {Fmt.DayLabel(d)}. Tăng ca từ 3 giờ bắt buộc dùng suất cơm.</div></div></div>");
    }

    // ---------- otLive (bản render phía server cho lần tải đầu; ot.js cập nhật khi đổi giờ) ----------
    /// <summary>Nội dung hộp #otRule.</summary>
    public static IHtmlContent RuleBox(OtRules.Result r)
    {
        if (!r.Ok) return new HtmlString(E(r.Error));
        var sb = new StringBuilder();
        sb.Append($"<b>{E(r.Start)}–{E(r.End)}</b> · {Fmt.Num(r.Hours)} giờ{(r.Lunch ? " (đã trừ nghỉ trưa)" : "")} · {E(r.Tier)}<br>Nghỉ giải lao: {E(r.Break)}<br>Suất ăn: ");
        sb.Append(r.MealLevel == OtMeal.Com ? "<b>cơm</b>" : r.MealLevel == OtMeal.Nhe ? "ăn nhẹ, thực đơn cố định theo ngày" : "không có");
        if (r.Allow > 0) sb.Append("<br>Phụ cấp thêm <b>20.000đ</b>, trả vào lương");
        return new HtmlString(sb.ToString());
    }

    /// <summary>Nội dung #otMealNote.</summary>
    public static string MealNote(OtRules.Result r) => !r.Ok ? ""
        : r.MealLevel == OtMeal.Com ? "Tăng ca từ 3 giờ (đến 20:00 trở đi): cơm."
        : r.MealLevel == OtMeal.Nhe ? "Tăng ca 2 giờ (17:00–19:00): ăn nhẹ, thực đơn cố định theo ngày."
        : "Làm thêm dưới 2 giờ không có chế độ suất ăn.";

    /// <summary>Thứ tự trạng thái trong danh sách duyệt (otApprove: cho_duyet, da_duyet, tu_choi).</summary>
    public static int StatusOrder(OtStatus s) => s switch { OtStatus.ChoDuyet => 0, OtStatus.DaDuyet => 1, _ => 2 };

    /// <summary>"Duyệt" / "Từ chối" bởi … lúc hh:mm.</summary>
    public static string DecidedText(Overtime x) => $"{(x.Status == OtStatus.TuChoi ? "Từ chối" : "Duyệt")} bởi {x.DecidedBy ?? ""} lúc {VnTime.Hm(x.DecidedAt)}";
}
