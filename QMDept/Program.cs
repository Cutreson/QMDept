using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

var builder = WebApplication.CreateBuilder(args);
// Cấu hình riêng máy (connection string thật, KHÔNG commit — đã có trong .gitignore). Ưu tiên cao hơn appsettings.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ===== Cơ sở dữ liệu: MySQL qua MySql.EntityFrameworkCore (Oracle). Connection string: ConnectionStrings:Default =====
var conn = builder.Configuration.GetConnectionString("Default");
// Bộ đệm đọc dùng chung (tối ưu tốc độ — DB ở xa): tự hết hạn khi có lệnh ghi qua EF (Services/DataCache.cs).
builder.Services.AddSingleton<DataCache>();
builder.Services.AddSingleton<DataCacheInterceptor>();
builder.Services.AddSingleton<KeepOpenInterceptor>();
// Factory (DataCache nạp song song nhiều bảng trên nhiều kết nối) + một AppDbContext cho mỗi request.
builder.Services.AddDbContextFactory<AppDbContext>((sp, o) => o
    .UseMySQL(string.IsNullOrWhiteSpace(conn) ? "server=not-configured" : conn)
    .AddInterceptors(sp.GetRequiredService<DataCacheInterceptor>(), sp.GetRequiredService<KeepOpenInterceptor>())
    // DB thật là MariaDB: thay khóa migration của provider (lỗi trên MariaDB) — xem Data/MariaDbHistoryRepository.cs
    .ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IHistoryRepository, MariaDbHistoryRepository>());
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Login";
        o.Cookie.Name = "qm_auth";
        o.Cookie.HttpOnly = true;
        o.SlidingExpiration = true;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        // Phiên bị thu hồi (đổi mật khẩu / vai trò / mã, xóa tài khoản → SecurityStamp đổi) bị từ chối ở mọi endpoint,
        // kể cả hub SignalR. Đọc từ DataCache nên không tốn truy vấn DB mỗi request.
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var ok = int.TryParse(ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
                && (await ctx.HttpContext.RequestServices.GetRequiredService<DataCache>().GetBaseAsync()).People
                    .Find(p => p.Id == id && !p.IsDeleted) is { } p
                && p.SecurityStamp == ctx.Principal?.FindFirst(ScopeResolver.StampClaim)?.Value;
            if (!ok)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();
// Chống dò mật khẩu: tối đa 10 lần đăng nhập / phút / IP.
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy(QMDept.Controllers.AccountController.LoginRateLimit, ctx => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.OnRejected = async (c, ct) =>
    {
        c.HttpContext.Response.StatusCode = 429;
        c.HttpContext.Response.ContentType = "text/html; charset=utf-8";
        await c.HttpContext.Response.WriteAsync("<!doctype html><meta charset=utf-8><p style='font-family:sans-serif;padding:24px'>Đăng nhập sai quá nhiều lần. Vui lòng thử lại sau 1 phút. <a href='/Account/Login'>Quay lại</a></p>", ct);
    };
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IPasswordHasher<Person>, PasswordHasher<Person>>();
builder.Services.AddScoped<DevSwitch>();
builder.Services.AddScoped<ViewScope>();
builder.Services.AddScoped<ScopeResolver>();
builder.Services.AddScoped<Dept>();
builder.Services.AddScoped<ChromeBuilder>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<Notifier>();
builder.Services.AddSignalR();

builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

if (string.IsNullOrWhiteSpace(conn))
{
    // Chưa ghép connection string: mọi request hiện hướng dẫn cấu hình (không tự tạo DB tạm — xem agents.md).
    app.Run(async ctx =>
    {
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync("<!doctype html><meta charset=utf-8><title>QMDept</title><div style=\"font-family:sans-serif;max-width:620px;margin:40px auto;padding:16px\">"
            + "<h2>Chưa cấu hình cơ sở dữ liệu</h2><p>Điền <code>ConnectionStrings:Default</code> (MySQL) trong <code>appsettings.json</code>, "
            + "user-secrets hoặc biến môi trường <code>ConnectionStrings__Default</code>, rồi chạy lại ứng dụng.</p></div>");
    });
    app.Run();
    return;
}

// Tự áp dụng migration khi khởi động (tắt bằng Database:AutoMigrate=false).
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var s = app.Services.CreateScope();
    s.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHub<NotifyHub>("/hubs/notify");
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
