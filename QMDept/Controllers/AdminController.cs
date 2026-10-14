using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.Controllers;

/// <summary>
/// Quản trị tài khoản (KHÔNG có trong artifact — bổ sung theo yêu cầu chủ dự án 2026-10-13):
/// tài khoản vai trò Admin tạo / sửa / xóa / khôi phục tài khoản và đặt mật khẩu trực tiếp cho bất kỳ ai.
/// (TP vẫn quản lý tài khoản như artifact ở Kế hoạch &amp; duyệt → Nhân sự, nhưng chỉ "đặt lại" để người dùng tự đặt mật khẩu.)
/// </summary>
[UiRole(Role.Admin)]
public class AdminController(IPasswordHasher<Person> hasher) : AppController
{
    /// <summary>/Controller → trang mặc định của giao diện.</summary>
    [HttpGet] public IActionResult Index() => Redirect(Ui.To(Request, "/Admin/Accounts"));

    public sealed class AccountInput
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public Role Role { get; set; } = Role.Staff;
        public int? LeaderId { get; set; }
        public Level? Level { get; set; }
        public string? Password { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Accounts()
    {
        await D.EnsureAsync(Date, Date);
        ViewData["Title"] = "Tài khoản";
        return View();
    }

    void RequireAdmin()
    {
        if (Scope.Account.Role != Role.Admin) throw new BizException("Chỉ tài khoản Quản trị được thực hiện");
    }

    async Task ApplyAsync(Person p, AccountInput x)
    {
        var name = (x.Name ?? "").Trim();
        var code = AccountRules.NormalizeCode(x.Code);
        if (code.Length == 0 && p.Id == 0) code = AccountRules.SuggestCode(name); // tạo mới để trống mã → theo quy tắc tên + chữ cái đầu họ đệm
        if (name.Length == 0 || code.Length == 0) throw new BizException("Nhập họ tên và mã nhân viên");
        if (name.Length > 100) throw new BizException("Họ tên tối đa 100 ký tự");
        if (!Enum.IsDefined(x.Role)) throw new BizException("Chọn vai trò");
        if (x.Level is Level lvl && !Enum.IsDefined(lvl)) throw new BizException("Chọn cấp bậc");
        await AccountRules.ValidateCodeAsync(Db, code, p.Id == 0 ? null : p.Id);
        if (p.Id == Scope.Account.Id && x.Role != Role.Admin) throw new BizException("Không thể tự đổi vai trò của tài khoản đang đăng nhập");
        int? leader = null;
        if (x.Role == Role.Staff && x.LeaderId is int lid)
        {
            if (lid == p.Id || !await Db.People.AnyAsync(l => l.Id == lid && l.Role == Role.Leader && !l.IsDeleted)) throw new BizException("Nhóm không hợp lệ");
            leader = lid;
        }
        var changed = p.Id != 0 && (p.Code != code || p.Role != x.Role);
        p.Name = name;
        p.Code = code;
        p.Role = x.Role;
        p.LeaderId = leader;
        p.Level = x.Role == Role.Staff ? x.Level : null;
        if (changed) p.SecurityStamp = AccountRules.NewStamp();
    }

    [HttpPost]
    public Task<IActionResult> Create(AccountInput x) => Do(async () =>
    {
        RequireAdmin();
        if (!ModelState.IsValid) throw new BizException("Dữ liệu không hợp lệ (vai trò / cấp bậc / nhóm)");
        // Tài khoản Quản trị / Trưởng phòng phải có mật khẩu ngay (tránh người khác đăng nhập trước để chiếm quyền).
        if (x.Role is Role.Admin or Role.Manager && string.IsNullOrEmpty(x.Password)) throw new BizException("Tài khoản Quản trị / Trưởng phòng phải đặt mật khẩu khi tạo");
        var p = new Person { CreatedAt = VnTime.UtcNow, SortOrder = await Db.People.CountAsync() + 1 };
        await ApplyAsync(p, x);
        if (!string.IsNullOrEmpty(x.Password)) { AccountRules.ValidatePassword(x.Password); AccountRules.SetPassword(p, x.Password, hasher); }
        else AccountRules.SetPassword(p, AccountRules.DefaultPassword, hasher); // không nhập → mật khẩu mặc định
        Db.People.Add(p);
        await Db.SaveChangesAsync();
        Toast("Đã tạo tài khoản " + p.Code);
    });

    [HttpPost]
    public Task<IActionResult> Edit(AccountInput x) => Do(async () =>
    {
        RequireAdmin();
        if (!ModelState.IsValid) throw new BizException("Dữ liệu không hợp lệ (vai trò / cấp bậc / nhóm)");
        var p = await Db.People.FirstOrDefaultAsync(q => q.Id == x.Id && !q.IsDeleted) ?? throw new BizException("Không tìm thấy tài khoản");
        // Không nâng tài khoản chưa có mật khẩu lên Quản trị / TP (ai đăng nhập trước sẽ đặt mật khẩu và chiếm quyền).
        if (x.Role is Role.Admin or Role.Manager && p.PasswordHash is null)
            throw new BizException("Tài khoản chưa có mật khẩu — đặt mật khẩu trước khi đổi sang Quản trị / Trưởng phòng");
        var wasLeader = p.Role == Role.Leader;
        await ApplyAsync(p, x);
        // Leader đổi sang vai trò khác → NV trong nhóm thành "Chưa xếp nhóm".
        if (wasLeader && p.Role != Role.Leader) await AccountRules.ClearTeamAsync(Db, p.Id);
        await Db.SaveChangesAsync();
    }, "Đã lưu tài khoản");

    /// <summary>Đặt mật khẩu trực tiếp; clear=true → xóa mật khẩu để người dùng tự đặt ở lần đăng nhập tới.</summary>
    [HttpPost]
    public Task<IActionResult> SetPassword(int id, string? password, string? confirm, bool clear = false) => Do(async () =>
    {
        RequireAdmin();
        var p = await Db.People.FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted) ?? throw new BizException("Không tìm thấy tài khoản");
        if (clear)
        {
            if (p.Id == Scope.Account.Id) throw new BizException("Không đặt lại mật khẩu của chính mình ở đây — đổi ở trang Cài đặt");
            AccountRules.SetPassword(p, AccountRules.DefaultPassword, hasher);
        }
        else
        {
            AccountRules.ValidatePassword(password);
            if (password != confirm) throw new BizException("Mật khẩu nhập lại không khớp");
            AccountRules.SetPassword(p, password, hasher);
        }
        await Db.SaveChangesAsync();
        Toast(clear ? $"Đã đặt lại mật khẩu của {p.Code} về mặc định ({AccountRules.DefaultPassword})" : "Đã đặt mật khẩu cho " + p.Code);
    });

    [HttpPost]
    public Task<IActionResult> Delete(int id) => Do(async () =>
    {
        RequireAdmin();
        if (id == Scope.Account.Id) throw new BizException("Không thể xóa tài khoản đang đăng nhập");
        var p = await Db.People.FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted) ?? throw new BizException("Không tìm thấy tài khoản");
        p.IsDeleted = true;
        p.SecurityStamp = AccountRules.NewStamp();
        Db.Assignments.RemoveRange(Db.Assignments.Where(a => a.EmployeeId == id));
        // Leader bị xóa → NV trong nhóm thành "Chưa xếp nhóm" (không còn gắn với người đã xóa — quy tắc 15).
        if (p.Role == Role.Leader) await AccountRules.ClearTeamAsync(Db, p.Id);
        await Db.SaveChangesAsync();
    }, "Đã xóa");

    [HttpPost]
    public Task<IActionResult> Restore(int id) => Do(async () =>
    {
        RequireAdmin();
        var p = await Db.People.FirstOrDefaultAsync(q => q.Id == id && q.IsDeleted) ?? throw new BizException("Không tìm thấy tài khoản");
        p.IsDeleted = false;
        p.SecurityStamp = AccountRules.NewStamp();
        await Db.SaveChangesAsync();
    }, "Đã khôi phục tài khoản");
}
