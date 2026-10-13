using Microsoft.EntityFrameworkCore;
using QMDept.Data;

namespace QMDept.Services;

/// <summary>Lỗi nghiệp vụ hiển thị được cho người dùng (controller bắt và đưa vào toast).</summary>
public sealed class BizException(string message) : Exception(message);

/// <summary>
/// Thao tác GHI dùng chung giữa nhiều màn hình (port từ approveExtra / openExtra / calAssignForm / assignForm / delTask).
/// Các thao tác chỉ thuộc một màn hình nằm trong controller của màn hình đó.
/// Mọi hàm tự gọi SaveChangesAsync.
/// </summary>
public sealed class WorkflowService(AppDbContext db, PhotoStore photos)
{
    /// <summary>approveExtra(x, role): duyệt phát sinh → tạo việc PhatSinh (đã chọn) cho NV, gắn vào kế hoạch ngày nếu có.</summary>
    public async Task ApproveExtraAsync(int extraId, string actor, Role role)
    {
        var x = await db.Extras.FirstOrDefaultAsync(e => e.Id == extraId) ?? throw new BizException("Không tìm thấy phát sinh");
        if (x.TaskId is not null || x.Status != ExtraStatus.ChoDuyet) return;
        var t = new WorkTask
        {
            Date = x.Date, EmployeeId = x.EmployeeId, ProjectId = x.ProjectId, Title = x.Title,
            Start = x.Start ?? "08:00", End = x.End ?? "17:00", Status = TaskState.ChuaLam, Source = TaskSource.PhatSinh,
            ExtraId = x.Id, Selected = true, CreatedAt = VnTime.UtcNow,
        };
        db.WorkTasks.Add(t);
        await db.SaveChangesAsync();
        x.Status = ExtraStatus.DaDuyet; x.TaskId = t.Id; x.DecidedBy = actor; x.DecidedRole = role; x.DecidedAt = VnTime.UtcNow;
        var plan = await db.DayPlans.FirstOrDefaultAsync(p => p.Date == x.Date && p.EmployeeId == x.EmployeeId);
        if (plan is not null) db.DayPlanTasks.Add(new DayPlanTask { DayPlanId = plan.Id, WorkTaskId = t.Id });
        await db.SaveChangesAsync();
    }

    /// <summary>Từ chối phát sinh (bắt buộc lý do).</summary>
    public async Task RejectExtraAsync(int extraId, string reason, string actor, Role role)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BizException("Nhập lý do từ chối");
        var x = await db.Extras.FirstOrDefaultAsync(e => e.Id == extraId) ?? throw new BizException("Không tìm thấy phát sinh");
        if (x.Status != ExtraStatus.ChoDuyet) return;
        x.Status = ExtraStatus.TuChoi; x.Reason = reason.Trim(); x.DecidedBy = actor; x.DecidedRole = role; x.DecidedAt = VnTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>openExtra: NV đề xuất (chờ Leader duyệt) hoặc Leader thêm (duyệt ngay).</summary>
    public async Task<Extra> CreateExtraAsync(DateOnly date, int employeeId, int projectId, string title, string? desc,
        string? start, string? end, IEnumerable<IFormFile>? files, int uploaderId, bool byLeader, string actor, Role role)
    {
        title = (title ?? "").Trim();
        if (title.Length == 0) throw new BizException("Nhập tên công việc phát sinh");
        if (start is not null && !Fmt.IsHhmm(start)) throw new BizException("Giờ bắt đầu không hợp lệ");
        if (!string.IsNullOrEmpty(end) && (!Fmt.IsHhmm(end) || (start is not null && end.Le(start)))) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        var x = new Extra
        {
            Date = date, EmployeeId = employeeId, ProjectId = projectId, Title = title, Description = desc?.Trim(),
            Start = start, End = string.IsNullOrEmpty(end) ? null : end, Status = ExtraStatus.ChoDuyet, CreatedAt = VnTime.UtcNow,
        };
        db.Extras.Add(x);
        await db.SaveChangesAsync();
        foreach (var p in await photos.SaveAsync(files, uploaderId)) { p.ExtraId = x.Id; db.Photos.Add(p); }
        await db.SaveChangesAsync();
        if (byLeader) await ApproveExtraAsync(x.Id, actor, role);
        return x;
    }

    /// <summary>
    /// calAssignForm / assignForm: giao một việc cho nhiều người trong một ngày (nguồn GiaoViec, "Leader giao").
    /// Nếu NV đã gửi kế hoạch ngày đó → việc được chọn sẵn và thêm vào kế hoạch. Cập nhật vai trò trong dự án.
    /// </summary>
    public async Task<int> AssignTaskAsync(DateOnly date, int projectId, int? itemId, string? title, string start, string end,
        IReadOnlyList<(int EmployeeId, ProjectRole Role)> people, string actor)
    {
        if (people.Count == 0) throw new BizException("Tích chọn ít nhất 1 người tham gia");
        var item = itemId is int iid ? await db.PlanItems.FirstOrDefaultAsync(i => i.Id == iid && i.ProjectId == projectId) : null;
        var t0 = string.IsNullOrWhiteSpace(title) ? item?.Title : title.Trim();
        if (string.IsNullOrEmpty(t0)) throw new BizException("Nhập nội dung công việc hoặc chọn đầu mục");
        if (!Fmt.IsHhmm(start) || !Fmt.IsHhmm(end) || end.Le(start)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) throw new BizException("Không tìm thấy dự án");
        foreach (var (emp, role) in people)
        {
            var plan = await db.DayPlans.FirstOrDefaultAsync(p => p.Date == date && p.EmployeeId == emp);
            var t = new WorkTask
            {
                Date = date, EmployeeId = emp, ProjectId = projectId, PlanItemId = item?.Id, Title = t0, Start = start, End = end,
                Status = TaskState.ChuaLam, Source = TaskSource.GiaoViec, GivenBy = actor, Selected = plan is not null, CreatedAt = VnTime.UtcNow,
            };
            db.WorkTasks.Add(t);
            await db.SaveChangesAsync();
            if (plan is not null) db.DayPlanTasks.Add(new DayPlanTask { DayPlanId = plan.Id, WorkTaskId = t.Id });
            await UpsertAssignmentAsync(projectId, emp, role, actor, keepNote: true);
            await db.SaveChangesAsync();
        }
        return people.Count;
    }

    /// <summary>Đặt vai trò NV trong dự án (chỉ ghi khi khác vai trò hiện tại).</summary>
    public async Task UpsertAssignmentAsync(int projectId, int employeeId, ProjectRole role, string actor, bool keepNote)
    {
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.EmployeeId == employeeId);
        if (a is null) db.Assignments.Add(new Assignment { ProjectId = projectId, EmployeeId = employeeId, Role = role, By = actor, At = VnTime.UtcNow });
        else if (a.Role != role) { a.Role = role; a.By = actor; a.At = VnTime.UtcNow; if (!keepNote) a.Note = null; }
    }

    /// <summary>delTask: xóa việc khỏi lịch, dọn tham chiếu chuyển tiếp và ảnh.</summary>
    public async Task DeleteTaskAsync(int taskId)
    {
        var t = await db.WorkTasks.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == taskId);
        if (t is null) return;
        await db.WorkTasks.Where(x => x.CarriedToId == taskId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CarriedToId, (int?)null).SetProperty(x => x.CarryDate, (DateOnly?)null));
        await db.WorkTasks.Where(x => x.CarriedFromId == taskId).ExecuteUpdateAsync(s => s.SetProperty(x => x.CarriedFromId, (int?)null));
        foreach (var p in t.Photos.ToList()) { photos.Delete(p.StoredName); db.Photos.Remove(p); }
        db.WorkTasks.Remove(t);
        await db.SaveChangesAsync();
    }
}

/// <summary>Lưu ảnh hiện trường ra đĩa (cấu hình Uploads:Path, mặc định App_Data/uploads). Tối đa 4 ảnh/lần, 10 MB/ảnh.</summary>
public sealed class PhotoStore(IWebHostEnvironment env, IConfiguration cfg)
{
    public const int MaxFiles = 4;
    public const long MaxBytes = 10 * 1024 * 1024;
    static readonly Dictionary<string, string> Ext = new(StringComparer.OrdinalIgnoreCase)
    { ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/gif"] = ".gif", ["image/heic"] = ".heic" };

    public string Root
    {
        get
        {
            var p = cfg["Uploads:Path"];
            var root = string.IsNullOrWhiteSpace(p) ? Path.Combine(env.ContentRootPath, "App_Data", "uploads") : Path.GetFullPath(p, env.ContentRootPath);
            Directory.CreateDirectory(root);
            return root;
        }
    }

    /// <summary>Lưu file hợp lệ, trả về entity Photo chưa gắn TaskId/ExtraId (caller gắn và Add vào DbContext).</summary>
    public async Task<List<Photo>> SaveAsync(IEnumerable<IFormFile>? files, int uploaderId)
    {
        var list = new List<Photo>();
        if (files is null) return list;
        foreach (var f in files.Where(f => f.Length > 0).Take(MaxFiles))
        {
            if (!Ext.TryGetValue(f.ContentType, out var ext)) throw new BizException("Chỉ nhận file ảnh (jpg, png, webp, gif, heic)");
            if (f.Length > MaxBytes) throw new BizException("Ảnh vượt quá 10 MB");
            var name = Guid.NewGuid().ToString("N") + ext;
            await using (var s = File.Create(Path.Combine(Root, name))) await f.CopyToAsync(s);
            list.Add(new Photo { StoredName = name, ContentType = f.ContentType, Size = f.Length, UploadedById = uploaderId, CreatedAt = VnTime.UtcNow });
        }
        return list;
    }

    public string PathOf(string storedName) => Path.Combine(Root, Path.GetFileName(storedName));
    public void Delete(string storedName) { try { File.Delete(PathOf(storedName)); } catch (IOException) { } }
}
