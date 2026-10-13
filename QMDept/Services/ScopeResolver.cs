using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

public enum ScopeResult { Ok, Unauthenticated, Forbidden }

/// <summary>
/// Điền <see cref="ViewScope"/> cho request: tài khoản (từ cookie), giao diện, người đang xem (?as=), ngày (?d=).
/// ?as=&lt;id&gt; hoặc ?as=all được nhớ trong cookie qm_as_{role} (giống localStorage me_* của artifact).
/// </summary>
public sealed class ScopeResolver(AppDbContext db, ViewScope scope)
{
    public const string StampClaim = "qm:stamp";

    public static List<Role> AllowedRoles(Role accountRole) => accountRole switch
    {
        Role.Tp => [Role.Tp, Role.Leader, Role.Nv],
        Role.Leader => [Role.Leader, Role.Nv],
        _ => [Role.Nv],
    };

    /// <summary>Trang mặc định của mỗi giao diện.</summary>
    public static string HomeOf(Role r) => r switch { Role.Tp => "/Tp/TongQuan", Role.Leader => "/Leader/TongQuan", _ => "/Nv/Thang" };

    static List<Person> Sorted(IEnumerable<Person> s) => s.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, Fmt.ViCompare).ToList();

    public async Task<ScopeResult> ResolveAsync(HttpContext http, Role uiRole)
    {
        var idStr = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return ScopeResult.Unauthenticated;
        var acc = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (acc is null || acc.SecurityStamp != http.User.FindFirstValue(StampClaim)) return ScopeResult.Unauthenticated;

        scope.Account = acc;
        scope.AllowedRoles = AllowedRoles(acc.Role);
        scope.Today = VnTime.Today;
        scope.Date = Fmt.ParseKey(http.Request.Query["d"]) ?? scope.Today;
        if (!scope.AllowedRoles.Contains(uiRole)) { scope.Role = acc.Role; scope.Resolved = true; return ScopeResult.Forbidden; }
        scope.Role = uiRole;

        // poolFor(role)
        List<Person> pool = uiRole switch
        {
            Role.Tp => acc.Role == Role.Tp ? [acc] : [],
            Role.Leader => acc.Role == Role.Leader ? [acc] : Sorted(await db.People.AsNoTracking().Where(p => p.Role == Role.Leader && !p.IsDeleted).ToListAsync()),
            _ => acc.Role == Role.Nv ? [acc]
                : acc.Role == Role.Leader ? Sorted(await db.People.AsNoTracking().Where(p => p.Role == Role.Nv && p.LeaderId == acc.Id && !p.IsDeleted).ToListAsync())
                : Sorted(await db.People.AsNoTracking().Where(p => p.Role == Role.Nv && !p.IsDeleted).ToListAsync()),
        };
        scope.Pool = pool;

        var cookieKey = "qm_as_" + uiRole.ToString().ToLowerInvariant();
        string? sel = http.Request.Query["as"];
        if (!string.IsNullOrEmpty(sel))
            http.Response.Cookies.Append(cookieKey, sel, new CookieOptions { HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Expires = DateTimeOffset.UtcNow.AddDays(180) });
        else sel = http.Request.Cookies[cookieKey];

        scope.AllNv = uiRole == Role.Nv && acc.Role != Role.Nv && sel == "all";
        scope.Me = int.TryParse(sel, out var mid) ? pool.Find(p => p.Id == mid) ?? pool.FirstOrDefault() : pool.FirstOrDefault();
        scope.Resolved = true;
        return ScopeResult.Ok;
    }
}
