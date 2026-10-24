namespace QMDept.Data;

// Toàn bộ enum được lưu xuống MySQL dưới dạng chuỗi (tên enum) — xem AppDbContext.ConfigureConventions.
// Tên trong code là tiếng Anh (yêu cầu chủ dự án); nhãn tiếng Việt, class CSS và màu nằm trong Labels.cs.
// Comment từng giá trị ghi khóa gốc trong artifact để tra cứu.

/// <summary>Vai trò tài khoản (people.role).</summary>
public enum Role
{
    Staff,   // 'nv'     Nhân viên
    Leader,  // 'leader' Leader
    Manager, // 'tp'     Trưởng phòng
    Admin,   // (không có trong artifact) Quản trị: toàn quyền tài khoản + xem màn hình của mọi người
}

/// <summary>Cấp bậc nhân viên (people.level).</summary>
public enum Level
{
    Tech1, // 'tech1'
    Tech2, // 'tech2'
    Tech3, // 'tech3'
}

/// <summary>Trạng thái công việc / đầu mục (TS). Đầu mục không dùng Late.</summary>
public enum TaskState
{
    NotStarted, // 'chua_lam'   Chưa làm
    InProgress, // 'dang_lam'   Đang làm
    Done,       // 'hoan_thanh' Hoàn thành
    Late,       // 'cham'       Chậm
}

/// <summary>Nguồn gốc công việc (tasks.source, SRC).</summary>
public enum TaskSource
{
    Planned,   // 'ke_hoach'        Leader xếp lịch tuần (không gắn nhãn)
    Unplanned, // 'ngoai_ke_hoach'  NV tự thêm ở báo cáo đầu ngày ("Ngoài kế hoạch")
    Extra,     // 'phat_sinh'       tạo ra khi phát sinh được duyệt ("Phát sinh")
    CarryOver, // 'chuyen_tiep'     chuyển tiếp từ việc chưa xong ("Chuyển tiếp")
    Assigned,  // 'giao_viec'       Leader / TP giao ("Leader giao")
    Proposed,  // (mới 2026-10-15)  NV đề xuất ở lịch tháng, Leader duyệt ("Đề xuất")
}

/// <summary>Mức độ ưu tiên của công việc khi cấp quản lý giao (yêu cầu 2026-10-24): người được giao biết việc có được ưu tiên hay không.</summary>
public enum TaskPriority
{
    Normal, // Bình thường
    High,   // Ưu tiên
    Urgent, // Khẩn cấp
}

/// <summary>Trạng thái dự án (PS). Override chỉ dùng OnTrack/InProgress/Behind/AtRisk; Completed là trạng thái tính ra.</summary>
public enum ProjectStatus
{
    OnTrack,    // 'dat'        Đạt
    InProgress, // 'dang_lam'   Đang làm
    Behind,     // 'cham'       Chậm
    AtRisk,     // 'nguy_co'    Nguy cơ
    Completed,  // 'hoan_thanh' Hoàn thành
}

/// <summary>Quyết định duyệt (leaderSt / tpSt = 'ok' | 'no'); null = chưa quyết định.</summary>
public enum Decision
{
    Ok,
    No,
}

/// <summary>Trạng thái kế hoạch đầu ngày — giá trị TÍNH RA (planSt), không lưu DB.</summary>
public enum PlanState
{
    NotSent,        // 'chua_gui'  Chưa gửi
    Pending,        // 'cho_duyet' Chờ duyệt
    PendingManager, // 'cho_tp'    Chờ TP duyệt
    Approved,       // 'da_duyet'  Đã duyệt
    Rejected,       // 'tu_choi'   Từ chối
}

/// <summary>Trạng thái báo cáo cuối ngày — giá trị TÍNH RA (eodSt), không lưu DB.</summary>
public enum EodState
{
    NotSent,  // 'chua_gui'
    Pending,  // 'cho_duyet'
    Approved, // 'da_duyet'
    Rejected, // 'tu_choi' (hiển thị "Yêu cầu bổ sung")
}

/// <summary>Trạng thái làm việc khai báo đầu ngày (WST).</summary>
public enum WorkStatus
{
    AtOffice,    // 'cong_ty'   Làm việc tại công ty
    DayTrip,     // 'ct_ngay'   Công tác trong ngày
    LongTrip,    // 'ct_dai'    Công tác dài ngày
    PaidLeave,   // 'nghi_phep' Xin nghỉ phép
    UnpaidLeave, // 'nghi_kl'   Xin nghỉ không lương
}

/// <summary>Báo cơm đầu ngày (MEAL).</summary>
public enum Meal
{
    None,   // 'cat' Cắt cơm
    Rice,   // 'com' Cơm
    Noodle, // 'bun' Bún
}

/// <summary>Loại nghỉ (LT): nghỉ phép theo buổi / nghỉ không lương theo giờ.</summary>
public enum LeaveType
{
    Paid,   // 'phep'
    Unpaid, // 'kl'
}

/// <summary>Buổi nghỉ phép (LS).</summary>
public enum LeaveSession
{
    Morning,   // 'sang'    08:00–12:00
    Afternoon, // 'chieu'   13:00–17:00
    FullDay,   // 'ca_ngay' 08:00–17:00
}

/// <summary>Phạm vi nhân sự hỗ trợ (SUP). OtherTeam / OtherDept cần thêm Trưởng phòng duyệt.</summary>
public enum SupportScope
{
    None,      // 'khong'      Không cần hỗ trợ
    Team,      // 'nhom'       Nhân sự trong nhóm (Leader duyệt)
    OtherTeam, // 'nhom_khac'  Nhân sự nhóm khác (cần TP duyệt)
    OtherDept, // 'phong_khac' Nhân sự phòng ban khác (cần TP duyệt)
}

/// <summary>Trạng thái LƯU của phát sinh (extras.status).</summary>
public enum ExtraStatus
{
    Pending,  // 'cho_duyet'
    Approved, // 'da_duyet'
    Rejected, // 'tu_choi'
    PendingManager, // Leader đã duyệt đăng ký của NV làm thêm sau 20:00 — chờ Trưởng phòng duyệt (yêu cầu 2026-10-21)
}

/// <summary>Trạng thái HIỂN THỊ của phát sinh (XS) — tính bởi Dept.ExSt.</summary>
public enum ExtraView
{
    Pending,    // 'cho_duyet'
    Approved,   // 'da_duyet'
    InProgress, // 'dang_xu_ly'
    Done,       // 'xong'
    Rejected,   // 'tu_choi'
}

/// <summary>Trạng thái đăng ký làm thêm (OT_ST).</summary>
public enum OtStatus
{
    Pending,  // 'cho_duyet'
    Approved, // 'da_duyet'
    Rejected, // 'tu_choi'
    PendingManager, // Leader đã duyệt đăng ký của NV làm thêm sau 20:00 — chờ Trưởng phòng duyệt (yêu cầu 2026-10-21)
}

/// <summary>Hạn đăng ký làm thêm (OT_REG).</summary>
public enum OtReg
{
    Early,  // 'du_tru'      dự trù: trước hạn (OtRules.DeadlineHhmm) ngày làm việc liền trước
    OnTime, // 'chinh_thuc'  chính thức: trước hạn cùng ngày
    Late,   // 'tre_han'     sau hạn cùng ngày
}

/// <summary>Loại ngày làm thêm (OT_DAY).</summary>
public enum OtDayType
{
    Workday, // 'lv'   Ngày làm việc — ngoài giờ hành chính
    Holiday, // 'nghi' Ngày nghỉ / lễ
}

/// <summary>Suất ăn tăng ca.</summary>
public enum OtMeal
{
    None,   // 'khong' Không đăng ký
    Snack,  // 'nhe'   Ăn nhẹ (15.000đ)
    Rice,   // 'com'   Cơm (35.000đ)
    Noodle, // 'bun'   Bún (35.000đ) — tính như suất cơm
}

/// <summary>Vai trò trong dự án (AROLE).</summary>
public enum ProjectRole
{
    Lead,    // 'lead'    Leader dự án
    Support, // 'support' Hỗ trợ dự án
}

/// <summary>
/// Loại thông báo đẩy (SignalR) — KHÔNG có trong artifact, bổ sung theo yêu cầu chủ dự án 2026-10-13.
/// Người dùng bật/tắt từng loại ở trang Cài đặt (/Settings). Danh mục + người nhận: Services/Notifier.cs (NotifyCatalog).
/// </summary>
public enum NotifyType
{
    PlanSubmitted,       // NV gửi / gửi lại kế hoạch đầu ngày → Leader nhóm
    PlanDecided,         // kế hoạch được duyệt / từ chối / góp ý → NV
    SupportNeedsManager, // kế hoạch cần TP duyệt hỗ trợ nhân sự → TP
    ReportSubmitted,     // NV gửi báo cáo cuối ngày → Leader nhóm
    ReportDecided,       // báo cáo cuối ngày được duyệt / yêu cầu bổ sung → NV
    ExtraProposed,       // NV đề xuất công việc phát sinh → Leader nhóm
    ExtraDecided,        // phát sinh được duyệt / từ chối / Leader thêm phát sinh → NV
    CarryOver,           // kế hoạch chuyển tiếp được duyệt / sửa / hoàn tác → NV
    TaskAssigned,        // được giao việc, thêm / xóa việc trong lịch → NV
    LeaveMarked,         // Leader ghi nghỉ / bỏ nghỉ → NV
    OvertimeSubmitted,   // đăng ký làm thêm cần duyệt → Leader (của NV) / TP (của Leader)
    OvertimeDecided,     // đăng ký làm thêm được duyệt / từ chối / hoàn tác → người đăng ký
    LeaderReportSent,    // Leader gửi báo cáo ngày → TP
    OvertimeCancelled,   // người đăng ký hủy đăng ký làm thêm → Leader (của NV) / TP (của Leader)
    CarryProposed,       // NV đề xuất / sửa việc chuyển tiếp sau khi báo cáo cuối ngày đã được duyệt → Leader nhóm
    ProjectLead,         // được chọn / không còn là Leader dự án → NV
    CalendarProposed,    // NV đề xuất lịch (công việc / người hỗ trợ) ở lịch tháng → Leader nhóm
    CalendarDecided,     // đề xuất lịch được duyệt / từ chối → NV đề xuất
    ProjectOwner,        // TP giao / bỏ giao dự án cho nhóm Leader phụ trách → Leader
    TeamChanged,         // TP chuyển nhân viên sang nhóm khác trên sơ đồ nhân sự → nhân viên + Leader cũ / mới
    LeaveRequested,      // NV / Leader xin nghỉ → Leader nhóm / Trưởng phòng duyệt
    LeaveDecided,        // xin nghỉ được duyệt / từ chối → người xin
    TaskAcked,           // người được giao việc đã xác nhận → người giao (Leader / Trưởng phòng)
    SupportRequested,    // kế hoạch xin người hỗ trợ từ nhóm khác đã được duyệt → Leader nhóm được xin (xác nhận + chọn nhân viên)
    SupportConfirmed,    // Leader nhóm được xin đã xác nhận + chọn nhân viên → người đề xuất, Leader của họ, nhân viên được chọn
}

/// <summary>
/// Kết quả làm thêm (yêu cầu 2026-10-19): sau khi đăng ký được duyệt, NV làm thêm → cập nhật công việc + giờ thực tế → người duyệt
/// duyệt kết quả (Approved = lưu trữ, giờ được tính) hoặc từ chối (Rejected = giờ KHÔNG được tính, phải cập nhật lại).
/// </summary>
public enum OtCompletion
{
    None,     // chưa cập nhật kết quả
    Pending,  // đã cập nhật, chờ duyệt kết quả
    Approved, // kết quả được duyệt: lưu trữ, giờ làm thêm được tính
    Rejected, // bị từ chối: giờ làm thêm không được tính, cập nhật lại
}
