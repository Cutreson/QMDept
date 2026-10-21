using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>Định nghĩa một tab ở thanh dưới (TABS của artifact).</summary>
public sealed record TabDef(string Id, string Label, string Icon, string Path, string Controller, string? Action);

public sealed record TabLink(string Id, string Label, string Icon, string Href, bool Active, int Badge);

/// <summary>Một lựa chọn trong combobox "Xem màn hình" (Group null = không thuộc nhóm).</summary>
public sealed record ViewOption(string Value, string Label, bool Selected, string? Group);

public sealed class ChromeModel
{
    public required Person Account { get; init; }
    public required string AccountTeam { get; init; }
    public required string Accent { get; init; }
    public List<TabLink> Tabs { get; init; } = [];
    /// <summary>Combobox "Xem màn hình" của cấp trên (rỗng với NV).</summary>
    public List<ViewOption> ViewOptions { get; init; } = [];
    /// <summary>Đang xem màn hình cấp dưới: tên + mô tả người đó (cho dải thông báo "chỉ xem").</summary>
    public string? ViewingName { get; init; }
    public string? ViewingInfo { get; init; }
    /// <summary>Thông báo đẩy đang bật cho tài khoản (site.js kết nối SignalR khi true).</summary>
    public bool NotifyEnabled { get; init; } = true;
    /// <summary>Link "Về màn hình của tôi".</summary>
    public string HomeHref { get; init; } = "/";
    /// <summary>Nút "Lịch của tôi" (lịch tháng riêng của Leader, ?as=mine) — chỉ Leader.</summary>
    public string? OwnHref { get; init; }
    public bool OwnActive { get; init; }
    public string OwnLabel { get; init; } = "Kế hoạch";
    /// <summary>Leader: ô "Xem màn hình" nhân viên nằm trong trang Kế hoạch (lịch tháng), không nằm ở thanh trên (yêu cầu 2026-10-16).</summary>
    public bool ViewInPlan { get; init; }
    /// <summary>Nút "Lịch nhóm" cạnh combobox (lịch tháng cả nhóm, ?as=all) — null với NV / khi không có NV cấp dưới.</summary>
    public string? TeamHref { get; init; }
    /// <summary>Đang ở lịch nhóm.</summary>
    public bool TeamActive { get; init; }
    /// <summary>Tên nút lịch cả nhóm: "Lịch của phòng" với Trưởng phòng / Quản trị, "Lịch nhóm" với Leader.</summary>
    public string TeamLabel { get; init; } = "Lịch nhóm";
    /// <summary>Đang ở màn hình của chính mình (nút "Màn hình của tôi" sáng).</summary>
    public bool MineActive { get; init; }
}

/// <summary>Dựng phần khung (thanh trên + combobox xem màn hình + thanh tab + badge) cho _Layout (renderChrome của artifact).</summary>
public sealed class ChromeBuilder(ViewScope scope, Dept d, IHttpContextAccessor http, DataCache cache)
{
    public static readonly Dictionary<Role, TabDef[]> TabDefs = new()
    {
        [Role.Staff] =
        [
            new("month", "Kế hoạch tháng", "week", "/Staff/Month", "Staff", "Month"),
            new("projects", "Thông tin dự án", "home", "/Staff/Projects", "Staff", "Projects"),
            new("morning", "Báo cáo đầu ngày", "sun", "/Staff/Morning", "Staff", "Morning"),
            new("overtime", "ĐK làm thêm tại CTy", "clock", "/Overtime", "Overtime", null),
            new("evening", "Báo cáo cuối ngày", "flag", "/Staff/Evening", "Staff", "Evening"),
        ],
        [Role.Leader] =
        [
            new("overview", "Tổng quan", "home", "/Leader/Overview", "Leader", "Overview"),
            // Trạng thái làm việc trong ngày của chính Leader (yêu cầu 2026-10-16): trang kiểu nhân viên, chế độ cá nhân ?as=mine
            new("status", "Trạng thái làm việc", "sun", "/Staff/Morning", "Staff", "Morning"),
            new("planning", "Kế hoạch & duyệt", "check", "/Planning/Proposals", "Planning", null),
            new("tracking", "Theo dõi", "eye", "/Leader/Tracking", "Leader", "Tracking"),
            new("reports", "Báo cáo TP", "send", "/Leader/Reports", "Leader", "Reports"),
        ],
        [Role.Manager] =
        [
            new("overview", "Tổng quan", "grid", "/Manager/Overview", "Manager", "Overview"),
            // Trạng thái làm việc của TP / Quản trị (yêu cầu 2026-10-21) — gộp luôn Đăng ký làm thêm (Của tôi / Duyệt Leader / Tổng hợp suất ăn); chế độ cá nhân ?as=mine
            new("status", "Trạng thái làm việc", "sun", "/Staff/Morning", "Staff", "Morning"),
            new("approvals", "Phê duyệt", "check", "/Manager/Approvals", "Manager", "Approvals"),
            new("projects", "Dự án", "folder", "/Manager/Projects", "Manager", "Projects"),
            new("reports", "Báo cáo", "doc", "/Manager/Reports", "Manager", "Reports"),
            // Không có trong tab TP của artifact (TP quản lý tài khoản ở giao diện Leader) — xem ViewScope.
            new("people", "Nhân sự", "eye", "/Planning/People", "Planning", "People"),
        ],
        // Không có trong artifact: tab thêm của Quản trị (Quản trị dùng giao diện TP + tab này — ScopeResolver.UiRoleOf).
        [Role.Admin] =
        [
            new("accounts", "Tài khoản", "grid", "/Admin/Accounts", "Admin", "Accounts"),
        ],
    };

    ChromeModel? _cache;

    /// <summary>null khi chưa đăng nhập / chưa phân giải phạm vi (trang đăng nhập, trang lỗi).</summary>
    public async Task<ChromeModel?> BuildAsync()
    {
        if (_cache is not null) return _cache;
        if (!scope.Resolved || scope.Account is null) return null;
        var ctx = http.HttpContext!;
        var date = scope.Date;
        await d.EnsureAsync(date, date);

        var acc = scope.Account;
        var keepD = date != scope.Today ? Fmt.Key(date) : null;
        string WithQ(string path, string? asVal = null)
        {
            var q = new List<string>();
            if (keepD is not null) q.Add("d=" + keepD);
            if (asVal is not null) q.Add("as=" + asVal);
            return q.Count == 0 ? path : path + (path.Contains('?') ? "&" : "?") + string.Join("&", q);
        }

        // Thanh tab: giao diện của người đang xem; ở lịch cả nhóm (AllStaff) → tab của chính tài khoản (kèm as=me để thoát).
        var controller = ctx.GetRouteValue("controller")?.ToString();
        var action = ctx.GetRouteValue("action")?.ToString();
        var onStatus = scope.OwnCalendar && string.Equals(controller, "Staff", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "Morning", StringComparison.OrdinalIgnoreCase);   // trang Trạng thái làm việc của Leader thuộc "Màn hình của tôi"
        var away = scope.AllStaff || scope.OwnCalendar || scope.IsViewingOther;   // lịch nhóm / chế độ cá nhân / xem nhân viên của Leader: hiện tab Leader (kèm as=me để thoát)
        var onPlanMonth = acc.Role == Role.Leader && away && string.Equals(controller, "Staff", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "Month", StringComparison.OrdinalIgnoreCase);   // Kế hoạch tháng của Leader thuộc tab "Tổng quan" (yêu cầu 2026-10-20; trước đó nằm ở "Kế hoạch & duyệt")
        var onOverviewMonth = acc.Role is Role.Manager or Role.Admin && away && string.Equals(controller, "Staff", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "Month", StringComparison.OrdinalIgnoreCase);   // Kế hoạch tháng của phòng thuộc tab "Tổng quan" của TP
        var onDept = acc.Role == Role.Leader && !away && string.Equals(controller, "Leader", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "Department", StringComparison.OrdinalIgnoreCase);   // Nhân sự phòng (Leader có cờ HrDuty) thuộc tab Tổng quan
        var tabRole = away ? ScopeResolver.UiRoleOf(acc.Role) : scope.Role;
        // TP / Quản trị: tab "Trạng thái làm việc" gồm cả Đăng ký làm thêm (/Overtime) — sáng ở cả hai trang (yêu cầu 2026-10-21)
        var onOvertime = tabRole is Role.Manager or Role.Leader && !away && string.Equals(controller, "Overtime", StringComparison.OrdinalIgnoreCase);   // ĐK làm thêm của TP / Leader thuộc tab Trạng thái làm việc (yêu cầu 2026-10-21)
        var otApprove = onOvertime && tabRole == Role.Manager && ctx.Request.Query["tab"].ToString() is "approve" or "summary";   // duyệt làm thêm của Leader + tổng hợp suất ăn thuộc tab Phê duyệt
        var onMgrOvertime = onOvertime && !otApprove;   // ĐK làm thêm của chính TP thuộc tab Trạng thái làm việc
        IEnumerable<TabDef> defs = TabDefs.GetValueOrDefault(tabRole) ?? [];
        if (scope.IsAdmin && !scope.IsViewingOther) defs = defs.Concat(TabDefs[Role.Admin]);
        var tabs = defs.Any()
            ? defs.Select(t => new TabLink(t.Id, t.Label, t.Icon, WithQ(t.Path, t.Id == "status" ? "mine" : away ? "me" : null),
                (t.Id == "overview" && (onOverviewMonth || onPlanMonth || onDept)) || (t.Id == "status" && onMgrOvertime) || (t.Id == "approvals" && otApprove) || (t.Id == "status" ? scope.OwnCalendar && string.Equals(controller, "Staff", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "Morning", StringComparison.OrdinalIgnoreCase) : (!away || acc.Role == Role.Staff)) && ((string.Equals(t.Controller, controller, StringComparison.OrdinalIgnoreCase) && (t.Action is null || string.Equals(t.Action, action, StringComparison.OrdinalIgnoreCase)))
                    // TP / Quản trị: trang tạo + sửa dự án (/Planning/Projects) thuộc tab "Dự án"
                    || (tabRole == Role.Manager && t.Id == "projects" && string.Equals(controller, "Planning", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "Projects", StringComparison.OrdinalIgnoreCase))),
                away ? 0 : Badge(t.Id))).ToList()
            : [];

        // Combobox "Xem màn hình": chỉ cấp dưới theo nhóm vai trò. "Màn hình của tôi" (HomeHref) và lịch cả nhóm (TeamHref) là hai nút riêng cạnh combobox.
        var opts = new List<ViewOption>();
        if (scope.Subordinates.Count > 0)
        {
            foreach (var p in scope.Subordinates)
            {
                var label = p.Role == Role.Staff && acc.Role != Role.Leader ? $"{p.Name} · {d.GroupText(p)}" : p.Name;
                opts.Add(new(p.Id.ToString(), label, scope.IsViewingOther && scope.MeId == p.Id, Labels.Title(p.Role)));
            }
        }

        string? vName = null, vInfo = null;
        if (scope.IsViewingOther && scope.Me is { } v)
        {
            vName = v.Name;
            var parts = new List<string> { Labels.Title(v.Role), v.Code };
            if (v.Role == Role.Staff) parts.Add(d.GroupText(v));
            if (v.Level is Level level) parts.Add(Labels.Of(level));
            vInfo = string.Join(" · ", parts);
        }

        return _cache = new ChromeModel
        {
            Account = acc,
            AccountTeam = d.TeamText(acc),
            Accent = scope.Role is Role.Manager or Role.Admin ? "var(--mgr)" : "var(--brand)",
            Tabs = tabs,
            ViewOptions = opts,
            ViewingName = vName,
            ViewingInfo = vInfo,
            HomeHref = WithQ(ScopeResolver.HomeOf(acc.Role), "me"),
            TeamHref = acc.Role is not (Role.Staff or Role.Leader) && scope.TeamPeople.Count > 0 ? WithQ("/Staff/Month", "all") : null,
            OwnHref = acc.Role == Role.Leader ? WithQ("/Staff/Month", "mine") : null,
            OwnActive = (scope.OwnCalendar && !onStatus) || (acc.Role == Role.Leader && (scope.AllStaff || scope.IsViewingOther)),
            ViewInPlan = acc.Role != Role.Staff,   // Leader / TP: ô "Xem màn hình" nằm trong Kế hoạch tháng (Leader) / Tổng quan (TP), không ở thanh trên
            TeamActive = scope.AllStaff,
            TeamLabel = acc.Role is Role.Manager or Role.Admin ? "Lịch của phòng" : "Lịch nhóm",
            MineActive = !scope.AllStaff && (!scope.OwnCalendar || onStatus) && !scope.IsViewingOther,
            NotifyEnabled = (await cache.GetBaseAsync()).Notify.TryGetValue(acc.Id, out var ns) ? ns.Enabled : true,
        };

        int Badge(string id)
        {
            if (scope.Role == Role.Leader && id == "planning") return d.LeaderPending(date) + d.OtPending(date).Count(x => x.NeedsDecision) + (scope.MeId is int lid ? d.ProposalsFor(lid).Count + d.LeavesFor(lid).Count : 0);
            if (scope.Role == Role.Manager && id == "approvals")
                return d.Plans.Count(p => p.Date == date && d.P(p.EmployeeId) is not { Role: Role.Leader } && Dept.PlanSt(p) == PlanState.PendingManager)
                    + d.Plans.Count(p => p.Date == date && d.P(p.EmployeeId) is { Role: Role.Leader } && Dept.PlanSt(p) == PlanState.Pending) + d.LeaderLeaves().Count + d.OtPending(date).Count(x => x.NeedsTpDecision);
            if (id == "overtime") return scope.Role == Role.Manager ? d.OtPending(date).Count(x => x.NeedsTpDecision) : 0;   // Leader duyệt làm thêm ở Kế hoạch đề xuất
            if (scope.Role == Role.Staff && id == "evening" && scope.MeId is int me)
                return d.ExtrasOn(date).Count(x => x.EmployeeId == me && x.Status == ExtraStatus.Pending);
            return 0;
        }
    }
}
