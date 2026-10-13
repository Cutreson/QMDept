using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Leader → Kế hoạch &amp; duyệt: các tab con quản lý — Lịch tuần (ldWeek), Dự án &amp; đầu mục (ldItems, projForm),
/// Nhân sự (ldPeople, openPerson) và Phân công (ldAssign). Mọi POST kiểm quyền Leader; quản lý tài khoản chỉ TP.
/// </summary>
public partial class PlanningController
{
    // ======================= Lịch tuần (ldWeek) =======================

    /// <summary>GET /Planning/Week?w=&amp;emp= — bảng NV × 7 ngày, việc từng ngày của NV đang chọn, form thêm việc (wkForm).</summary>
    [HttpGet]
    public async Task<IActionResult> Week(int? emp)
    {
        var m = Nav.Week(Request, Date);
        await D.EnsureAsync(m, m.AddDays(6));
        await D.EnsureAsync(Date, Date);
        var staff = D.Staff();
        ViewData["Title"] = "Lịch tuần";
        return View(new PlanningWeekVm
        {
            Monday = m,
            Days = Enumerable.Range(0, 7).Select(k => m.AddDays(k)).ToList(),
            Staff = staff,
            Emp = staff.Find(p => p.Id == emp) ?? staff.FirstOrDefault(),
        });
    }

    /// <summary>POST wkForm: thêm việc "Kế hoạch" (chưa chọn) vào lịch tuần của NV.</summary>
    [HttpPost]
    public Task<IActionResult> AddWeekTask(int emp, TaskFieldsInput f) => Do(async () =>
    {
        RequireLead();
        await RequireInScopeAsync(emp);
        PlanningManage.RequireStaff(D, emp);
        var (title, item) = await f.ResolveAsync(Db);
        PlanningManage.Text(title, 300, "Nội dung công việc");
        var date = PlanningManage.Date(f.Date) ?? throw new BizException("Chọn ngày");
        Db.WorkTasks.Add(new WorkTask
        {
            Date = date, EmployeeId = emp, ProjectId = f.ProjectId, PlanItemId = item?.Id, Title = title,
            Start = f.Start, End = f.End, Status = TaskState.NotStarted, Source = TaskSource.Planned, Selected = false,
            CreatedAt = VnTime.UtcNow,
        });
        await Db.SaveChangesAsync();
        await Notify.TaskAssignedAsync([emp], date, title, "Việc mới trong lịch");
    }, "Đã thêm vào lịch tuần");

    /// <summary>POST delTask: xóa việc khỏi lịch (Lịch tuần, Việc đã giao gần đây).</summary>
    [HttpPost]
    public Task<IActionResult> DeleteTask(int id) => Do(async () =>
    {
        RequireLead();
        var t = await Db.WorkTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy công việc");
        await RequireInScopeAsync(t.EmployeeId);
        await Flow.DeleteTaskAsync(id);
        await Notify.TaskAssignedAsync([t.EmployeeId], t.Date, t.Title, "Việc bị xóa khỏi lịch");
    }, "Đã xóa");

    // ======================= Dự án & đầu mục (ldItems) =======================

    /// <summary>GET /Planning/Projects?pj=id|new — chip dự án, thông tin dự án (projForm), bảng đầu mục + thêm đầu mục.</summary>
    [HttpGet]
    public async Task<IActionResult> Projects(string? pj)
    {
        await D.EnsureAsync(Date, Date);
        var ps = D.AllPj();
        var key = string.IsNullOrEmpty(pj) ? ps.FirstOrDefault()?.Id.ToString() : pj;
        var p = key != "new" && int.TryParse(key, out var id) ? D.PJ(id) : null;
        ViewData["Title"] = "Dự án & đầu mục";
        return View(new PlanningProjectsVm { Projects = ps, Key = p?.Id.ToString() ?? key, Project = p });
    }

    /// <summary>POST projForm: tạo dự án mới (→ chuyển sang chip dự án vừa tạo) hoặc lưu thông tin dự án.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveProject(int? id, string? name, string? factory, string? location, string? start, string? end,
        int? need, string? statusOverride, bool closed)
    {
        int? created = null;
        var res = await Do(async () =>
        {
            RequireLead();
            var n = PlanningManage.Text(name, 200, "Tên dự án") ?? throw new BizException("Nhập tên dự án");
            var fa = PlanningManage.Text(factory, 200, "Nhà máy");
            var lo = PlanningManage.Text(location, 200, "Địa điểm");
            var s = PlanningManage.Date(start);
            var e = PlanningManage.Date(end);
            if (s is DateOnly s0 && e is DateOnly e0 && e0 < s0) throw new BizException("Ngày kết thúc phải sau ngày bắt đầu");
            var ov = PlanningManage.ParseEnum(statusOverride, PlanningManage.OverrideStates);
            var k = Math.Max(0, need ?? 0);
            if (id is int pid)
            {
                var p = await Db.Projects.FirstOrDefaultAsync(x => x.Id == pid) ?? throw new BizException("Không tìm thấy dự án");
                p.Name = n; p.Factory = fa; p.Location = lo; p.StartDate = s; p.EndDate = e; p.Need = k; p.StatusOverride = ov; p.Closed = closed;
                await Db.SaveChangesAsync();
                Toast("Đã lưu dự án");
            }
            else
            {
                var p = new Project
                {
                    Name = n, Factory = fa, Location = lo, StartDate = s, EndDate = e, Need = k, StatusOverride = ov, Closed = false,
                    SortOrder = await Db.Projects.CountAsync() + 1, CreatedAt = VnTime.UtcNow,
                };
                Db.Projects.Add(p);
                await Db.SaveChangesAsync();
                created = p.Id;
                Toast("Đã tạo dự án " + n);
            }
        });
        return created is int nid ? Redirect(PlanningManage.WithQuery(BackUrl(), "pj", nid.ToString())) : res;
    }

    /// <summary>POST itemForm: thêm đầu mục kế hoạch vào cuối danh sách của dự án.</summary>
    [HttpPost]
    public Task<IActionResult> AddItem(int projectId, string? title, string? start, string? end) => Do(async () =>
    {
        RequireLead();
        if (!await Db.Projects.AnyAsync(p => p.Id == projectId)) throw new BizException("Không tìm thấy dự án");
        var t = PlanningManage.Text(title, 300, "Tên đầu mục") ?? throw new BizException("Nhập tên đầu mục");
        var s = PlanningManage.Date(start);
        var e = PlanningManage.Date(end);
        if (s is DateOnly s0 && e is DateOnly e0 && e0 < s0) throw new BizException("Ngày kết thúc phải sau ngày bắt đầu");
        var max = await Db.PlanItems.Where(i => i.ProjectId == projectId).MaxAsync(i => (int?)i.SortOrder) ?? 0;
        Db.PlanItems.Add(new PlanItem
        {
            ProjectId = projectId, Title = t, StartDate = s, EndDate = e, SortOrder = Math.Max(0, max) + 1, Status = TaskState.NotStarted,
        });
        await Db.SaveChangesAsync();
    }, "Đã thêm đầu mục");

    /// <summary>POST itemSt: đổi trạng thái đầu mục (Hoàn thành → ghi ngày xong / người xong).</summary>
    [HttpPost]
    public Task<IActionResult> SetItemStatus(int id, string? status) => Do(async () =>
    {
        RequireLead();
        var st = PlanningManage.ParseEnum(status, PlanningManage.ItemStates) ?? throw new BizException("Trạng thái không hợp lệ");
        var i = await Db.PlanItems.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy đầu mục");
        i.Status = st;
        if (st == TaskState.Done) { i.DoneDate = Date; i.DoneBy = Scope.ActorName; }
        else { i.DoneDate = null; i.DoneBy = null; }
        await Db.SaveChangesAsync();
    }, "Đã cập nhật đầu mục");

    /// <summary>POST delItem: xóa đầu mục (việc gắn đầu mục được DB đặt PlanItemId = null).</summary>
    [HttpPost]
    public Task<IActionResult> DeleteItem(int id) => Do(async () =>
    {
        RequireLead();
        var i = await Db.PlanItems.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy đầu mục");
        Db.PlanItems.Remove(i);
        await Db.SaveChangesAsync();
    }, "Đã xóa đầu mục");

    // ======================= Nhân sự (ldPeople, openPerson) =======================

    /// <summary>GET /Planning/People — Leader: nhóm mình + Báo nghỉ; TP: mọi tài khoản + Sửa / Tạo tài khoản.</summary>
    [HttpGet]
    public async Task<IActionResult> People()
    {
        await D.EnsureAsync(Date, Date);
        var isManager = Scope.Role == Role.Manager; // giao diện đang xem (TP tự xem, hoặc Quản trị xem màn hình TP)
        var leader = D.P(Scope.MeId);
        var src = isManager ? D.Active.Where(p => p.Role != Role.Admin) : new[] { leader }.OfType<Person>().Concat(D.Staff());
        ViewData["Title"] = "Nhân sự";
        return View(new PlanningPeopleVm { IsManager = isManager, People = PlanningManage.SortPeople(D, src), Leader = leader });
    }

    /// <summary>POST leave: ghi nghỉ (on=true, kèm lý do) / bỏ nghỉ (on=false) của NV trong ngày đang chọn.
    /// Nút gửi rõ ý định để trang cũ (không realtime như artifact) không đảo ngược trạng thái; thiếu on → bật/tắt như artifact.</summary>
    [HttpPost]
    public Task<IActionResult> ToggleLeave(int emp, string? reason, bool? on) => Do(async () =>
    {
        RequireLead();
        // TP thấy mọi NV ở danh sách (artifact) → không giới hạn theo nhóm đang xem.
        if (Scope.IsManager) await D.EnsureAsync(Date, Date);
        else await RequireInScopeAsync(emp);
        PlanningManage.RequireStaff(D, emp);
        var leave = await Db.Leaves.FirstOrDefaultAsync(x => x.Date == Date && x.EmployeeId == emp);
        var set = on ?? leave is null;
        if (!set)
        {
            if (leave is not null) { Db.Leaves.Remove(leave); await Db.SaveChangesAsync(); await Notify.LeaveMarkedAsync(emp, Date, false); }
            Toast("Đã bỏ nghỉ");
            return;
        }
        var r = PlanningManage.Text(reason, 500, "Lý do") ?? "";
        if (leave is null) Db.Leaves.Add(new Leave { Date = Date, EmployeeId = emp, Reason = r });
        else leave.Reason = r;
        await Db.SaveChangesAsync();
        await Notify.LeaveMarkedAsync(emp, Date, true, r);
        Toast("Đã ghi nghỉ");
    });

    /// <summary>POST personForm (chỉ TP): tạo tài khoản mới (chưa có mật khẩu).</summary>
    [HttpPost]
    public Task<IActionResult> CreateAccount(string? name, string? code, string? role, int? leaderId, string? level) => Do(async () =>
    {
        RequireManager();
        // Mã lưu chữ thường theo quy tắc chủ dự án (AccountRules.NormalizeCode; artifact dùng chữ HOA) — docs/nghiep-vu.md §1.
        var n = PlanningManage.Text(name, 100, "Họ tên");
        var c = AccountRules.NormalizeCode(code);
        if (n is null || c.Length == 0) throw new BizException("Nhập họ tên và mã nhân viên");
        await AccountRules.ValidateCodeAsync(Db, c);
        var r = PlanningManage.ParseEnum(role, PlanningManage.Roles) ?? throw new BizException("Chọn vai trò");
        // Tài khoản ở đây tạo không mật khẩu (người dùng tự đặt lần đầu) → không cho tạo TP theo cách này (tránh bị chiếm quyền);
        // TP mới do Quản trị tạo kèm mật khẩu.
        if (r == Role.Manager) throw new BizException("Tài khoản Trưởng phòng do Quản trị tạo (phải đặt mật khẩu)");
        int? lid = null; Level? lvl = null;
        if (r == Role.Staff)
        {
            if (leaderId is int l && !await Db.People.AnyAsync(x => x.Id == l && x.Role == Role.Leader && !x.IsDeleted))
                throw new BizException("Không tìm thấy Leader của nhóm");
            lid = leaderId; lvl = PlanningManage.ParseEnum<Level>(level);
        }
        Db.People.Add(new Person
        {
            Name = n, Code = c, Role = r, LeaderId = lid, Level = lvl, SortOrder = await Db.People.CountAsync() + 1,
            CreatedAt = VnTime.UtcNow,
        });
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new BizException("Mã nhân viên đã tồn tại"); }
        Toast("Đã tạo tài khoản " + c);
    });

    /// <summary>POST openPerson (chỉ TP): sửa họ tên, mã, vai trò, nhóm, cấp bậc; đặt lại mật khẩu.</summary>
    [HttpPost]
    public Task<IActionResult> EditAccount(int id, string? name, string? code, string? role, int? leaderId, string? level, bool resetPassword) => Do(async () =>
    {
        RequireManager();
        var p = await Db.People.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.Role != Role.Admin) ?? throw new BizException("Không tìm thấy tài khoản");
        var n = PlanningManage.Text(name, 100, "Họ tên");
        var c = AccountRules.NormalizeCode(code);
        if (n is null || c.Length == 0) throw new BizException("Nhập họ tên và mã nhân viên");
        await AccountRules.ValidateCodeAsync(Db, c, id);
        var r = PlanningManage.ParseEnum(role, PlanningManage.Roles) ?? throw new BizException("Chọn vai trò");
        if (id == Scope.Account.Id && r != Role.Manager) throw new BizException("Không thể tự đổi vai trò của tài khoản đang đăng nhập");
        int? lid = null; Level? lvl = null;
        if (r == Role.Staff)
        {
            if (leaderId is int l && (l == id || !await Db.People.AnyAsync(x => x.Id == l && x.Role == Role.Leader && !x.IsDeleted)))
                throw new BizException("Không tìm thấy Leader của nhóm");
            lid = leaderId; lvl = PlanningManage.ParseEnum<Level>(level);
        }
        var codeChanged = c != AccountRules.NormalizeCode(p.Code);
        var roleChanged = r != p.Role;
        // Tài khoản TP không được ở trạng thái "chưa có mật khẩu" (ai đăng nhập trước sẽ chiếm quyền) — nhờ Quản trị đặt mật khẩu.
        if (r == Role.Manager && (resetPassword || codeChanged || p.PasswordHash is null))
            throw new BizException("Không xóa mật khẩu / đổi mã của tài khoản Trưởng phòng ở đây — nhờ Quản trị đặt mật khẩu");
        var wasLeader = p.Role == Role.Leader;
        if (resetPassword || codeChanged) p.PasswordHash = null;
        if (resetPassword || codeChanged || roleChanged) p.SecurityStamp = AccountRules.NewStamp();
        p.Name = n; p.Code = c; p.Role = r; p.LeaderId = lid; p.Level = lvl;
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new BizException("Mã nhân viên đã tồn tại"); }
        // Leader đổi sang vai trò khác → NV trong nhóm thành "Chưa xếp nhóm" (như AdminController.Edit).
        if (wasLeader && r != Role.Leader)
            await Db.People.Where(m => m.LeaderId == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.LeaderId, (int?)null));
    }, "Đã lưu tài khoản");

    /// <summary>POST delPerson (chỉ TP): xóa mềm tài khoản (lịch sử giữ nguyên), bỏ các phân công dự án.</summary>
    [HttpPost]
    public Task<IActionResult> DeleteAccount(int id) => Do(async () =>
    {
        RequireManager();
        if (id == Scope.Account.Id) throw new BizException("Không thể xóa tài khoản đang đăng nhập");
        var p = await Db.People.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.Role != Role.Admin) ?? throw new BizException("Không tìm thấy tài khoản");
        p.IsDeleted = true;
        p.SecurityStamp = AccountRules.NewStamp();
        Db.Assignments.RemoveRange(await Db.Assignments.Where(a => a.EmployeeId == id).ToListAsync());
        await Db.SaveChangesAsync();
    }, "Đã xóa");

    // ======================= Phân công (ldAssign) =======================

    /// <summary>GET /Planning/Assign — giao việc 1 NV, việc đã giao gần đây, phân công theo NV / theo dự án.</summary>
    [HttpGet]
    public async Task<IActionResult> Assign()
    {
        await D.EnsureAsync(Date, Date);
        var staff = D.Staff();
        // inScope(t.employee) của artifact: người đã xóa không còn trong S.people → không tính (D.People gồm cả người đã xóa).
        var ids = D.Active.Where(p => D.InScope(p.Id)).Select(p => p.Id).ToList();
        var from = Date.AddDays(-7);
        var given = await Db.WorkTasks.AsNoTracking()
            .Where(t => t.Source == TaskSource.Assigned && t.Date >= from && ids.Contains(t.EmployeeId))
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).Take(15).ToListAsync();
        ViewData["Title"] = "Phân công";
        return View(new PlanningAssignVm { Staff = staff, Given = given });
    }

    /// <summary>POST assignForm: giao một việc cho một NV vào ngày chọn + đặt vai trò trong dự án.</summary>
    [HttpPost]
    public Task<IActionResult> AssignTask(int? emp, string? date, int? projectId, int? itemId, string? title, string? start, string? end, string? role) => Do(async () =>
    {
        RequireLead();
        var d = PlanningManage.Date(date);
        if (emp is not int e || projectId is not int pid || d is not DateOnly day) throw new BizException("Chọn nhân viên, ngày và dự án");
        await RequireInScopeAsync(e);
        var who = PlanningManage.RequireStaff(D, e);
        PlanningManage.Text(title, 300, "Nội dung công việc");
        var r = PlanningManage.ParseEnum<ProjectRole>(role) ?? ProjectRole.Support;
        await Flow.AssignTaskAsync(day, pid, itemId, title, start ?? "", end ?? "", [(e, r)], Scope.ActorName);
        Toast($"Đã giao việc cho {who.Name} ngày {Fmt.Dm(day)} · {Labels.Of(r)}");
    });

    /// <summary>POST unassign: bỏ phân công NV khỏi dự án.</summary>
    [HttpPost]
    public Task<IActionResult> Unassign(int id) => Do(async () =>
    {
        RequireLead();
        var a = await Db.Assignments.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy phân công");
        await RequireInScopeAsync(a.EmployeeId);
        Db.Assignments.Remove(a);
        await Db.SaveChangesAsync();
    }, "Đã bỏ giao");
}
