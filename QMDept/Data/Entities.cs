using System.ComponentModel.DataAnnotations;

namespace QMDept.Data;

// Quy ước chung (xem docs/database.md):
// - Ngày làm việc: DateOnly (cột `date`). Mốc thời gian (…At): DateTime UTC (cột `datetime(6)`); hiển thị qua VnTime.
// - Giờ trong ngày: chuỗi "HH:mm" (5 ký tự), cho phép "24:00" làm giờ kết thúc; so sánh chuỗi = so sánh giờ.
// - Tên người duyệt/thực hiện (…By) lưu dạng chuỗi tên tại thời điểm thao tác (giống artifact) để lịch sử không đổi.
// - Enum lưu dạng chuỗi.

/// <summary>Tài khoản / nhân sự (collection `people`).</summary>
public class Person
{
    public int Id { get; set; }
    [MaxLength(20)] public string Code { get; set; } = "";          // Mã nhân viên, viết HOA, duy nhất
    [MaxLength(100)] public string Name { get; set; } = "";
    public Role Role { get; set; }
    public int? LeaderId { get; set; }                               // Nhóm (chỉ với NV): Id của Leader
    public Person? Leader { get; set; }
    public Level? Level { get; set; }                                // Cấp bậc (chỉ với NV)
    public int SortOrder { get; set; }
    [MaxLength(200)] public string? PasswordHash { get; set; }      // null = chưa đặt mật khẩu (đặt ở lần đăng nhập đầu)
    [MaxLength(40)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N"); // đổi khi reset mật khẩu / đổi vai trò → cookie cũ mất hiệu lực
    public bool IsDeleted { get; set; }                              // xóa mềm: lịch sử công việc vẫn giữ
    public DateTime CreatedAt { get; set; }

    public ICollection<Person> Members { get; set; } = new List<Person>();
}

/// <summary>Dự án (collection `prj`).</summary>
public class Project
{
    public int Id { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(200)] public string? Factory { get; set; }           // Nhà máy (= khách hàng)
    [MaxLength(200)] public string? Location { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int Need { get; set; } = 2;                               // Nhu cầu nhân sự/ngày mặc định
    public ProjectStatus? StatusOverride { get; set; }               // null = tự động theo tiến độ
    public bool Closed { get; set; }                                 // Đã bàn giao
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<PlanItem> Items { get; set; } = new List<PlanItem>();
}

/// <summary>Đầu mục kế hoạch của dự án (collection `items`).</summary>
public class PlanItem
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int SortOrder { get; set; }
    public TaskState Status { get; set; } = TaskState.NotStarted;      // chỉ NotStarted / InProgress / Done
    public DateOnly? DoneDate { get; set; }
    [MaxLength(100)] public string? DoneBy { get; set; }
}

/// <summary>Phân công nhân viên vào dự án (collection `assign`, id = `${project}_${employee}`).</summary>
public class Assignment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int EmployeeId { get; set; }
    public Person? Employee { get; set; }
    public ProjectRole Role { get; set; } = ProjectRole.Support;
    [MaxLength(500)] public string? Note { get; set; }
    [MaxLength(100)] public string? By { get; set; }
    public DateTime At { get; set; }
}

/// <summary>Công việc trong lịch của một nhân viên vào một ngày (collection `tasks`).</summary>
public class WorkTask
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int EmployeeId { get; set; }
    public Person? Employee { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? PlanItemId { get; set; }                             // itemId: đầu mục kế hoạch (nếu có)
    public PlanItem? PlanItem { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    [MaxLength(5)] public string Start { get; set; } = "08:00";
    [MaxLength(5)] public string End { get; set; } = "17:00";
    public TaskState Status { get; set; } = TaskState.NotStarted;
    public TaskSource Source { get; set; } = TaskSource.Planned;
    public bool Selected { get; set; }                               // NV đã chọn làm trong ngày (báo cáo đầu ngày)
    public bool Supported { get; set; }                              // người hỗ trợ đã tích "đã hỗ trợ" (kết quả do Leader dự án báo cáo)
    [MaxLength(2000)] public string? Note { get; set; }              // Kết quả / lý do chưa hoàn thành

    // Chuyển tiếp (việc chưa xong → kế hoạch tiếp theo)
    public bool CarryOk { get; set; }                                // việc CarryOver (chuyen_tiep) đã được Leader duyệt (mới vào lịch)
    public int? CarriedFromId { get; set; }                          // (trên việc chuyen_tiep) việc gốc
    public int? CarriedToId { get; set; }                            // (trên việc gốc) việc chuyển tiếp
    public DateOnly? FromDate { get; set; }                          // (trên việc chuyen_tiep) ngày của việc gốc
    public DateOnly? CarryDate { get; set; }                         // (trên việc gốc) ngày chuyển sang
    [MaxLength(100)] public string? CarryBy { get; set; }
    public DateTime? CarryAt { get; set; }
    [MaxLength(100)] public string? EditedBy { get; set; }           // Leader đã chỉnh đề xuất chuyển
    public DateTime? EditedAt { get; set; }

    [MaxLength(100)] public string? GivenBy { get; set; }            // người giao (source = Assigned)
    public int? ExtraId { get; set; }                                // phát sinh đã tạo ra việc này (source = Extra)

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

}

/// <summary>Kế hoạch đầu ngày của NV (collection `dayPlans`, id = `${date}_${employee}`).</summary>
public class DayPlan
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int EmployeeId { get; set; }
    public Person? Employee { get; set; }

    public WorkStatus Status { get; set; }
    public Meal Meal { get; set; }

    // Nghỉ (khi Status = PaidLeave / UnpaidLeave)
    public LeaveType? LeaveType { get; set; }
    public LeaveSession? LeaveSession { get; set; }                  // chỉ với nghỉ phép
    [MaxLength(5)] public string? LeaveFrom { get; set; }
    [MaxLength(5)] public string? LeaveTo { get; set; }
    public decimal? LeaveHours { get; set; }
    public decimal? LeaveDays { get; set; }                          // chỉ với nghỉ phép: 0.5 / 1

    // Công tác (khi Status = DayTrip / LongTrip)
    [MaxLength(100)] public string? TripProvince { get; set; }
    [MaxLength(200)] public string? TripCustomer { get; set; }

    // Nhân sự hỗ trợ
    public SupportScope SupportScope { get; set; } = SupportScope.None;
    [MaxLength(2000)] public string? SupportText { get; set; }

    [MaxLength(2000)] public string? Note { get; set; }
    public DateTime SentAt { get; set; }

    public Decision? LeaderDecision { get; set; }                    // leaderSt
    [MaxLength(100)] public string? LeaderBy { get; set; }
    public DateTime? LeaderAt { get; set; }
    [MaxLength(2000)] public string? LeaderNote { get; set; }

    public Decision? ManagerDecision { get; set; }                        // tpSt
    [MaxLength(100)] public string? ManagerBy { get; set; }
    public DateTime? ManagerAt { get; set; }
    [MaxLength(2000)] public string? ManagerNote { get; set; }

    /// <summary>taskIds: các việc NV chọn làm trong ngày (+ việc được thêm sau khi gửi: phát sinh đã duyệt, việc được giao).</summary>
    public ICollection<DayPlanTask> PlanTasks { get; set; } = new List<DayPlanTask>();
}

/// <summary>Bảng nối DayPlan ↔ WorkTask (dayPlans.taskIds).</summary>
public class DayPlanTask
{
    public int DayPlanId { get; set; }
    public DayPlan? DayPlan { get; set; }
    public int WorkTaskId { get; set; }
    public WorkTask? WorkTask { get; set; }
}

/// <summary>Báo cáo cuối ngày của NV (collection `dayReports`, id = `${date}_${employee}`).</summary>
public class DayReport
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int EmployeeId { get; set; }
    public Person? Employee { get; set; }
    public bool Blocked { get; set; }                                // Có vướng mắc
    [MaxLength(2000)] public string? Note { get; set; }
    public DateTime SentAt { get; set; }
    public Decision? LeaderDecision { get; set; }
    [MaxLength(100)] public string? LeaderBy { get; set; }
    public DateTime? LeaderAt { get; set; }
    [MaxLength(2000)] public string? LeaderNote { get; set; }
}

/// <summary>Leader ghi nghỉ cho NV (collection `leaves`, id = `${date}_${employee}`).</summary>
public class Leave
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int EmployeeId { get; set; }
    public Person? Employee { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
}

/// <summary>Nhu cầu nhân sự theo ngày của dự án, ghi đè Project.Need (collection `needs`).</summary>
public class ProjectNeed
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int Need { get; set; }
}

/// <summary>Công việc phát sinh (collection `extras`).</summary>
public class Extra
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int EmployeeId { get; set; }
    public Person? Employee { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    [MaxLength(5)] public string? Start { get; set; }
    [MaxLength(5)] public string? End { get; set; }                  // null = chưa rõ
    public ExtraStatus Status { get; set; } = ExtraStatus.Pending;
    [MaxLength(2000)] public string? Reason { get; set; }            // lý do từ chối
    public int? TaskId { get; set; }                                 // việc được tạo khi duyệt
    public WorkTask? Task { get; set; }
    [MaxLength(100)] public string? DecidedBy { get; set; }
    public Role? DecidedRole { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime CreatedAt { get; set; }

}


/// <summary>Báo cáo ngày Leader gửi TP (collection `leaderReports`). Khác artifact: khóa theo (ngày, leader).</summary>
public class LeaderReport
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int LeaderId { get; set; }
    public Person? Leader { get; set; }
    [MaxLength(100)] public string? By { get; set; }
    public DateTime SentAt { get; set; }
    [MaxLength(2000)] public string? Note { get; set; }
    /// <summary>Ảnh chụp số liệu lúc gửi — JSON của <see cref="QMDept.Services.LeaderReportSummary"/>.</summary>
    public string SummaryJson { get; set; } = "{}";
}

/// <summary>Đăng ký làm thêm (collection `ot`, id = `${date}_${person}`).</summary>
public class Overtime
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public Role Role { get; set; }                                   // vai trò lúc đăng ký: NV → Leader duyệt; Leader → TP duyệt; TP tự ghi nhận
    public OtDayType DayType { get; set; }
    [MaxLength(5)] public string Start { get; set; } = "17:00";
    [MaxLength(5)] public string End { get; set; } = "20:00";
    public decimal Hours { get; set; }
    [MaxLength(100)] public string Tier { get; set; } = "";
    [MaxLength(200)] public string Break { get; set; } = "";         // brk: nghỉ giải lao
    public bool Exempt { get; set; }                                 // công tác / giao hàng: không áp dụng suất ăn
    public OtMeal Meal { get; set; }
    public int Cost { get; set; }                                    // chi phí suất ăn (đ)
    public int Allowance { get; set; }                               // phụ cấp (đ), trả vào lương
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    [MaxLength(2000)] public string Content { get; set; } = "";      // nội dung tổng hợp
    [MaxLength(2000)] public string? ExtraContent { get; set; }      // "Nội dung khác"
    public OtStatus Status { get; set; } = OtStatus.Pending;
    [MaxLength(100)] public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    [MaxLength(2000)] public string? Reason { get; set; }
    public OtReg Reg { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<OvertimeTask> Items { get; set; } = new List<OvertimeTask>();
}

/// <summary>Việc làm tiếp trong đăng ký làm thêm (ot.items: {id,title,project} — lưu snapshot tiêu đề).</summary>
public class OvertimeTask
{
    public int Id { get; set; }
    public int OvertimeId { get; set; }
    public Overtime? Overtime { get; set; }
    public int? WorkTaskId { get; set; }
    public WorkTask? WorkTask { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    public int? ProjectId { get; set; }
}

/// <summary>TP gửi tổng hợp suất ăn tăng ca cho Phòng HCNS (collection `otSends`, id = date).</summary>
public class MealOrder
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    [MaxLength(100)] public string? By { get; set; }
    public DateTime SentAt { get; set; }
    public int RiceCount { get; set; }                               // số suất cơm (gồm cả bún — tính như suất cơm)
    public int NoodleCount { get; set; }                             // trong đó số suất bún
    public int SnackCount { get; set; }                              // số suất ăn nhẹ
    public int AllowanceCount { get; set; }                          // số người được phụ cấp 4 giờ
    public int People { get; set; }
}

/// <summary>Cài đặt thông báo đẩy của một tài khoản (không có bản ghi = bật tất cả). Bổ sung, không có trong artifact.</summary>
public class NotifySetting
{
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    /// <summary>Bật / tắt toàn bộ thông báo đẩy.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Các loại bị TẮT, phân cách dấu phẩy (tên NotifyType) — loại mới thêm sau mặc định bật.</summary>
    [MaxLength(1000)] public string DisabledTypes { get; set; } = "";
    public DateTime UpdatedAt { get; set; }

    public bool Allows(NotifyType t) => Enabled && !DisabledTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(t.ToString());
}
