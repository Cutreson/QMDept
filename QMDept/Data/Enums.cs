namespace QMDept.Data;

// Toàn bộ enum được lưu xuống MySQL dưới dạng chuỗi (tên enum) — xem AppDbContext.ConfigureConventions.
// Nhãn tiếng Việt, class CSS và màu nằm trong Labels.cs. Khóa gốc trong artifact ghi ở comment từng giá trị.

/// <summary>Vai trò tài khoản (people.role).</summary>
public enum Role
{
    Nv,     // 'nv'     Nhân viên Setup
    Leader, // 'leader' Leader Setup
    Tp,     // 'tp'     Trưởng phòng
}

/// <summary>Cấp bậc nhân viên (people.level).</summary>
public enum Level
{
    Tech1, // 'tech1'
    Tech2, // 'tech2'
    Tech3, // 'tech3'
}

/// <summary>Trạng thái công việc / đầu mục (TS). Đầu mục không dùng Cham.</summary>
public enum TaskState
{
    ChuaLam,   // 'chua_lam'
    DangLam,   // 'dang_lam'
    HoanThanh, // 'hoan_thanh'
    Cham,      // 'cham'
}

/// <summary>Nguồn gốc công việc (tasks.source, SRC).</summary>
public enum TaskSource
{
    KeHoach,      // 'ke_hoach'        Leader xếp lịch tuần (không gắn nhãn)
    NgoaiKeHoach, // 'ngoai_ke_hoach'  NV tự thêm ở báo cáo đầu ngày
    PhatSinh,     // 'phat_sinh'       tạo ra khi phát sinh được duyệt
    ChuyenTiep,   // 'chuyen_tiep'     chuyển tiếp từ việc chưa xong
    GiaoViec,     // 'giao_viec'       Leader / TP giao
}

/// <summary>Trạng thái dự án (PS). Override chỉ dùng Dat/DangLam/Cham/NguyCo; HoanThanh là trạng thái tính ra.</summary>
public enum ProjectStatus
{
    Dat,       // 'dat'
    DangLam,   // 'dang_lam'
    Cham,      // 'cham'
    NguyCo,    // 'nguy_co'
    HoanThanh, // 'hoan_thanh'
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
    ChuaGui,  // 'chua_gui'
    ChoDuyet, // 'cho_duyet'
    ChoTp,    // 'cho_tp'
    DaDuyet,  // 'da_duyet'
    TuChoi,   // 'tu_choi'
}

/// <summary>Trạng thái báo cáo cuối ngày — giá trị TÍNH RA (eodSt), không lưu DB.</summary>
public enum EodState
{
    ChuaGui,  // 'chua_gui'
    ChoDuyet, // 'cho_duyet'
    DaDuyet,  // 'da_duyet'
    TuChoi,   // 'tu_choi' (hiển thị "Yêu cầu bổ sung")
}

/// <summary>Trạng thái làm việc khai báo đầu ngày (WST).</summary>
public enum WorkStatus
{
    CongTy,   // 'cong_ty'   Làm việc tại công ty
    CtNgay,   // 'ct_ngay'   Công tác trong ngày
    CtDai,    // 'ct_dai'    Công tác dài ngày
    NghiPhep, // 'nghi_phep' Xin nghỉ phép
    NghiKl,   // 'nghi_kl'   Xin nghỉ không lương
}

/// <summary>Báo cơm đầu ngày (MEAL).</summary>
public enum Meal
{
    Cat, // 'cat' Cắt cơm
    Com, // 'com' Cơm
    Bun, // 'bun' Bún
}

/// <summary>Loại nghỉ (LT): nghỉ phép theo buổi / nghỉ không lương theo giờ.</summary>
public enum LeaveType
{
    Phep, // 'phep'
    Kl,   // 'kl'
}

/// <summary>Buổi nghỉ phép (LS).</summary>
public enum LeaveSession
{
    Sang,   // 'sang'    08:00–12:00
    Chieu,  // 'chieu'   13:00–17:00
    CaNgay, // 'ca_ngay' 08:00–17:00
}

/// <summary>Phạm vi nhân sự hỗ trợ (SUP). NhomKhac / PhongKhac cần thêm Trưởng phòng duyệt.</summary>
public enum SupportScope
{
    Khong,     // 'khong'      Không cần hỗ trợ
    Nhom,      // 'nhom'       Nhân sự trong nhóm (Leader duyệt)
    NhomKhac,  // 'nhom_khac'  Nhân sự nhóm khác (cần TP duyệt)
    PhongKhac, // 'phong_khac' Nhân sự phòng ban khác (cần TP duyệt)
}

/// <summary>Trạng thái LƯU của phát sinh (extras.status).</summary>
public enum ExtraStatus
{
    ChoDuyet, // 'cho_duyet'
    DaDuyet,  // 'da_duyet'
    TuChoi,   // 'tu_choi'
}

/// <summary>Trạng thái HIỂN THỊ của phát sinh (XS) — tính bởi Dept.ExSt.</summary>
public enum ExtraView
{
    ChoDuyet,  // 'cho_duyet'
    DaDuyet,   // 'da_duyet'
    DangXuLy,  // 'dang_xu_ly'
    Xong,      // 'xong'
    TuChoi,    // 'tu_choi'
}

/// <summary>Trạng thái đăng ký làm thêm (OT_ST).</summary>
public enum OtStatus
{
    ChoDuyet, // 'cho_duyet'
    DaDuyet,  // 'da_duyet'
    TuChoi,   // 'tu_choi'
}

/// <summary>Hạn đăng ký làm thêm (OT_REG).</summary>
public enum OtReg
{
    DuTru,     // 'du_tru'      trước 14:00 ngày làm việc liền trước
    ChinhThuc, // 'chinh_thuc'  trước 14:00 cùng ngày
    TreHan,    // 'tre_han'     sau 14:00 cùng ngày
}

/// <summary>Loại ngày làm thêm (OT_DAY).</summary>
public enum OtDayType
{
    Lv,   // 'lv'   Ngày làm việc — ngoài giờ hành chính
    Nghi, // 'nghi' Ngày nghỉ / lễ
}

/// <summary>Suất ăn tăng ca.</summary>
public enum OtMeal
{
    Khong, // 'khong' Không đăng ký
    Nhe,   // 'nhe'   Ăn nhẹ (15.000đ)
    Com,   // 'com'   Cơm (35.000đ)
    Bun,   // 'bun'   Bún (35.000đ) — tính như suất cơm
}

/// <summary>Vai trò trong dự án (AROLE).</summary>
public enum ProjectRole
{
    Lead,    // 'lead'    Leader dự án
    Support, // 'support' Hỗ trợ dự án
}
