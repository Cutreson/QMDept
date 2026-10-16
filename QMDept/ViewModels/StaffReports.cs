using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Html;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

// Báo cáo đầu ngày (nvMorning) / cuối ngày (nvEvening) của Nhân viên — view-model, dữ liệu form và helper.
// Controller: Controllers/StaffController.Reports.cs · View: Views/Staff/Morning.cshtml, Evening.cshtml · JS: wwwroot/js/staff.js.

/// <summary>Dữ liệu planForm gửi lên /Staff/SubmitPlan (tên field = tên thuộc tính viết thường đầu).</summary>
public sealed class StaffPlanInput
{
    /// <summary>Trạng thái làm việc (tên enum WorkStatus) — field "status".</summary>
    public string? Status { get; set; }
    /// <summary>Báo cơm (tên enum Meal).</summary>
    public string? Meal { get; set; }
    /// <summary>Nghỉ đã chọn trong hộp thoại (hidden): tên enum LeaveType "Paid" | "Unpaid".</summary>
    public string? LeaveType { get; set; }
    public string? LeaveSession { get; set; }
    public string? LeaveFrom { get; set; }
    public string? LeaveTo { get; set; }
    /// <summary>Công tác: tỉnh / thành, khách hàng.</summary>
    public string? Province { get; set; }
    public string? Customer { get; set; }
    /// <summary>Nhân sự hỗ trợ (tên enum SupportScope) + nội dung.</summary>
    public string? Support { get; set; }
    public string? SupportText { get; set; }
    /// <summary>Lựa chọn xin hỗ trợ (JSON do ô chọn người hỗ trợ dựng) — xem StaffSupportPick.</summary>
    public string? SupportPick { get; set; }
    public string? Note { get; set; }
    /// <summary>Các việc được tích (name="picks").</summary>
    public List<int> Picks { get; set; } = [];
}

/// <summary>Dữ liệu sheet "Cập nhật công việc" (openUpdate) gửi lên /Staff/UpdateTask.</summary>
public sealed class StaffUpdateInput
{
    public int TaskId { get; set; }
    /// <summary>Trạng thái mới (tên enum TaskState) — field "state".</summary>
    public string? State { get; set; }
    /// <summary>Tích "Đầu mục … đã xong toàn bộ".</summary>
    public bool ItemDone { get; set; }
    public string? NextDate { get; set; }
    public string? NextStart { get; set; }
    public string? NextEnd { get; set; }
    public string? NextTitle { get; set; }
    /// <summary>Giờ làm việc thực tế của hạng mục (NV chỉnh ở báo cáo cuối ngày) — field "start" / "end".</summary>
    public string? Start { get; set; }
    public string? End { get; set; }
    public string? Note { get; set; }
}

/// <summary>eodForm → /Staff/SubmitReport.</summary>
public sealed class StaffEveningInput
{
    /// <summary>Có vướng mắc: "yes" | "no".</summary>
    public string? Blocked { get; set; }
    public string? Note { get; set; }
}

/// <summary>Giá trị hiển thị của planForm (lấy từ kế hoạch đã gửi, hoặc từ dữ liệu vừa gửi bị lỗi kiểm tra).</summary>
public sealed class StaffMorningForm
{
    public WorkStatus? Status { get; init; }
    public Meal? Meal { get; init; }
    public LeaveRules.Leave? Leave { get; init; }
    public string Province { get; init; } = "";
    public string Customer { get; init; } = "";
    public SupportScope Support { get; init; } = SupportScope.None;
    public string SupportText { get; init; } = "";
    /// <summary>JSON lựa chọn xin hỗ trợ (dựng lại thẻ ở ô chọn).</summary>
    public string SupportPick { get; init; } = "";
    public string Note { get; init; } = "";
    /// <summary>null = tích theo mặc định (có kế hoạch: việc trong kế hoạch / đã chọn; chưa có: tất cả).</summary>
    public HashSet<int>? Picks { get; init; }

    /// <summary>lvNow: thời gian nghỉ khớp loại nghỉ của trạng thái đang chọn.</summary>
    public LeaveRules.Leave? LeaveNow => Status is WorkStatus w && Leave is { } l && LeaveRules.TypeOf(w) == l.Type ? l : null;

    public static StaffMorningForm FromPlan(DayPlan? p) => p is null ? new() : new()
    {
        Status = p.Status, Meal = p.Meal, Leave = StaffReports.LeaveOf(p),
        Province = p.TripProvince ?? "", Customer = p.TripCustomer ?? "",
        Support = p.SupportScope, SupportPick = p.SupportPick ?? "",
        // Ô "Lý do" hiện lý do gốc (trong SupportPick); kế hoạch cũ chưa có SupportPick thì hiện nguyên nội dung đã lưu.
        SupportText = StaffSupportPick.Parse(p.SupportPick)?.Reason ?? p.SupportText ?? "", Note = p.Note ?? "",
    };

    public static StaffMorningForm FromInput(StaffPlanInput i) => new()
    {
        Status = StaffReports.ParseEnum<WorkStatus>(i.Status), Meal = StaffReports.ParseEnum<Meal>(i.Meal),
        Leave = StaffReports.LeaveOf(i.LeaveType, i.LeaveSession, i.LeaveFrom, i.LeaveTo),
        Province = i.Province ?? "", Customer = i.Customer ?? "",
        Support = StaffReports.ParseEnum<SupportScope>(i.Support) ?? SupportScope.None, SupportText = i.SupportText ?? "", SupportPick = i.SupportPick ?? "", Note = i.Note ?? "",
        Picks = i.Picks.ToHashSet(),
    };
}

/// <summary>Model của Views/Staff/Morning.cshtml (nvMorning).</summary>
public sealed class StaffMorningVm
{
    /// <summary>Nhân viên đang xem (null: nhóm chưa có nhân viên).</summary>
    public Person? Me { get; init; }
    public DayPlan? Plan { get; init; }
    /// <summary>Hiện form (chưa gửi, hoặc đang sửa ?edit=yyyy-MM-dd).</summary>
    public bool ShowForm { get; init; }
    /// <summary>Đang sửa kế hoạch đã gửi (S.editPlan).</summary>
    public bool Editing { get; init; }
    public StaffMorningForm Form { get; init; } = new();
    /// <summary>Việc trong ngày, bỏ việc chuyển tiếp chưa được duyệt.</summary>
    public List<WorkTask> List { get; init; } = [];

    /// <summary>isOn(t): việc được tích sẵn.</summary>
    public bool IsOn(WorkTask t) => Form.Picks is { } p ? p.Contains(t.Id)
        : Plan is { } pl ? Dept.TaskIdsOf(pl).Contains(t.Id) || t.Selected || (t.Source == TaskSource.Unplanned && t.CreatedAt > pl.SentAt) : true;
}

/// <summary>Model của Views/Staff/Evening.cshtml (nvEvening).</summary>
public sealed class StaffEveningVm
{
    public Person? Me { get; init; }
    /// <summary>Các việc đang mở "Chi tiết" (?open=1,2 — S.open).</summary>
    public HashSet<int> Open { get; init; } = [];
}

/// <summary>Helper của Báo cáo đầu ngày / cuối ngày.</summary>
public static class StaffReports
{
    /// <summary>Khóa TempData giữ dữ liệu planForm khi gửi bị lỗi kiểm tra.</summary>
    public const string DraftKey = "staffPlanDraft";

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
        if (plan is not null && Dept.PlanSt(plan) == PlanState.Approved)
            throw new BizException("Kế hoạch đã được duyệt, không sửa được nữa");
    }

    /// <summary>Dự án phải đang triển khai (select của artifact chỉ liệt kê activePj()).</summary>
    public static async Task RequireActiveProjectAsync(AppDbContext db, int projectId)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId && !p.Closed)) throw new BizException("Chọn dự án");
    }

    /// <summary>Địa điểm + khách hàng công tác từ các dự án của việc đã chọn (không trùng, cắt cho vừa cột; rỗng → null).</summary>
    public static (string? Place, string? Customer) TripPlace(IEnumerable<Project> pjs)
    {
        static string? Join(IEnumerable<string?> xs, string sep, int max)
        {
            var s = string.Join(sep, xs.Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase));
            return s.Length == 0 ? null : s.Length > max ? s[..max] : s;
        }
        var l = pjs.ToList();
        return (Join(l.Select(p => p.Location), "; ", 100), Join(l.Select(p => p.Factory), ", ", 200));
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

    /// <summary>Nghỉ từ các hidden input (không ném lỗi; không hợp lệ → null). type = tên enum LeaveType.</summary>
    public static LeaveRules.Leave? LeaveOf(string? type, string? session, string? from, string? to) => ParseEnum<LeaveType>(type) switch
    {
        LeaveType.Paid => ParseEnum<LeaveSession>(session) is LeaveSession s ? LeaveRules.Paid(s) : null,
        LeaveType.Unpaid => LeaveRules.Unpaid(from, to).Value,
        _ => null,
    };

    /// <summary>Kiểm tra thời gian nghỉ khi gửi kế hoạch (readLeave + kiểm tra lv?.type === LT[wst]).</summary>
    public static LeaveRules.Leave ReadLeave(LeaveType lt, StaffPlanInput i)
    {
        var chosen = ParseEnum<LeaveType>(i.LeaveType);
        if (lt == LeaveType.Paid)
            return chosen == LeaveType.Paid && ParseEnum<LeaveSession>(i.LeaveSession) is LeaveSession s
                ? LeaveRules.Paid(s) : throw new BizException("Chọn thời gian nghỉ");
        if (chosen != LeaveType.Unpaid || string.IsNullOrEmpty(i.LeaveFrom) || string.IsNullOrEmpty(i.LeaveTo)) throw new BizException("Chọn thời gian nghỉ");
        var (v, err) = LeaveRules.Unpaid(i.LeaveFrom, i.LeaveTo);
        if (v is null) throw new BizException(err ?? "Chọn thời gian nghỉ");
        if (!LeaveRules.UnpaidFrom.Contains(v.From) || !LeaveRules.UnpaidTo.Contains(v.To)) throw new BizException("Chọn thời gian nghỉ");
        return v;
    }

    /// <summary>leaveQty(l)</summary>
    public static string LeaveQty(LeaveRules.Leave l) => l.Type == LeaveType.Paid
        ? $"{Fmt.Num(l.Days ?? 0)} ngày phép" : $"{Fmt.Num(l.Hours)} giờ không lương";

    /// <summary>leaveBox(status, l). ro: ẩn nút (chế độ xem). Bản JS tương ứng trong wwwroot/js/staff.js.</summary>
    public static IHtmlContent LeaveBox(WorkStatus status, LeaveRules.Leave? l, bool ro)
    {
        if (l is null)
            return new HtmlString("<div class=\"leave-box leave-miss\"><div><div class=\"leave-t\">Chưa chọn thời gian nghỉ</div><div class=\"note\">"
                + (status == WorkStatus.PaidLeave ? "Nghỉ phép tối thiểu theo buổi" : "Nghỉ không lương tối thiểu 1 giờ") + "</div></div>"
                + (ro ? "" : "<button type=\"button\" class=\"btn btn-sm btn-primary\" data-leave-time>Chọn giờ nghỉ</button>") + "</div>");
        var title = l.Type == LeaveType.Paid ? "Nghỉ phép · " + (l.Session is LeaveSession s ? LeaveRules.Session(s).Label : "") : "Nghỉ không lương";
        return new HtmlString($"<div class=\"leave-box\"><div><div class=\"leave-t\">{title}</div><div class=\"note\"><b class=\"mono\">{E(l.From)}–{E(l.To)}</b> · {LeaveQty(l)}</div></div>"
            + (ro ? "" : "<button type=\"button\" class=\"btn btn-sm\" data-leave-time>Đổi</button>") + "</div>");
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

    sealed record Draft(string D, StaffPlanInput I);

    // TempData nằm trong cookie: không escape tiếng Việt thành \uXXXX (gấp ~2–3 lần kích thước, dễ vượt giới hạn header).
    // Chuỗi này chỉ được đọc lại bằng JsonSerializer, không chèn vào HTML.
    static readonly JsonSerializerOptions DraftJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// JSON bản nháp, hoặc null nếu quá lớn cho cookie TempData (ký tự ngoài ASCII còn bị escape \uXXXX ở tầng TempData;
    /// cookie quá lớn làm mọi request sau bị từ chối 431). Khi đó bản nháp sessionStorage của staff.js vẫn giữ dữ liệu.
    /// </summary>
    public static string? WriteDraft(StaffPlanInput i, DateOnly d)
    {
        var json = JsonSerializer.Serialize(new Draft(Fmt.Key(d), i), DraftJson);
        return json.Sum(c => c < 128 ? 1 : 6) <= 12000 ? json : null;
    }

    public static StaffPlanInput? ReadDraft(string? json, DateOnly d)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<Draft>(json, DraftJson) is { } x && x.D == Fmt.Key(d) ? x.I : null; }
        catch (JsonException) { return null; }
    }

    /// <summary>Cấu hình cho wwwroot/js/staff.js (luật nghỉ lấy từ LeaveRules để JS và C# dùng chung một nguồn).</summary>
    public static string MorningCfg(DateOnly d, int me, bool clear) => JsonSerializer.Serialize(new
    {
        key = $"qm_mdraft_{Fmt.Key(d)}_{me}",
        day = Fmt.DayLabel(d),
        unpaidFrom = LeaveRules.UnpaidFrom,
        unpaidTo = LeaveRules.UnpaidTo,
        sessions = Enum.GetValues<LeaveSession>().ToDictionary(s => s.ToString(), s => { var x = LeaveRules.Session(s); return new[] { x.Label, x.From, x.To }; }),
        clear,
    });
}

/// <summary>
/// Lựa chọn xin hỗ trợ ở báo cáo đầu ngày (yêu cầu 2026-10-15) — cùng thiết kế với đề xuất công việc ở lịch tháng:
/// người trong NHÓM MÌNH (Emp), phòng ban theo số lượng (Qd: chỉ số trong Fmt.SupportDepartments), nhóm khác theo số lượng (Qg: Id Leader), kèm lý do.
/// Lưu JSON ở day_plans.SupportPick; SupportScope / SupportText do máy chủ suy ra (Team / OtherTeam / OtherDept).
/// </summary>
public sealed class StaffSupportPick
{
    public List<int> Emp { get; set; } = [];
    public Dictionary<string, int> Qd { get; set; } = [];
    public Dictionary<string, int> Qg { get; set; } = [];
    public string? Reason { get; set; }

    static readonly System.Text.Json.JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    public static StaffSupportPick? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<StaffSupportPick>(json, Opts); } catch { return null; }
    }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, Opts);
}
