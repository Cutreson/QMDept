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
        if (sel == "all" && acc.Role != Role.Staff && scope.Pool.Count > 0) allStaff = true;
        else if (int.TryParse(sel, out var vid) && vid != acc.Id) viewed = subs.Find(p => p.Id == vid);

        if (explicitSel || (sel == "me" && http.Request.Cookies[ViewCookie] is { } c && c != "me"))
        {
            var val = allStaff ? "all" : viewed is not null ? viewed.Id.ToString() : "me";
            http.Response.Cookies.Append(ViewCookie, val, new CookieOptions { HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(30) });
        }

        scope.AllStaff = allStaff;
        scope.Me = allStaff ? scope.Pool.FirstOrDefault() : viewed ?? acc;
        scope.Role = allStaff ? Role.Staff : UiRoleOf(scope.Me!.Role);
        scope.Resolved = true;

        // Trang riêng của Quản trị ([UiRole(Admin)]) chỉ mở ở màn hình của chính tài khoản Quản trị.
        return allowed.Contains(scope.Role) || (scope.IsSelf && allowed.Contains(acc.Role)) ? ScopeResult.Ok : ScopeResult.Forbidden;
    }
}
