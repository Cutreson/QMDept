using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.Controllers;

/// <summary>Khai báo giao diện (vai trò UI) mà controller phục vụ.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class UiRoleAttribute(Role role) : Attribute
{
    public Role Role { get; } = role;
}

/// <summary>
/// Controller nền cho mọi màn hình sau đăng nhập.
/// - Trước mỗi action: phân giải <see cref="ViewScope"/> (tài khoản, giao diện, người đang xem, ngày).
/// - Giao diện lấy từ [UiRole] trên controller, hoặc override <see cref="ResolveUiRole"/> (LamThemController đọc ?r=).
/// - Quy ước POST: Post–Redirect–Get; thông báo qua <see cref="Toast"/>; lỗi nghiệp vụ ném <see cref="BizException"/>
///   và bọc bằng <see cref="Do"/> để tự hiện toast lỗi rồi quay lại trang trước.
/// </summary>
[Authorize]
public abstract class AppController : Controller
{
    protected AppDbContext Db => HttpContext.RequestServices.GetRequiredService<AppDbContext>();
    protected Dept D => HttpContext.RequestServices.GetRequiredService<Dept>();
    protected ViewScope Scope => HttpContext.RequestServices.GetRequiredService<ViewScope>();
    protected WorkflowService Flow => HttpContext.RequestServices.GetRequiredService<WorkflowService>();
    protected PhotoStore Photos => HttpContext.RequestServices.GetRequiredService<PhotoStore>();
    /// <summary>Ngày đang chọn (?d=).</summary>
    protected DateOnly Date => Scope.Date;

    protected virtual Role ResolveUiRole(ActionExecutingContext ctx) =>
        (GetType().GetCustomAttributes(typeof(UiRoleAttribute), true).FirstOrDefault() as UiRoleAttribute)?.Role ?? Role.Nv;

    public override async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var resolver = HttpContext.RequestServices.GetRequiredService<ScopeResolver>();
        var res = await resolver.ResolveAsync(HttpContext, ResolveUiRole(ctx));
        if (res == ScopeResult.Unauthenticated)
        {
            await HttpContext.SignOutAsync();
            ctx.Result = Redirect("/Account/Login");
            return;
        }
        if (res == ScopeResult.Forbidden)
        {
            ctx.Result = Redirect(ScopeResolver.HomeOf(Scope.Account.Role));
            return;
        }
        await next();
    }

    /// <summary>Hiện thông báo ngắn (toast) ở trang kế tiếp.</summary>
    protected void Toast(string msg) => TempData["toast"] = msg;

    /// <summary>URL quay lại sau POST: field "returnUrl" của form → Referer → fallback.</summary>
    protected string BackUrl(string? fallback = null)
    {
        var ru = Request.HasFormContentType ? Request.Form["returnUrl"].ToString() : null;
        if (!string.IsNullOrEmpty(ru) && Url.IsLocalUrl(ru)) return ru;
        if (Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out var r) && r.Host == Request.Host.Host)
            return r.PathAndQuery;
        return fallback ?? ScopeResolver.HomeOf(Scope.Role);
    }

    protected IActionResult Back(string? fallback = null) => Redirect(BackUrl(fallback));

    /// <summary>
    /// Chạy thao tác ghi: thành công → toast <paramref name="ok"/> (nếu có) rồi quay lại; BizException → toast lỗi rồi quay lại.
    /// Dùng: <c>return await Do(async () => { ... }, "Đã lưu");</c>
    /// </summary>
    protected async Task<IActionResult> Do(Func<Task> action, string? ok = null, string? redirect = null)
    {
        try
        {
            await action();
            if (ok is not null) Toast(ok);
        }
        catch (BizException ex)
        {
            Toast(ex.Message);
        }
        return Redirect(redirect is not null && Url.IsLocalUrl(redirect) ? redirect : BackUrl());
    }

    /// <summary>Ném lỗi nếu đang ở chế độ chỉ xem trang NV của người khác.</summary>
    protected void RequireSelfNv()
    {
        if (Scope.Role != Role.Nv || !Scope.IsSelf || Scope.Account.Role != Role.Nv)
            throw new BizException("Chế độ xem: không sửa được trang của nhân viên");
    }

    /// <summary>Ném lỗi nếu tài khoản không có quyền Leader (Leader hoặc TP).</summary>
    protected void RequireLead()
    {
        if (!Scope.CanLead) throw new BizException("Chỉ Leader hoặc Trưởng phòng được thực hiện");
    }

    /// <summary>Ném lỗi nếu tài khoản không phải Trưởng phòng.</summary>
    protected void RequireTp()
    {
        if (!Scope.IsTp) throw new BizException("Chỉ Trưởng phòng được thực hiện");
    }

    /// <summary>Ném lỗi nếu NV không thuộc phạm vi đang xem (giao diện Leader: nhóm của Leader đang xem).</summary>
    protected async Task RequireInScopeAsync(int employeeId)
    {
        await D.EnsureAsync(Date, Date);
        if (D.P(employeeId) is null or { IsDeleted: true }) throw new BizException("Không tìm thấy nhân viên");
        if (!D.InScope(employeeId)) throw new BizException("Nhân viên không thuộc nhóm bạn quản lý");
    }
}
