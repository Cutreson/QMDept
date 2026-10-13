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
        var t = new WorkTask
        {
            Date = x.Date, EmployeeId = x.EmployeeId, ProjectId = x.ProjectId, Title = x.Title,
            Start = x.Start ?? "08:00", End = x.End ?? "17:00", Status = TaskState.NotStarted, Source = TaskSource.Extra,
            ExtraId = x.Id, Selected = true, CreatedAt = VnTime.UtcNow,
        };
        db.WorkTasks.Add(t);
        await db.SaveChangesAsync();
        x.Status = ExtraStatus.Approved; x.TaskId = t.Id; x.DecidedBy = actor; x.DecidedRole = role; x.DecidedAt = VnTime.UtcNow;
        var plan = await db.DayPlans.FirstOrDefaultAsync(p => p.Date == x.Date && p.EmployeeId == x.EmployeeId);
        if (plan is not null) db.DayPlanTasks.Add(new DayPlanTask { DayPlanId = plan.Id, WorkTaskId = t.Id });
        await db.SaveChangesAsync();
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
        if (start is not null && !Fmt.IsHhmm(start)) throw new BizException("Giờ bắt đầu không hợp lệ");
        if (!string.IsNullOrEmpty(end) && (!Fmt.IsHhmm(end) || (start is not null && end.Le(start)))) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        if (!await db.Projects.AnyAsync(p => p.Id == projectId && !p.Closed)) throw new BizException("Chọn dự án");
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
        IReadOnlyList<(int EmployeeId, ProjectRole Role)> people, string actor)
    {
        if (people.Count == 0) throw new BizException("Tích chọn ít nhất 1 người tham gia");
        var item = itemId is int iid ? await db.PlanItems.FirstOrDefaultAsync(i => i.Id == iid && i.ProjectId == projectId) : null;
        var t0 = string.IsNullOrWhiteSpace(title) ? item?.Title : title.Trim();
        if (string.IsNullOrEmpty(t0)) throw new BizException("Nhập nội dung công việc hoặc chọn đầu mục");
        if (t0.Length > 300) throw new BizException("Nội dung công việc tối đa 300 ký tự");
        if (!Fmt.IsHhmm(start) || !Fmt.IsHhmm(end) || end.Le(start)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        var pj = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId) ?? throw new BizException("Không tìm thấy dự án");
        if (pj.Closed) throw new BizException("Dự án đã bàn giao, không giao thêm việc được");
        // Giao cho nhiều người: tất cả hoặc không (tự mở transaction nếu caller chưa mở).
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync() : null;
        foreach (var (emp, role) in people)
        {
            var plan = await db.DayPlans.FirstOrDefaultAsync(p => p.Date == date && p.EmployeeId == emp);
            var t = new WorkTask
            {
                Date = date, EmployeeId = emp, ProjectId = projectId, PlanItemId = item?.Id, Title = t0, Start = start, End = end,
                Status = TaskState.NotStarted, Source = TaskSource.Assigned, GivenBy = actor, Selected = plan is not null, CreatedAt = VnTime.UtcNow,
            };
            db.WorkTasks.Add(t);
            await db.SaveChangesAsync();
            if (plan is not null) db.DayPlanTasks.Add(new DayPlanTask { DayPlanId = plan.Id, WorkTaskId = t.Id });
            await UpsertAssignmentAsync(projectId, emp, role, actor, keepNote: true);
            await db.SaveChangesAsync();
        }
        if (tx is not null) await tx.CommitAsync();
        await notify.TaskAssignedAsync(people.Select(p => p.EmployeeId), date, t0);
        return people.Count;
    }

    /// <summary>Đặt vai trò NV trong dự án (chỉ ghi khi khác vai trò hiện tại).</summary>
    public async Task UpsertAssignmentAsync(int projectId, int employeeId, ProjectRole role, string actor, bool keepNote)
    {
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.EmployeeId == employeeId);
        if (a is null) db.Assignments.Add(new Assignment { ProjectId = projectId, EmployeeId = employeeId, Role = role, By = actor, At = VnTime.UtcNow });
        else if (a.Role != role) { a.Role = role; a.By = actor; a.At = VnTime.UtcNow; if (!keepNote) a.Note = null; }
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

