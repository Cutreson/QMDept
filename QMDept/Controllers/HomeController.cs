using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QMDept.Models;
using QMDept.Services;

namespace QMDept.Controllers;

public class HomeController(DataCache cache) : Controller
{
    /// <summary>Trang gốc: chuyển tới màn hình mặc định theo vai trò tài khoản.</summary>
    [Authorize]
    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Redirect("/Account/Login");
        var p = (await cache.GetBaseAsync()).People.Find(x => x.Id == id && !x.IsDeleted);
        if (p is null || p.SecurityStamp != User.FindFirstValue(ScopeResolver.StampClaim))
        {
            // Phiên đã bị thu hồi (tài khoản bị xóa / đổi mật khẩu): đăng xuất để không lặp chuyển hướng với trang đăng nhập.
            await HttpContext.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/Account/Login");
        }
        return Redirect(ScopeResolver.HomeOf(p.Role));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
