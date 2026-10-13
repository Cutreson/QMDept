using QMDept.Data;

namespace QMDept.Services;

/// <summary>Định nghĩa một tab ở thanh dưới (TABS của artifact).</summary>
public sealed record TabDef(string Id, string Label, string Icon, string Path, string Controller, string? Action);

public sealed record TabLink(string Id, string Label, string Icon, string Href, bool Active, int Badge);
public sealed record RoleLink(Role Role, string Label, string Href, bool Active, string Color);

public sealed class ChromeModel
{
    public required Person Account { get; init; }
    public required string AccountTeam { get; init; }
    public required string Accent { get; init; }
    public List<RoleLink> Roles { get; init; } = [];
    public List<TabLink> Tabs { get; init; } = [];
    /// <summary>Hiện ô "Xem nhân viên / Xem Leader".</summary>
    public bool ShowViewAs { get; init; }
    public string ViewAsLabel { get; init; } = "";
    public List<(string Value, string Label, bool Selected)> ViewAsOptions { get; init; } = [];
    /// <summary>Thông tin thêm khi Leader/TP xem một NV (cấp bậc, mã, số dự án).</summary>
    public string? ViewAsInfoHtml { get; init; }
    /// <summary>Giao diện NV nhưng nhóm chưa có nhân viên.</summary>
    public bool EmptyTeamNote { get; init; }
}

/// <summary>Dựng phần khung (thanh trên + thanh tab + badge) cho _Layout từ ViewScope (renderChrome của artifact).</summary>
public sealed class ChromeBuilder(ViewScope scope, Dept d, IHttpContextAccessor http)
{
    public static readonly Dictionary<Role, TabDef[]> TabDefs = new()
    {
        [Role.Nv] =
        [
            new("thang", "Kế hoạch tháng", "week", "/Nv/Thang", "Nv", "Thang"),
            new("du_an", "Thông tin dự án", "home", "/Nv/DuAn", "Nv", "DuAn"),
            new("dau_ngay", "Báo cáo đầu ngày", "sun", "/Nv/DauNgay", "Nv", "DauNgay"),
            new("lam_them", "Làm thêm", "clock", "/LamThem?r=nv", "LamThem", null),
            new("cuoi_ngay", "Báo cáo cuối ngày", "flag", "/Nv/CuoiNgay", "Nv", "CuoiNgay"),
        ],
        [Role.Leader] =
        [
            new("tong_quan", "Tổng quan", "home", "/Leader/TongQuan", "Leader", "TongQuan"),
            new("ke_hoach", "Kế hoạch & duyệt", "check", "/KeHoach/Duyet", "KeHoach", null),
            new("theo_doi", "Theo dõi", "eye", "/Leader/TheoDoi", "Leader", "TheoDoi"),
            new("bao_cao", "Báo cáo TP", "send", "/Leader/BaoCao", "Leader", "BaoCao"),
            new("lam_them", "Làm thêm", "clock", "/LamThem?r=leader", "LamThem", null),
        ],
        [Role.Tp] =
        [
            new("tong_quan", "Tổng quan", "grid", "/Tp/TongQuan", "Tp", "TongQuan"),
            new("phe_duyet", "Phê duyệt", "check", "/Tp/PheDuyet", "Tp", "PheDuyet"),
            new("du_an", "Dự án", "folder", "/Tp/DuAn", "Tp", "DuAn"),
            new("bao_cao", "Báo cáo", "doc", "/Tp/BaoCao", "Tp", "BaoCao"),
            new("lam_them", "Làm thêm", "clock", "/LamThem?r=tp", "LamThem", null),
        ],
        // Không có trong artifact: tài khoản Quản trị chỉ quản lý tài khoản.
        [Role.Admin] =
        [
            new("tai_khoan", "Tài khoản", "grid", "/Admin/TaiKhoan", "Admin", "TaiKhoan"),
        ],
    };

    ChromeModel? _cache;

    /// <summary>null khi chưa đăng nhập / chưa phân giải phạm vi (trang đăng nhập, trang lỗi).</summary>
    public async Task<ChromeModel?> BuildAsync()
    {
        if (_cache is not null) return _cache;
        if (!scope.Resolved || scope.Account is null) return null;
        var ctx = http.HttpContext!;
        var req = ctx.Request;
        var date = scope.Date;
        await d.EnsureAsync(date, date);

        var acc = scope.Account;
        var keepD = date != scope.Today ? Fmt.Key(date) : null;
        string WithD(string path) => keepD is null ? path : path + (path.Contains('?') ? "&" : "?") + "d=" + keepD;

        var roles = scope.AllowedRoles.Count < 2 ? [] : new[] { Role.Tp, Role.Leader, Role.Nv }.Where(scope.AllowedRoles.Contains)
            .Select(r => new RoleLink(r, r == Role.Nv && acc.Role != Role.Nv ? "Lịch nhân viên" : Labels.Of(r),
                WithD(ScopeResolver.HomeOf(r)), r == scope.Role, r == Role.Tp ? "var(--tp)" : "var(--brand)")).ToList();

        // Thanh tab: Leader/TP xem lịch NV → không có thanh tab (chỉ xem Kế hoạch tháng).
        var viewOnly = scope.Role == Role.Nv && acc.Role != Role.Nv;
        var controller = ctx.GetRouteValue("controller")?.ToString();
        var action = ctx.GetRouteValue("action")?.ToString();
        var tabs = viewOnly ? [] : TabDefs[scope.Role].Select(t => new TabLink(t.Id, t.Label, t.Icon, WithD(t.Path),
            string.Equals(t.Controller, controller, StringComparison.OrdinalIgnoreCase) && (t.Action is null || string.Equals(t.Action, action, StringComparison.OrdinalIgnoreCase)),
            Badge(t.Id))).ToList();

        // Ô "Xem nhân viên / Leader"
        var pool = scope.Pool;
        var opts = new List<(string, string, bool)>();
        if (pool.Count > 1)
        {
            if (scope.Role == Role.Nv) opts.Add(("all", $"Tất cả nhân viên ({pool.Count})", scope.AllNv));
            opts.AddRange(pool.Select(p => (p.Id.ToString(), p.Name, p.Id == scope.MeId && !scope.AllNv)));
        }
        string? info = null;
        if (pool.Count > 1 && scope.Role == Role.Nv && scope.Me is { } v && !scope.AllNv)
        {
            var asg = d.AssignsOf(v.Id);
            var leads = asg.Count(a => a.Role == ProjectRole.Lead);
            var lv = v.Level is Level l ? Labels.PillHtml(l) : "<span class=\"pill p-idle\">Chưa xếp cấp</span>";
            info = $"<span class=\"va-info\">{lv} <span class=\"mono note\">{System.Net.WebUtility.HtmlEncode(v.Code)}</span> <span class=\"note\">· {asg.Count} dự án{(leads > 0 ? $" (Leader {leads})" : "")}</span></span>";
        }

        return _cache = new ChromeModel
        {
            Account = acc,
            AccountTeam = d.TeamText(acc),
            Accent = scope.Role == Role.Tp ? "var(--tp)" : "var(--brand)",
            Roles = roles,
            Tabs = tabs,
            ShowViewAs = pool.Count > 1,
            ViewAsLabel = scope.Role == Role.Nv ? "nhân viên" : "Leader",
            ViewAsOptions = opts,
            ViewAsInfoHtml = info,
            EmptyTeamNote = scope.Role == Role.Nv && pool.Count == 0,
        };

        int Badge(string id)
        {
            if (scope.Role == Role.Leader && id == "ke_hoach") return d.LeaderPending(date);
            if (scope.Role == Role.Tp && id == "phe_duyet") return d.Plans.Count(p => p.Date == date && Dept.PlanSt(p) == PlanState.ChoTp);
            if (id == "lam_them") return scope.Role == Role.Nv ? 0 : d.OtPending(date).Count(x => x.Status == OtStatus.ChoDuyet);
            if (scope.Role == Role.Nv && id == "cuoi_ngay" && scope.MeId is int me)
                return d.ExtrasOn(date).Count(x => x.EmployeeId == me && x.Status == ExtraStatus.ChoDuyet);
            return 0;
        }
    }
}
