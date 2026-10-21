using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>Lỗi nghiệp vụ hiển thị được cho người dùng (controller bắt và đưa vào toast).</summary>
public sealed class BizException(string message) : Exception(message);

/// <summary>
/// Thao tác GHI dùng chung giữa nhiều màn hình (port từ approveExtra / openExtra / calAssignForm / assignForm / delTask).
/// Các thao tác chỉ thuộc một màn hình nằm trong controller của màn hình đó.
/// Mọi hàm tự gọi SaveChangesAsync, rồi gửi thông báo đẩy liên quan (Notifier) sau khi lưu thành công.
/// </summary>
public sealed class WorkflowService(AppDbContext db, Notifier notify)
{
    /// <summary>approveExtra(x, role): duyệt phát sinh → tạo việc nguồn Extra (Phát sinh, đã chọn) cho NV, gắn vào kế hoạch ngày nếu có.</summary>
    public async Task ApproveExtraAsync(int extraId, string actor, Role role, bool sendNotify = true)
    {
        var x = await db.Extras.FirstOrDefaultAsync(e => e.Id == extraId) ?? throw new BizException("Không tìm thấy phát sinh");
        if (x.TaskId is not null || x.Status != ExtraStatus.Pending) return;
        if (!await db.Projects.AnyAsync(p => p.Id == x.ProjectId)) throw new BizException("Không tìm thấy dự án");
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync() : null;
        // Giành quyền duyệt bằng một câu UPDATE có điều kiện: hai người duyệt cùng lúc chỉ một người tạo việc.
        var claimed = await db.Extras.Where(e => e.Id == extraId && e.Status == ExtraStatus.Pending && e.TaskId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, ExtraStatus.Approved));
        if (claimed == 0) return;
        var start = x.Start ?? "08:00";
        // Giờ kết thúc "Chưa rõ" → 17:00; nếu bắt đầu từ 17:00 trở đi thì +30 phút (không để kết thúc trước bắt đầu).
        var end = x.End ?? ("17:00".Le(start) ? Fmt.Hhmm(Math.Min(Fmt.Min(start) + 30, 24 * 60)) : "17:00");
        var t = new WorkTask
        {
            Date = x.Date, EmployeeId = x.EmployeeId, ProjectId = x.ProjectId, Title = x.Title,
            Start = start, End = end, Status = TaskState.NotStarted, Source = TaskSource.Extra,
            ExtraId = x.Id, Selected = true, CreatedAt = VnTime.UtcNow,
        };
        db.WorkTasks.Add(t);
        await db.SaveChangesAsync();
        x.Status = ExtraStatus.Approved; x.TaskId = t.Id; x.DecidedBy = actor; x.DecidedRole = role; x.DecidedAt = VnTime.UtcNow;
        var plan = await db.DayPlans.FirstOrDefaultAsync(p => p.Date == x.Date && p.EmployeeId == x.EmployeeId);
        if (plan is not null) db.DayPlanTasks.Add(new DayPlanTask { DayPlanId = plan.Id, WorkTaskId = t.Id });
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        if (sendNotify) await notify.ExtraDecidedAsync(x.EmployeeId, x.Date, x.Title, "đã được duyệt");
    }

    /// <summary>Từ chối phát sinh (bắt buộc lý do).</summary>
    public async Task RejectExtraAsync(int extraId, string reason, string actor, Role role)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BizException("Nhập lý do từ chối");
        if (reason.Trim().Length > 2000) throw new BizException("Lý do tối đa 2000 ký tự");
        var x = await db.Extras.FirstOrDefaultAsync(e => e.Id == extraId) ?? throw new BizException("Không tìm thấy phát sinh");
        if (x.Status != ExtraStatus.Pending) return;
        x.Status = ExtraStatus.Rejected; x.Reason = reason.Trim(); x.DecidedBy = actor; x.DecidedRole = role; x.DecidedAt = VnTime.UtcNow;
        await db.SaveChangesAsync();
        await notify.ExtraDecidedAsync(x.EmployeeId, x.Date, x.Title, "bị từ chối", x.Reason);
    }

    /// <summary>openExtra: NV đề xuất (chờ Leader duyệt) hoặc Leader thêm (duyệt ngay).</summary>
    public async Task<Extra> CreateExtraAsync(DateOnly date, int employeeId, int projectId, string title, string? desc,
        string? start, string? end, bool byLeader, string actor, Role role)
    {
        title = (title ?? "").Trim();
        if (title.Length == 0) throw new BizException("Nhập tên công việc phát sinh");
        if (title.Length > 300) throw new BizException("Tên công việc tối đa 300 ký tự");
        if ((desc?.Trim().Length ?? 0) > 2000) throw new BizException("Mô tả tối đa 2000 ký tự");
        if (start is not null && (!Fmt.IsHhmm(start) || start == "24:00")) throw new BizException("Giờ bắt đầu không hợp lệ");
        if (!string.IsNullOrEmpty(end) && (!Fmt.IsHhmm(end) || (start is not null && end.Le(start)))) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) throw new BizException("Chọn dự án");
        var x = new Extra
        {
            Date = date, EmployeeId = employeeId, ProjectId = projectId, Title = title, Description = desc?.Trim(),
            Start = start, End = string.IsNullOrEmpty(end) ? null : end, Status = ExtraStatus.Pending, CreatedAt = VnTime.UtcNow,
        };
        db.Extras.Add(x);
        await db.SaveChangesAsync();
        if (byLeader)
        {
            await ApproveExtraAsync(x.Id, actor, role, sendNotify: false);
            await notify.ExtraDecidedAsync(employeeId, date, title, "do Leader thêm cho bạn");
        }
        else await notify.ExtraProposedAsync(employeeId, date, title);
        return x;
    }

    /// <summary>
    /// calAssignForm / assignForm: giao một việc cho nhiều người trong một ngày (nguồn Assigned, "Leader giao").
    /// Nếu NV đã gửi kế hoạch ngày đó → việc được chọn sẵn và thêm vào kế hoạch. Cập nhật vai trò trong dự án.
    /// </summary>
    public async Task<int> AssignTaskAsync(DateOnly date, int projectId, int? itemId, string? title, string start, string end,
        IReadOnlyList<int> people, string actor, TaskSource source = TaskSource.Assigned, int? actorId = null)
    {
        if (people.Count == 0) throw new BizException("Tích chọn ít nhất 1 người tham gia");
        var item = itemId is int iid ? await db.PlanItems.FirstOrDefaultAsync(i => i.Id == iid && i.ProjectId == projectId) : null;
        var t0 = string.IsNullOrWhiteSpace(title) ? item?.Title : title.Trim();
        if (string.IsNullOrEmpty(t0)) throw new BizException("Nhập nội dung công việc hoặc chọn đầu mục");
        if (t0.Length > 300) throw new BizException("Nội dung công việc tối đa 300 ký tự");
        if (!Fmt.IsHhmm(start) || !Fmt.IsHhmm(end) || end.Le(start)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        var pj = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId) ?? throw new BizException("Không tìm thấy dự án");
        // Vai trò do dự án quyết định: Leader dự án đã chọn = Leader dự án, những người còn lại = hỗ trợ (yêu cầu 2026-10-14).
        var leadId = await db.Assignments.AsNoTracking().Where(a => a.ProjectId == projectId && a.Role == ProjectRole.Lead)
            .Select(a => (int?)a.EmployeeId).FirstOrDefaultAsync();
        // Giao cho nhiều người: tất cả hoặc không (tự mở transaction nếu caller chưa mở).
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync() : null;
        foreach (var emp in people)
        {
            var plan = await db.DayPlans.FirstOrDefaultAsync(p => p.Date == date && p.EmployeeId == emp);
            var t = new WorkTask
            {
                Date = date, EmployeeId = emp, ProjectId = projectId, PlanItemId = item?.Id, Title = t0, Start = start, End = end,
                Status = TaskState.NotStarted, Source = source, GivenBy = actor, GivenById = actorId is int gid && gid != emp ? gid : null, Selected = plan is not null, CreatedAt = VnTime.UtcNow,
            };
            db.WorkTasks.Add(t);
            await db.SaveChangesAsync();
            if (plan is not null) db.DayPlanTasks.Add(new DayPlanTask { DayPlanId = plan.Id, WorkTaskId = t.Id });
            await UpsertAssignmentAsync(projectId, emp, leadId == emp ? ProjectRole.Lead : ProjectRole.Support, actor, keepNote: true);
            await db.SaveChangesAsync();
        }
        if (tx is not null) await tx.CommitAsync();
        await notify.TaskAssignedAsync(people, date, t0);
        return people.Count;
    }


    // ======================= Đề xuất lịch của NV (yêu cầu 2026-10-15) =======================

    /// <summary>
    /// NV đề xuất ở lịch tháng: thêm công việc của mình (people = chỉ người đề xuất), hoặc thêm người hỗ trợ / Leader hỗ trợ cho dự án mình là
    /// Leader dự án. Chưa ghi vào lịch — Leader nhóm duyệt (<see cref="DecideProposalAsync"/>) thì mới tạo việc.
    /// </summary>
    public async Task<CalendarProposal> ProposeAsync(DateOnly date, int proposerId, int projectId, int? itemId, string? title, string start, string end,
        IReadOnlyCollection<int> people, IReadOnlyCollection<(string Name, int Qty)>? quotas = null, string? supportReason = null, bool includeSelf = true)
    {
        var me = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == proposerId && !p.IsDeleted && p.Role == Role.Staff)
            ?? throw new BizException("Không tìm thấy nhân viên");
        if (me.LeaderId is null) throw new BizException("Bạn chưa được xếp nhóm nên chưa có Leader duyệt đề xuất");
        var pj = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId) ?? throw new BizException("Không tìm thấy dự án");
        var item = itemId is int iid ? await db.PlanItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == iid && i.ProjectId == projectId) : null;
        var t0 = string.IsNullOrWhiteSpace(title) ? item?.Title : title.Trim();
        if (string.IsNullOrEmpty(t0)) throw new BizException("Nhập nội dung công việc hoặc chọn đầu mục");
        if (t0.Length > 300) throw new BizException("Nội dung công việc tối đa 300 ký tự");
        if (!Fmt.IsHhmm(start) || !Fmt.IsHhmm(end) || end.Le(start)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");

        // NV chỉ xem / đề xuất công việc của dự án mình được giao làm Leader dự án (yêu cầu 2026-10-16) — cả việc của chính mình lẫn xin hỗ trợ.
        if (!await db.Assignments.AnyAsync(a => a.ProjectId == projectId && a.EmployeeId == proposerId && a.Role == ProjectRole.Lead))
            throw new BizException("Chỉ đề xuất được công việc của dự án bạn được giao làm Leader dự án");
        var ids = people.Distinct().ToList();
        // Người đề xuất (Leader dự án) mặc định cùng thực hiện; bỏ tích thì chỉ những người được chỉ định trong nhóm làm.
        if (includeSelf && !ids.Contains(proposerId)) ids.Insert(0, proposerId);
        if (ids.Count == 0) throw new BizException("Chọn người thực hiện: tích “Tôi thực hiện” hoặc chọn người hỗ trợ trong nhóm của mình");
        // Chỉ chỉ định được người trong NHÓM CỦA MÌNH (NV cùng Leader + Leader của mình); phòng ban / nhóm khác chỉ xin theo số lượng.
        var others = ids.Where(x => x != proposerId).ToList();
        if (others.Count > 0)
        {
            var okCount = await db.People.CountAsync(p => others.Contains(p.Id) && !p.IsDeleted
                && ((p.Role == Role.Staff && p.LeaderId == me.LeaderId) || p.Id == me.LeaderId));
            if (okCount != others.Count) throw new BizException("Chỉ được chỉ định người hỗ trợ trong nhóm của mình");
        }
        // Số lượng xin theo phòng ban (Fmt.SupportDepartments) và theo nhóm khác ("Nhóm <tên Leader>"), mỗi mục 1–50 người.
        var otherGroups = await db.People.AsNoTracking().Where(p => p.Role == Role.Leader && !p.IsDeleted && p.Id != me.LeaderId).Select(p => "Nhóm " + p.Name).ToListAsync();
        var q = (quotas ?? []).Where(z => z.Qty > 0).GroupBy(z => z.Name.Trim()).Select(g => (Name: g.Key, Qty: g.Sum(z => z.Qty))).ToList();
        if (q.Any(z => !Fmt.SupportDepartments.Contains(z.Name) && !otherGroups.Contains(z.Name) || z.Qty > 50))
            throw new BizException("Phòng ban / nhóm xin hỗ trợ không hợp lệ");
        var reasonText = string.IsNullOrWhiteSpace(supportReason) ? null : supportReason.Trim();
        if ((others.Count > 0 || q.Count > 0) && reasonText is null) throw new BizException("Nhập lý do xin người hỗ trợ");
        if (reasonText is { Length: > 500 }) throw new BizException("Lý do tối đa 500 ký tự");
        var quotaText = q.Count > 0 ? string.Join(";", q.Select(z => $"{z.Name}={z.Qty}")) : null;
        if (quotaText is { Length: > 300 }) throw new BizException("Danh sách phòng ban / nhóm xin hỗ trợ quá dài");
        var x = new CalendarProposal
        {
            Date = date, ProposerId = proposerId, ProjectId = projectId, PlanItemId = item?.Id, Title = t0, Start = start, End = end,
            Departments = quotaText, SupportReason = reasonText,
            Status = ExtraStatus.Pending, CreatedAt = VnTime.UtcNow,
        };
        foreach (var id in ids) x.People.Add(new CalendarProposalPerson { EmployeeId = id });
        db.CalendarProposals.Add(x);
        await db.SaveChangesAsync();
        await notify.CalendarProposedAsync(proposerId, date, t0);
        return x;
    }

    /// <summary>
    /// Leader nhóm (hoặc TP / Quản trị) duyệt / từ chối đề xuất. Duyệt → tạo việc (nguồn "Đề xuất") cho mọi người trong đề xuất,
    /// vào lịch NV và lịch nhóm; từ chối bắt buộc lý do.
    /// </summary>
    public async Task DecideProposalAsync(int id, bool approve, string? reason, Person actor)
    {
        var x = await db.CalendarProposals.Include(p => p.People).FirstOrDefaultAsync(p => p.Id == id) ?? throw new BizException("Không tìm thấy đề xuất");
        if (x.Status != ExtraStatus.Pending) throw new BizException("Đề xuất đã được quyết định");
        var proposer = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == x.ProposerId);
        if (actor.Role == Role.Leader && proposer?.LeaderId != actor.Id) throw new BizException("Đề xuất không thuộc nhóm bạn quản lý");
        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!approve && note is null) throw new BizException("Nhập lý do từ chối");
        if (note is not null && note.Length > 2000) throw new BizException("Lý do tối đa 2000 ký tự");
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync() : null;
        if (approve)
        {
            // Giành quyền duyệt: hai Leader bấm cùng lúc chỉ tạo việc một lần.
            var claimed = await db.CalendarProposals.Where(p => p.Id == id && p.Status == ExtraStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, ExtraStatus.Approved));
            if (claimed == 0) throw new BizException("Đề xuất đã được quyết định");
            // Phòng ban / nhóm khác chỉ xin theo số lượng (không chỉ định người) → ghi vào tên việc để hiện trên lịch.
            var title = string.IsNullOrEmpty(x.Departments) ? x.Title : $"{x.Title} · Xin hỗ trợ: {Fmt.QuotaText(x.Departments)}";
            if (title.Length > 300) title = title[..300];
            await AssignTaskAsync(x.Date, x.ProjectId, x.PlanItemId, title, x.Start, x.End, x.People.Select(p => p.EmployeeId).ToList(),
                proposer?.Name ?? actor.Name, TaskSource.Proposed);
        }
        else x.Status = ExtraStatus.Rejected;
        x.Reason = approve ? null : note;
        x.DecidedBy = actor.Name;
        x.DecidedAt = VnTime.UtcNow;
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        await notify.CalendarDecidedAsync(x.ProposerId, x.Date, x.Title, approve, note);
    }

    /// <summary>
    /// Duyệt (approve) / từ chối (bắt buộc lý do) đề xuất xin nghỉ (yêu cầu 2026-10-20). Duyệt → ghi "Nghỉ" vào lịch (bảng leaves, kèm loại + buổi / giờ + lý do)
    /// nên lịch tháng và các màn hình khác hiện nghỉ ngay. Quyền duyệt do controller kiểm (Leader: NV nhóm mình; TP: Leader).
    /// </summary>
    public async Task DecideLeaveAsync(int id, bool approve, string? note, Person actor)
    {
        var x = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id) ?? throw new BizException("Không tìm thấy đề xuất xin nghỉ");
        if (x.Status != ExtraStatus.Pending) throw new BizException("Đề xuất xin nghỉ đã được xử lý — tải lại trang");
        var n = (note ?? "").Trim();
        if (!approve && n.Length == 0) throw new BizException("Nhập lý do từ chối");
        if (n.Length > 500) n = n[..500];
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync() : null;
        x.Status = approve ? ExtraStatus.Approved : ExtraStatus.Rejected;
        x.DecidedBy = actor.Name; x.DecidedAt = VnTime.UtcNow; x.DecisionNote = approve ? null : n;
        if (approve)
        {
            var lv = await db.Leaves.FirstOrDefaultAsync(l => l.Date == x.Date && l.EmployeeId == x.PersonId);
            if (lv is null) { lv = new Leave { Date = x.Date, EmployeeId = x.PersonId }; db.Leaves.Add(lv); }
            lv.Reason = x.Reason; lv.LeaveType = x.LeaveType; lv.LeaveSession = x.LeaveSession;
            lv.LeaveFrom = x.LeaveFrom; lv.LeaveTo = x.LeaveTo; lv.LeaveHours = x.LeaveHours; lv.LeaveDays = x.LeaveDays;
        }
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        await notify.LeaveDecidedAsync(x.PersonId, x.Date, approve, approve ? null : n);
    }
    /// <summary>
    /// Đặt vai trò NV trong dự án (chỉ ghi khi khác vai trò hiện tại). Mỗi dự án chỉ có MỘT Leader dự án (yêu cầu 2026-10-14):
    /// chọn người mới làm Leader dự án thì người đang là Leader dự án chuyển về "Hỗ trợ".
    /// </summary>
    public async Task UpsertAssignmentAsync(int projectId, int employeeId, ProjectRole role, string actor, bool keepNote)
    {
        if (role == ProjectRole.Lead)
            foreach (var o in await db.Assignments.Where(x => x.ProjectId == projectId && x.EmployeeId != employeeId && x.Role == ProjectRole.Lead).ToListAsync())
            { o.Role = ProjectRole.Support; o.By = actor; o.At = VnTime.UtcNow; }
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.EmployeeId == employeeId);
        if (a is null) db.Assignments.Add(new Assignment { ProjectId = projectId, EmployeeId = employeeId, Role = role, By = actor, At = VnTime.UtcNow });
        else if (a.Role != role) { a.Role = role; a.By = actor; a.At = VnTime.UtcNow; if (!keepNote) a.Note = null; }
    }

    /// <summary>
    /// Đồng bộ việc chuyển tiếp với kế hoạch ngày (gọi trước SaveChangesAsync, sau khi đã đổi CarryOk / Date):
    /// đã duyệt + NV đã gửi kế hoạch ngày đó → chọn sẵn và gắn vào kế hoạch (như việc được giao / phát sinh đã duyệt),
    /// để NV vẫn cập nhật / báo cáo được khi kế hoạch đã được duyệt trước khi Leader duyệt chuyển tiếp.
    /// Bỏ duyệt hoặc đổi ngày → gỡ khỏi kế hoạch cũ.
    /// </summary>
    public async Task SyncCarryToPlanAsync(WorkTask n)
    {
        var links = await db.DayPlanTasks.Where(x => x.WorkTaskId == n.Id).Include(x => x.DayPlan).ToListAsync();
        foreach (var l in links.Where(l => !n.CarryOk || l.DayPlan!.Date != n.Date)) db.DayPlanTasks.Remove(l);
        if (!n.CarryOk) { n.Selected = false; return; }
        if (links.Any(l => l.DayPlan!.Date == n.Date)) { n.Selected = true; return; }   // đã gắn sẵn vào kế hoạch ngày đó
        var plan = await db.DayPlans.FirstOrDefaultAsync(p => p.Date == n.Date && p.EmployeeId == n.EmployeeId);
        // Chưa gửi kế hoạch, hoặc NV đã gửi báo cáo cuối ngày đó → không chèn vào ngày đã báo cáo (chỉ hiện trong lịch).
        if (plan is null || await db.DayReports.AnyAsync(r => r.Date == n.Date && r.EmployeeId == n.EmployeeId)) { n.Selected = false; return; }
        n.Selected = true;
        if (!links.Any(l => l.DayPlanId == plan.Id)) db.DayPlanTasks.Add(new DayPlanTask { DayPlanId = plan.Id, WorkTaskId = n.Id });
    }

    /// <summary>
    /// Leader / TP chuyển một việc sang lịch khác (ngày khác và / hoặc người khác — yêu cầu 2026-10-20). Gỡ khỏi kế hoạch ngày cũ;
    /// người nhận đã gửi kế hoạch ngày mới (và chưa báo cáo cuối ngày) thì việc vào kế hoạch đó. Quyền do controller kiểm.
    /// </summary>
    public async Task MoveTaskAsync(WorkTask t, DateOnly newDate, int newEmp, string actor, int? actorId = null)
    {
        var oldEmp = t.EmployeeId;
        var links = await db.DayPlanTasks.Where(x => x.WorkTaskId == t.Id).ToListAsync();
        db.DayPlanTasks.RemoveRange(links);
        t.Date = newDate; t.EmployeeId = newEmp; t.Selected = false; t.UpdatedAt = VnTime.UtcNow;
        var plan = await db.DayPlans.FirstOrDefaultAsync(p => p.Date == newDate && p.EmployeeId == newEmp);
        if (plan is not null && !await db.DayReports.AnyAsync(r => r.Date == newDate && r.EmployeeId == newEmp))
        {
            t.Selected = true;
            db.DayPlanTasks.Add(new DayPlanTask { DayPlanId = plan.Id, WorkTaskId = t.Id });
        }
        // Việc chuyển tiếp: ngày chuyển sang ghi trên việc gốc cũng đổi theo.
        if (t.CarriedFromId is int origId)
            await db.WorkTasks.Where(x => x.Id == origId && x.CarriedToId == t.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CarryDate, (DateOnly?)newDate));
        if (newEmp != oldEmp)
        {
            t.GivenBy = actor; t.GivenById = actorId is int gid && gid != newEmp ? gid : null; t.AckAt = null;   // người nhận mới phải xác nhận lại
            var leadId = await db.Assignments.AsNoTracking().Where(a => a.ProjectId == t.ProjectId && a.Role == ProjectRole.Lead).Select(a => (int?)a.EmployeeId).FirstOrDefaultAsync();
            await UpsertAssignmentAsync(t.ProjectId, newEmp, leadId == newEmp ? ProjectRole.Lead : ProjectRole.Support, actor, keepNote: true);
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Sửa nội dung / dự án / giờ của một việc (Leader / TP sửa việc trong quyền của mình — yêu cầu 2026-10-21). Quyền + khóa do controller kiểm.</summary>
    public async Task EditTaskAsync(WorkTask t, int projectId, string? title, string start, string end, string actor)
    {
        var tt = (title ?? "").Trim();
        if (tt.Length == 0) throw new BizException("Nhập nội dung công việc");
        if (tt.Length > 300) throw new BizException("Nội dung công việc tối đa 300 ký tự");
        if (!Fmt.IsHhmm(start) || !Fmt.IsHhmm(end) || end.Le(start)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        var pj = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId) ?? throw new BizException("Không tìm thấy dự án");
        if (projectId != t.ProjectId)
        {
            t.ProjectId = projectId; t.PlanItemId = null;   // đầu mục thuộc dự án cũ
            var leadId = await db.Assignments.AsNoTracking().Where(a => a.ProjectId == projectId && a.Role == ProjectRole.Lead).Select(a => (int?)a.EmployeeId).FirstOrDefaultAsync();
            await UpsertAssignmentAsync(projectId, t.EmployeeId, leadId == t.EmployeeId ? ProjectRole.Lead : ProjectRole.Support, actor, keepNote: true);
        }
        t.Title = tt; t.Start = start; t.End = end; t.UpdatedAt = VnTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>delTask: xóa việc khỏi lịch, dọn tham chiếu chuyển tiếp.</summary>
    public async Task DeleteTaskAsync(int taskId)
    {
        var t = await db.WorkTasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (t is null) return;
        await db.WorkTasks.Where(x => x.CarriedToId == taskId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CarriedToId, (int?)null).SetProperty(x => x.CarryDate, (DateOnly?)null));
        await db.WorkTasks.Where(x => x.CarriedFromId == taskId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CarriedFromId, (int?)null));
        db.WorkTasks.Remove(t);
        await db.SaveChangesAsync();
    }
}

