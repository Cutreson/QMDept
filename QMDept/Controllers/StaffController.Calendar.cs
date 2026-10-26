using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;
using QMDept.ViewModels;

namespace QMDept.Controllers;

/// <summary>
/// Giao diện Nhân viên — Kế hoạch tháng (nvMonth, teamDayList, calAssignForm) và Thông tin dự án (nvProjects, projectDetail).
/// Leader/TP xem các trang này ở chế độ chỉ xem; riêng chế độ "Tất cả nhân sự" có form Giao thêm việc (AssignTask).
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
        var leaveReqs = (Scope.Account.Role == Role.Staff || (Scope.Account.Role == Role.Leader && Scope.OwnCalendar)) && !Scope.IsViewingOther
            ? await Db.LeaveRequests.AsNoTracking().Where(r => r.PersonId == Scope.Account.Id && r.Date == Date).ToListAsync() : [];
        ViewData["Title"] = "Kế hoạch tháng";
        return View(new StaffMonthVm
        {
            MonthStart = ms,
            ReadOnly = ro,
            AllMode = ro && Scope.AllStaff,
            Me = Scope.Me,
            People = Scope.TeamPeople,
            Proposals = props,
            LeaveRequests = leaveReqs,
        });
    }

    /// <summary>
    /// POST /Staff/AssignTask — submit của calAssignForm: giao một việc (nguồn "Leader giao") cho nhiều NV trong ngày ?d=,
    /// đặt vai trò trong dự án (Hỗ trợ / Leader dự án). Leader: nhân viên trong nhóm (+ chính mình); TP / Quản trị: chỉ Leader (không giao vượt cấp).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AssignTask(int projectId, int? itemId, string? title, string? start, string? end, string? priority)
    {
        return await Do(async () =>
        {
            RequireLead();
            // Không giao vượt cấp (yêu cầu 2026-10-20): Trưởng phòng / Quản trị chỉ giao cho Leader; Leader giao tiếp cho nhân viên trong nhóm.
            var pool = Scope.IsManager ? Scope.TeamPeople.Where(p => p.Role == Role.Leader).ToList() : Scope.TeamPeople;
            if (Scope.IsManager && Request.Form["emp"].Any(s => int.TryParse(s, out var i) && Scope.TeamPeople.Any(p => p.Id == i && p.Role == Role.Staff)))
                throw new BizException("Trưởng phòng chỉ giao việc cho Leader — Leader giao tiếp cho nhân viên, không giao vượt cấp");
            var people = StaffCalendar.ReadPeople(Request.Form, pool);
            var lines = TaskLines.Validate(TaskLines.Read(Request.Form, title, start, end));
            foreach (var pid in people) await TaskLines.EnsureFreeAsync(Db, pid, null, Date, lines);   // không trùng giờ việc đã có của từng người được giao
            var pri = StaffReports.ParseEnum<TaskPriority>(priority) ?? TaskPriority.Normal;
            await using var tx = await Db.Database.BeginTransactionAsync();
            foreach (var l in lines) await Flow.AssignTaskAsync(Date, projectId, itemId, l.Title, l.Start, l.End, people, Scope.ActorName, TaskSource.Assigned, Scope.Account.Id, pri, silent: true);
            await tx.CommitAsync();
            await Notify.TaskAssignedAsync(people, Date, string.Join("; ", lines.Select(l => l.Title)), priority: pri);
            Toast($"Đã giao {lines.Count} việc cho {people.Count} người ngày {Fmt.Dm(Date)}");
        });
    }


    /// <summary>
    /// POST /Staff/AddOwnTask — Leader (ở "Lịch của tôi", ?as=mine) cập nhật kế hoạch cá nhân: thêm việc của CHÍNH MÌNH vào ngày ?d=
    /// (nguồn Planned — không gắn nhãn, vào lịch ngay; yêu cầu 2026-10-17).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddOwnTask(int projectId, int? itemId, string? title, string? start, string? end) => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true, managerOk: true);
        var me = Scope.Me!;
        if (me.Role is Role.Staff) throw new BizException("Chỉ Leader / Trưởng phòng dùng mục này");
        // Ô "Loại" = Xin nghỉ (Leader): kiểm tra nghỉ trước; nghỉ nửa ngày / vài giờ + có nội dung công việc → thêm việc ngoài khung nghỉ; ghi đề xuất nghỉ sau cùng (không ghi dở khi lỗi).
        var isLeave = Request.Form["kind"] == "leave";
        if (isLeave && me.Role != Role.Leader) throw new BizException("Chỉ Leader xin nghỉ ở đây");
        (LeaveRules.Leave Lv, string Why)? lv = isLeave ? await ReadLeaveFormAsync() : null;
        var rawLines = TaskLines.Read(Request.Form, title, start, end);
        var withWork = !isLeave || (IsPartialLeave(lv!.Value.Lv) && rawLines.Count > 0);
        if (isLeave && withWork) foreach (var l in rawLines) EnsureOutsideLeave(lv!.Value.Lv, l.Start, l.End);
        if (withWork)
        {
            var lines = TaskLines.Validate(rawLines);
            await TaskLines.EnsureFreeAsync(Db, me.Id, me.Name, Date, lines);   // không trùng giờ việc đã có (yêu cầu 2026-10-24)
            await using var tx = await Db.Database.BeginTransactionAsync();
            foreach (var l in lines) await Flow.AssignTaskAsync(Date, projectId, itemId, l.Title, l.Start, l.End, [me.Id], Scope.ActorName, TaskSource.Planned, silent: true);
            await tx.CommitAsync();
        }
        if (lv is { } lq) await SaveLeaveRequestAsync(lq.Lv, lq.Why);
        var apn = isLeave ? await ApproverNameAsync(Scope.Account) : "";
        Toast(!isLeave ? "Đã thêm công việc vào kế hoạch cá nhân" : withWork ? $"Đã thêm công việc và gửi đề xuất xin nghỉ, chờ {apn} duyệt" : $"Đã gửi đề xuất xin nghỉ, chờ {apn} duyệt");
    });

    /// <summary>Nạp việc để xóa / chuyển: kiểm quyền (StaffCalendar.OwnsTask), chưa có kết quả, NV chưa báo cáo cuối ngày.</summary>
    async Task<WorkTask> LoadManagedTaskAsync(int id)
    {
        RequireLead();
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy công việc — có thể đã bị xóa");
        var emp = await Db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == t.EmployeeId);
        if (t.NeedsGiverReview && t.EmployeeId == Scope.Account.Id)
            throw new BizException($"Việc do {t.GivenBy} giao — không tự sửa / xóa được. Bấm “Đề xuất lại” để người giao duyệt");
        if (!StaffCalendar.OwnsTask(Scope.Account, Scope.IsManager, Scope.IsViewingOther, emp, t))
            throw new BizException(Scope.IsManager ? "Trưởng phòng chỉ xóa / chuyển lịch việc của mình và của Leader" : "Chỉ xóa / chuyển lịch việc của mình và của nhân viên trong nhóm");
        if (t.Status is TaskState.Done or TaskState.Late || await Db.DayReports.AnyAsync(r => r.Date == t.Date && r.EmployeeId == t.EmployeeId))
            throw new BizException("Việc đã có kết quả / báo cáo cuối ngày, không xóa hoặc chuyển lịch được");
        return t;
    }

    /// <summary>
    /// POST /Staff/AckTask — người được cấp quản lý giao việc xác nhận đã nhận và bắt đầu triển khai (yêu cầu 2026-10-21):
    /// ghi giờ xác nhận lên việc, người giao nhận thông báo đẩy. Chỉ người nhận, chỉ việc do người khác giao, chỉ một lần.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AckTask(int id) => await Do(async () =>
    {
        if (Scope.IsViewingOther) throw new BizException("Chế độ xem: không xác nhận thay người khác được");
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy công việc");
        if (t.EmployeeId != Scope.Account.Id) throw new BizException("Chỉ người được giao việc mới xác nhận được");
        if (t.GivenById is not int giver) throw new BizException("Việc này không cần xác nhận");
        if (t.AckAt is not null) throw new BizException("Bạn đã xác nhận việc này rồi");
        t.AckAt = VnTime.UtcNow;
        await Db.SaveChangesAsync();
        await Notify.TaskAckedAsync(giver, t.EmployeeId, t.Date, t.Title);
    }, "Đã xác nhận nhận việc");
    /// <summary>
    /// POST /Staff/UpdateOwnTask — Trưởng phòng / Quản trị tự cập nhật việc CỦA CHÍNH MÌNH, giống hệt cập nhật báo cáo cuối ngày của nhân viên (giờ thực tế, trạng thái, đầu mục đã xong, kế hoạch tiếp theo, lý do / kết quả)
    /// nhưng chỉ để tự quản lý kế hoạch (yêu cầu 2026-10-24): KHÔNG báo cáo cuối ngày, không ai duyệt, không thông báo; việc chuyển tiếp tự duyệt, ở ngày gốc hiện "Chuyển sang dd/MM/yyyy".
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdateOwnTask(StaffUpdateInput input) => await Do(async () =>
    {
        if (Scope.IsViewingOther || Scope.Account.Role is not (Role.Manager or Role.Admin)) throw new BizException("Chỉ Trưởng phòng tự cập nhật việc của mình ở mục này");
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == input.TaskId) ?? throw new BizException("Không tìm thấy công việc — có thể đã bị xóa");
        if (t.EmployeeId != Scope.Account.Id) throw new BizException("Chỉ cập nhật được việc của chính mình");
        if (t.NeedsGiverReview) throw new BizException($"Việc do {t.GivenBy} giao — báo kết quả để người giao duyệt");
        var s = StaffReports.ParseEnum<TaskState>(input.State) ?? t.Status;
        var tStart = string.IsNullOrEmpty(input.Start) ? t.Start : input.Start;
        var tEnd = string.IsNullOrEmpty(input.End) ? t.End : input.End;
        if (!Fmt.IsHhmm(tStart) || !Fmt.IsHhmm(tEnd) || tEnd.Le(tStart)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        var note = StaffReports.Clip(input.Note, 2000);
        if (s != TaskState.Done && note is null) throw new BizException("Nhập lý do chưa hoàn thành");
        if (tStart != t.Start || tEnd != t.End) await TaskLines.EnsureFreeAsync(Db, t.EmployeeId, null, t.Date, [new TaskLines.Line(t.Title, tStart, tEnd)], t.Id);
        var cur = t.CarriedToId is int cid ? await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == cid) : null;
        DateOnly nd = default; string ns = "", ne = "", nt = "";
        if (s != TaskState.Done)
        {
            nt = StaffReports.Clip(input.NextTitle, 300) ?? throw new BizException("Nhập nội dung kế hoạch tiếp theo");
            ns = input.NextStart ?? ""; ne = input.NextEnd ?? "";
            if (!Fmt.IsHhmm(ns) || !Fmt.IsHhmm(ne) || ne.Le(ns)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
            if (Fmt.ParseKey(input.NextDate) is not DateOnly d0 || !StaffReports.NextDateOptions(t.Date, cur?.Date).Contains(d0)) throw new BizException("Chọn ngày chuyển sang");
            nd = d0;
            if ((await Db.Leaves.AsNoTracking().Where(l => l.Date == nd && l.EmployeeId == t.EmployeeId).ToListAsync()).Any(Dept.IsFullDayLeave))
                throw new BizException($"Bạn nghỉ cả ngày {Fmt.Dmy(nd)} — chọn ngày khác");
            await TaskLines.EnsureFreeAsync(Db, t.EmployeeId, null, nd, [new TaskLines.Line(nt, ns, ne)], cur?.Id);
        }
        await using var tx = await Db.Database.BeginTransactionAsync();
        if (s != TaskState.Done)
        {
            if (cur is not null)
            {
                cur.Date = nd; cur.Start = ns; cur.End = ne; cur.Title = nt; cur.CarryOk = true; cur.EditedBy = null;
                await Flow.SyncCarryToPlanAsync(cur);
                t.CarriedToId = cur.Id;
            }
            else
            {
                var n = new WorkTask
                {
                    Date = nd, EmployeeId = t.EmployeeId, ProjectId = t.ProjectId, PlanItemId = t.PlanItemId, Title = nt, Start = ns, End = ne,
                    Status = TaskState.NotStarted, Source = TaskSource.CarryOver, CarryOk = true, CarriedFromId = t.Id, FromDate = t.Date, Selected = false, CreatedAt = VnTime.UtcNow,
                };
                Db.WorkTasks.Add(n);
                await Db.SaveChangesAsync();
                await Flow.SyncCarryToPlanAsync(n);
                t.CarriedToId = n.Id;
            }
            t.CarryDate = nd; t.CarryBy = Scope.ActorName; t.CarryAt = VnTime.UtcNow;
        }
        else if (cur is not null)
        {
            if (cur.Status == TaskState.NotStarted) await Flow.DeleteTaskAsync(cur.Id);   // đổi lại thành Hoàn thành: bỏ việc chuyển tiếp chưa làm
            t.CarriedToId = null; t.CarryDate = null;
        }
        t.Start = tStart; t.End = tEnd; t.Status = s; t.Note = note; t.UpdatedAt = VnTime.UtcNow;
        if (t.PlanItemId is int iid && await Db.PlanItems.FirstOrDefaultAsync(i => i.Id == iid) is { } it)
        {
            if (s == TaskState.Done && input.ItemDone && it.Status != TaskState.Done) { it.Status = TaskState.Done; it.DoneDate = t.Date; it.DoneBy = Scope.ActorName; }
            else if (s is TaskState.InProgress or TaskState.Done or TaskState.Late && it.Status == TaskState.NotStarted) it.Status = TaskState.InProgress;
        }
        await Db.SaveChangesAsync();
        await tx.CommitAsync();
    }, "Đã cập nhật");

    /// <summary>
    /// POST /Staff/ReviewDone — NGƯỜI GIAO duyệt kết quả việc đã giao (yêu cầu 2026-10-24): v=ok → tính là xong; v=no (bắt buộc lý do) → việc về "Đang làm", báo cáo cuối ngày của người nhận mở lại để cập nhật.
    /// Chỉ người giao, chỉ việc người nhận đã báo hoàn thành và đã gửi báo cáo cuối ngày.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReviewDone(int id, string? v, string? note) => await Do(async () =>
    {
        if (Scope.IsViewingOther) throw new BizException("Chế độ xem: không duyệt thay người khác được");
        if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy công việc");
        if (t.GivenById != Scope.Account.Id) throw new BizException("Chỉ người giao việc mới duyệt được kết quả công việc này");
        if (!t.DonePending) throw new BizException("Việc này không còn chờ duyệt — tải lại trang");
        var now = VnTime.UtcNow; var actor = Scope.ActorName;
        if (v == "ok")
        {
            t.DonePending = false; t.DoneReviewBy = actor; t.DoneReviewAt = now; t.DoneRejectNote = null;
            if (t.ItemDoneClaim && t.PlanItemId is int iid && await Db.PlanItems.FirstOrDefaultAsync(i => i.Id == iid) is { } it && it.Status != TaskState.Done)
            { it.Status = TaskState.Done; it.DoneDate = t.Date; it.DoneBy = await Db.People.Where(p => p.Id == t.EmployeeId).Select(p => p.Name).FirstOrDefaultAsync() ?? actor; }
            t.ItemDoneClaim = false;
            await Db.SaveChangesAsync();
            await Notify.TaskDoneDecidedAsync(t.EmployeeId, t.Date, t.Title, true);
            Toast("Đã duyệt — công việc được tính là hoàn thành");
        }
        else
        {
            var why = StaffReports.Clip(note, 500) ?? throw new BizException("Nhập lý do chưa duyệt");
            t.Status = TaskState.InProgress; t.DonePending = false; t.ItemDoneClaim = false;
            t.DoneReviewBy = actor; t.DoneReviewAt = now; t.DoneRejectNote = why;
            // mở lại báo cáo cuối ngày của người nhận để cập nhật lại kết quả
            var r = await Db.DayReports.FirstOrDefaultAsync(x => x.Date == t.Date && x.EmployeeId == t.EmployeeId);
            if (r is not null && r.LeaderDecision != Decision.No)
            {
                r.LeaderDecision = Decision.No; r.LeaderBy = actor; r.LeaderAt = now;
                r.LeaderNote = $"{actor} chưa duyệt kết quả việc “{t.Title}”: {why}";
            }
            await Db.SaveChangesAsync();
            await Notify.TaskDoneDecidedAsync(t.EmployeeId, t.Date, t.Title, false, why);
            Toast("Đã gửi yêu cầu cập nhật lại cho người nhận việc");
        }
    });

    /// <summary>
    /// POST /Staff/ProposeChange — NGƯỜI NHẬN việc do người khác giao đề xuất lại nội dung / thời gian (chồng chéo, chưa phù hợp) → chờ NGƯỜI GIAO duyệt; không được xóa (yêu cầu 2026-10-24).
    /// Việc giữ nguyên cho tới khi người giao đồng ý. Bắt buộc lý do; thời gian mới không được trùng việc khác của người nhận.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ProposeChange(int id, string? title, string? to, string? start, string? end, string? reason) => await Do(async () =>
    {
        if (Scope.IsViewingOther) throw new BizException("Chế độ xem: không đề xuất thay người khác được");
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy công việc — có thể đã bị xóa");
        if (t.EmployeeId != Scope.Account.Id || !t.NeedsGiverReview) throw new BizException("Chỉ người được giao mới đề xuất lại được việc do người khác giao");
        if (t.RePending) throw new BizException($"Đã đề xuất, đang chờ {t.GivenBy} duyệt");
        if (t.Status is TaskState.Done or TaskState.Late || t.CarriedToId is not null || await Db.DayReports.AnyAsync(r => r.Date == t.Date && r.EmployeeId == t.EmployeeId))
            throw new BizException("Việc đã có kết quả / báo cáo cuối ngày, không đề xuất lại được");
        var why = StaffReports.Clip(reason, 500) ?? throw new BizException("Nhập lý do đề xuất lại");
        var nTitle = StaffReports.Clip(title, 300) ?? t.Title;
        var nDate = string.IsNullOrWhiteSpace(to) ? t.Date : Fmt.ParseInput(to) ?? throw new BizException("Nhập ngày đúng dạng dd/MM/yyyy");
        var nS = string.IsNullOrEmpty(start) ? t.Start : start; var nE = string.IsNullOrEmpty(end) ? t.End : end;
        if (!Fmt.IsHhmm(nS) || !Fmt.IsHhmm(nE) || nE.Le(nS)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        if (nDate < Scope.Today) throw new BizException("Chỉ đề xuất ngày từ hôm nay trở đi");
        if (nDate == t.Date && nTitle == t.Title && nS == t.Start && nE == t.End) throw new BizException("Chưa thay đổi nội dung hay thời gian nào so với việc được giao");
        await EnsureProposedSlotAsync(t, nDate, nTitle, nS, nE);
        t.RePending = true; t.ReTitle = nTitle; t.ReDate = nDate; t.ReStart = nS; t.ReEnd = nE; t.ReReason = why; t.ReAt = VnTime.UtcNow; t.ReRejectNote = null; t.ReReviewBy = null; t.ReReviewAt = null;
        await Db.SaveChangesAsync();
        if (t.GivenById is int giver) await Notify.TaskChangeProposedAsync(giver, t.EmployeeId, t.Date, t.Title, why);
        Toast($"Đã gửi đề xuất — chờ {t.GivenBy} duyệt");
    });

    /// <summary>Thời gian / ngày đề xuất: người nhận không nghỉ cả ngày và không trùng giờ việc khác.</summary>
    async Task EnsureProposedSlotAsync(WorkTask t, DateOnly nDate, string nTitle, string nS, string nE)
    {
        if ((await Db.Leaves.AsNoTracking().Where(l => l.Date == nDate && l.EmployeeId == t.EmployeeId).ToListAsync()).Any(Dept.IsFullDayLeave))
            throw new BizException($"Người nhận nghỉ cả ngày {Fmt.Dmy(nDate)} — chọn ngày khác");
        await TaskLines.EnsureFreeAsync(Db, t.EmployeeId, null, nDate, [new TaskLines.Line(nTitle, nS, nE)], t.Id);
    }

    /// <summary>POST /Staff/DecideChange — NGƯỜI GIAO đồng ý (áp dụng nội dung / thời gian mới, người nhận coi như đã xác nhận) hoặc không đồng ý (bắt buộc lý do, việc giữ nguyên).</summary>
    [HttpPost]
    public async Task<IActionResult> DecideChange(int id, string? v, string? note) => await Do(async () =>
    {
        if (Scope.IsViewingOther) throw new BizException("Chế độ xem: không duyệt thay người khác được");
        if (v is not ("ok" or "no")) throw new BizException("Thao tác không hợp lệ");
        var t = await Db.WorkTasks.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BizException("Không tìm thấy công việc");
        if (t.GivenById != Scope.Account.Id) throw new BizException("Chỉ người giao việc mới duyệt được đề xuất này");
        if (!t.RePending) throw new BizException("Đề xuất này không còn chờ duyệt — tải lại trang");
        var actor = Scope.ActorName; var oldDate = t.Date; var oldTitle = t.Title;
        if (v == "ok")
        {
            var nDate = t.ReDate ?? t.Date; var nTitle = t.ReTitle ?? t.Title; var nS = t.ReStart ?? t.Start; var nE = t.ReEnd ?? t.End;
            await EnsureProposedSlotAsync(t, nDate, nTitle, nS, nE);
            await using var tx = await Db.Database.BeginTransactionAsync();
            await Flow.EditTaskAsync(t, t.ProjectId, nTitle, nS, nE, actor, null);
            if (nDate != t.Date) await Flow.MoveTaskAsync(t, nDate, t.EmployeeId, actor, Scope.Account.Id);
            t.RePending = false; t.ReTitle = null; t.ReDate = null; t.ReStart = null; t.ReEnd = null; t.ReReason = null; t.ReRejectNote = null; t.ReReviewBy = actor; t.ReReviewAt = VnTime.UtcNow;
            t.AckAt ??= VnTime.UtcNow;   // người nhận tự đề xuất bản này nên coi như đã xác nhận
            await Db.SaveChangesAsync();
            await tx.CommitAsync();
            await Notify.TaskChangeDecidedAsync(t.EmployeeId, t.Date, oldTitle, true);
            Toast("Đã đồng ý — công việc được cập nhật theo đề xuất");
        }
        else
        {
            var why = StaffReports.Clip(note, 500) ?? throw new BizException("Nhập lý do không đồng ý");
            t.RePending = false; t.ReTitle = null; t.ReDate = null; t.ReStart = null; t.ReEnd = null; t.ReReason = null; t.ReRejectNote = why; t.ReReviewBy = actor; t.ReReviewAt = VnTime.UtcNow;
            await Db.SaveChangesAsync();
            await Notify.TaskChangeDecidedAsync(t.EmployeeId, oldDate, oldTitle, false, why);
            Toast("Đã gửi lý do không đồng ý cho người nhận việc");
        }
    });

    /// <summary>
    /// POST /Staff/EditTask — Leader / TP sửa việc trong quyền của mình (lịch của tôi, lịch nhóm / phòng, trang Trạng thái làm việc):
    /// dự án, nội dung, giờ, VÀ chuyển sang lịch khác (ngày từ hôm nay trở đi + người nhận: Leader → mình + nhân viên nhóm; TP → mình + Leader).
    /// Chuyển lịch gộp vào Sửa (yêu cầu 2026-10-21). Người nhận nghỉ cả ngày thì chặn; việc đã chuyển tiếp đi nơi khác không đổi ngày / người được.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EditTask(int id, int projectId, string? title, string? start, string? end, string? to, int? emp, string? priority) => await Do(async () =>
    {
        var t = await LoadManagedTaskAsync(id);
        // Kiểm tra phần chuyển lịch TRƯỚC khi ghi gì (để lỗi không ghi dở).
        var day = string.IsNullOrWhiteSpace(to) ? t.Date : Fmt.ParseInput(to) ?? throw new BizException("Nhập ngày đúng dạng dd/MM/yyyy");
        var target = emp ?? t.EmployeeId;
        var moved = day != t.Date || target != t.EmployeeId;
        Person? tp = null;
        if (moved)
        {
            if (t.CarriedToId is not null) throw new BizException("Việc này đã được chuyển tiếp sang ngày khác — sửa ngày / người ở việc mới đó");
            if (day < Scope.Today) throw new BizException("Chỉ chuyển đến hôm nay trở đi");
            if (t.FromDate is DateOnly fd && day <= fd) throw new BizException($"Việc chuyển tiếp phải đặt sau ngày của việc gốc ({Fmt.Dmy(fd)})");
            tp = await Db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == target) ?? throw new BizException("Không tìm thấy người nhận");
            if (!StaffCalendar.OwnsTask(Scope.Account, Scope.IsManager, false, tp, new WorkTask { EmployeeId = target }))
                throw new BizException("Chỉ chuyển cho mình hoặc người thuộc quyền của mình");
            if ((await Db.Leaves.AsNoTracking().Where(l => l.Date == day && l.EmployeeId == target).ToListAsync()).Any(Dept.IsFullDayLeave))
                throw new BizException($"{tp.Name} nghỉ cả ngày {Fmt.Dmy(day)} — chọn ngày hoặc người khác");
        }
        // không trùng giờ việc khác của người thực hiện trong ngày (yêu cầu 2026-10-24)
        if (Fmt.IsHhmm(start) && Fmt.IsHhmm(end) && !end!.Le(start!))
            await TaskLines.EnsureFreeAsync(Db, target, null, day, [new TaskLines.Line((title ?? t.Title).Trim(), start!, end!)], t.Id);
        await using var tx = await Db.Database.BeginTransactionAsync();
        await Flow.EditTaskAsync(t, projectId, title, start ?? "", end ?? "", Scope.ActorName, StaffReports.ParseEnum<TaskPriority>(priority));
        if (moved) await Flow.MoveTaskAsync(t, day, target, Scope.ActorName, Scope.Account.Id);
        await tx.CommitAsync();
        if (moved && target != Scope.Account.Id) await Notify.TaskAssignedAsync([target], day, t.Title, "Bạn được chuyển việc", t.Priority);
        Toast(moved ? $"Đã sửa và chuyển “{t.Title}” sang {(target == Scope.Account.Id || tp is null ? "" : tp.Name + " · ")}ngày {Fmt.Dmy(day)}" : "Đã sửa công việc");
    });

    /// <summary>POST /Staff/DeleteTask — Leader / TP xóa công việc khỏi lịch của mình hoặc của người thuộc quyền (lịch của tôi, lịch nhóm / phòng).</summary>
    [HttpPost]
    public async Task<IActionResult> DeleteTask(int id) => await Do(async () =>
    {
        var t = await LoadManagedTaskAsync(id);
        await Flow.DeleteTaskAsync(t.Id);
    }, "Đã xóa công việc");

    /// <summary>POST /Staff/DeleteOwnTask — Leader xóa việc kế hoạch cá nhân của chính mình (chỉ việc nguồn Planned do mình thêm).</summary>
    [HttpPost]
    public async Task<IActionResult> DeleteOwnTask(int id) => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true, managerOk: true);
        var me = Scope.Me!;
        if (me.Role is Role.Staff) throw new BizException("Chỉ Leader / Trưởng phòng dùng mục này");
        var t = await Db.WorkTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == me.Id && x.Source == TaskSource.Planned)
            ?? throw new BizException("Không tìm thấy công việc");
        await Flow.DeleteTaskAsync(t.Id);
    }, "Đã xóa công việc");

    /// <summary>
    /// Xin nghỉ nằm chung sheet "Đề xuất công việc / xin nghỉ" (yêu cầu 2026-10-20; ô kind=leave của ProposeCalendar / AddOwnTask): ngày = ngày đang chọn, nghỉ phép theo buổi /
    /// không lương theo giờ, BẮT BUỘC nêu lý do. Đọc + kiểm tra toàn bộ field nghỉ trong form (chưa ghi gì) — trả loại nghỉ và lý do.
    /// </summary>
    async Task<(LeaveRules.Leave Lv, string Why)> ReadLeaveFormAsync()
    {
        var me = Scope.Me!;
        if (me.Role == Role.Staff && me.LeaderId is null) throw new BizException("Bạn chưa được xếp nhóm — nhờ Trưởng phòng xếp nhóm trước khi xin nghỉ");
        if (me.Role is not (Role.Staff or Role.Leader)) throw new BizException("Chỉ nhân viên / Leader xin nghỉ");
        if (Date < Scope.Today) throw new BizException("Chỉ xin nghỉ cho hôm nay trở đi");
        var why = Request.Form["reason"].ToString().Trim();
        if (why.Length == 0) throw new BizException("Nhập lý do nghỉ (bắt buộc)");
        if (why.Length > 500) throw new BizException("Lý do tối đa 500 ký tự");
        var lt = StaffReports.ParseEnum<LeaveType>(Request.Form["type"].ToString()) ?? throw new BizException("Chọn loại nghỉ");
        LeaveRules.Leave lv;
        if (lt == LeaveType.Paid)
            lv = StaffReports.ParseEnum<LeaveSession>(Request.Form["session"].ToString()) is LeaveSession s ? LeaveRules.Paid(s) : throw new BizException("Chọn buổi nghỉ");
        else
        {
            var (v, err) = LeaveRules.Unpaid(Request.Form["from"].ToString(), Request.Form["to"].ToString());
            if (v is null) throw new BizException(err ?? "Chọn thời gian nghỉ");
            if (!LeaveRules.UnpaidFrom.Contains(v.From) || !LeaveRules.UnpaidTo.Contains(v.To)) throw new BizException("Chọn thời gian nghỉ");
            lv = v;
        }
        if (await Db.Leaves.AnyAsync(l => l.Date == Date && l.EmployeeId == me.Id)) throw new BizException("Ngày này đã được ghi nghỉ");
        var x = await Db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Date == Date && r.PersonId == me.Id);
        if (x is not null && x.Status == ExtraStatus.Approved) throw new BizException("Đề xuất xin nghỉ ngày này đã được duyệt");
        return (lv, why);
    }

    /// <summary>Nghỉ nửa ngày / vài giờ thì còn làm việc phần còn lại (Dept.IsFullDayLeave: nghỉ đủ 8 giờ = cả ngày).</summary>
    static bool IsPartialLeave(LeaveRules.Leave lv) => lv.Hours < 8;

    /// <summary>Việc làm trong phần thời gian còn lại không được trùng khung giờ nghỉ.</summary>
    static void EnsureOutsideLeave(LeaveRules.Leave lv, string? start, string? end)
    {
        if (start is not null && end is not null && Fmt.IsHhmm(start) && Fmt.IsHhmm(end) && start.Lt(lv.To) && end.Gt(lv.From))
            throw new BizException($"Công việc trùng giờ nghỉ ({lv.From}–{lv.To}) — chọn giờ làm việc ngoài khung nghỉ");
    }

    /// <summary>Ghi đề xuất xin nghỉ (chờ duyệt) + thông báo người duyệt.</summary>
    async Task SaveLeaveRequestAsync(LeaveRules.Leave lv, string why)
    {
        var me = Scope.Me!;
        var x = await Db.LeaveRequests.FirstOrDefaultAsync(r => r.Date == Date && r.PersonId == me.Id);
        var now = VnTime.UtcNow;
        if (x is null) { x = new LeaveRequest { Date = Date, PersonId = me.Id, CreatedAt = now }; Db.LeaveRequests.Add(x); }
        x.Role = me.Role; x.LeaveType = lv.Type; x.LeaveSession = lv.Session; x.LeaveFrom = lv.From; x.LeaveTo = lv.To;
        x.LeaveHours = lv.Hours; x.LeaveDays = lv.Days; x.Reason = why;
        x.Status = ExtraStatus.Pending; x.DecidedBy = null; x.DecidedAt = null; x.DecisionNote = null; x.UpdatedAt = now;
        await Db.SaveChangesAsync();
        await Notify.LeaveRequestedAsync(me.Id, me.Role, Date, $"{(lv.Type == LeaveType.Paid ? "Nghỉ phép" : "Nghỉ không lương")} {lv.From}–{lv.To} · {why}");
    }

    /// <summary>POST /Staff/CancelLeaveRequest — rút lại đề xuất xin nghỉ của mình (chưa được duyệt).</summary>
    [HttpPost]
    public async Task<IActionResult> CancelLeaveRequest(int id) => await Do(async () =>
    {
        RequireSelfStaff(leaderOk: true);
        var x = await Db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id && r.PersonId == Scope.Me!.Id) ?? throw new BizException("Không tìm thấy đề xuất xin nghỉ");
        if (x.Status == ExtraStatus.Approved) throw new BizException("Đề xuất đã được duyệt, không rút lại được — nhờ Leader hủy nghỉ");
        Db.LeaveRequests.Remove(x);
        await Db.SaveChangesAsync();
    }, "Đã rút đề xuất xin nghỉ");
    /// <summary>
    /// POST /Staff/ProposeCalendar — NV đề xuất ở lịch tháng (ngày = ngày đang chọn): công việc của mình (không có "emp"), hoặc người hỗ trợ /
    /// Leader hỗ trợ (field "emp" nhiều giá trị) cho dự án mình là Leader dự án. Leader nhóm duyệt thì mới ghi vào lịch.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ProposeCalendar(int projectId, int? itemId, string? title, string? start, string? end) => await Do(async () =>
    {
        RequireSelfStaff();
        // Ô "Loại đề xuất" = Xin nghỉ: nghỉ nửa ngày / vài giờ + có nội dung công việc → đề xuất thêm công việc ngoài khung nghỉ (Leader nhóm duyệt cả hai).
        var isLeave = Request.Form["kind"] == "leave";
        (LeaveRules.Leave Lv, string Why)? lv = isLeave ? await ReadLeaveFormAsync() : null;
        var rawLines = TaskLines.Read(Request.Form, title, start, end);
        var withWork = !isLeave || (IsPartialLeave(lv!.Value.Lv) && rawLines.Count > 0);
        if (isLeave && !withWork) { await SaveLeaveRequestAsync(lv!.Value.Lv, lv.Value.Why); Toast($"Đã gửi đề xuất xin nghỉ, chờ {await ApproverNameAsync(Scope.Account)} duyệt"); return; }
        if (isLeave) foreach (var rl in rawLines) EnsureOutsideLeave(lv!.Value.Lv, rl.Start, rl.End);
        var people = Request.Form["emp"].Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).ToList();
        // Xin người hỗ trợ theo số lượng: từng phòng ban (qd_<chỉ số>) và từng nhóm khác (qg_<Id Leader>) — không chỉ định người ngoài nhóm.
        var quotas = new List<(string Name, int Qty)>();
        for (var k = 0; k < Fmt.SupportDepartments.Length; k++)
            if (int.TryParse(Request.Form["qd_" + k], out var qd) && qd > 0) quotas.Add((Fmt.SupportDepartments[k], qd));
        foreach (var l in await Db.People.AsNoTracking().Where(p => p.Role == Role.Leader && !p.IsDeleted).ToListAsync())
            if (int.TryParse(Request.Form["qg_" + l.Id], out var qg) && qg > 0) quotas.Add(("Nhóm " + l.Name, qg));
        var includeSelf = Request.Form["self"].Count > 0;   // ô tích "Tôi thực hiện"
        var plines = TaskLines.Validate(rawLines);
        // không trùng giờ việc đã có của người đề xuất (nếu tự làm) và của người hỗ trợ được chỉ định (yêu cầu 2026-10-24)
        if (includeSelf) await TaskLines.EnsureFreeAsync(Db, Scope.Account.Id, Scope.Account.Name, Date, plines);
        foreach (var pid in people.Distinct()) await TaskLines.EnsureFreeAsync(Db, pid, null, Date, plines);
        await using var ptx = await Db.Database.BeginTransactionAsync();
        foreach (var pl in plines) await Flow.ProposeAsync(Date, Scope.Account.Id, projectId, itemId, pl.Title, pl.Start, pl.End, people, quotas, Request.Form["supportReason"].ToString(), includeSelf);
        await ptx.CommitAsync();
        if (lv is { } lq) await SaveLeaveRequestAsync(lq.Lv, lq.Why);
        { var apn2 = await ApproverNameAsync(Scope.Account); Toast(isLeave ? $"Đã gửi đề xuất xin nghỉ và công việc — chờ {apn2} duyệt" : $"Đã gửi đề xuất — chờ {apn2} duyệt"); }
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
