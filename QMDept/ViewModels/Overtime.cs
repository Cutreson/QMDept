using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.WebUtilities;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>Model trang Làm thêm (otScreen) — dùng chung 3 giao diện NV / Leader / TP (giao diện = Scope.Role).</summary>
public sealed class OvertimeVm
{
    /// <summary>Tab con: "mine" (Của tôi) | "approve" (Duyệt) | "summary" (Tổng hợp suất ăn). Giao diện NV luôn "mine".</summary>
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
    public MealOrder? Send { get; init; }
}

/// <summary>Helper riêng cho màn hình Làm thêm: URL POST, khóa giá trị form, hộp xem trước otLive.</summary>
public static class OvertimeUi
{
    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Thứ tự vai trò trong danh sách Tổng hợp suất ăn — giữ đúng thứ tự của artifact (sắp theo khóa vai trò
    /// 'leader' &lt; 'nv' &lt; 'tp'): Leader → Nhân viên → Trưởng phòng.</summary>
    public static int SummaryOrder(Role r) => r switch { Role.Leader => 0, Role.Manager => 2, _ => 1 };

    // ---------- khóa giá trị form: loại ngày "workday" | "holiday"; báo cơm "rice" | "snack" | "noodle" | "none" (overtime.js dùng cùng khóa) ----------
    public static string DayKey(OtDayType t) => t == OtDayType.Holiday ? "holiday" : "workday";
    public static string MealKey(OtMeal m) => m switch { OtMeal.Rice => "rice", OtMeal.Snack => "snack", OtMeal.Noodle => "noodle", _ => "none" };
    public static OtDayType? ParseDay(string? s) => s?.Trim().ToLowerInvariant() switch { "workday" => OtDayType.Workday, "holiday" => OtDayType.Holiday, _ => null };
    public static OtMeal? ParseMeal(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "rice" => OtMeal.Rice,
        "snack" => OtMeal.Snack,
        "noodle" => OtMeal.Noodle,
        "none" => OtMeal.None,
        _ => null,
    };
    /// <summary>Nhãn option báo cơm (otMealOpts).</summary>
    public static string MealOptLabel(OtMeal m) => m switch { OtMeal.Rice => "Cơm", OtMeal.Snack => "Ăn nhẹ", OtMeal.Noodle => "Bún", _ => "Không đăng ký" };

    // ---------- URL ----------
    /// <summary>URL POST /Overtime/{action}?d=…[&amp;as=…] — ScopeResolver đọc ngày và người đang xem từ query,
    /// nên luôn ghi rõ ngày đang xem (tránh lệch ngày khi gửi form qua nửa đêm).</summary>
    public static string PostUrl(HttpRequest req, string action, DateOnly d)
    {
        var q = new Dictionary<string, string?> { ["d"] = Fmt.Key(d) };
        // Luôn ghi rõ người đang xem: đổi combobox ở tab khác (cookie) không làm form này bị chặn / đổi người.
        var scope = req.HttpContext.RequestServices.GetRequiredService<ViewScope>();
        q["as"] = scope.AllStaff ? "all" : scope.IsViewingOther ? scope.MeId!.Value.ToString() : "me";
        return QueryHelpers.AddQueryString("/Overtime/" + action, q);
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

    // ---------- otLive (bản render phía server cho lần tải đầu; overtime.js cập nhật khi đổi giờ) ----------
    /// <summary>Nội dung hộp #otRule.</summary>
    public static IHtmlContent RuleBox(OtRules.Result r)
    {
        if (!r.Ok) return new HtmlString(E(r.Error));
        var sb = new StringBuilder();
        sb.Append($"<b>{E(r.Start)}–{E(r.End)}</b> · {Fmt.Num(r.Hours)} giờ{(r.Lunch ? " (đã trừ nghỉ trưa)" : "")} · {E(r.Tier)}<br>Nghỉ giải lao: {E(r.Break)}<br>Suất ăn: ");
        sb.Append(r.MealLevel == OtMeal.Rice ? "<b>cơm</b>" : r.MealLevel == OtMeal.Snack ? "ăn nhẹ, thực đơn cố định theo ngày" : "không có");
        if (r.Allow > 0) sb.Append("<br>Phụ cấp thêm <b>20.000đ</b>, trả vào lương");
        return new HtmlString(sb.ToString());
    }

    /// <summary>Nội dung #otMealNote.</summary>
    public static string MealNote(OtRules.Result r) => !r.Ok ? ""
        : r.MealLevel == OtMeal.Rice ? "Tăng ca từ 3 giờ (đến 20:00 trở đi): cơm."
        : r.MealLevel == OtMeal.Snack ? "Tăng ca 2 giờ (17:00–19:00): ăn nhẹ, thực đơn cố định theo ngày."
        : "Làm thêm dưới 2 giờ không có chế độ suất ăn.";

    /// <summary>Thứ tự trạng thái trong danh sách duyệt (otApprove: cho_duyet, da_duyet, tu_choi).</summary>
    public static int StatusOrder(OtStatus s) => s switch { OtStatus.Pending => 0, OtStatus.Approved => 1, _ => 2 };

    /// <summary>"Duyệt" / "Từ chối" bởi … lúc hh:mm.</summary>
    public static string DecidedText(Overtime x) => $"{(x.Status == OtStatus.Rejected ? "Từ chối" : "Duyệt")} bởi {x.DecidedBy ?? ""} lúc {VnTime.Hm(x.DecidedAt)}";
}
