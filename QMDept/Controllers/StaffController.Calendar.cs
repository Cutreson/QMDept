using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Giao diện Nhân viên — Kế hoạch tháng (nvMonth, teamDayList, calAssignForm) và Thông tin dự án (nvProjects, projectDetail).
/// Leader/TP xem các trang này ở chế độ chỉ xem; riêng chế độ "Tất cả nhân viên" có form Giao thêm việc (AssignTask).
/// </summary>
public partial class StaffController
{
    /// <summary>GET /Staff/Month — nvMonth. ?m= tháng đang xem, ?d= ngày chọn.</summary>
    [HttpGet]
    public async Task<IActionResult> Month()
    {
        var ms = Nav.Month(Request, Date);
        await D.EnsureAsync(ms, ms.AddDays(Fmt.DaysInMonth(ms) - 1));
        await D.EnsureAsync(Date, Date);
        var ro = Scope.IsReadOnly;
        // Đề xuất lịch của chính NV trong ngày đang chọn (để hiện trạng thái chờ duyệt / đã duyệt / từ chối).
        var props = Scope.Account.Role == Role.Staff && !Scope.IsViewingOther
            ? await Db.CalendarProposals.AsNoTracking().Include(p => p.People)
                .Where(p => p.ProposerId == Scope.Account.Id && p.Date == Date).OrderBy(p => p.CreatedAt).ToListAsync()
            : [];
        ViewData["Title"] = "Kế hoạch tháng";
        return View(new StaffMonthVm
        {
            MonthStart = ms,
            ReadOnly = ro,
            AllMode = ro && Scope.AllStaff,
            Me = Scope.Me,
            People = Scope.TeamPeople,
            Proposals = props,
        });
    }

    /// <summary>
    /// POST /Staff/AssignTask — submit của calAssignForm: giao một việc (nguồn "Leader giao") cho nhiều NV trong ngày ?d=,
    /// đặt vai trò trong dự án (Hỗ trợ / Leader dự án). Chỉ Leader/TP, chỉ NV thuộc nhóm của tài khoản (poolFor('nv')).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AssignTask(int projectId, int? itemId, string? title, string? start, string? end)
    {
        return await Do(async () =>
        {
            RequireLead();
            var people = StaffCalendar.ReadPeople(Request.Form, Scope.TeamPeople);
            // Form chỉ liệt kê activePj(): không nhận dự án đã đóng (vd. đã bàn giao sau khi trang được tải).
            if (people.Count > 0 && await Db.Projects.AnyAsync(p => p.Id == projectId && p.Closed))
                throw new BizException("Dự án đã bàn giao, không giao thêm việc được");
            var n = await Flow.AssignTaskAsync(Date, projectId, itemId, title, start ?? "", end ?? "", people, Scope.ActorName);
            Toast($"Đã giao việc cho {n} người ngày {Fmt.Dm(Date)}");
        });
    }

    /// <summary>
    /// POST /Staff/ProposeCalendar — NV đề xuất ở lịch tháng (ngày = ngày đang chọn): công việc của mình (không có "emp"), hoặc người hỗ trợ /
    /// Leader hỗ trợ (field "emp" nhiều giá trị) cho dự án mình là Leader dự án. Leader nhóm duyệt thì mới ghi vào lịch.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ProposeCalendar(int projectId, int? itemId, string? title, string? start, string? end) => await Do(async () =>
    {
        RequireSelfStaff();
        var people = Request.Form["emp"].Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).ToList();
        var depts = Request.Form["dept"].Select(s => s ?? "").ToList();
        await Flow.ProposeAsync(Date, Scope.Account.Id, projectId, itemId, title, start ?? "", end ?? "", people, depts);
        Toast("Đã gửi đề xuất — chờ Leader duyệt");
    });

    /// <summary>GET /Staff/Projects — nvProjects (?pt=active|done); có ?pj= → projectDetail(pj, 'nv') (?tab=overview|plan|people).</summary>
    [HttpGet]
    public async Task<IActionResult> Projects(int? pj, string? pt)
    {
        ViewData["Title"] = "Thông tin dự án";
        await D.EnsureAsync(Date, Date);
        // Nhân viên chỉ thấy dự án mà mình được chọn làm Leader dự án (yêu cầu 2026-10-14); dự án chỉ hỗ trợ / không tham gia thì ẩn.
        var led = Scope.AllStaff || Scope.MeId is not int who
            ? new HashSet<int>()
            : D.Assigns.Where(a => a.EmployeeId == who && a.Role == ProjectRole.Lead).Select(a => a.ProjectId).ToHashSet();
        if (pj is int pid)
        {
            if (!led.Contains(pid)) return Redirect(Ui.Here(Request, ("pj", null), ("tab", null)));
            var req = Request;
            var detail = await ProjectDetailVm.LoadAsync(D, Db, Date, pid, ProjectDetailVm.StaffView, req.Query["tab"].ToString(),
                Ui.Here(req, ("pj", null), ("tab", null)), t => Ui.Here(req, ("tab", t)));
            return View(new StaffProjectsVm { Detail = detail });
        }

        var m = Fmt.Monday(Date);
        await D.EnsureAsync(m, m.AddDays(6));

        var all = D.AllPj().Where(p => led.Contains(p.Id)).ToList();
        var active = all.Where(p => !p.Closed && D.PjStatus(p.Id, Date) != ProjectStatus.Completed).ToList();
        var done = all.Where(p => p.Closed || D.PjStatus(p.Id, Date) == ProjectStatus.Completed).ToList();

        // mine: nhãn vai trò — danh sách chỉ còn dự án người này là Leader dự án.
        var mine = led.ToDictionary(id => id, _ => Labels.Of(ProjectRole.Lead));

        return View(new StaffProjectsVm { Tab = pt == "done" ? "done" : "active", Active = active, Done = done, Mine = mine });
    }
}
