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
        var t = role == Role.Staff ? "mine" : tab is "summary" || (tab == "approve" && role != Role.Staff) ? tab : "mine";
        var pending = role == Role.Staff ? 0 : D.OtPending(d).Count(x => role == Role.Manager ? x.NeedsTpDecision : x.NeedsDecision);

        // Leader duyệt đăng ký làm thêm của NV ở Planning → Kế hoạch đề xuất (yêu cầu 2026-10-19); chỉ TP còn tab Duyệt ở đây.
        if (t == "approve" && role == Role.Leader) return Redirect(Ui.To(Request, "/Planning/Proposals", ("k", "ot")));
        if (t == "approve")
        {
            // otApprove: Leader duyệt NV trong nhóm; TP duyệt Leader.
            var list = D.OtPending(d)
                .OrderBy(x => (role == Role.Manager ? x.NeedsTpDecision : x.NeedsDecision) ? 0 : 1).ThenBy(x => OvertimeUi.StatusOrder(x.Status)).ThenBy(x => D.PName(x.PersonId), Fmt.ViCompare).ToList();
            return View(new OvertimeVm { Tab = t, Pending = pending, Approve = list });
        }

        if (t == "summary")
        {
            // otSummary: mọi đăng ký chưa bị từ chối; giao diện Leader: không gồm TP và chỉ trong phạm vi nhóm.
            var all = D.Ots.Where(x => x.Date == d && x.Status != OtStatus.Rejected && (role == Role.Manager || Scope.Account.HrDuty || (x.Role != Role.Manager && D.InScope(x.PersonId))))
                .OrderBy(x => OvertimeUi.SummaryOrder(x.Role)).ThenBy(x => D.PName(x.PersonId), Fmt.ViCompare).ToList();
            var send = role == Role.Manager || Scope.Account.HrDuty ? await Db.MealOrders.AsNoTracking().FirstOrDefaultAsync(s => s.Date == d) : null;
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
            if (x is not null && x.Completion == OtCompletion.Approved) throw new BizException("Kết quả làm thêm đã được duyệt và lưu trữ, không sửa đăng ký được");
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
            x.LeaderBy = null; x.LeaderAt = null;   // sửa đăng ký → duyệt lại từ Leader
            ResetCompletion(x);   // sửa đăng ký → kết quả làm thêm (nếu đã cập nhật) phải làm lại
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
            if (x.Completion == OtCompletion.Approved) throw new BizException("Kết quả làm thêm đã được duyệt và lưu trữ, không hủy được");
            Db.Overtimes.Remove(x);
            await Db.SaveChangesAsync();
            await Notify.OvertimeCancelledAsync(x.PersonId, x.Role, x.Date, x.Start, x.End, x.Status == OtStatus.Approved, alsoManagers: x.NeedsManager && x.LeaderAt != null);
        }, "Đã hủy đăng ký", OvertimeUi.WithoutParam(BackUrl(), "edit"));

    /// <summary>
    /// Kiểm quyền duyệt: Leader duyệt NV trong nhóm; TP duyệt Leader, và duyệt tiếp đăng ký của NV kết thúc sau 20:00 (sau khi Leader duyệt — yêu cầu 2026-10-21).
    /// <paramref name="forResult"/> = duyệt KẾT QUẢ làm thêm: NV do Leader duyệt, Leader do TP duyệt (TP không duyệt kết quả của NV). Trả về đăng ký (tracked).
    /// </summary>
    async Task<Overtime> LoadForDecisionAsync(int id, bool forResult = false)
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
            var staffStep = !forResult && x.Role == Role.Staff && x.NeedsManager;
            if (x.Role != Role.Leader && !staffStep) throw new BizException(x.Role == Role.Staff && !forResult
                ? "Đăng ký làm thêm kết thúc trước hoặc đúng 20:00 do Leader duyệt — Trưởng phòng chỉ duyệt đăng ký của Leader và đăng ký của nhân viên kết thúc sau 20:00"
                : "Trưởng phòng duyệt đăng ký của Leader");
        }
        else throw new BizException("Chỉ Leader hoặc Trưởng phòng được thực hiện");
        return x;
    }

    /// <summary>otDecide: duyệt (v=ok) / từ chối (v=no, bắt buộc lý do). NV làm thêm sau 20:00: Leader duyệt → chờ Trưởng phòng duyệt → mới được duyệt.</summary>
    [HttpPost]
    public async Task<IActionResult> Decide(int id, string? v, string? reason)
    {
        var ok = v == "ok";
        string? toast = null;
        return await Do(async () =>
        {
            if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
            var x = await LoadForDecisionAsync(id);
            var tpStep = Scope.Role == Role.Manager && x.Role == Role.Staff;   // bước Trưởng phòng của đăng ký NV sau 20:00
            if (x.Status != (tpStep ? OtStatus.PendingManager : OtStatus.Pending)) throw new BizException(tpStep && x.Status == OtStatus.Pending
                ? "Leader chưa duyệt đăng ký này — Trưởng phòng duyệt sau khi Leader duyệt"
                : "Đăng ký đã được quyết định — tải lại trang, dùng Hoàn tác nếu cần đổi");
            var now = VnTime.UtcNow;
            if (ok)
            {
                x.Reason = "";
                if (!tpStep && x.NeedsManager)
                {
                    // Leader duyệt đăng ký NV kết thúc sau 20:00 → chưa được duyệt hẳn, chờ Trưởng phòng
                    x.Status = OtStatus.PendingManager; x.LeaderBy = Scope.ActorName; x.LeaderAt = now; x.DecidedBy = null; x.DecidedAt = null;
                    toast = "Đã duyệt — đăng ký làm thêm sau 20:00 chờ Trưởng phòng duyệt";
                }
                else { x.Status = OtStatus.Approved; x.DecidedBy = Scope.ActorName; x.DecidedAt = now; }
            }
            else
            {
                var why = (reason ?? "").Trim();
                if (why.Length == 0) throw new BizException("Nhập lý do");
                if (why.Length > 2000) why = why[..2000];
                x.Status = OtStatus.Rejected; x.Reason = why; x.DecidedBy = Scope.ActorName; x.DecidedAt = now;
            }
            await Db.SaveChangesAsync();
            if (x.Status == OtStatus.PendingManager)
            {
                await Notify.OvertimeDecidedAsync(x.PersonId, x.Date, "đã được Leader duyệt, chờ Trưởng phòng duyệt (làm thêm sau 20:00)");
                await Notify.OvertimeToManagerAsync(x.PersonId, x.Date, x.Start, x.End);
            }
            else await Notify.OvertimeDecidedAsync(x.PersonId, x.Date, ok ? "đã được duyệt" : "bị từ chối", ok ? null : x.Reason);
            Toast(toast ?? (ok ? "Đã duyệt đăng ký làm thêm" : "Đã từ chối"));
        });
    }

    /// <summary>otUndo: đưa về bước trước. NV sau 20:00: Trưởng phòng hoàn tác → về chờ Trưởng phòng; Leader hoàn tác → về chờ Leader (chỉ khi Trưởng phòng chưa quyết định).</summary>
    [HttpPost]
    public async Task<IActionResult> Undo(int id) =>
        await Do(async () =>
        {
            var x = await LoadForDecisionAsync(id);
            var tp = Scope.Role == Role.Manager;
            if (x.Role == Role.Staff && x.NeedsManager)
            {
                if (tp)
                {
                    if (x.LeaderAt is null || x.Status is OtStatus.Pending or OtStatus.PendingManager) throw new BizException("Chưa có quyết định của Trưởng phòng để hoàn tác");
                    x.Status = OtStatus.PendingManager; x.Reason = null; x.DecidedBy = null; x.DecidedAt = null;
                }
                else
                {
                    if (x.Status == OtStatus.Pending) throw new BizException("Đăng ký đang chờ duyệt, không có gì để hoàn tác");
                    if (x.Status != OtStatus.PendingManager && x.LeaderAt is not null) throw new BizException("Trưởng phòng đã quyết định — nhờ Trưởng phòng hoàn tác trước");
                    x.Status = OtStatus.Pending; x.Reason = null; x.DecidedBy = null; x.DecidedAt = null; x.LeaderBy = null; x.LeaderAt = null;
                }
            }
            else
            {
                if (x.Status == OtStatus.Pending) throw new BizException("Đăng ký đang chờ duyệt, không có gì để hoàn tác");
                x.Status = OtStatus.Pending; x.Reason = null; x.DecidedBy = null; x.DecidedAt = null; x.LeaderBy = null; x.LeaderAt = null;
            }
            ResetCompletion(x);
            await Db.SaveChangesAsync();
            await Notify.OvertimeDecidedAsync(x.PersonId, x.Date, x.Status == OtStatus.PendingManager ? "được hoàn tác, chờ Trưởng phòng duyệt lại" : "được hoàn tác, chờ duyệt lại");
        }, "Đã hoàn tác");

    // ======================= Kết quả làm thêm (yêu cầu 2026-10-19) =======================
    // Đăng ký được duyệt → NV làm thêm → cập nhật giờ thực tế + công việc đã triển khai → người duyệt (Leader / TP) duyệt kết quả:
    // Approved = lưu trữ, giờ được tính; Rejected (làm chưa đủ / vượt giờ đăng ký…) = giờ KHÔNG được tính, phải cập nhật lại.

    static void ResetCompletion(Overtime x)
    {
        x.Completion = OtCompletion.None;
        x.ActualStart = x.ActualEnd = null; x.ActualHours = null; x.DoneContent = null; x.DoneSentAt = null;
        x.DoneDecidedBy = null; x.DoneDecidedAt = null; x.DoneReason = null;
    }

    /// <summary>Cập nhật kết quả làm thêm của mình: giờ thực tế (start / end) + công việc đã triển khai. Chỉ khi đăng ký đã được duyệt và đã đến ngày làm thêm.</summary>
    [HttpPost]
    public async Task<IActionResult> ReportDone(string? start, string? end, string? content) =>
        await Do(async () =>
        {
            var me = RequireOwnOt();
            var d = Date;
            var x = await Db.Overtimes.FirstOrDefaultAsync(o => o.Date == d && o.PersonId == me.Id) ?? throw new BizException("Chưa có đăng ký làm thêm ngày này");
            if (x.Status != OtStatus.Approved) throw new BizException("Đăng ký chưa được duyệt, chưa cập nhật kết quả được");
            if (d > VnTime.Today) throw new BizException("Chưa đến ngày làm thêm");
            if (x.Completion == OtCompletion.Approved) throw new BizException("Kết quả làm thêm đã được duyệt và lưu trữ");
            if (!Fmt.IsHhmm(start) || !Fmt.IsHhmm(end) || start == "24:00" || end!.Le(start!)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
            var hours = OtRules.ActualHours(x.DayType, start, end) ?? throw new BizException("Khoảng giờ không hợp lệ");
            var c = (content ?? "").Trim();
            if (c.Length == 0) throw new BizException("Nhập công việc đã triển khai");
            if (c.Length > 2000) throw new BizException("Công việc tối đa 2000 ký tự");
            var now = VnTime.UtcNow;
            x.ActualStart = start; x.ActualEnd = end; x.ActualHours = hours; x.DoneContent = c; x.DoneSentAt = now;
            x.DoneReason = null;
            if (x.Role == Role.Manager)
            {
                // Trưởng phòng tự ghi nhận (không có cấp duyệt) — giống đăng ký.
                x.Completion = OtCompletion.Approved; x.DoneDecidedBy = Scope.ActorName; x.DoneDecidedAt = now;
            }
            else { x.Completion = OtCompletion.Pending; x.DoneDecidedBy = null; x.DoneDecidedAt = null; }
            x.UpdatedAt = now;
            await Db.SaveChangesAsync();
            await Notify.OvertimeDoneSubmittedAsync(x.PersonId, x.Role, x.Date, start!, end);
        }, Scope.Role == Role.Manager ? "Đã ghi nhận kết quả làm thêm" : "Đã gửi cập nhật, chờ duyệt kết quả");

    /// <summary>Duyệt kết quả (v=ok → lưu trữ, tính giờ) / từ chối (v=no, bắt buộc lý do → giờ không được tính, NV cập nhật lại).</summary>
    [HttpPost]
    public async Task<IActionResult> DecideDone(int id, string? v, string? reason)
    {
        var ok = v == "ok";
        return await Do(async () =>
        {
            if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
            var x = await LoadForDecisionAsync(id, forResult: true);
            if (x.Status != OtStatus.Approved || x.Completion != OtCompletion.Pending)
                throw new BizException("Kết quả làm thêm không còn ở trạng thái chờ duyệt — tải lại trang");
            var now = VnTime.UtcNow;
            if (ok) { x.Completion = OtCompletion.Approved; x.DoneReason = null; }
            else
            {
                var why = (reason ?? "").Trim();
                if (why.Length == 0) throw new BizException("Nhập lý do");
                if (why.Length > 2000) why = why[..2000];
                x.Completion = OtCompletion.Rejected; x.DoneReason = why;
            }
            x.DoneDecidedBy = Scope.ActorName; x.DoneDecidedAt = now;
            await Db.SaveChangesAsync();
            await Notify.OvertimeDecidedAsync(x.PersonId, x.Date,
                ok ? "kết quả làm thêm đã được duyệt" : "kết quả làm thêm bị từ chối — thời gian làm thêm không được tính, cập nhật lại", ok ? null : x.DoneReason);
        }, ok ? "Đã duyệt kết quả làm thêm" : "Đã từ chối kết quả — giờ làm thêm không được tính");
    }

    /// <summary>Hoàn tác quyết định kết quả → về Chờ duyệt kết quả.</summary>
    [HttpPost]
    public async Task<IActionResult> UndoDone(int id) =>
        await Do(async () =>
        {
            var x = await LoadForDecisionAsync(id, forResult: true);
            if (x.Completion is not (OtCompletion.Approved or OtCompletion.Rejected)) throw new BizException("Không có quyết định để hoàn tác");
            x.Completion = OtCompletion.Pending; x.DoneReason = null; x.DoneDecidedBy = null; x.DoneDecidedAt = null;
            await Db.SaveChangesAsync();
            await Notify.OvertimeDecidedAsync(x.PersonId, x.Date, "kết quả làm thêm được hoàn tác, chờ duyệt lại");
        }, "Đã hoàn tác");

    /// <summary>otSend: TP gửi tổng hợp suất ăn cho Phòng HCNS (gửi lại = cập nhật số mới nhất).</summary>
    [HttpPost]
    public async Task<IActionResult> SendMealOrder() =>
        await Do(async () =>
        {
            // TP / Quản trị, hoặc Leader được giao quản lý nhân sự / báo cơm (HrDuty — yêu cầu 2026-10-21)
            if (!Scope.IsManager && !(Scope.Account.HrDuty && Scope.Account.Role == Role.Leader)) throw new BizException("Chỉ Trưởng phòng hoặc Leader phụ trách báo cơm được gửi");
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
