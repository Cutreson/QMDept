using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Giao diện Nhân viên — Kế hoạch tháng (nvMonth, teamDayList, calAssignForm) và Thông tin dự án (nvProjects, projectDetail).
/// Leader/TP xem các trang này ở chế độ chỉ xem; riêng chế độ "Tất cả nhân viên" có form Giao thêm việc (GiaoViec).
/// </summary>
public partial class NvController
{
    /// <summary>GET /Nv/Thang — nvMonth. ?m= tháng đang xem, ?d= ngày chọn.</summary>
    [HttpGet]
    public async Task<IActionResult> Thang()
    {
        var ms = Nav.Month(Request, Date);
        await D.EnsureAsync(ms, ms.AddDays(Fmt.DaysInMonth(ms) - 1));
        await D.EnsureAsync(Date, Date);
        var ro = Scope.IsReadOnly;
        ViewData["Title"] = "Kế hoạch tháng";
        return View(new NvThangVm
        {
            MonthStart = ms,
            ReadOnly = ro,
            AllMode = ro && Scope.AllNv,
            Me = Scope.Me,
            People = Scope.Pool,
        });
    }

    /// <summary>
    /// POST /Nv/GiaoViec — submit của calAssignForm: giao một việc (nguồn "Leader giao") cho nhiều NV trong ngày ?d=,
    /// đặt vai trò trong dự án (Hỗ trợ / Leader dự án). Chỉ Leader/TP, chỉ NV thuộc nhóm của tài khoản (poolFor('nv')).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GiaoViec(int projectId, int? itemId, string? title, string? start, string? end)
    {
        return await Do(async () =>
        {
            RequireLead();
            var people = NvLich.ReadPeople(Request.Form, Scope.Pool);
            // Form chỉ liệt kê activePj(): không nhận dự án đã đóng (vd. đã bàn giao sau khi trang được tải).
            if (people.Count > 0 && await Db.Projects.AnyAsync(p => p.Id == projectId && p.Closed))
                throw new BizException("Dự án đã bàn giao, không giao thêm việc được");
            var n = await Flow.AssignTaskAsync(Date, projectId, itemId, title, start ?? "", end ?? "", people, Scope.ActorName);
            Toast($"Đã giao việc cho {n} người ngày {Fmt.Dm(Date)}");
        });
    }

    /// <summary>GET /Nv/DuAn — nvProjects (?pt=act|done); có ?pj= → projectDetail(pj, 'nv') (?tab=tq|kh|ns).</summary>
    [HttpGet]
    public async Task<IActionResult> DuAn(int? pj, string? pt)
    {
        ViewData["Title"] = "Thông tin dự án";
        if (pj is int pid)
        {
            var req = Request;
            var detail = await ProjectDetailVm.LoadAsync(D, Db, Date, pid, "nv", req.Query["tab"].ToString(),
                Ui.Here(req, ("pj", null), ("tab", null)), t => Ui.Here(req, ("tab", t)));
            return View(new NvDuAnVm { Detail = detail });
        }

        var m = Fmt.Monday(Date);
        await D.EnsureAsync(m, m.AddDays(6));
        await D.EnsureAsync(Date, Date);

        var all = D.AllPj();
        var act = all.Where(p => !p.Closed && D.PjStatus(p.Id, Date) != ProjectStatus.HoanThanh).ToList();
        var done = all.Where(p => p.Closed || D.PjStatus(p.Id, Date) == ProjectStatus.HoanThanh).ToList();

        // mine: vai trò theo phân công (assignsOf), hoặc "Bạn đang tham gia" nếu có việc ở dự án trong tuần.
        var mine = new Dictionary<int, string>();
        if (!Scope.AllNv && Scope.MeId is int me)
        {
            var weekPj = Enumerable.Range(0, 7).SelectMany(k => D.TasksOn(m.AddDays(k), me)).Select(t => t.ProjectId).ToHashSet();
            foreach (var id in weekPj) mine[id] = "Bạn đang tham gia";
            foreach (var a in D.AssignsOf(me)) mine[a.ProjectId] = Labels.Of(a.Role);
        }

        return View(new NvDuAnVm { Tab = pt == "done" ? "done" : "act", Act = act, Done = done, Mine = mine });
    }
}
