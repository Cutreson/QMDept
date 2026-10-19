using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

public enum ScopeResult { Ok, Unauthenticated, Forbidden }

/// <summary>
/// Điền <see cref="ViewScope"/> cho request: tài khoản (cookie đăng nhập), người đang xem (?as= / cookie qm_view),
/// giao diện (= vai trò người đang xem), ngày (?d= hoặc field form "d").
/// Xem mô hình "xem màn hình cấp dưới" ở ViewScope.
/// </summary>
public sealed class ScopeResolver(ViewScope scope, DataCache cache)
{
    public const string StampClaim = "qm:stamp";
    public const string ViewCookie = "qm_view";

    /// <summary>Trang mặc định của mỗi giao diện (Quản trị dùng giao diện TP → Tổng quan TP).</summary>
    public static string HomeOf(Role r) => UiRoleOf(r) switch
    {
        Role.Manager => "/Manager/Overview", Role.Leader => "/Leader/Overview", _ => "/Staff/Month",
    };

    /// <summary>
    /// Giao diện của một vai trò. Quản trị cao hơn TP (yêu cầu chủ dự án 2026-10-13): dùng toàn bộ giao diện + quyền TP,
    /// cộng thêm tab "Tài khoản" (trang [UiRole(Admin)] chỉ mở cho chính tài khoản Quản trị).
    /// </summary>
    public static Role UiRoleOf(Role r) => r == Role.Admin ? Role.Manager : r;

    static readonly Dictionary<Role, int> RoleOrder = new() { [Role.Admin] = 0, [Role.Manager] = 1, [Role.Leader] = 2, [Role.Staff] = 3 };
    static List<Person> Sorted(IEnumerable<Person> s) =>
        s.OrderBy(p => RoleOrder.GetValueOrDefault(p.Role, 9)).ThenBy(p => p.SortOrder).ThenBy(p => p.Name, Fmt.ViCompare).ToList();

    /// <summary>Cấp dưới mà tài khoản được xem màn hình.</summary>
    public static IEnumerable<Person> Subordinates(IEnumerable<Person> people, Person acc) => acc.Role switch
    {
        Role.Admin => people.Where(p => !p.IsDeleted && p.Role != Role.Admin),
        Role.Manager => people.Where(p => !p.IsDeleted && (p.Role == Role.Leader || p.Role == Role.Staff)),
        Role.Leader => people.Where(p => !p.IsDeleted && p.Role == Role.Staff && p.LeaderId == acc.Id),
        _ => [],
    };

    /// <param name="allowed">Các giao diện trang này phục vụ (một vai trò; dùng chung NV/Leader/TP; hoặc mọi vai trò).</param>
    public async Task<ScopeResult> ResolveAsync(HttpContext http, IReadOnlyCollection<Role> allowed)
    {
        var idStr = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return ScopeResult.Unauthenticated;
        // Bảng people lấy từ DataCache (đổi mật khẩu / xóa tài khoản là lệnh ghi → cache tự làm mới ngay).
        var people = (await cache.GetBaseAsync()).People;
        var acc = people.Find(p => p.Id == id && !p.IsDeleted);
        if (acc is null || acc.SecurityStamp != http.User.FindFirstValue(StampClaim)) return ScopeResult.Unauthenticated;

        scope.Account = acc;
        scope.Today = VnTime.Today;
        // Ngày đang chọn: ?d= trên URL; với POST không có ?d= thì lấy field "d" của form (Ui.ReturnField tự chèn).
        string? dKey = http.Request.Query["d"];
        if (string.IsNullOrEmpty(dKey) && http.Request.HasFormContentType) dKey = (await http.Request.ReadFormAsync())["d"];
        scope.Date = Fmt.ParseKey(dKey) ?? scope.Today;

        var subs = acc.Role == Role.Staff ? [] : Sorted(Subordinates(people, acc));
        scope.Subordinates = subs;
        scope.Pool = acc.Role == Role.Staff ? [acc] : subs.Where(p => p.Role == Role.Staff).ToList();
        scope.TeamPeople = acc.Role switch
        {
            // NV: lịch nhóm gồm Leader của mình (việc Leader tự đặt) + các NV cùng nhóm — giống lịch nhóm bên giao diện Leader.
            Role.Staff => acc.LeaderId is int gl
                ? [.. people.Where(p => !p.IsDeleted && p.Role == Role.Leader && p.Id == gl),
                   .. Sorted(people.Where(p => !p.IsDeleted && p.Role == Role.Staff && p.LeaderId == gl))]
                : [acc],
            Role.Leader => [acc, .. scope.Pool],
            // TP / Quản trị: lịch của phòng gồm cả Leader và nhân viên (Tổng quan → Kế hoạch tháng, yêu cầu 2026-10-20)
            _ => subs.Where(p => p.Role is Role.Leader or Role.Staff).ToList(),
        };

        // Người đang xem: ?as= (ghi nhớ vào cookie) → cookie → chính mình.
        string? sel = http.Request.Query["as"];
        var explicitSel = !string.IsNullOrEmpty(sel);
        if (!explicitSel) sel = http.Request.Cookies[ViewCookie];
        // Mở trang thuộc giao diện của chính mình mà không chỉ định ?as= → về màn hình của mình (vd. bấm link thông báo).
        // Ngoại lệ: người trong cookie dùng cùng giao diện với trang (Quản trị đang xem TP rồi bấm tab TP) → giữ người đó.
        // Chỉ áp dụng ngoại lệ cho GET: form POST không có ?as= luôn là thao tác ở màn hình của chính mình.
        if (!explicitSel && allowed.Count == 1 && (allowed.Contains(acc.Role) || allowed.Contains(UiRoleOf(acc.Role)))
            && !(HttpMethods.IsGet(http.Request.Method) && int.TryParse(sel, out var cid) && subs.Find(p => p.Id == cid) is { } cp && allowed.Contains(UiRoleOf(cp.Role)))) sel = "me";

        Person? viewed = null;
        var allStaff = false;
        // NV chỉ vào lịch nhóm khi bấm rõ (?as=all) — cookie "all" cũ không biến các trang báo cáo của NV thành chỉ xem.
        if (sel == "all" && scope.TeamPeople.Count > 0 && (acc.Role != Role.Staff || explicitSel)) allStaff = true;
        else if (int.TryParse(sel, out var vid) && vid != acc.Id) viewed = subs.Find(p => p.Id == vid);

        // Leader dùng các trang cá nhân kiểu nhân viên của chính mình (?as=mine: lịch tháng, báo cáo đầu ngày, báo cáo cuối ngày) — chỉ ở /Staff/*.
        // Ghi nhớ vào cookie để các POST của form (không có ?as=) vẫn chạy đúng; sang trang Leader thì tự về "me".
        var ownCal = !allStaff && sel == "mine" && acc.Role == Role.Leader
            && (http.Request.Path.Value ?? "").StartsWith("/Staff/", StringComparison.OrdinalIgnoreCase);

        if (explicitSel || (sel == "me" && http.Request.Cookies[ViewCookie] is { } c && c != "me"))
        {
            var val = allStaff ? "all" : ownCal ? "mine" : viewed is not null ? viewed.Id.ToString() : "me";
            http.Response.Cookies.Append(ViewCookie, val, new CookieOptions { HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(30) });
        }

        scope.AllStaff = allStaff;
        scope.OwnCalendar = ownCal;
        scope.Me = allStaff ? scope.Pool.FirstOrDefault() ?? acc : viewed ?? acc;
        // Xem màn hình người khác (TP / Quản trị / Leader): chỉ xem KẾ HOẠCH THÁNG của họ (yêu cầu 2026-10-20) → ở /Staff/Month người được xem (Leader hay NV) dùng giao diện kiểu nhân viên.
        var viewMonth = viewed is not null && HttpMethods.IsGet(http.Request.Method) && (http.Request.Path.Value ?? "").TrimEnd('/').Equals("/Staff/Month", StringComparison.OrdinalIgnoreCase);
        scope.Role = allStaff || ownCal || viewMonth ? Role.Staff : UiRoleOf(scope.Me!.Role);
        scope.Resolved = true;

        // Trang riêng của Quản trị ([UiRole(Admin)]) chỉ mở ở màn hình của chính tài khoản Quản trị.
        return allowed.Contains(scope.Role) || (scope.IsSelf && allowed.Contains(acc.Role)) ? ScopeResult.Ok : ScopeResult.Forbidden;
    }
}
