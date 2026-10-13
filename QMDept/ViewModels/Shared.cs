using Microsoft.EntityFrameworkCore;
using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>Tham số điều hướng tuần (?w=yyyy-MM-dd thứ 2) và tháng (?m=yyyy-MM-01).</summary>
public static class Nav
{
    /// <summary>Thứ 2 của tuần đang xem (S.week || monday(S.date)).</summary>
    public static DateOnly Week(HttpRequest r, DateOnly date) => Fmt.ParseKey(r.Query["w"]) is DateOnly w ? Fmt.Monday(w) : Fmt.Monday(date);
    /// <summary>Ngày 1 của tháng đang xem (S.month || đầu tháng của S.date).</summary>
    public static DateOnly Month(HttpRequest r, DateOnly date) => Fmt.ParseKey(r.Query["m"]) is DateOnly m ? Fmt.MonthStart(m) : Fmt.MonthStart(date);
}

/// <summary>Model partial _TaskFields (addTaskFields(px, pid, withDate)): dự án, đầu mục, nội dung, [ngày], giờ.</summary>
public sealed class TaskFieldsVm
{
    /// <summary>Tiền tố id phần tử (vd "o", "w") để nhiều form trên cùng trang không trùng id.</summary>
    public required string Prefix { get; init; }
    public int? ProjectId { get; init; }
    /// <summary>Danh sách ngày cho select "Ngày" (null = không hiện ô ngày).</summary>
    public List<DateOnly>? Dates { get; init; }
    public DateOnly? SelectedDate { get; init; }
    public string Start { get; init; } = "08:00";
    public string End { get; init; } = "12:00";
}

/// <summary>Dữ liệu form _TaskFields gửi lên (tên field: projectId, itemId, title, start, end, date). readTaskFields().</summary>
public sealed class TaskFieldsInput
{
    public int ProjectId { get; set; }
    public int? ItemId { get; set; }
    public string? Title { get; set; }
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public string? Date { get; set; }

    /// <summary>Kiểm tra &amp; trả (tiêu đề, đầu mục). Lỗi → BizException với câu của artifact.</summary>
    public async Task<(string Title, PlanItem? Item)> ResolveAsync(AppDbContext db)
    {
        var pj = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ProjectId) ?? throw new BizException("Chọn dự án");
        if (pj.Closed) throw new BizException("Dự án đã bàn giao, không thêm việc được");
        var item = ItemId is int iid ? await db.PlanItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == iid && i.ProjectId == ProjectId) : null;
        var title = string.IsNullOrWhiteSpace(Title) ? item?.Title ?? "" : Title.Trim();
        if (title.Length == 0) throw new BizException("Nhập nội dung hoặc chọn đầu mục");
        if (!Fmt.IsHhmm(Start) || !Fmt.IsHhmm(End) || End.Le(Start)) throw new BizException("Giờ kết thúc phải sau giờ bắt đầu");
        return (title, item);
    }
}

/// <summary>Model partial _ExtraSheet (openExtra(byLeader)): template sheet đề xuất / thêm phát sinh.</summary>
public sealed class ExtraSheetVm
{
    public required string TemplateId { get; init; }
    /// <summary>URL action POST nhận <see cref="ExtraInput"/>.</summary>
    public required string Action { get; init; }
    /// <summary>true = Leader thêm (có chọn NV, duyệt ngay); false = NV đề xuất.</summary>
    public bool ByLeader { get; init; }
    /// <summary>NV mặc định (NV đề xuất: chính mình; Leader: NV đầu tiên của staff()).</summary>
    public int? EmployeeId { get; init; }
}

/// <summary>Dữ liệu form _ExtraSheet gửi lên.</summary>
public sealed class ExtraInput
{
    public int? EmployeeId { get; set; }
    public int ProjectId { get; set; }
    public string? Title { get; set; }
    public string? Desc { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
}

/// <summary>
/// Model cho partial _ProjectDetail (projectDetail(pid, who) của artifact): tab Tổng quan / Kế hoạch / Nhân sự.
/// Dùng <see cref="LoadAsync"/> trong controller để nạp đủ dữ liệu.
/// </summary>
public sealed class ProjectDetailVm
{
    /// <summary>Giá trị <see cref="Who"/>: trang của Nhân viên (có nút quay lại) / của Trưởng phòng.</summary>
    public const string StaffView = "staff", ManagerView = "manager";
    /// <summary>Tab con (?tab=): Tổng quan / Kế hoạch (mặc định) / Nhân sự.</summary>
    public const string OverviewTab = "overview", PlanTab = "plan", PeopleTab = "people";

    public required int Pid { get; init; }
    /// <summary><see cref="StaffView"/> (có nút quay lại) hoặc <see cref="ManagerView"/>.</summary>
    public required string Who { get; init; }
    /// <summary>"overview" | "plan" | "people" (mặc định "plan").</summary>
    public required string Tab { get; init; }
    public string? BackHref { get; init; }
    /// <summary>Tạo URL cho tab con (tham số: "overview"/"plan"/"people").</summary>
    public required Func<string, string> TabHref { get; init; }
    /// <summary>6 phát sinh gần nhất của dự án (mọi ngày).</summary>
    public List<Extra> RecentExtras { get; init; } = [];

    public static async Task<ProjectDetailVm> LoadAsync(Dept d, AppDbContext db, DateOnly date, int pid, string who, string? tab,
        string? backHref, Func<string, string> tabHref)
    {
        var m = Fmt.Monday(date);
        await d.EnsureAsync(m, m.AddDays(6));
        await d.EnsureAsync(date, date);
        var xs = await db.Extras.AsNoTracking().Where(x => x.ProjectId == pid)
            .OrderByDescending(x => x.CreatedAt).Take(6).ToListAsync();
        // exSt(x) cần việc đã tạo của phát sinh đã duyệt (cùng ngày phát sinh) — phát sinh có thể nằm ngoài tuần đang xem.
        foreach (var day in xs.Where(x => x.Status == ExtraStatus.Approved && x.TaskId is not null).Select(x => x.Date).Distinct())
            await d.EnsureAsync(day, day);
        return new ProjectDetailVm
        {
            Pid = pid, Who = who, Tab = tab is OverviewTab or PeopleTab ? tab : PlanTab, BackHref = backHref, TabHref = tabHref, RecentExtras = xs,
        };
    }
}
