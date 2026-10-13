using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Html;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

// Báo cáo đầu ngày (nvMorning) / cuối ngày (nvEvening) của Nhân viên — view-model, dữ liệu form và helper.
// Controller: Controllers/NvController.BaoCao.cs · View: Views/Nv/DauNgay.cshtml, CuoiNgay.cshtml · JS: wwwroot/js/nv.js.

/// <summary>Dữ liệu planForm gửi lên /Nv/GuiKeHoach (tên field = tên thuộc tính viết thường đầu).</summary>
public sealed class NvPlanInput
{
    /// <summary>Trạng thái làm việc (tên enum WorkStatus).</summary>
    public string? Wst { get; set; }
    /// <summary>Báo cơm (tên enum Meal).</summary>
    public string? Meal { get; set; }
    /// <summary>Nghỉ đã chọn trong hộp thoại (hidden): "Phep" | "Kl".</summary>
    public string? LeaveType { get; set; }
    public string? LeaveSession { get; set; }
    public string? LeaveFrom { get; set; }
    public string? LeaveTo { get; set; }
    /// <summary>Công tác: tỉnh / thành, khách hàng.</summary>
    public string? Prov { get; set; }
    public string? Cus { get; set; }
    /// <summary>Nhân sự hỗ trợ (tên enum SupportScope) + nội dung.</summary>
    public string? Sup { get; set; }
    public string? SupText { get; set; }
    public string? Note { get; set; }
    /// <summary>Các việc được tích (name="pick").</summary>
    public List<int> Pick { get; set; } = [];
}

/// <summary>Dữ liệu sheet "Cập nhật công việc" (openUpdate) gửi lên /Nv/CapNhatViec.</summary>
public sealed class NvUpdateInput
{
    public int TaskId { get; set; }
    /// <summary>Trạng thái mới (tên enum TaskState).</summary>
    public string? St { get; set; }
    /// <summary>Tích "Đầu mục … đã xong toàn bộ".</summary>
    public bool ItemDone { get; set; }
    public string? NextDate { get; set; }
    public string? NextStart { get; set; }
    public string? NextEnd { get; set; }
    public string? NextTitle { get; set; }
    public string? Note { get; set; }
    public List<IFormFile>? Photos { get; set; }
}

/// <summary>eodForm → /Nv/GuiBaoCao.</summary>
public sealed class NvEodInput
{
    /// <summary>"co" | "khong".</summary>
    public string? Blocked { get; set; }
    public string? Note { get; set; }
}

/// <summary>Giá trị hiển thị của planForm (lấy từ kế hoạch đã gửi, hoặc từ dữ liệu vừa gửi bị lỗi kiểm tra).</summary>
public sealed class NvMorningForm
{
    public WorkStatus? Wst { get; init; }
    public Meal? Meal { get; init; }
    public LeaveRules.Leave? Leave { get; init; }
    public string Prov { get; init; } = "";
    public string Cus { get; init; } = "";
    public SupportScope Sup { get; init; } = SupportScope.Khong;
    public string SupText { get; init; } = "";
    public string Note { get; init; } = "";
    /// <summary>null = tích theo mặc định (có kế hoạch: việc trong kế hoạch / đã chọn; chưa có: tất cả).</summary>
    public HashSet<int>? Picks { get; init; }

    /// <summary>lvNow: thời gian nghỉ khớp loại nghỉ của trạng thái đang chọn.</summary>
    public LeaveRules.Leave? LvNow => Wst is WorkStatus w && Leave is { } l && LeaveRules.TypeOf(w) == l.Type ? l : null;

    public static NvMorningForm FromPlan(DayPlan? p) => p is null ? new() : new()
    {
        Wst = p.Status, Meal = p.Meal, Leave = NvBaoCao.LeaveOf(p),
        Prov = p.TripProvince ?? "", Cus = p.TripCustomer ?? "",
        Sup = p.SupportScope, SupText = p.SupportText ?? "", Note = p.Note ?? "",
    };

    public static NvMorningForm FromInput(NvPlanInput i) => new()
    {
        Wst = NvBaoCao.ParseEnum<WorkStatus>(i.Wst), Meal = NvBaoCao.ParseEnum<Meal>(i.Meal),
        Leave = NvBaoCao.LeaveOf(i.LeaveType, i.LeaveSession, i.LeaveFrom, i.LeaveTo),
        Prov = i.Prov ?? "", Cus = i.Cus ?? "",
        Sup = NvBaoCao.ParseEnum<SupportScope>(i.Sup) ?? SupportScope.Khong, SupText = i.SupText ?? "", Note = i.Note ?? "",
        Picks = i.Pick.ToHashSet(),
    };
}

/// <summary>Model của Views/Nv/DauNgay.cshtml (nvMorning).</summary>
public sealed class NvMorningVm
{
    /// <summary>Nhân viên đang xem (null: nhóm chưa có nhân viên).</summary>
    public Person? Me { get; init; }
    public DayPlan? Plan { get; init; }
    /// <summary>Hiện form (chưa gửi, hoặc đang sửa ?edit=yyyy-MM-dd).</summary>
    public bool ShowForm { get; init; }
    /// <summary>Đang sửa kế hoạch đã gửi (S.editPlan).</summary>
    public bool Editing { get; init; }
    public NvMorningForm Form { get; init; } = new();
    /// <summary>Việc trong ngày, bỏ việc chuyển tiếp chưa được duyệt.</summary>
    public List<WorkTask> List { get; init; } = [];

    /// <summary>isOn(t): việc được tích sẵn.</summary>
    public bool IsOn(WorkTask t) => Form.Picks is { } p ? p.Contains(t.Id)
        : Plan is { } pl ? Dept.TaskIdsOf(pl).Contains(t.Id) || t.Selected : true;
}

/// <summary>Model của Views/Nv/CuoiNgay.cshtml (nvEvening).</summary>
public sealed class NvEveningVm
{
    public Person? Me { get; init; }
    /// <summary>Các việc đang mở "Chi tiết" (?open=1,2 — S.open).</summary>
    public HashSet<int> Open { get; init; } = [];
}

/// <summary>Helper của Báo cáo đầu ngày / cuối ngày.</summary>
public static class NvBaoCao
{
    /// <summary>Khóa TempData giữ dữ liệu planForm khi gửi bị lỗi kiểm tra.</summary>
    public const string DraftKey = "nvPlanDraft";

    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Đọc tên enum (chỉ nhận đúng tên, không nhận số).</summary>
    public static T? ParseEnum<T>(string? s) where T : struct, Enum =>
        !string.IsNullOrEmpty(s) && Enum.GetNames<T>().Contains(s) ? Enum.Parse<T>(s) : null;

    /// <summary>
    /// Kế hoạch đã được duyệt thì không sửa / gửi lại / thêm việc ngoài kế hoạch được nữa
    /// (artifact chỉ hiện nút "Sửa kế hoạch và gửi lại" khi planSt ≠ da_duyet).
    /// </summary>
    public static void RequireEditable(DayPlan? plan)
    {
        if (plan is not null && Dept.PlanSt(plan) == PlanState.DaDuyet)
            throw new BizException("Kế hoạch đã được duyệt, không sửa được nữa");
    }

    /// <summary>Dự án phải đang triển khai (select của artifact chỉ liệt kê activePj()).</summary>
    public static async Task RequireActiveProjectAsync(AppDbContext db, int projectId)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId && !p.Closed)) throw new BizException("Chọn dự án");
    }

    /// <summary>Cắt khoảng trắng; rỗng → null; quá dài → lỗi.</summary>
    public static string? Clip(string? s, int max)
    {
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (s.Length > max) throw new BizException($"Nội dung quá dài (tối đa {max} ký tự)");
        return s;
    }

    /// <summary>plan.leave</summary>
    public static LeaveRules.Leave? LeaveOf(DayPlan p) => p.LeaveType is LeaveType t
        ? new(t, p.LeaveSession, p.LeaveFrom ?? "", p.LeaveTo ?? "", p.LeaveHours ?? 0, p.LeaveDays)
        : null;

    /// <summary>Nghỉ từ các hidden input (không ném lỗi; không hợp lệ → null).</summary>
    public static LeaveRules.Leave? LeaveOf(string? type, string? session, string? from, string? to) => type switch
    {
        "Phep" => ParseEnum<LeaveSession>(session) is LeaveSession s ? LeaveRules.Phep(s) : null,
        "Kl" => LeaveRules.Kl(from, to).Value,
        _ => null,
    };

    /// <summary>Kiểm tra thời gian nghỉ khi gửi kế hoạch (readLeave + kiểm tra lv?.type === LT[wst]).</summary>
    public static LeaveRules.Leave ReadLeave(LeaveType lt, NvPlanInput i)
    {
        if (lt == LeaveType.Phep)
            return i.LeaveType == "Phep" && ParseEnum<LeaveSession>(i.LeaveSession) is LeaveSession s
                ? LeaveRules.Phep(s) : throw new BizException("Chọn thời gian nghỉ");
        if (i.LeaveType != "Kl" || string.IsNullOrEmpty(i.LeaveFrom) || string.IsNullOrEmpty(i.LeaveTo)) throw new BizException("Chọn thời gian nghỉ");
        var (v, err) = LeaveRules.Kl(i.LeaveFrom, i.LeaveTo);
        if (v is null) throw new BizException(err ?? "Chọn thời gian nghỉ");
        if (!LeaveRules.KlFrom.Contains(v.From) || !LeaveRules.KlTo.Contains(v.To)) throw new BizException("Chọn thời gian nghỉ");
        return v;
    }

    /// <summary>leaveQty(l)</summary>
    public static string LeaveQty(LeaveRules.Leave l) => l.Type == LeaveType.Phep
        ? $"{Fmt.Num(l.Days ?? 0)} ngày phép" : $"{Fmt.Num(l.Hours)} giờ không lương";

    /// <summary>leaveBox(wst, l). ro: ẩn nút (chế độ xem). Bản JS tương ứng trong wwwroot/js/nv.js.</summary>
    public static IHtmlContent LeaveBox(WorkStatus wst, LeaveRules.Leave? l, bool ro)
    {
        if (l is null)
            return new HtmlString("<div class=\"lv-box lv-miss\"><div><div class=\"lv-t\">Chưa chọn thời gian nghỉ</div><div class=\"note\">"
                + (wst == WorkStatus.NghiPhep ? "Nghỉ phép tối thiểu theo buổi" : "Nghỉ không lương tối thiểu 1 giờ") + "</div></div>"
                + (ro ? "" : "<button type=\"button\" class=\"btn btn-sm btn-primary\" data-lv-time>Chọn giờ nghỉ</button>") + "</div>");
        var title = l.Type == LeaveType.Phep ? "Nghỉ phép · " + (l.Session is LeaveSession s ? LeaveRules.Session(s).Label : "") : "Nghỉ không lương";
        return new HtmlString($"<div class=\"lv-box\"><div><div class=\"lv-t\">{title}</div><div class=\"note\"><b class=\"mono\">{E(l.From)}–{E(l.To)}</b> · {LeaveQty(l)}</div></div>"
            + (ro ? "" : "<button type=\"button\" class=\"btn btn-sm\" data-lv-time>Đổi</button>") + "</div>");
    }

    /// <summary>Ô quyết định duyệt (Leader / TP) ở "Trạng thái duyệt": pill + giờ, hoặc HTML chờ.</summary>
    public static IHtmlContent DecisionCell(Decision? dec, DateTime? at, string waitHtml) => new HtmlString(dec is Decision d
        ? $"{Labels.PillHtml(d)} <span class=\"mono note\">{VnTime.Hm(at)}</span>" : waitHtml);

    /// <summary>Ô tích trạng thái duyệt.</summary>
    public static IHtmlContent DecisionCk(Decision? dec) => new HtmlString(
        $"<span class=\"ck {(dec == Decision.Ok ? "on" : dec == Decision.No ? "bad" : "")}\">{(dec == Decision.Ok ? "✓" : "")}</span>");

    /// <summary>nextWDs(t.date, 14) (+ ngày chuyển hiện tại nếu không nằm trong danh sách).</summary>
    public static List<DateOnly> NextDateOptions(DateOnly taskDate, DateOnly? cur)
    {
        var nds = Fmt.NextDays(taskDate, 14).ToList();
        if (cur is DateOnly c && !nds.Contains(c)) nds.Insert(0, c);
        return nds;
    }

    /// <summary>"Chủ nhật, 18/10/2026 (ngày nghỉ)"</summary>
    public static string NextDateLabel(DateOnly x) => Fmt.DayLabel(x) + (x.DayOfWeek == DayOfWeek.Sunday ? " (ngày nghỉ)" : "");

    /// <summary>?open=1,2: các việc đang mở Chi tiết.</summary>
    public static HashSet<int> OpenIds(HttpRequest r) => r.Query["open"].ToString()
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => int.TryParse(x, out var v) ? v : 0).Where(v => v > 0).ToHashSet();

    /// <summary>URL bật/tắt Chi tiết của một việc.</summary>
    public static string ToggleOpen(HttpRequest r, HashSet<int> open, int id)
    {
        var s = open.ToHashSet();
        if (!s.Remove(id)) s.Add(id);
        return Ui.Here(r, ("open", s.Count == 0 ? null : string.Join(",", s.Order())));
    }

    // ---------- bản nháp planForm ----------

    sealed record Draft(string D, NvPlanInput I);

    // TempData nằm trong cookie: không escape tiếng Việt thành \uXXXX (gấp ~2–3 lần kích thước, dễ vượt giới hạn header).
    // Chuỗi này chỉ được đọc lại bằng JsonSerializer, không chèn vào HTML.
    static readonly JsonSerializerOptions DraftJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// JSON bản nháp, hoặc null nếu quá lớn cho cookie TempData (ký tự ngoài ASCII còn bị escape \uXXXX ở tầng TempData;
    /// cookie quá lớn làm mọi request sau bị từ chối 431). Khi đó bản nháp sessionStorage của nv.js vẫn giữ dữ liệu.
    /// </summary>
    public static string? WriteDraft(NvPlanInput i, DateOnly d)
    {
        var json = JsonSerializer.Serialize(new Draft(Fmt.Key(d), i), DraftJson);
        return json.Sum(c => c < 128 ? 1 : 6) <= 12000 ? json : null;
    }

    public static NvPlanInput? ReadDraft(string? json, DateOnly d)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<Draft>(json, DraftJson) is { } x && x.D == Fmt.Key(d) ? x.I : null; }
        catch (JsonException) { return null; }
    }

    /// <summary>Cấu hình cho wwwroot/js/nv.js (luật nghỉ lấy từ LeaveRules để JS và C# dùng chung một nguồn).</summary>
    public static string MorningCfg(DateOnly d, int me, bool clear) => JsonSerializer.Serialize(new
    {
        key = $"qm_mdraft_{Fmt.Key(d)}_{me}",
        day = Fmt.DayLabel(d),
        klFrom = LeaveRules.KlFrom,
        klTo = LeaveRules.KlTo,
        ls = Enum.GetValues<LeaveSession>().ToDictionary(s => s.ToString(), s => { var x = LeaveRules.Session(s); return new[] { x.Label, x.From, x.To }; }),
        clear,
    });
}
