using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Models;
using QMDept.Services;

namespace QMDept.Controllers;

public class HomeController(AppDbContext db, PhotoStore photos) : Controller
{
    /// <summary>Trang gốc: chuyển tới màn hình mặc định theo vai trò tài khoản.</summary>
    [Authorize]
    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Redirect("/Account/Login");
        var p = await db.People.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return p is null ? Redirect("/Account/Login") : Redirect(ScopeResolver.HomeOf(p.Role));
    }

    /// <summary>Ảnh hiện trường (chỉ người đã đăng nhập).</summary>
    [Authorize]
    [HttpGet("/photo/{id:int}")]
    public async Task<IActionResult> Photo(int id)
    {
        var p = await db.Photos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        var path = photos.PathOf(p.StoredName);
        return System.IO.File.Exists(path) ? PhysicalFile(path, p.ContentType) : NotFound();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
