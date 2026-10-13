using QMDept.Data;
using QMDept.Services;

namespace QMDept.ViewModels;

/// <summary>
/// Leader → Kế hoạch &amp; duyệt → tab con "Kế hoạch" (ldApprove). Danh sách tính sẵn trong controller;
/// view chỉ vẽ. Cần Dept đã nạp ngày đang xem.
/// </summary>
public sealed class PlanningApprovalsVm
{
    /// <summary>"all" (Nhân viên đề xuất) | "pending" (Chờ duyệt) — ?ap=.</summary>
    public required string Mode { get; init; }
    /// <summary>Lọc dự án (?pj=), null = Tất cả.</summary>
    public int? Pj { get; init; }
    /// <summary>Kế hoạch trong phạm vi sau lọc dự án (filt).</summary>
    public required List<DayPlan> Filtered { get; init; }
    /// <summary>Kế hoạch chờ Leader duyệt sau lọc dự án (pend).</summary>
    public required List<DayPlan> Pending { get; init; }
    /// <summary>Danh sách hiển thị, sắp theo tên NV.</summary>
    public required List<DayPlan> List { get; init; }
    /// <summary>NV chưa gửi kế hoạch và không nghỉ.</summary>
    public required List<Person> NotSent { get; init; }
    /// <summary>Phát sinh chờ duyệt trong ngày (thuộc phạm vi).</summary>
    public required List<Extra> PendingExtras { get; init; }

    /// <summary>ldApprove(): tính các danh sách như artifact.</summary>
    public static PlanningApprovalsVm Build(Dept d, DateOnly date, string? ap, int? pj)
    {
        var all = d.Plans.Where(p => p.Date == date && d.InScope(p.EmployeeId)).ToList();
        var filt = all.Where(p => pj is null || Dept.TaskIdsOf(p).Any(id => d.Task(id)?.ProjectId == pj)).ToList();
        var pend = filt.Where(p => Dept.PlanSt(p) == PlanState.Pending).ToList();
        var mode = ap == "pending" ? "pending" : "all";
        var list = (mode == "all" ? filt : pend).OrderBy(p => d.PName(p.EmployeeId), Fmt.ViCompare).ToList();
        return new PlanningApprovalsVm
        {
            Mode = mode, Pj = pj, Filtered = filt, Pending = pend, List = list,
            NotSent = d.Staff().Where(p => d.PlanOf(date, p.Id) is null && d.OnLeave(date, p.Id) is null).ToList(),
            PendingExtras = d.ExtrasOn(date).Where(x => x.Status == ExtraStatus.Pending).ToList(),
        };
    }
}

/// <summary>
/// Leader → Kế hoạch &amp; duyệt → tab con "Báo cáo" (ldEodApprove + carrySection).
/// Controller nạp Dept cho [ngày, ngày + 14].
/// </summary>
public sealed class PlanningReportsVm
{
    /// <summary>Đề xuất chuyển kế hoạch từ ngày đang xem: chờ duyệt trước, rồi theo tên NV.</summary>
    public required List<WorkTask> Carries { get; init; }
    public int CarryPending => Carries.Count(t => !t.CarryOk);
    /// <summary>NV đã gửi kế hoạch, chưa gửi báo cáo cuối ngày, không nghỉ.</summary>
    public required List<Person> NotSent { get; init; }
    /// <summary>Báo cáo cuối ngày: Chờ duyệt, Yêu cầu bổ sung, Đã duyệt; rồi theo tên NV.</summary>
    public required List<DayReport> Reports { get; init; }
    /// <summary>Việc chuyển tiếp có nút "Sửa" trên trang (mỗi việc một &lt;template&gt; sheet).</summary>
    public required List<WorkTask> SheetTasks { get; init; }

    static int Ord(EodState s) => s switch { EodState.Pending => 0, EodState.Rejected => 1, _ => 2 };

    /// <summary>Việc chuyển tiếp được đề xuất từ việc <paramref name="t"/> (hiện dòng "Đề xuất chuyển" khi việc chưa hoàn thành).</summary>
    public static WorkTask? NextOf(Dept d, WorkTask t) =>
        t.CarriedToId is not null && t.Status != TaskState.Done ? d.Task(t.CarriedToId) : null;

    /// <summary>Chỉ việc nguồn "Chuyển tiếp" mới sửa được qua sheet (POST EditCarry kiểm tra lại).</summary>
    public static bool CanEditCarry(WorkTask? n) => n is { Source: TaskSource.CarryOver };

    /// <summary>ldEodApprove() + carrySection(d).</summary>
    public static PlanningReportsVm Build(Dept d, DateOnly date)
    {
        var carries = d.CarriesFrom(date).OrderBy(t => t.CarryOk ? 1 : 0).ThenBy(t => d.PName(t.EmployeeId), Fmt.ViCompare).ToList();
        var reports = d.Reports.Where(r => r.Date == date && d.InScope(r.EmployeeId))
            .OrderBy(r => Ord(Dept.EodSt(r))).ThenBy(r => d.PName(r.EmployeeId), Fmt.ViCompare).ToList();
        var sheets = new Dictionary<int, WorkTask>();
        foreach (var n in carries.Where(t => !t.CarryOk)) sheets.TryAdd(n.Id, n);
        foreach (var r in reports)
            foreach (var t in d.Chosen(date, r.EmployeeId))
                if (NextOf(d, t) is { } n && CanEditCarry(n)) sheets.TryAdd(n.Id, n);
        return new PlanningReportsVm
        {
            Carries = carries,
            NotSent = d.Staff().Where(p => d.ReportOf(date, p.Id) is null && d.OnLeave(date, p.Id) is null && d.PlanOf(date, p.Id) is not null).ToList(),
            Reports = reports,
            SheetTasks = [.. sheets.Values],
        };
    }
}

/// <summary>Tiện ích dùng chung giữa view và controller của tab duyệt (tên riêng để không trùng các phần khác).</summary>
public static class PlanningApproval
{
    /// <summary>Độ dài tối đa của ghi chú / lý do (cột 2000 ký tự).</summary>
    public const int NoteMax = 2000;
    /// <summary>Độ dài tối đa tiêu đề công việc (cột 300 ký tự).</summary>
    public const int TitleMax = 300;

    /// <summary>
    /// eodCarry: các ngày được chọn khi sửa đề xuất chuyển = 14 ngày kế tiếp sau ngày của việc gốc
    /// (nextWDs(fromDate || date, 14)), cộng ngày hiện tại của việc nếu chưa có trong danh sách (đặt lên đầu).
    /// </summary>
    public static List<DateOnly> CarryDays(WorkTask n)
    {
        var days = Fmt.NextDays(n.FromDate ?? n.Date, 14).ToList();
        if (!days.Contains(n.Date)) days.Insert(0, n.Date);
        return days;
    }

    /// <summary>Nhãn ngày trong select: "Thứ 2, 13/10/2026" (+ " (ngày nghỉ)" với Chủ nhật).</summary>
    public static string CarryDayLabel(DateOnly x) => Fmt.DayLabel(x) + (x.DayOfWeek == DayOfWeek.Sunday ? " (ngày nghỉ)" : "");

    /// <summary>Kiểm tra độ dài văn bản người dùng nhập; trả về chuỗi đã Trim (null nếu rỗng).</summary>
    public static string? CleanNote(string? s, int max = NoteMax)
    {
        var v = s?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Length > max) throw new BizException($"Nội dung quá dài (tối đa {max} ký tự)");
        return v;
    }
}
