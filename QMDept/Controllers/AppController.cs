using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.Controllers;

/// <summary>Khai báo giao diện (vai trò UI) mà controller phục vụ.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class UiRoleAttribute(Role role) : Attribute
{
    public Role Role { get; } = role;
}

/// <summary>Trang dùng được ở mọi giao diện, kể cả Quản trị (vd. Cài đặt thông báo).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AnyUiRoleAttribute : Attribute;

/// <summary>
/// Controller nền cho mọi màn hình sau đăng nhập.
/// - Trước mỗi action: phân giải <see cref="ViewScope"/> (tài khoản, người đang xem, giao diện, ngày).
/// - Giao diện của trang lấy từ [UiRole] trên controller (null = dùng chung NV/Leader/TP, vd. Làm thêm; [AnyUiRole] = mọi giao diện).
///   Trang không khớp giao diện của người đang xem → chuyển về trang mặc định của giao diện đó.
/// - CHỈ XEM khi đang xem màn hình cấp dưới: mọi POST bị từ chối tại đây (không cần từng action tự kiểm tra).
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
    protected Notifier Notify => HttpContext.RequestServices.GetRequiredService<Notifier>();
    /// <summary>Ngày đang chọn (?d=).</summary>
    protected DateOnly Date => Scope.Date;

    protected static readonly Role[] SharedRoles = [Role.Staff, Role.Leader, Role.Manager];
    static readonly Role[] AllRoles = [Role.Staff, Role.Leader, Role.Manager, Role.Admin];

    /// <summary>Các giao diện trang (action) này phục vụ: [UiRole(x)] → {x}; [AnyUiRole] → mọi vai trò; không khai báo → dùng chung NV/Leader/TP.</summary>
    protected virtual Role[] UiRoles(ActionExecutingContext ctx) =>
        GetType().IsDefined(typeof(AnyUiRoleAttribute), true) ? AllRoles
        : GetType().GetCustomAttributes(typeof(UiRoleAttribute), true).FirstOrDefault() is UiRoleAttribute a ? [a.Role]
        : SharedRoles;

    public override async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var resolver = HttpContext.RequestServices.GetRequiredService<ScopeResolver>();
        var res = await resolver.ResolveAsync(HttpContext, UiRoles(ctx));
        if (res == ScopeResult.Unauthenticated)
        {
            await HttpContext.SignOutAsync();
            ctx.Result = Redirect("/Account/Login");
            return;
        }
        if (res == ScopeResult.Forbidden)
        {
            if (HttpMethods.IsPost(Request.Method)) Toast("Màn hình đang chọn không có thao tác này — vui lòng mở lại trang và thử lại");
            ctx.Result = Redirect(ScopeResolver.HomeOf(Scope.Role));
            return;
        }
        // Leader xem nhân viên trong nhóm: chỉ thấy LỊCH THÁNG của người đó (yêu cầu 2026-10-14), không thấy báo cáo / dự án / làm thêm.
        // (TP / Quản trị xem cấp dưới vẫn thấy đủ màn hình — quy tắc 8.)
        if (HttpMethods.IsGet(Request.Method) && Scope.IsViewingOther && Scope.Account.Role == Role.Leader
            && !(ctx.RouteData.Values["controller"] is string sc && sc.Equals("Staff", StringComparison.OrdinalIgnoreCase)
                 && ctx.RouteData.Values["action"] is string sa && sa.Equals("Month", StringComparison.OrdinalIgnoreCase)))
        {
            ctx.Result = Redirect($"/Staff/Month?as={Scope.MeId}" + (Date != Scope.Today ? "&d=" + Fmt.Key(Date) : ""));
            return;
        }
        // Lịch cả nhóm (?as=all): chỉ form "Giao thêm việc" được ghi.
        if (HttpMethods.IsPost(Request.Method) && Scope.AllStaff
            && !(ctx.RouteData.Values["controller"] is string c && c.Equals("Staff", StringComparison.OrdinalIgnoreCase)
                 && ctx.RouteData.Values["action"] is string a && a.Equals("AssignTask", StringComparison.OrdinalIgnoreCase)))
        {
            Toast("Lịch chỉ xem — mở màn hình của bạn để thao tác");
            ctx.Result = Redirect(BackUrl());
            return;
        }
        if (HttpMethods.IsPost(Request.Method) && Scope.IsViewingOther)
        {
            Toast($"Đang xem màn hình của {Scope.Me?.Name}: chỉ xem, không thao tác được");
            ctx.Result = Redirect(BackUrl());
            return;
        }
        var done = await next();
        // Hai lần gửi cùng lúc (2 tab / 2 thiết bị / mạng gửi lại) chạm unique index hoặc bản ghi vừa bị sửa:
        // báo nhẹ và quay lại thay vì trang lỗi 500.
        if (HttpMethods.IsPost(Request.Method) && done.Exception is DbUpdateException && !done.ExceptionHandled)
        {
            done.ExceptionHandled = true;
            Toast("Dữ liệu vừa được cập nhật ở nơi khác — vui lòng xem lại và thử lại");
            done.Result = Redirect(BackUrl());
        }
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
    protected void RequireSelfStaff(bool leaderOk = false)
    {
        // Leader dùng được trang cá nhân kiểu NV của chính mình (báo cáo đầu / cuối ngày — Trưởng phòng duyệt) khi ở chế độ ?as=mine.
        if (Scope.Role != Role.Staff || !Scope.IsSelf || !(Scope.Account.Role == Role.Staff || (leaderOk && Scope.Account.Role == Role.Leader && Scope.OwnCalendar)))
            throw new BizException("Chế độ xem: không sửa được trang của nhân viên");
    }

    /// <summary>Ném lỗi nếu tài khoản không có quyền Leader (Leader hoặc TP).</summary>
    protected void RequireLead()
    {
        if (!Scope.CanLead) throw new BizException("Chỉ Leader hoặc Trưởng phòng được thực hiện");
    }

    /// <summary>Ném lỗi nếu tài khoản không phải Trưởng phòng.</summary>
    protected void RequireManager()
    {
        if (!Scope.IsManager) throw new BizException("Chỉ Trưởng phòng được thực hiện");
    }

    /// <summary>Ném lỗi nếu NV không thuộc phạm vi đang xem (giao diện Leader: nhóm của Leader đang xem).</summary>
    protected async Task RequireInScopeAsync(int employeeId)
    {
        await D.EnsureAsync(Date, Date);
        if (D.P(employeeId) is null or { IsDeleted: true }) throw new BizException("Không tìm thấy nhân viên");
        if (!D.InScope(employeeId)) throw new BizException("Nhân viên không thuộc nhóm bạn quản lý");
    }
}
