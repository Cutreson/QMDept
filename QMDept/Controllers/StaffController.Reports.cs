using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Nhân viên → Báo cáo đầu ngày (nvMorning) và Báo cáo cuối ngày (nvEvening) cùng các thao tác ghi của chúng
/// (submit planForm, addOut, openUpdate, openExtra, submit eodForm). Mọi POST: chỉ NV trên trang của chính mình (RequireSelfStaff).
/// </summary>
public partial class StaffController
{
    // ======================= Báo cáo đầu ngày =======================

    /// <summary>TP / Quản trị tự duyệt trạng thái làm việc của mình (không có cấp trên duyệt).</summary>
    static bool SelfApproves(Person? p) => p is { Role: Role.Manager or Role.Admin };

    /// <summary>nvMorning. ?edit=yyyy-MM-dd (= ngày đang xem) → sửa kế hoạch đã gửi (S.editPlan).</summary>
    [HttpGet]
    public async Task<IActionResult> Morning()
    {
        await D.EnsureAsync(Date, Date.AddDays(14));
        var me = Scope.Me;
        var plan = me is null ? null : D.PlanOf(Date, me.Id);
        // artifact chỉ có nút "Sửa kế hoạch và gửi lại" khi kế hoạch CHƯA được duyệt → ?edit= bị bỏ qua khi đã duyệt.
        var editing = plan is not null && (Dept.PlanSt(plan) != PlanState.Approved || SelfApproves(me)) && Request.Query["edit"] == Fmt.Key(Date);
        var showForm = plan is null || editing;
        // Dữ liệu vừa gửi bị lỗi kiểm tra (SubmitPlan) → điền lại form, không mất dữ liệu đã nhập.
        var draft = StaffReports.ReadDraft(TempData[StaffReports.DraftKey] as string, Date);
        return View(new StaffMorningVm
        {
            Me = me,
            Plan = plan,
            ShowForm = showForm,
            Editing = editing,
            Form = draft is not null && showForm && !Scope.IsReadOnly ? StaffMorningForm.FromInput(draft) : StaffMorningForm.FromPlan(plan),
            List = me is null ? [] : D.TasksOn(Date, me.Id).Where(t => t.Source != TaskSource.CarryOver || D.OkCal(t)).ToList(),
        });
    }

    /// <summary>submit planForm: kiểm tra theo đúng thứ tự artifact rồi ghi đè kế hoạch ngày (xóa các quyết định duyệt cũ).</summary>
    [HttpPost]
    public async Task<IActionResult> SubmitPlan(StaffPlanInput input)
    {
        try
        {
            RequireSelfStaff(leaderOk: true, managerOk: true);
            var me = Scope.Me!;
            var plan = await Db.DayPlans.Include(p => p.PlanTasks).FirstOrDefaultAsync(p => p.Date == Date && p.EmployeeId == me.Id);
            if (!SelfApproves(me)) StaffReports.RequireEditable(plan);
            var status = StaffReports.ParseEnum<WorkStatus>(input.Status) ?? throw new BizException("Chọn trạng thái làm việc");
            var leave = LeaveRules.TypeOf(status) is LeaveType lt ? StaffReports.ReadLeave(lt, input) : null;
            var meal = StaffReports.ParseEnum<Meal>(input.Meal) ?? throw new BizException("Chọn báo cơm");

            var all = await Db.WorkTasks.Where(t => t.Date == Date && t.EmployeeId == me.Id).ToListAsync();
            // Chỉ nhận việc của chính NV trong ngày, đang hiện trên form (việc chuyển tiếp phải đã được duyệt).
            var shown = all.Where(t => t.Source != TaskSource.CarryOver || t.CarryOk).Select(t => t.Id).ToHashSet();
            var picks = input.Picks.Where(shown.Contains).ToHashSet();
            if (picks.Count == 0 && !(leave is not null && leave.Hours >= 8)) throw new BizException("Chọn ít nhất 1 công việc");

            // Công tác: địa điểm + khách hàng lấy theo dự án của các việc đã chọn, NV không phải chọn (yêu cầu 2026-10-16).
            string? province = null, customer = null;
            if (LeaveRules.IsTrip(status))
            {
                var pids = all.Where(t => picks.Contains(t.Id)).Select(t => t.ProjectId).Distinct().ToList();
                var pjs = await Db.Projects.Where(p => pids.Contains(p.Id)).ToListAsync();
                (province, customer) = StaffReports.TripPlace(pids.Select(i => pjs.FirstOrDefault(p => p.Id == i)).OfType<Project>());
            }

            // Nhân sự hỗ trợ (cùng thiết kế với đề xuất công việc): chỉ chỉ định người trong nhóm mình; phòng ban / nhóm khác xin theo số lượng.
            // Phạm vi suy ra từ lựa chọn: có phòng ban → OtherDept, có nhóm khác → OtherTeam, chỉ người trong nhóm → Team (Leader duyệt).
            var (support, supportText, supportPick) = await ResolveSupportAsync(me, input);
            var note = StaffReports.Clip(input.Note, 2000);

            foreach (var t in all) t.Selected = picks.Contains(t.Id);

            if (plan is null)
            {
                plan = new DayPlan { Date = Date, EmployeeId = me.Id };
                Db.DayPlans.Add(plan);
            }
            plan.Status = status;
            plan.Meal = meal;
            plan.LeaveType = leave?.Type;
            plan.LeaveSession = leave?.Session;
            plan.LeaveFrom = leave?.From;
            plan.LeaveTo = leave?.To;
            plan.LeaveHours = leave?.Hours;
            plan.LeaveDays = leave?.Days;
            plan.TripProvince = province;
            plan.TripCustomer = customer;
            plan.SupportScope = support;
            plan.SupportText = supportText;
            plan.SupportPick = supportPick;
            plan.Note = note;
            plan.SentAt = VnTime.UtcNow;
            // artifact .set() thay cả bản ghi → mọi quyết định duyệt / góp ý cũ bị xóa
            plan.LeaderDecision = null; plan.LeaderBy = null; plan.LeaderAt = null; plan.LeaderNote = null;
            plan.ManagerDecision = null; plan.ManagerBy = null; plan.ManagerAt = null; plan.ManagerNote = null;
            if (plan.Id != 0) Db.SupportRequests.RemoveRange(await Db.SupportRequests.Where(x => x.PlanId == plan.Id).ToListAsync());   // gửi lại kế hoạch → yêu cầu hỗ trợ nhóm khác làm lại từ đầu
            // TP / Quản trị không có người duyệt: trạng thái làm việc của chính mình tự được duyệt (vẫn sửa và gửi lại được) — yêu cầu 2026-10-21.
            if (SelfApproves(me)) { plan.LeaderDecision = Decision.Ok; plan.LeaderBy = me.Name; plan.LeaderAt = VnTime.UtcNow; }
            // taskIds = picks
            var have = plan.PlanTasks.Select(x => x.WorkTaskId).ToHashSet();
            foreach (var x in plan.PlanTasks.Where(x => !picks.Contains(x.WorkTaskId)).ToList())
            {
                plan.PlanTasks.Remove(x);
                Db.DayPlanTasks.Remove(x);
            }
            foreach (var id in picks.Where(id => !have.Contains(id))) plan.PlanTasks.Add(new DayPlanTask { WorkTaskId = id });

            await Db.SaveChangesAsync();
            await Notify.PlanSubmittedAsync(Scope.Me!.Id, Date);
        }
        catch (BizException ex)
        {
            Toast(ex.Message);
            if (Scope.IsSelf && !Scope.IsReadOnly && StaffReports.WriteDraft(input, Date) is { } draft) TempData[StaffReports.DraftKey] = draft;
            return Back();
        }
        Toast("Đã gửi kế hoạch đầu ngày");
        return Redirect(Ui.To(Request, "/Staff/Morning", ("d", Fmt.Key(Date))));
    }

    /// <summary>Đọc lựa chọn xin hỗ trợ của báo cáo đầu ngày → (phạm vi, nội dung hiển thị, JSON lưu lại).</summary>
    async Task<(SupportScope Scope, string? Text, string? Pick)> ResolveSupportAsync(Person me, StaffPlanInput input)
    {
        var pick = StaffSupportPick.Parse(input.SupportPick);
        var reason = StaffReports.Clip(input.SupportText, 1500);
        if (pick is null) return (SupportScope.None, null, null);
        var gid = me.Role == Role.Leader ? me.Id : me.LeaderId;   // Leader: nhóm của chính mình
        var emp = pick.Emp.Distinct().Where(x => x != me.Id).ToList();
        var qd = pick.Qd.Where(kv => kv.Value > 0 && int.TryParse(kv.Key, out var k) && k >= 0 && k < Fmt.SupportDepartments.Length).ToDictionary(kv => kv.Key, kv => Math.Min(kv.Value, 50));
        var leaders = await Db.People.AsNoTracking().Where(p => p.Role == Role.Leader && !p.IsDeleted && p.Id != gid && p.Id != me.Id).ToListAsync();
        var qg = pick.Qg.Where(kv => kv.Value > 0 && int.TryParse(kv.Key, out var k) && leaders.Any(l => l.Id == k)).ToDictionary(kv => kv.Key, kv => Math.Min(kv.Value, 50));
        if (emp.Count == 0 && qd.Count == 0 && qg.Count == 0) return (SupportScope.None, null, null);
        var staff = await Db.People.AsNoTracking().Where(p => emp.Contains(p.Id) && !p.IsDeleted
            && ((p.Role == Role.Staff && p.LeaderId == gid) || (p.Id == me.LeaderId && me.Role != Role.Leader))).ToListAsync();
        if (staff.Count != emp.Count) throw new BizException("Chỉ được chỉ định người hỗ trợ trong nhóm của mình");
        if (reason is null) throw new BizException("Nhập lý do xin người hỗ trợ");
        var parts = new List<string>();
        if (staff.Count > 0) parts.Add("Người hỗ trợ: " + string.Join(", ", staff.Select(p => p.Name)));
        if (qd.Count > 0) parts.Add("Phòng ban: " + string.Join(", ", qd.Select(kv => $"{Fmt.SupportDepartments[int.Parse(kv.Key)]} ×{kv.Value}")));
        if (qg.Count > 0) parts.Add("Nhóm khác: " + string.Join(", ", qg.Select(kv => $"Nhóm {leaders.First(l => l.Id == int.Parse(kv.Key)).Name} ×{kv.Value}")));
        var scope = qd.Count > 0 ? SupportScope.OtherDept : qg.Count > 0 ? SupportScope.OtherTeam : SupportScope.Team;
        var text = string.Join(" · ", parts) + " — Lý do: " + reason;
        if (text.Length > 2000) text = text[..2000];
        pick.Emp = emp; pick.Qd = qd; pick.Qg = qg; pick.Reason = reason;
        return (scope, text, pick.ToJson());
    }

    /// <summary>addOut: thêm việc ngoài kế hoạch (đã chọn) vào ngày đang xem.</summary>
    [HttpPost]
    public async Task<IActionResult> AddUnplannedTask(TaskFieldsInput input) => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true, managerOk: true);
        // Nút "Thêm công việc ngoài kế hoạch" chỉ có trên form kế hoạch (chưa gửi / đang sửa, chưa được duyệt).
        var sent = await Db.DayPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Date == Date && p.EmployeeId == Scope.Me!.Id);
        if (!SelfApproves(Scope.Me)) StaffReports.RequireEditable(sent);
        await StaffReports.RequireActiveProjectAsync(Db, input.ProjectId);
        var (title, item) = await input.ResolveAsync(Db);
        StaffReports.Clip(title, 300);
        Db.WorkTasks.Add(new WorkTask
        {
            Date = Date, EmployeeId = Scope.Me!.Id, ProjectId = input.ProjectId, PlanItemId = item?.Id, Title = title,
            Start = input.Start, End = input.End, Status = TaskState.NotStarted, Source = TaskSource.Unplanned,
            // Kế hoạch đã gửi: việc mới chỉ được chọn khi NV gửi lại kế hoạch có tích việc này (form sửa tích sẵn —
            // StaffMorningVm.IsOn), để Leader luôn duyệt đúng danh sách việc và lịch tháng không lệch báo cáo.
            Selected = sent is null, CreatedAt = VnTime.UtcNow,
        });
        await Db.SaveChangesAsync();
    }, "Đã thêm việc ngoài kế hoạch");

    // ======================= Báo cáo cuối ngày =======================

    /// <summary>nvEvening. ?open=1,2 → mở "Chi tiết" các việc (S.open).</summary>
    [HttpGet]
    public async Task<IActionResult> Evening()
    {
        // Leader có báo cáo cuối ngày kiểu người hỗ trợ (tích "Đã hỗ trợ"; Leader dự án báo kết quả) ở chế độ cá nhân ?as=mine (yêu cầu 2026-10-22); Leader ở chế độ khác: về lịch tháng.
        if (Scope.Account.Role == Role.Leader && !Scope.OwnCalendar) return Redirect(Ui.To(Request, "/Staff/Month", ("as", "mine")));
        await D.EnsureAsync(Date, Date.AddDays(14));
        return View(new StaffEveningVm { Me = Scope.Me, Open = StaffReports.OpenIds(Request) });
    }

    /// <summary>Báo cáo cuối ngày đã gửi (chờ duyệt) hoặc đã duyệt thì khóa chỉnh sửa; Leader từ chối → mở lại để nhân sự cập nhật (yêu cầu 2026-10-20).</summary>
    async Task EnsureReportOpenAsync()
    {
        var me = Scope.Me!.Id;
        var r = await Db.DayReports.AsNoTracking().FirstOrDefaultAsync(x => x.Date == Date && x.EmployeeId == me);
        if (r is null || r.LeaderDecision == Decision.No) return;
        throw new BizException(r.LeaderDecision == Decision.Ok
            ? "Báo cáo đã được duyệt — không sửa được nữa"
            : "Báo cáo đã gửi, đang chờ Leader duyệt — bấm “Rút lại để sửa” nếu cần chỉnh");
    }

    /// <summary>POST /Staff/WithdrawReport — nhân viên rút lại báo cáo cuối ngày đang chờ duyệt để sửa (đã duyệt thì không rút được; Leader hoàn tác duyệt mới mở lại).</summary>
    [HttpPost]
    public async Task<IActionResult> WithdrawReport() => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true);
        var me = Scope.Me!.Id;
        var r = await Db.DayReports.FirstOrDefaultAsync(x => x.Date == Date && x.EmployeeId == me) ?? throw new BizException("Chưa có báo cáo để rút lại");
        if (r.LeaderDecision == Decision.Ok) throw new BizException("Báo cáo đã được duyệt, không rút lại được");
        Db.DayReports.Remove(r);
        await Db.SaveChangesAsync();
    }, "Đã rút lại báo cáo — bạn có thể cập nhật rồi gửi lại");

    /// <summary>openUpdate → Lưu cập nhật: trạng thái, ghi chú, ảnh, kế hoạch tiếp theo (việc chuyển tiếp), đầu mục.</summary>
    [HttpPost]
    public async Task<IActionResult> UpdateTask(StaffUpdateInput input) => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true);
        await EnsureReportOpenAsync();
        var me = Scope.Me!;
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == input.TaskId && x.Selected)
            ?? throw new BizException("Không tìm thấy công việc");
        // Dự án đã có Leader dự án: mọi vấn đề / nội dung của dự án do Leader dự án báo cáo cuối ngày (kể cả việc người hỗ trợ làm một mình);
        // người hỗ trợ chỉ tích "đã hỗ trợ" (TickSupport). Việc của chính mình ở dự án chưa có Leader dự án: báo cáo đầy đủ như trước.
        var leadId = await Db.Assignments.AsNoTracking().Where(a => a.ProjectId == t.ProjectId && a.Role == ProjectRole.Lead)
            .Select(a => (int?)a.EmployeeId).FirstOrDefaultAsync();
        if (t.EmployeeId == me.Id)
        {
            if (leadId is int l0 && l0 != me.Id) throw new BizException("Việc hỗ trợ: chỉ cần tích “Đã hỗ trợ”, Leader dự án báo cáo kết quả");
        }
        else if (leadId != me.Id) throw new BizException("Không tìm thấy công việc");
        var s = StaffReports.ParseEnum<TaskState>(input.State) ?? t.Status;
        // Chỉnh thời gian làm việc của hạng mục (yêu cầu 2026-10-15).
        var tStart = string.IsNullOrEmpty(input.Start) ? t.Start : input.Start;
        var tEnd = string.IsNullOrEmpty(input.End) ? t.End : input.End;
        if (!Fmt.IsHhmm(tStart) || !Fmt.IsHhmm(tEnd) || tEnd.Le(tStart)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        var note = StaffReports.Clip(input.Note, 2000);
        if (s != TaskState.Done && note is null) throw new BizException("Nhập lý do chưa hoàn thành");
        var cur = t.CarriedToId is int cid ? await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == cid) : null;
        // Việc của Leader / TP: chuyển tiếp tự duyệt (không ai duyệt Leader ở danh sách của Leader; Trưởng phòng duyệt cả báo cáo) — cả khi người báo cáo là Leader dự án (nhân viên) báo thay (yêu cầu 2026-10-23)
        var ownerSelfOk = me.Role == Role.Leader || await Db.People.AsNoTracking().AnyAsync(p => p.Id == t.EmployeeId && (p.Role == Role.Leader || p.Role == Role.Manager || p.Role == Role.Admin));

        DateOnly nd = default;
        string ns = "", ne = "", nt = "";
        var proposed = false;   // có đề xuất chuyển tiếp mới / vừa sửa (chờ Leader duyệt)
        if (s != TaskState.Done)
        {
            nt = StaffReports.Clip(input.NextTitle, 300) ?? throw new BizException("Nhập nội dung kế hoạch tiếp theo");
            ns = input.NextStart ?? ""; ne = input.NextEnd ?? "";
            if (!Fmt.IsHhmm(ns) || !Fmt.IsHhmm(ne) || ne.Le(ns)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
            if (Fmt.ParseKey(input.NextDate) is not DateOnly d0 || !StaffReports.NextDateOptions(t.Date, cur?.Date).Contains(d0))
                throw new BizException("Chọn ngày chuyển sang");
            nd = d0;
        }
        {
            await using var tx = await Db.Database.BeginTransactionAsync();
            if (s != TaskState.Done)
            {
                if (cur is not null)
                {
                    if (!(cur.Date == nd && cur.Start == ns && cur.End == ne && cur.Title == nt))
                    {
                        cur.Date = nd; cur.Start = ns; cur.End = ne; cur.Title = nt;
                        cur.CarryOk = ownerSelfOk; cur.EditedBy = null;   // Leader tự duyệt việc chuyển tiếp của mình (Trưởng phòng duyệt cả báo cáo)
                        await Flow.SyncCarryToPlanAsync(cur);
                        proposed = true;
                    }
                    t.CarriedToId = cur.Id; t.CarryDate = nd;
                }
                else
                {
                    var n = new WorkTask
                    {
                        Date = nd, EmployeeId = t.EmployeeId, ProjectId = t.ProjectId, PlanItemId = t.PlanItemId, Title = nt,
                        Start = ns, End = ne, Status = TaskState.NotStarted, Source = TaskSource.CarryOver, CarryOk = ownerSelfOk,
                        CarriedFromId = t.Id, FromDate = t.Date, Selected = false, CreatedAt = VnTime.UtcNow,
                    };
                    Db.WorkTasks.Add(n);
                    await Db.SaveChangesAsync();
                    t.CarriedToId = n.Id; t.CarryDate = nd;
                    proposed = true;
                }
            }
            else if (cur is not null)
            {
                // Đổi lại thành Hoàn thành: xóa việc chuyển tiếp nếu nó còn "Chưa làm".
                if (cur.Status == TaskState.NotStarted) await Flow.DeleteTaskAsync(cur.Id);
                t.CarriedToId = null; t.CarryDate = null;
            }

            t.Start = tStart; t.End = tEnd;
            t.Status = s;
            t.Note = note;
            t.UpdatedAt = VnTime.UtcNow;

            if (t.PlanItemId is int iid && await Db.PlanItems.FirstOrDefaultAsync(i => i.Id == iid) is { } it)
            {
                if (s == TaskState.Done && input.ItemDone && it.Status != TaskState.Done)
                {
                    it.Status = TaskState.Done; it.DoneDate = t.Date; it.DoneBy = me.Name;
                }
                else if (s is TaskState.InProgress or TaskState.Done or TaskState.Late && it.Status == TaskState.NotStarted)
                    it.Status = TaskState.InProgress;
            }
            await Db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        // Báo cáo cuối ngày đã được duyệt thì Leader không nhận thông báo gửi báo cáo nữa → báo riêng đề xuất chuyển tiếp.
        if (proposed && me.Role != Role.Leader && await Db.DayReports.AnyAsync(r => r.Date == t.Date && r.EmployeeId == t.EmployeeId && r.LeaderDecision == Decision.Ok))
            await Notify.CarryProposedAsync(t.EmployeeId, t.Date, nt, nd);
    }, "Đã cập nhật");

    /// <summary>Người hỗ trợ tích / bỏ tích "Đã hỗ trợ" cho việc của mình ở dự án đã có Leader dự án (Leader dự án báo cáo kết quả).</summary>
    [HttpPost]
    public async Task<IActionResult> TickSupport(int taskId, bool on, string? start, string? end) => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true);
        await EnsureReportOpenAsync();
        var me = Scope.Me!;
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.EmployeeId == me.Id && x.Selected)
            ?? throw new BizException("Không tìm thấy công việc");
        var leadId = await Db.Assignments.AsNoTracking().Where(a => a.ProjectId == t.ProjectId && a.Role == ProjectRole.Lead)
            .Select(a => (int?)a.EmployeeId).FirstOrDefaultAsync();
        if (leadId is null || leadId == me.Id) throw new BizException("Việc này báo cáo đầy đủ ở nút Cập nhật");
        // NV hỗ trợ cũng chỉnh được giờ làm thực tế của việc (yêu cầu 2026-10-15).
        var s0 = string.IsNullOrEmpty(start) ? t.Start : start;
        var e0 = string.IsNullOrEmpty(end) ? t.End : end;
        if (!Fmt.IsHhmm(s0) || !Fmt.IsHhmm(e0) || e0.Le(s0)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        t.Start = s0; t.End = e0;
        t.Supported = on;
        t.UpdatedAt = VnTime.UtcNow;
        await Db.SaveChangesAsync();
    }, null);

    /// <summary>openExtra(false): NV đề xuất phát sinh (chờ Leader duyệt).</summary>
    [HttpPost]
    public async Task<IActionResult> ProposeExtra(ExtraInput input) => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true);
        await EnsureReportOpenAsync();
        await StaffReports.RequireActiveProjectAsync(Db, input.ProjectId);
        StaffReports.Clip(input.Title, 300);
        StaffReports.Clip(input.Desc, 2000);
        await Flow.CreateExtraAsync(Date, Scope.Me!.Id, input.ProjectId, input.Title ?? "", input.Desc,
            string.IsNullOrEmpty(input.Start) ? null : input.Start, input.End,
            byLeader: Scope.Me!.Role == Role.Leader, Scope.ActorName, Scope.Me!.Role == Role.Leader ? Role.Leader : Role.Staff);   // Leader: phát sinh của mình tự được duyệt
    }, "Đã gửi đề xuất phát sinh");

    /// <summary>submit eodForm: chặn nếu còn việc chưa xong chưa có kế hoạch tiếp; gửi lại = Leader duyệt lại.</summary>
    [HttpPost]
    public async Task<IActionResult> SubmitReport(StaffEveningInput input) => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true);
        await EnsureReportOpenAsync();
        var me = Scope.Me!.Id;
        // Dự án đã có Leader dự án (yêu cầu 2026-10-14): người hỗ trợ chỉ cần tích "đã hỗ trợ" mọi việc của mình; Leader dự án báo cáo kết quả
        // (hoàn thành / chưa + kế hoạch tiếp) cho cả việc của mình lẫn việc của người hỗ trợ trong dự án mình phụ trách.
        var leads = await Db.Assignments.AsNoTracking().Where(a => a.Role == ProjectRole.Lead).Select(a => new { a.ProjectId, a.EmployeeId }).ToListAsync();
        var supportPj = leads.Where(l => l.EmployeeId != me).Select(l => l.ProjectId).ToList();
        var ledPj = leads.Where(l => l.EmployeeId == me).Select(l => l.ProjectId).ToList();
        var miss = await Db.WorkTasks.CountAsync(t => t.Date == Date && t.EmployeeId == me && t.Selected
            && t.Status != TaskState.Done && t.CarriedToId == null && !supportPj.Contains(t.ProjectId));
        if (miss > 0) throw new BizException($"Còn {miss} việc chưa hoàn thành chưa có kế hoạch tiếp theo");
        var missTeam = await Db.WorkTasks.CountAsync(t => t.Date == Date && t.EmployeeId != me && t.Selected && ledPj.Contains(t.ProjectId)
            && t.Status != TaskState.Done && t.CarriedToId == null);
        if (missTeam > 0) throw new BizException($"Còn {missTeam} việc của dự án bạn phụ trách chưa báo hoàn thành hay kế hoạch tiếp theo");
        var missSup = await Db.WorkTasks.CountAsync(t => t.Date == Date && t.EmployeeId == me && t.Selected && !t.Supported && supportPj.Contains(t.ProjectId));
        if (missSup > 0) throw new BizException($"Còn {missSup} việc hỗ trợ chưa tích “Đã hỗ trợ”");
        // Việc chưa hoàn thành (của mình, không phải việc hỗ trợ; và việc của dự án mình phụ trách): BẮT BUỘC có lý do + chuyển sang ngày SAU ngày gửi báo cáo — chặn cả dữ liệu cũ thiếu / sai (yêu cầu 2026-10-23).
        var floor = Date > VnTime.Today ? Date : VnTime.Today;
        var openTasks = await Db.WorkTasks.AsNoTracking().Where(t => t.Date == Date && t.Selected && t.Status != TaskState.Done && t.CarriedToId != null
            && ((t.EmployeeId == me && !supportPj.Contains(t.ProjectId)) || (t.EmployeeId != me && ledPj.Contains(t.ProjectId)))).ToListAsync();
        foreach (var t in openTasks)
        {
            if (string.IsNullOrWhiteSpace(t.Note)) throw new BizException($"Việc “{t.Title}” chưa hoàn thành: nhập lý do (bấm Cập nhật)");
            if (t.CarryDate is not DateOnly cd || cd <= floor) throw new BizException($"Việc “{t.Title}” chưa hoàn thành: chọn kế hoạch chuyển sang ngày sau {Fmt.Dmy(floor)} (bấm Cập nhật)");
        }
        var note = StaffReports.Clip(input.Note, 2000);
        var r = await Db.DayReports.FirstOrDefaultAsync(x => x.Date == Date && x.EmployeeId == me);
        if (r is null)
        {
            r = new DayReport { Date = Date, EmployeeId = me };
            Db.DayReports.Add(r);
        }
        r.Blocked = input.Blocked == "yes";
        r.Note = note;
        r.SentAt = VnTime.UtcNow;
        r.LeaderDecision = null; r.LeaderBy = null; r.LeaderAt = null; r.LeaderNote = null;
        if (Scope.Account.Role == Role.Leader) { r.LeaderDecision = Decision.Ok; r.LeaderBy = "Tự xác nhận"; r.LeaderAt = VnTime.UtcNow; }   // Leader tự xác nhận, không qua duyệt (yêu cầu 2026-10-22)
        await Db.SaveChangesAsync();
        if (Scope.Account.Role != Role.Leader) await Notify.ReportSubmittedAsync(me, Date);
    }, "Đã gửi báo cáo cuối ngày");
}
