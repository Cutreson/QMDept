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
builder.Services.AddDbContext<AppDbContext>(o => o.UseMySQL(string.IsNullOrWhiteSpace(conn) ? "server=not-configured" : conn));

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
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IPasswordHasher<Person>, PasswordHasher<Person>>();
builder.Services.AddSingleton<PhotoStore>();
builder.Services.AddScoped<ViewScope>();
builder.Services.AddScoped<ScopeResolver>();
builder.Services.AddScoped<Dept>();
builder.Services.AddScoped<ChromeBuilder>();
builder.Services.AddScoped<WorkflowService>();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
