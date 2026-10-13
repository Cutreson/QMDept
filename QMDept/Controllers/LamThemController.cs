using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Làm thêm (otScreen) — dùng chung 3 giao diện, chọn bằng ?r=nv|leader|tp (POST: query hoặc field "r").
/// Tab con (Leader/TP): ?tab=me|duyet|th. Đang sửa đăng ký: ?edit=yyyy-MM-dd (S.otEdit — gắn với ngày đang xem,
/// nên chuyển ngày bằng thanh ngày là tự thoát chế độ sửa, giống artifact đặt S.otEdit=false khi đổi ngày).
/// Port: otScreen, otMine, otApprove, otSummary + handler otForm / otDel / otDecide / otUndo / otSend.
/// Luật tính dùng OtRules (Services/Rules.cs); bản JS xem trước: wwwroot/js/ot.js.
/// </summary>
public class LamThemController : AppController
{
    /// <summary>Giao diện lấy từ ?r= (hoặc field "r" của form POST); mặc định = vai trò tài khoản.
    /// Chỉ 3 giao diện của artifact (nv / leader / tp): tài khoản Quản trị (Admin) không có màn Làm thêm → trả Nv để
    /// ScopeResolver từ chối (Forbidden) và chuyển về trang mặc định của Admin.</summary>
    protected override Role ResolveUiRole(ActionExecutingContext ctx)
    {
        var req = ctx.HttpContext.Request;
        string? r = req.Query["r"];
        if (string.IsNullOrEmpty(r) && req.HasFormContentType) r = req.Form["r"];
        if (LamThemUi.ParseRole(r) is Role role) return role;
        return Enum.TryParse<Role>(ctx.HttpContext.User.FindFirstValue(ClaimTypes.Role), out var acc) && acc is Role.Leader or Role.Tp ? acc : Role.Nv;
    }

    string RKey => LamThemUi.RoleKey(Scope.Role);

    // ======================= GET /LamThem =======================

    /// <summary>otScreen: tiêu đề, thanh ngày, hạn đăng ký, tab con (Leader/TP) → otMine / otApprove / otSummary.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? tab, string? edit)
    {
        var d = Date;
        await D.EnsureAsync(d, d);
        var role = Scope.Role;
        var t = role == Role.Nv ? "me" : tab is "duyet" or "th" ? tab : "me";
        var pending = role == Role.Nv ? 0 : D.OtPending(d).Count(x => x.Status == OtStatus.ChoDuyet);

        if (t == "duyet")
        {
            // otApprove: Leader duyệt NV trong nhóm; TP duyệt Leader.
            var list = D.OtPending(d)
                .OrderBy(x => LamThemUi.StatusOrder(x.Status)).ThenBy(x => D.PName(x.PersonId), Fmt.ViCompare).ToList();
            return View(new LamThemVm { R = RKey, Tab = t, Pending = pending, Approve = list });
        }

        if (t == "th")
        {
            // otSummary: mọi đăng ký chưa bị từ chối; giao diện Leader: không gồm TP và chỉ trong phạm vi nhóm.
            var all = D.Ots.Where(x => x.Date == d && x.Status != OtStatus.TuChoi && (role == Role.Tp || (x.Role != Role.Tp && D.InScope(x.PersonId))))
                .OrderBy(x => LamThemUi.RoleKey(x.Role), StringComparer.Ordinal).ThenBy(x => D.PName(x.PersonId), Fmt.ViCompare).ToList();
            var send = role == Role.Tp ? await Db.OtSends.AsNoTracking().FirstOrDefaultAsync(s => s.Date == d) : null;
            return View(new LamThemVm { R = RKey, Tab = t, Pending = pending, Summary = all, Send = send });
        }

        // otMine
        var me = Scope.Me;
        Overtime? mine = null;
        List<WorkTask> open = [];
        HashSet<int> picked = [];
        if (me is not null)
        {
            mine = D.OtOf(d, me.Id);
            picked = mine?.Items.Where(i => i.WorkTaskId is not null).Select(i => i.WorkTaskId!.Value).ToHashSet() ?? [];
            var dayT = D.TasksOn(d, me.Id);
            var baseList = dayT.Any(x => x.Selected) ? dayT.Where(x => x.Selected) : dayT;
            open = baseList.Where(x => x.Status != TaskState.HoanThanh || picked.Contains(x.Id)).ToList();
        }
        return View(new LamThemVm
        {
            R = RKey, Tab = t, Pending = pending, Edit = edit == Fmt.Key(d) && Scope.IsSelf,
            Mine = mine, Open = open, Picked = picked,
        });
    }

    // ======================= POST =======================

    /// <summary>Chỉ thao tác trên đăng ký của chính mình (TP xem Leader khác / Leader xem NV → chỉ xem).</summary>
    Person RequireOwnOt()
    {
        if (Scope.Me is null) throw new BizException("Chưa chọn người dùng");
        if (Scope.IsReadOnly) throw new BizException("Chế độ xem: không sửa được trang của nhân viên");
        if (!Scope.IsSelf) throw new BizException("Chỉ sửa được đăng ký làm thêm của chính mình");
        return Scope.Me;
    }

    /// <summary>otForm submit: gửi / sửa đăng ký làm thêm của mình cho ngày đang chọn.</summary>
    [HttpPost]
    public async Task<IActionResult> Luu(string? dayType, string? start, string? end, string? meal, List<int>? task, string? extra)
    {
        var done = LamThemUi.WithoutParam(BackUrl(), "edit");
        try
        {
            var me = RequireOwnOt();
            var d = Date;
            var role = Scope.Role;
            var dt = LamThemUi.ParseDay(dayType) ?? throw new BizException("Chọn loại ngày");
            var r = OtRules.Calc(dt, start, end);
            if (!r.Ok) throw new BizException(r.Error!);

            // việc làm tiếp: chỉ nhận việc của chính mình trong ngày, giữ thứ tự đã tích
            var ids = (task ?? []).Distinct().ToList();
            var found = ids.Count == 0 ? [] : await Db.WorkTasks.AsNoTracking()
                .Where(t => ids.Contains(t.Id) && t.Date == d && t.EmployeeId == me.Id).ToListAsync();
            var items = ids.Select(id => found.Find(t => t.Id == id)).OfType<WorkTask>().ToList();
            var ex = (extra ?? "").Trim();
            if (ex.Length > 2000) throw new BizException("Nội dung khác quá dài (tối đa 2000 ký tự)");
            var content = string.Join("; ", items.Select(t => t.Title).Append(ex).Where(s => !string.IsNullOrEmpty(s)));
            if (content.Length == 0) throw new BizException("Tích việc chưa xong cần làm tiếp hoặc nhập nội dung khác");
            if (content.Length > 2000) content = content[..2000];

            var opts = OtRules.MealOptions(r);
            var m = LamThemUi.ParseMeal(meal) is OtMeal pm && opts.Contains(pm) ? pm : opts[0];
            var now = VnTime.UtcNow;
            var x = await Db.Overtimes.Include(o => o.Items).FirstOrDefaultAsync(o => o.Date == d && o.PersonId == me.Id);
            var createdAt = x is not null && x.Meal == m ? x.CreatedAt : now;
            if (x is null)
            {
                x = new Overtime { Date = d, PersonId = me.Id };
                Db.Overtimes.Add(x);
            }
            x.Role = role;
            x.DayType = r.DayType;
            x.Start = r.Start;
            x.End = r.End;
            x.Hours = r.Hours;
            x.Tier = r.Tier;
            x.Break = r.Break;
            x.Exempt = false;
            x.Meal = m;
            x.Cost = OtRules.CostOf(m);
            x.Allowance = r.Allow;
            x.ProjectId = items.Count > 0 ? items[0].ProjectId : null;
            x.Content = content;
            x.ExtraContent = ex.Length > 0 ? ex : null;
            foreach (var old in x.Items.ToList()) Db.OvertimeTasks.Remove(old);
            x.Items.Clear();
            foreach (var t in items) x.Items.Add(new OvertimeTask { WorkTaskId = t.Id, Title = t.Title, ProjectId = t.ProjectId });
            if (role == Role.Tp) { x.Status = OtStatus.DaDuyet; x.DecidedBy = Scope.ActorName; x.DecidedAt = now; }
            else { x.Status = OtStatus.ChoDuyet; x.DecidedBy = null; x.DecidedAt = null; }
            x.Reason = null;
            x.Reg = OtRules.RegType(createdAt, d);
            x.CreatedAt = createdAt;
            x.UpdatedAt = now;
            await Db.SaveChangesAsync();
            Toast(role == Role.Tp ? "Đã ghi nhận đăng ký làm thêm" : "Đã gửi đăng ký, chờ duyệt");
            return Redirect(done);
        }
        catch (BizException e)
        {
            Toast(e.Message);
            return Back();
        }
    }

    /// <summary>otDel: hủy đăng ký làm thêm của mình ngày đang chọn.</summary>
    [HttpPost]
    public async Task<IActionResult> Huy() =>
        await Do(async () =>
        {
            var me = RequireOwnOt();
            var d = Date;
            var x = await Db.Overtimes.Include(o => o.Items).FirstOrDefaultAsync(o => o.Date == d && o.PersonId == me.Id);
            if (x is null) return;
            Db.Overtimes.Remove(x);
            await Db.SaveChangesAsync();
        }, "Đã hủy đăng ký", LamThemUi.WithoutParam(BackUrl(), "edit"));

    /// <summary>Kiểm quyền duyệt: Leader duyệt NV trong nhóm; TP duyệt Leader. Trả về đăng ký (tracked).</summary>
    async Task<Overtime> LoadForDecisionAsync(int id)
    {
        var x = await Db.Overtimes.FirstOrDefaultAsync(o => o.Id == id) ?? throw new BizException("Không tìm thấy đăng ký làm thêm");
        if (Scope.Role == Role.Leader)
        {
            RequireLead();
            if (x.Role != Role.Nv) throw new BizException("Leader chỉ duyệt đăng ký của nhân viên");
            await RequireInScopeAsync(x.PersonId);
        }
        else if (Scope.Role == Role.Tp)
        {
            RequireTp();
            if (x.Role != Role.Leader) throw new BizException("Trưởng phòng duyệt đăng ký của Leader");
        }
        else throw new BizException("Chỉ Leader hoặc Trưởng phòng được thực hiện");
        return x;
    }

    /// <summary>otDecide: duyệt (v=ok) / từ chối (v=no, bắt buộc lý do).</summary>
    [HttpPost]
    public async Task<IActionResult> Duyet(int id, string? v, string? reason)
    {
        var ok = v == "ok";
        return await Do(async () =>
        {
            if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
            var x = await LoadForDecisionAsync(id);
            if (ok)
            {
                x.Status = OtStatus.DaDuyet; x.DecidedBy = Scope.ActorName; x.DecidedAt = VnTime.UtcNow; x.Reason = "";
            }
            else
            {
                var why = (reason ?? "").Trim();
                if (why.Length == 0) throw new BizException("Nhập lý do");
                if (why.Length > 2000) why = why[..2000];
                x.Status = OtStatus.TuChoi; x.Reason = why; x.DecidedBy = Scope.ActorName; x.DecidedAt = VnTime.UtcNow;
            }
            await Db.SaveChangesAsync();
        }, ok ? "Đã duyệt đăng ký làm thêm" : "Đã từ chối");
    }

    /// <summary>otUndo: đưa về Chờ duyệt.</summary>
    [HttpPost]
    public async Task<IActionResult> HoanTac(int id) =>
        await Do(async () =>
        {
            var x = await LoadForDecisionAsync(id);
            x.Status = OtStatus.ChoDuyet; x.Reason = null; x.DecidedBy = null; x.DecidedAt = null;
            await Db.SaveChangesAsync();
        }, "Đã hoàn tác");

    /// <summary>otSend: TP gửi tổng hợp suất ăn cho Phòng HCNS (gửi lại = cập nhật số mới nhất).</summary>
    [HttpPost]
    public async Task<IActionResult> GuiHcns() =>
        await Do(async () =>
        {
            RequireTp();
            var d = Date;
            var all = await Db.Overtimes.AsNoTracking().Where(o => o.Date == d && o.Status != OtStatus.TuChoi).ToListAsync();
            var s = await Db.OtSends.FirstOrDefaultAsync(o => o.Date == d);
            if (s is null) { s = new OtSend { Date = d }; Db.OtSends.Add(s); }
            s.By = Scope.ActorName;
            s.SentAt = VnTime.UtcNow;
            s.Com = all.Count(o => OtRules.IsSuat(o.Meal));
            s.Bun = all.Count(o => o.Meal == OtMeal.Bun);
            s.Nhe = all.Count(o => o.Meal == OtMeal.Nhe);
            s.Pc = all.Count(o => o.Allowance > 0);
            s.People = all.Count;
            await Db.SaveChangesAsync();
        }, "Đã gửi đăng ký suất ăn cho HCNS");
}
