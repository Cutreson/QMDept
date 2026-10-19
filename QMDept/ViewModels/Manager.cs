using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Html;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>/Manager/Overview (tpOverview): báo cáo ngày các Leader đã gửi trong ngày đang xem + Leader chưa gửi.</summary>
public sealed class ManagerOverviewVm
{
    /// <summary>Mục đang xem của Tổng quan (?s=): "projects" (Dự án) | "people" (Nhân sự); "Kế hoạch tháng" là trang /Staff/Month?as=all.</summary>
    public string Section { get; init; } = "projects";
    /// <summary>Báo cáo Leader của ngày đang xem (kèm Leader), theo thời điểm gửi.</summary>
    public List<LeaderReport> Reports { get; init; } = [];
    /// <summary>Leader (còn hoạt động) chưa gửi báo cáo ngày đang xem.</summary>
    public List<Person> NotSent { get; init; } = [];
}

/// <summary>/Manager/Approvals (tpApprove): Hỗ trợ nhân sự (kế hoạch cần TP duyệt) / Phát sinh (chỉ theo dõi).</summary>
public sealed class ManagerApprovalsVm
{
    /// <summary>"plan" | "extra" | "leader" (?mode=, sub tpAp của artifact; "leader" = kế hoạch / báo cáo của Leader).</summary>
    public required string Mode { get; init; }
    /// <summary>"pending" | "approved" | "rejected" | "all" (?f=, sub tpF của artifact).</summary>
    public required string Filter { get; init; }
    /// <summary>Số kế hoạch trong ngày đang chờ TP duyệt (số trên nút "Hỗ trợ nhân sự").</summary>
    public int PendingManager { get; init; }
    /// <summary>Chế độ plan: kế hoạch ngày cần TP duyệt (needTp) phù hợp bộ lọc.</summary>
    public List<DayPlan> Plans { get; init; } = [];
    /// <summary>Chế độ extra: phát sinh (mọi ngày, mới nhất trước) phù hợp bộ lọc.</summary>
    public List<Extra> Extras { get; init; } = [];

    /// <summary>Số kế hoạch (trạng thái làm việc) của Leader đang chờ TP duyệt trong ngày (số trên nút "Của Leader").</summary>
    public int PendingLeader { get; init; }
    /// <summary>Chế độ leader: trạng thái làm việc trong ngày của Leader (theo bộ lọc) + Leader chưa gửi.</summary>
    public List<DayPlan> LeaderPlans { get; init; } = [];
    /// <summary>Xin nghỉ của Leader: chờ TP duyệt (mục Chờ duyệt) / đã quyết định gần đây.</summary>
    public List<LeaveRequest> LeaderLeaves { get; init; } = [];
    public List<Person> LeaderNotSentPlan { get; init; } = [];

    /// <summary>Các lựa chọn của ô "Trạng thái:".</summary>
    public static readonly (string Value, string Label)[] Filters =
        [("pending", "Chờ duyệt"), ("approved", "Đã duyệt"), ("rejected", "Từ chối"), ("all", "Tất cả")];

    /// <summary>Bộ lọc kế hoạch theo planSt: pending → Chờ duyệt / Chờ TP duyệt; approved → Đã duyệt; rejected → Từ chối.</summary>
    public static bool PlanMatches(DayPlan p, string f)
    {
        var st = Dept.PlanSt(p);
        return f switch
        {
            "all" => true,
            "pending" => st is PlanState.PendingManager or PlanState.Pending,
            "approved" => st == PlanState.Approved,
            _ => st == PlanState.Rejected,
        };
    }
}

/// <summary>/Manager/Projects (tpProjects): chip dự án + chi tiết dự án.</summary>
public sealed class ManagerProjectsVm
{
    public List<Project> Projects { get; init; } = [];
    public int? Pid { get; init; }
    /// <summary>null = chưa có dự án nào.</summary>
    public ProjectDetailVm? Detail { get; init; }
}

/// <summary>/Manager/Reports (tpReports): Báo cáo ngày Leader gửi / Báo cáo tuần.</summary>
public sealed class ManagerReportsVm
{
    /// <summary>"day" | "week".</summary>
    public required string Tab { get; init; }
    /// <summary>Thứ 2 của tuần đang xem (tab tuần).</summary>
    public DateOnly Week { get; init; }
    public List<ManagerLeaderReportItem> Reports { get; init; } = [];
}

/// <summary>Một báo cáo Leader đã gửi + số liệu chụp lúc gửi (summary).</summary>
public sealed class ManagerLeaderReportItem
{
    public required LeaderReport Report { get; init; }
    public required LeaderReportSummary Summary { get; init; }
    /// <summary>Các khóa có trong JSON summary (để hiện "–" khi thiếu, như <c>s.working ?? '–'</c> của artifact).</summary>
    public required HashSet<string> Keys { get; init; }

    /// <summary>Giá trị KPI hoặc "–" nếu summary không có khóa đó.</summary>
    public string Val(string key, int value) => Keys.Contains(key) ? value.ToString() : "–";

    public static ManagerLeaderReportItem From(LeaderReport r)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(r.SummaryJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(r.SummaryJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        if (prop.Value.ValueKind != JsonValueKind.Null) keys.Add(prop.Name);
            }
            catch (JsonException) { }
        }
        return new ManagerLeaderReportItem { Report = r, Summary = LeaderReportSummary.FromJson(r.SummaryJson), Keys = keys };
    }
}

/// <summary>Chuỗi / HTML nhỏ dùng riêng cho các màn hình Trưởng phòng.</summary>
public static class ManagerText
{
    static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>
    /// Người gửi báo cáo Leader: r.By (tên tài khoản đã bấm gửi); nếu khác tên Leader của báo cáo (vd TP gửi thay)
    /// thì thêm "(nhóm {Leader})". Khác artifact: báo cáo khóa theo (ngày, Leader).
    /// </summary>
    public static string Sender(LeaderReport r)
    {
        var by = r.By?.Trim();
        var leader = r.Leader?.Name;
        if (string.IsNullOrEmpty(by)) return leader ?? "";
        return string.IsNullOrEmpty(leader) || leader == by ? by : $"{by} (nhóm {leader})";
    }

    /// <summary>
    /// Tên Leader của báo cáo (artifact: lr.by = myName() = tên Leader đang được xem ở giao diện Leader).
    /// Dùng cho câu "Leader X đã gửi báo cáo ngày…" để không gọi TP (gửi thay) là "Leader".
    /// </summary>
    public static string LeaderName(LeaderReport r) =>
        r.Leader?.Name is { Length: > 0 } n ? n : r.By?.Trim() ?? "";

    /// <summary>Tài khoản đã bấm gửi nếu khác Leader của báo cáo (vd TP gửi thay); null nếu chính Leader gửi.</summary>
    public static string? OtherSender(LeaderReport r)
    {
        var by = r.By?.Trim();
        return string.IsNullOrEmpty(by) || by == r.Leader?.Name ? null : by;
    }

    /// <summary>Dòng "Nhân sự ngày mai" của tpReports: "Dự án: đã bố trí/nhu cầu" + "thiếu n" màu đỏ, nối bằng " · ".</summary>
    public static IHtmlContent Tomorrow(IEnumerable<LeaderReportSummary.TomorrowRow> rows) =>
        new HtmlString(string.Join(" · ", rows.Select(x =>
            $"{E(x.Name)}: {x.Assigned}/{x.Need}{(x.Assigned < x.Need ? $" <b class=\"red\">thiếu {x.Need - x.Assigned}</b>" : "")}")));

    /// <summary>Số phần trăm cho conic-gradient (dấu chấm thập phân).</summary>
    public static string Pct(double v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
}
