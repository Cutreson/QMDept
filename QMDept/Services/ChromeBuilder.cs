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
            new("planning", "Kế hoạch & duyệt", "check", "/Planning/Approvals", "Planning", null),
            new("tracking", "Theo dõi", "eye", "/Leader/Tracking", "Leader", "Tracking"),
            new("reports", "Báo cáo TP", "send", "/Leader/Reports", "Leader", "Reports"),
            new("overtime", "ĐK làm thêm tại CTy", "clock", "/Overtime", "Overtime", null),
        ],
        [Role.Manager] =
        [
            new("overview", "Tổng quan", "grid", "/Manager/Overview", "Manager", "Overview"),
            new("approvals", "Phê duyệt", "check", "/Manager/Approvals", "Manager", "Approvals"),
            new("projects", "Dự án", "folder", "/Manager/Projects", "Manager", "Projects"),
            new("reports", "Báo cáo", "doc", "/Manager/Reports", "Manager", "Reports"),
            new("overtime", "ĐK làm thêm tại CTy", "clock", "/Overtime", "Overtime", null),
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
        var tabRole = scope.AllStaff ? ScopeResolver.UiRoleOf(acc.Role) : scope.Role;
        IEnumerable<TabDef> defs = TabDefs.GetValueOrDefault(tabRole) ?? [];
        if (scope.IsAdmin && !scope.IsViewingOther) defs = defs.Concat(TabDefs[Role.Admin]);
        // Leader xem nhân viên: chỉ có lịch tháng của người đó (xem AppController).
        if (scope.IsViewingOther && acc.Role == Role.Leader) defs = defs.Where(t => t.Id == "month");
        var tabs = defs.Any()
            ? defs.Select(t => new TabLink(t.Id, t.Label, t.Icon, WithQ(t.Path, scope.AllStaff ? "me" : null),
                (!scope.AllStaff || acc.Role == Role.Staff) && ((string.Equals(t.Controller, controller, StringComparison.OrdinalIgnoreCase) && (t.Action is null || string.Equals(t.Action, action, StringComparison.OrdinalIgnoreCase)))
                    // TP / Quản trị: trang tạo + sửa dự án (/Planning/Projects) thuộc tab "Dự án"
                    || (tabRole == Role.Manager && t.Id == "projects" && string.Equals(controller, "Planning", StringComparison.OrdinalIgnoreCase) && string.Equals(action, "Projects", StringComparison.OrdinalIgnoreCase))),
                scope.AllStaff ? 0 : Badge(t.Id))).ToList()
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
            TeamHref = acc.Role != Role.Staff && scope.TeamPeople.Count > 0 ? WithQ("/Staff/Month", "all") : null,
            TeamActive = scope.AllStaff,
            TeamLabel = acc.Role is Role.Manager or Role.Admin ? "Lịch của phòng" : "Lịch nhóm",
            MineActive = !scope.AllStaff && !scope.IsViewingOther,
            NotifyEnabled = (await cache.GetBaseAsync()).Notify.TryGetValue(acc.Id, out var ns) ? ns.Enabled : true,
        };

        int Badge(string id)
        {
            if (scope.Role == Role.Leader && id == "planning") return d.LeaderPending(date);
            if (scope.Role == Role.Manager && id == "approvals") return d.Plans.Count(p => p.Date == date && Dept.PlanSt(p) == PlanState.PendingManager);
            if (id == "overtime") return scope.Role == Role.Staff ? 0 : d.OtPending(date).Count(x => x.Status == OtStatus.Pending);
            if (scope.Role == Role.Staff && id == "evening" && scope.MeId is int me)
                return d.ExtrasOn(date).Count(x => x.EmployeeId == me && x.Status == ExtraStatus.Pending);
            return 0;
        }
    }
}
