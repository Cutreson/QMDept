using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Làm thêm (otScreen) — dùng chung 3 giao diện NV / Leader / TP; giao diện = vai trò của người đang xem (ViewScope).
/// Tab con (Leader/TP): ?tab=mine|approve|summary. Đang sửa đăng ký: ?edit=yyyy-MM-dd (S.otEdit — gắn với ngày đang xem,
/// nên chuyển ngày bằng thanh ngày là tự thoát chế độ sửa, giống artifact đặt S.otEdit=false khi đổi ngày).
/// Port: otScreen, otMine, otApprove, otSummary + handler otForm / otDel / otDecide / otUndo / otSend.
/// Luật tính dùng OtRules (Services/Rules.cs); bản JS xem trước: wwwroot/js/overtime.js.
/// </summary>
public class OvertimeController : AppController
{
    // Trang dùng chung NV/Leader/TP (không khai báo [UiRole]): giao diện = vai trò của người đang xem (ViewScope).
    // Quản trị không có màn Làm thêm.

    // ======================= GET /Overtime =======================

    /// <summary>otScreen: tiêu đề, thanh ngày, hạn đăng ký, tab con (Leader/TP) → otMine / otApprove / otSummary.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? tab, string? edit)
    {
        var d = Date;
        await D.EnsureAsync(d, d);
        var role = Scope.Role;
        var t = role == Role.Staff ? "mine" : tab is "approve" or "summary" ? tab : "mine";
        var pending = role == Role.Staff ? 0 : D.OtPending(d).Count(x => x.Status == OtStatus.Pending);

        if (t == "approve")
        {
            // otApprove: Leader duyệt NV trong nhóm; TP duyệt Leader.
            var list = D.OtPending(d)
                .OrderBy(x => OvertimeUi.StatusOrder(x.Status)).ThenBy(x => D.PName(x.PersonId), Fmt.ViCompare).ToList();
            return View(new OvertimeVm { Tab = t, Pending = pending, Approve = list });
        }

        if (t == "summary")
        {
            // otSummary: mọi đăng ký chưa bị từ chối; giao diện Leader: không gồm TP và chỉ trong phạm vi nhóm.
            var all = D.Ots.Where(x => x.Date == d && x.Status != OtStatus.Rejected && (role == Role.Manager || (x.Role != Role.Manager && D.InScope(x.PersonId))))
                .OrderBy(x => OvertimeUi.SummaryOrder(x.Role)).ThenBy(x => D.PName(x.PersonId), Fmt.ViCompare).ToList();
            var send = role == Role.Manager ? await Db.MealOrders.AsNoTracking().FirstOrDefaultAsync(s => s.Date == d) : null;
            return View(new OvertimeVm { Tab = t, Pending = pending, Summary = all, Send = send });
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
            open = baseList.Where(x => x.Status != TaskState.Done || picked.Contains(x.Id)).ToList();
        }
        return View(new OvertimeVm
        {
            Tab = t, Pending = pending, Edit = edit == Fmt.Key(d) && Scope.IsSelf,
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
    public async Task<IActionResult> Save(string? dayType, string? start, string? end, string? meal, List<int>? task, string? extra)
    {
        var done = OvertimeUi.WithoutParam(BackUrl(), "edit");
        try
        {
            var me = RequireOwnOt();
            var d = Date;
            var role = Scope.Role;
            var dt = OvertimeUi.ParseDay(dayType) ?? throw new BizException("Chọn loại ngày");
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
            var m = OvertimeUi.ParseMeal(meal) is OtMeal pm && opts.Contains(pm) ? pm : opts[0];
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
            if (role == Role.Manager) { x.Status = OtStatus.Approved; x.DecidedBy = Scope.ActorName; x.DecidedAt = now; }
            else { x.Status = OtStatus.Pending; x.DecidedBy = null; x.DecidedAt = null; }
            x.Reason = null;
            x.Reg = OtRules.RegType(createdAt, d);
            x.CreatedAt = createdAt;
            x.UpdatedAt = now;
            await Db.SaveChangesAsync();
            await Notify.OvertimeSubmittedAsync(x.PersonId, role, x.Date, x.Start, x.End);
            Toast(role == Role.Manager ? "Đã ghi nhận đăng ký làm thêm" : "Đã gửi đăng ký, chờ duyệt");
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
    public async Task<IActionResult> Cancel() =>
        await Do(async () =>
        {
            var me = RequireOwnOt();
            var d = Date;
            var x = await Db.Overtimes.Include(o => o.Items).FirstOrDefaultAsync(o => o.Date == d && o.PersonId == me.Id);
            if (x is null) return;
            Db.Overtimes.Remove(x);
            await Db.SaveChangesAsync();
        }, "Đã hủy đăng ký", OvertimeUi.WithoutParam(BackUrl(), "edit"));

    /// <summary>Kiểm quyền duyệt: Leader duyệt NV trong nhóm; TP duyệt Leader. Trả về đăng ký (tracked).</summary>
    async Task<Overtime> LoadForDecisionAsync(int id)
    {
        var x = await Db.Overtimes.FirstOrDefaultAsync(o => o.Id == id) ?? throw new BizException("Không tìm thấy đăng ký làm thêm");
        if (Scope.Role == Role.Leader)
        {
            RequireLead();
            if (x.Role != Role.Staff) throw new BizException("Leader chỉ duyệt đăng ký của nhân viên");
            await RequireInScopeAsync(x.PersonId);
        }
        else if (Scope.Role == Role.Manager)
        {
            RequireManager();
            if (x.Role != Role.Leader) throw new BizException("Trưởng phòng duyệt đăng ký của Leader");
        }
        else throw new BizException("Chỉ Leader hoặc Trưởng phòng được thực hiện");
        return x;
    }

    /// <summary>otDecide: duyệt (v=ok) / từ chối (v=no, bắt buộc lý do).</summary>
    [HttpPost]
    public async Task<IActionResult> Decide(int id, string? v, string? reason)
    {
        var ok = v == "ok";
        return await Do(async () =>
        {
            if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
            var x = await LoadForDecisionAsync(id);
            if (x.Status != OtStatus.Pending) throw new BizException("Đăng ký đã được quyết định — tải lại trang, dùng Hoàn tác nếu cần đổi");
            if (ok)
            {
                x.Status = OtStatus.Approved; x.DecidedBy = Scope.ActorName; x.DecidedAt = VnTime.UtcNow; x.Reason = "";
            }
            else
            {
                var why = (reason ?? "").Trim();
                if (why.Length == 0) throw new BizException("Nhập lý do");
                if (why.Length > 2000) why = why[..2000];
                x.Status = OtStatus.Rejected; x.Reason = why; x.DecidedBy = Scope.ActorName; x.DecidedAt = VnTime.UtcNow;
            }
            await Db.SaveChangesAsync();
            await Notify.OvertimeDecidedAsync(x.PersonId, x.Date, ok ? "đã được duyệt" : "bị từ chối", ok ? null : x.Reason);
        }, ok ? "Đã duyệt đăng ký làm thêm" : "Đã từ chối");
    }

    /// <summary>otUndo: đưa về Chờ duyệt.</summary>
    [HttpPost]
    public async Task<IActionResult> Undo(int id) =>
        await Do(async () =>
        {
            var x = await LoadForDecisionAsync(id);
            if (x.Status == OtStatus.Pending) throw new BizException("Đăng ký đang chờ duyệt, không có gì để hoàn tác");
            x.Status = OtStatus.Pending; x.Reason = null; x.DecidedBy = null; x.DecidedAt = null;
            await Db.SaveChangesAsync();
            await Notify.OvertimeDecidedAsync(x.PersonId, x.Date, "được hoàn tác, chờ duyệt lại");
        }, "Đã hoàn tác");

    /// <summary>otSend: TP gửi tổng hợp suất ăn cho Phòng HCNS (gửi lại = cập nhật số mới nhất).</summary>
    [HttpPost]
    public async Task<IActionResult> SendMealOrder() =>
        await Do(async () =>
        {
            RequireManager();
            var d = Date;
            var all = await Db.Overtimes.AsNoTracking().Where(o => o.Date == d && o.Status != OtStatus.Rejected).ToListAsync();
            var s = await Db.MealOrders.FirstOrDefaultAsync(o => o.Date == d);
            if (s is null) { s = new MealOrder { Date = d }; Db.MealOrders.Add(s); }
            s.By = Scope.ActorName;
            s.SentAt = VnTime.UtcNow;
            s.RiceCount = all.Count(o => OtRules.IsFullMeal(o.Meal));
            s.NoodleCount = all.Count(o => o.Meal == OtMeal.Noodle);
            s.SnackCount = all.Count(o => o.Meal == OtMeal.Snack);
            s.AllowanceCount = all.Count(o => o.Allowance > 0);
            s.People = all.Count;
            await Db.SaveChangesAsync();
        }, "Đã gửi đăng ký suất ăn cho HCNS");
}
