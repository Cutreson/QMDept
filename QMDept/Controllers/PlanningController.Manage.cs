using Microsoft.AspNetCore.Identity;
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
            Start = f.Start, End = f.End, Status = TaskState.NotStarted, Source = TaskSource.Planned, Selected = false, GivenBy = Scope.ActorName, GivenById = emp != Scope.Account.Id ? Scope.Account.Id : null,
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
    public async Task<IActionResult> SaveProject(int? id, string? name, string? factory, string? location, string? start, string? end, string? code, int? quantity,
        int? need, string? statusOverride, bool? closed = null, string? customerAddress = null)
    {
        int? created = null;
        var saved = false;   // sửa thông tin dự án thành công → thoát chế độ sửa (bỏ ?edit=1)
        var res = await Do(async () =>
        {
            RequireLead();
            var n = PlanningManage.Text(name, 200, "Tên dự án") ?? throw new BizException("Nhập tên dự án");
            var fa = PlanningManage.Text(factory, 200, "Khách hàng");
            var lo = PlanningManage.Text(location, 200, "Địa điểm");
            var ca = PlanningManage.Text(customerAddress, 300, "Địa chỉ khách hàng");
            var cd = PlanningManage.Text(code, 80, "Mã dự án");
            var qt = quantity is int q0 && q0 >= 0 ? q0 : (int?)null;
            var s = PlanningManage.Date(start);
            var e = PlanningManage.Date(end);
            if (s is DateOnly s0 && e is DateOnly e0 && e0 < s0) throw new BizException("Ngày kết thúc phải sau ngày bắt đầu");
            var ov = PlanningManage.ParseEnum(statusOverride, PlanningManage.OverrideStates);
            // Form không còn ô "Nhu cầu nhân sự/ngày" (yêu cầu 2026-10-21): sửa dự án giữ nguyên Need, dự án mới mặc định 2; có gửi lên thì vẫn nhận.
            int? k = need is int n0 ? Math.Max(0, n0) : null;
            if (id is int pid)
            {
                var p = await Db.Projects.FirstOrDefaultAsync(x => x.Id == pid) ?? throw new BizException("Không tìm thấy dự án");
                p.Code = cd; p.Quantity = qt;
                p.Name = n; p.Factory = fa; p.Location = lo;
                if (customerAddress is not null) p.CustomerAddress = ca;   // form không còn ô này (2026-10-21): không ghi đè dữ liệu cũ
                p.StartDate = s; p.EndDate = e; p.Need = k ?? p.Need;
                // Form không còn ô Trạng thái / Đã bàn giao (yêu cầu 2026-10-21: tích hết đầu mục = dự án Hoàn thành): không gửi lên thì giữ nguyên
                if (statusOverride is not null) p.StatusOverride = ov;
                if (closed is bool cl) p.Closed = cl;
                await Db.SaveChangesAsync();
                Toast("Đã lưu dự án");
                saved = true;
            }
            else
            {
                var p = new Project
                {
                    Code = cd, Quantity = qt, Name = n, Factory = fa, Location = lo, CustomerAddress = ca, StartDate = s, EndDate = e, Need = k ?? 2, StatusOverride = ov, Closed = false,
                    SortOrder = await Db.Projects.CountAsync() + 1, CreatedAt = VnTime.UtcNow,
                };
                Db.Projects.Add(p);
                await Db.SaveChangesAsync();
                created = p.Id;
                Toast("Đã tạo dự án " + n);
            }
        });
        return created is int nid ? Redirect(PlanningManage.WithQuery(BackUrl(), "pj", nid.ToString())) : saved ? Redirect(OvertimeUi.WithoutParam(BackUrl(), "edit")) : res;
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

    /// <summary>POST itemsForm (chế độ Sửa): lưu thời gian bắt đầu / kết thúc của từng đầu mục trong một lần (danh sách id / start / end song song theo thứ tự). Kiểm tra hết rồi mới ghi.</summary>
    [HttpPost]
    public Task<IActionResult> SaveItems(int projectId, List<int>? id, List<string?>? start, List<string?>? end) => Do(async () =>
    {
        RequireLead();
        var ids = id ?? [];
        if (ids.Count == 0) throw new BizException("Không có đầu mục để lưu");
        var items = await Db.PlanItems.Where(i => i.ProjectId == projectId).ToListAsync();
        var changes = new List<(PlanItem Item, DateOnly? S, DateOnly? E)>();
        for (var k = 0; k < ids.Count; k++)
        {
            var it = items.Find(x => x.Id == ids[k]) ?? throw new BizException("Đầu mục không thuộc dự án này — tải lại trang");
            var s = PlanningManage.Date(start is not null && k < start.Count ? start[k] : null);
            var e = PlanningManage.Date(end is not null && k < end.Count ? end[k] : null);
            if (s is DateOnly s0 && e is DateOnly e0 && e0 < s0) throw new BizException($"Đầu mục “{it.Title}”: ngày kết thúc phải sau ngày bắt đầu");
            changes.Add((it, s, e));
        }
        foreach (var (it, s, e) in changes) { it.StartDate = s; it.EndDate = e; }
        await Db.SaveChangesAsync();
    }, "Đã lưu thời gian các đầu mục", OvertimeUi.WithoutParam(BackUrl(), "ei"));

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

    IPasswordHasher<Person> Hasher => HttpContext.RequestServices.GetRequiredService<IPasswordHasher<Person>>();

    /// <summary>POST personForm (chỉ TP): tạo tài khoản mới (mật khẩu mặc định).</summary>
    [HttpPost]
    public Task<IActionResult> CreateAccount(string? name, string? code, string? role, int? leaderId, string? level, string? password) => Do(async () =>
    {
        RequireManager();
        // Mã lưu chữ thường theo quy tắc chủ dự án (AccountRules.NormalizeCode; artifact dùng chữ HOA) — docs/nghiep-vu.md §1.
        var n = PlanningManage.Text(name, 100, "Họ tên");
        var c = AccountRules.NormalizeCode(code);
        if (c.Length == 0) c = AccountRules.SuggestCode(n); // để trống → theo quy tắc tên + chữ cái đầu họ đệm
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
        var np = new Person
        {
            Name = n, Code = c, Role = r, LeaderId = lid, Level = lvl, SortOrder = await Db.People.CountAsync() + 1,
            CreatedAt = VnTime.UtcNow,
        };
        var pw = string.IsNullOrEmpty(password) ? AccountRules.DefaultPassword : password;
        AccountRules.ValidatePassword(pw);
        AccountRules.SetPassword(np, pw, Hasher);
        Db.People.Add(np);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new BizException("Mã nhân viên đã tồn tại"); }
        Toast($"Đã tạo tài khoản {c}");
    });

    /// <summary>POST openPerson (chỉ TP): sửa họ tên, mã, vai trò, nhóm, cấp bậc; đặt lại mật khẩu.</summary>
    [HttpPost]
    public Task<IActionResult> EditAccount(int id, string? name, string? code, string? role, int? leaderId, string? level, bool resetPassword, bool hrDuty = false) => Do(async () =>
    {
        RequireManager();
        var p = await Db.People.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.Role != Role.Admin) ?? throw new BizException("Không tìm thấy tài khoản");
        // TP không được sửa / hạ vai trò / đặt lại mật khẩu tài khoản TP khác (sẽ chiếm được tài khoản đó) — việc này của Quản trị (/Admin/Accounts).
        if (p.Role == Role.Manager && id != Scope.Account.Id && !Scope.IsAdmin) throw new BizException("Tài khoản Trưởng phòng chỉ Quản trị sửa được");
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
        if (resetPassword || codeChanged) AccountRules.SetPassword(p, AccountRules.DefaultPassword, Hasher); // về mật khẩu mặc định
        if (resetPassword || codeChanged || roleChanged) p.SecurityStamp = AccountRules.NewStamp();
        p.Name = n; p.Code = c; p.Role = r; p.LeaderId = lid; p.Level = lvl;
        p.HrDuty = r == Role.Leader && hrDuty;   // chỉ Leader: được giao quản lý nhân sự / báo cơm / báo làm thêm của cả phòng
        // Leader đổi sang vai trò khác → NV trong nhóm thành "Chưa xếp nhóm" (như AdminController.Edit).
        if (wasLeader && r != Role.Leader) await AccountRules.ClearTeamAsync(Db, id);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException) { throw new BizException("Mã nhân viên đã tồn tại"); }
    }, "Đã lưu tài khoản");

    /// <summary>
    /// POST /Planning/MoveToTeam (chỉ TP / Quản trị) — thay đổi sơ đồ nhân sự: chuyển một nhân viên sang nhóm của Leader khác (leaderId trống = chưa xếp nhóm).
    /// Nhân viên tự vào đúng nhóm (Leader mới duyệt / thấy kế hoạch, báo cáo của người đó); NV, Leader cũ và mới nhận thông báo (yêu cầu 2026-10-19).
    /// </summary>
    [HttpPost]
    public Task<IActionResult> MoveToTeam(int emp, int? leaderId) => Do(async () =>
    {
        RequireManager();
        var p = await Db.People.FirstOrDefaultAsync(x => x.Id == emp && !x.IsDeleted && x.Role == Role.Staff) ?? throw new BizException("Không tìm thấy nhân viên");
        Person? nl = null;
        if (leaderId is int l)
            nl = await Db.People.AsNoTracking().FirstOrDefaultAsync(x => x.Id == l && x.Role == Role.Leader && !x.IsDeleted) ?? throw new BizException("Không tìm thấy Leader của nhóm");
        if (p.LeaderId == nl?.Id) { Toast($"{p.Name} đã ở nhóm này"); return; }
        var old = p.LeaderId;
        p.LeaderId = nl?.Id;
        await Db.SaveChangesAsync();
        await Notify.TeamChangedAsync(p.Id, old, nl?.Id);
        Toast(nl is null ? $"Đã đưa {p.Name} về “Chưa xếp nhóm”" : $"Đã chuyển {p.Name} sang nhóm {nl.Name}");
    });

    /// <summary>POST delPerson (chỉ TP): xóa mềm tài khoản (lịch sử giữ nguyên), bỏ các phân công dự án.</summary>
    [HttpPost]
    public Task<IActionResult> DeleteAccount(int id) => Do(async () =>
    {
        RequireManager();
        if (id == Scope.Account.Id) throw new BizException("Không thể xóa tài khoản đang đăng nhập");
        var p = await Db.People.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.Role != Role.Admin) ?? throw new BizException("Không tìm thấy tài khoản");
        if (p.Role == Role.Manager && !Scope.IsAdmin) throw new BizException("Tài khoản Trưởng phòng chỉ Quản trị xóa được");
        p.IsDeleted = true;
        p.SecurityStamp = AccountRules.NewStamp();
        Db.Assignments.RemoveRange(await Db.Assignments.Where(a => a.EmployeeId == id).ToListAsync());
        // Leader bị xóa → NV trong nhóm thành "Chưa xếp nhóm" (không còn gắn với người đã xóa — quy tắc 15).
        if (p.Role == Role.Leader) await AccountRules.ClearTeamAsync(Db, p.Id);
        await Db.SaveChangesAsync();
    }, "Đã xóa");

    // ======================= Leader dự án (yêu cầu 2026-10-14) =======================

    /// <summary>GET /Planning/ProjectLeads — chọn người làm Leader dự án cho từng dự án đang triển khai (NV chỉ thấy dự án mình là Leader dự án).</summary>
    [HttpGet]
    public async Task<IActionResult> ProjectLeads(string? f)
    {
        await D.EnsureAsync(Date, Date);
        ViewData["Title"] = "Leader dự án";
        // Chỉ Leader chọn Leader dự án (TP / Quản trị chỉ chọn Leader phụ trách dự án). Leader thấy dự án do mình phụ trách + dự án chưa có Leader phụ trách.
        var projects = D.AllPj().Where(p => !p.IsCommon && (p.OwnerLeaderId is null || p.OwnerLeaderId == Scope.MeId)).ToList();   // gồm cả dự án đã hoàn thành (vẫn phải triển khai khi khách hàng có sự cố)
        // Chia 2 mục: đã có Leader dự án (f=has) / chưa chọn Leader dự án (f=none); mặc định mục đầu có dự án (đã có → chưa chọn).
        var has = projects.Where(p => D.ProjectLead(p.Id) is not null).ToList();
        var none = projects.Where(p => D.ProjectLead(p.Id) is null).ToList();
        var filter = f == "has" || f == "none" ? f : has.Count > 0 ? "has" : "none";
        return View(new PlanningLeadsVm { Projects = filter == "has" ? has : none, Staff = D.Staff(), Filter = filter, HasCount = has.Count, NoneCount = none.Count, AllProjects = projects });
    }

    /// <summary>POST: chọn (hoặc bỏ chọn khi emp trống) Leader dự án của một dự án. Mỗi dự án một Leader dự án; người cũ về "Hỗ trợ".</summary>
    [HttpPost]
    public Task<IActionResult> SetProjectLead(int projectId, int? emp) => Do(async () =>
    {
        RequireLead();
        await D.EnsureAsync(Date, Date);
        var pj = await Db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId) ?? throw new BizException("Không tìm thấy dự án");
        if (pj.OwnerLeaderId is int ow && ow != Scope.MeId) throw new BizException("Dự án này do Leader nhóm khác phụ trách");
        var olds = await Db.Assignments.Where(a => a.ProjectId == projectId && a.Role == ProjectRole.Lead).ToListAsync();
        if (emp is int e)
        {
            await RequireInScopeAsync(e);
            var who = PlanningManage.RequireStaff(D, e);
            if (olds.Any(a => a.EmployeeId == e)) { Toast($"{who.Name} đang là Leader dự án {pj.Name}"); return; }
            await Flow.UpsertAssignmentAsync(projectId, e, ProjectRole.Lead, Scope.ActorName, keepNote: true);
            await Db.SaveChangesAsync();
            await Notify.ProjectLeadAsync(e, pj.Name, chosen: true);
            foreach (var o in olds) await Notify.ProjectLeadAsync(o.EmployeeId, pj.Name, chosen: false);
            Toast($"Đã chọn {who.Name} làm Leader dự án {pj.Name}");
        }
        else
        {
            foreach (var o in olds) { o.Role = ProjectRole.Support; o.By = Scope.ActorName; o.At = VnTime.UtcNow; }
            await Db.SaveChangesAsync();
            foreach (var o in olds) await Notify.ProjectLeadAsync(o.EmployeeId, pj.Name, chosen: false);
            Toast($"Đã bỏ chọn Leader dự án {pj.Name}");
        }
    });

    // ======================= Đề xuất lịch của NV (yêu cầu 2026-10-15) =======================

    /// <summary>
    /// GET /Planning/Proposals?k=work|ot&f=pending|approved — "Kế hoạch đề xuất" (yêu cầu 2026-10-19): kế hoạch đầu ngày của NV + phát sinh + đề xuất lịch (thêm việc / người hỗ trợ)
    /// + đăng ký làm thêm (kèm kết quả), chia 2 mục riêng Chờ duyệt / Đã duyệt.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Proposals(string? k, string? f)
    {
        await D.EnsureAsync(Date, Date);
        ViewData["Title"] = "Kế hoạch đề xuất";
        f = f == "approved" ? "approved" : "pending";
        k = k == "ot" ? "ot" : "work";   // 2 mục chính: work = Đề xuất công việc; ot = Đề xuất làm thêm tại công ty (yêu cầu 2026-10-19)
        var ids = D.Staff().Select(p => p.Id).ToList();
        var b = PlanningApprovalsVm.Build(D, Date, null, null);
        var pendPlans = b.Filtered.Where(p => p.LeaderDecision is null).OrderBy(p => D.PName(p.EmployeeId), Fmt.ViCompare).ToList();
        var donePlans = b.Filtered.Where(p => p.LeaderDecision is not null).OrderBy(p => D.PName(p.EmployeeId), Fmt.ViCompare).ToList();
        var pendProps = await Db.CalendarProposals.AsNoTracking().Include(p => p.People)
            .Where(p => ids.Contains(p.ProposerId) && p.Status == ExtraStatus.Pending).OrderBy(p => p.Date).ThenBy(p => p.CreatedAt).ToListAsync();
        var doneProps = await Db.CalendarProposals.AsNoTracking().Include(p => p.People)
            .Where(p => ids.Contains(p.ProposerId) && p.Status != ExtraStatus.Pending).OrderByDescending(p => p.DecidedAt).Take(15).ToListAsync();
        var ots = D.OtPending(Date).OrderBy(x => D.PName(x.PersonId), Fmt.ViCompare).ToList();
        var pendOts = ots.Where(x => x.NeedsDecision).ToList();
        var doneOts = ots.Where(x => !x.NeedsDecision).ToList();
        var pend = f == "pending";
        var work = k == "work";
        int workPend = pendPlans.Count + b.PendingExtras.Count + pendProps.Count, workDone = donePlans.Count + doneProps.Count;
        return View(new PlanningInboxVm
        {
            Kind = k, Filter = f,
            Plans = work ? (pend ? pendPlans : donePlans) : [],
            NotSent = work && pend ? b.NotSent : [],
            Extras = work && pend ? b.PendingExtras : [],
            Proposals = work ? (pend ? pendProps : doneProps) : [],
            Ots = work ? [] : (pend ? pendOts : doneOts),
            WorkPending = workPend, OtPending = pendOts.Count,
            PendingTotal = work ? workPend : pendOts.Count,
            ApprovedTotal = work ? workDone : doneOts.Count,
        });
    }

    /// <summary>
    /// GET /Planning/Leave — Leader → Kế hoạch & duyệt → "Xin nghỉ" (yêu cầu 2026-10-20: tách riêng khỏi đề xuất công việc): xin nghỉ của nhân viên trong nhóm,
    /// 2 mục Chờ duyệt / Đã duyệt (?f=pending|approved). Duyệt → ghi Nghỉ vào lịch; từ chối bắt buộc lý do (POST /Planning/DecideLeave).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Leave(string? f)
    {
        await D.EnsureAsync(Date, Date);
        ViewData["Title"] = "Xin nghỉ";
        var ids = D.Staff().Select(p => p.Id).ToList();
        // Lọc theo NGÀY NGHỈ đang chọn (thanh ngày như Kế hoạch đề xuất / Báo cáo — yêu cầu 2026-10-21); đề xuất chờ duyệt ở ngày khác hiện thành dòng gợi ý có liên kết.
        var pendAll = D.LeavesFor(Scope.Account.Id);
        var pend = pendAll.Where(x => x.Date == Date).ToList();
        var done = await Db.LeaveRequests.AsNoTracking().Where(r => ids.Contains(r.PersonId) && r.Status != ExtraStatus.Pending && r.Date == Date)
            .OrderByDescending(r => r.DecidedAt).ToListAsync();
        var pending = f != "approved";
        var other = pendAll.Where(x => x.Date != Date).GroupBy(x => x.Date).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())).ToList();
        return View(new PlanningLeaveVm { Filter = pending ? "pending" : "approved", Requests = pending ? pend : done, PendingTotal = pend.Count, ApprovedTotal = done.Count, OtherPending = other });
    }

    /// <summary>POST /Planning/DecideLeave — Leader duyệt (v=ok → ghi Nghỉ vào lịch) / từ chối (bắt buộc lý do) đề xuất xin nghỉ của NV trong nhóm.</summary>
    [HttpPost]
    public Task<IActionResult> DecideLeave(int id, string? v, string? note) => Do(async () =>
    {
        RequireLead();
        var x = await Db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id) ?? throw new BizException("Không tìm thấy đề xuất xin nghỉ");
        if (x.Role != Role.Staff) throw new BizException("Leader chỉ duyệt xin nghỉ của nhân viên");
        await D.EnsureAsync(x.Date, x.Date);
        if (D.P(x.PersonId) is not { IsDeleted: false, Role: Role.Staff } p || p.LeaderId != Scope.Account.Id) throw new BizException("Nhân viên không thuộc nhóm bạn quản lý");
        var approve = v switch { "ok" => true, "no" => false, _ => throw new BizException("Thao tác không hợp lệ") };
        await Flow.DecideLeaveAsync(id, approve, note, Scope.Account);
        Toast(approve ? "Đã duyệt xin nghỉ — đã ghi vào lịch" : "Đã từ chối xin nghỉ");
    });

    /// <summary>POST: duyệt (v=ok → tạo việc vào lịch NV + lịch nhóm) / từ chối (v=no, bắt buộc lý do) một đề xuất lịch.</summary>
    [HttpPost]
    public Task<IActionResult> DecideProposal(int id, string? v, string? note) => Do(async () =>
    {
        RequireLead();
        var approve = v switch { "ok" => true, "no" => false, _ => throw new BizException("Thao tác không hợp lệ") };
        await Flow.DecideProposalAsync(id, approve, note, Scope.Account);
        Toast(approve ? "Đã duyệt đề xuất — việc đã vào lịch" : "Đã từ chối đề xuất");
    });

    /// <summary>GET /Planning/ProjectOwners — TP / Quản trị chọn Leader (nhóm) phụ trách từng dự án (?f=has|none: đã có / chưa có Leader phụ trách).</summary>
    [HttpGet]
    public async Task<IActionResult> ProjectOwners(string? f)
    {
        if (!Scope.IsManager) return Redirect("/");
        await D.EnsureAsync(Date, Date);
        ViewData["Title"] = "Leader phụ trách dự án";
        var all = D.AllPj().Where(p => !p.IsCommon).ToList();   // gồm cả dự án đã bàn giao (yêu cầu 2026-10-21: dự án đã hoàn thành cũng phải có Leader phụ trách)
        var has = all.Where(p => p.OwnerLeaderId is not null).ToList();
        var none = all.Where(p => p.OwnerLeaderId is null).ToList();
        // 2 mục: Đã có Leader / Chưa có Leader (thay "Tất cả"); mặc định mục đầu tiên có dự án
        f = f is "has" or "none" ? f : has.Count > 0 ? "has" : "none";
        return View(new PlanningOwnersVm
        {
            Projects = f == "has" ? has : none, Leaders = D.Leaders(), Filter = f,
            Total = all.Count, Unassigned = none.Count, AllProjects = all,
            Stats = D.Leaders().Select(l => (l, has.Where(p => p.OwnerLeaderId == l.Id).ToList())).ToList(),
        });
    }

    /// <summary>POST: chọn (hoặc bỏ chọn khi leaderId trống) Leader nhóm phụ trách một dự án. Người được chọn / người cũ nhận thông báo.</summary>
    [HttpPost]
    public Task<IActionResult> SetProjectOwner(int projectId, int? leaderId) => Do(async () =>
    {
        RequireManager();
        var pj = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId) ?? throw new BizException("Không tìm thấy dự án");
        if (leaderId is int lid && !await Db.People.AnyAsync(p => p.Id == lid && p.Role == Role.Leader && !p.IsDeleted)) throw new BizException("Leader không hợp lệ");
        var old = pj.OwnerLeaderId;
        if (old == leaderId) { Toast("Không có thay đổi"); return; }
        pj.OwnerLeaderId = leaderId;
        await Db.SaveChangesAsync();
        await D.EnsureAsync(Date, Date);
        if (leaderId is int nl) await Notify.ProjectOwnerAsync(nl, pj.Name, assigned: true);
        if (old is int ol) await Notify.ProjectOwnerAsync(ol, pj.Name, assigned: false);
        Toast(leaderId is int n2 ? $"Đã giao dự án {pj.Name} cho nhóm {D.PName(n2)} phụ trách" : $"Đã bỏ Leader phụ trách dự án {pj.Name}");
    });

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

    /// <summary>POST assignForm: giao một việc cho một NV vào ngày chọn (vai trò theo dự án: Leader dự án đã chọn hoặc hỗ trợ).</summary>
    [HttpPost]
    public Task<IActionResult> AssignTask(int? emp, string? date, int? projectId, int? itemId, string? title, string? start, string? end) => Do(async () =>
    {
        RequireLead();
        var d = PlanningManage.Date(date);
        if (emp is not int e || projectId is not int pid || d is not DateOnly day) throw new BizException("Chọn nhân viên, ngày và dự án");
        await RequireInScopeAsync(e);
        var who = PlanningManage.RequireStaff(D, e);
        PlanningManage.Text(title, 300, "Nội dung công việc");
        await Flow.AssignTaskAsync(day, pid, itemId, title, start ?? "", end ?? "", [e], Scope.ActorName, TaskSource.Assigned, Scope.Account.Id);
        Toast($"Đã giao việc cho {who.Name} ngày {Fmt.Dm(day)}");
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
