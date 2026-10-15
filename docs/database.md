# Cơ sở dữ liệu

- **DB thật hiện tại: MariaDB 10.11.15** (host `db73445.public.databaseasp.net`, database `db73445`, `lower_case_table_names=1`, utf8mb4_general_ci).
  **Đổi DB 2026-10-14**: chủ dự án tạo DB mới `db73445` (DB cũ `db73331` bỏ). DB mới được dựng lại bằng migration (app tự `Migrate()` khi khởi động) —
  16 bảng + `__EFMigrationsHistory`, ban đầu trống; 5 tài khoản chính (tp, tp.leader1, tp.leader1.nv1, tp.leader1.nv2, admin) tạo lại
  (`docs/tai-khoan.local.md`).
  Kết nối qua **MySql.EntityFrameworkCore 10.0.9** (provider chính thức của Oracle; Pomelo chưa hỗ trợ EF Core 10).
  API: `options.UseMySQL(conn)`; extension cấu hình ở namespace `MySql.EntityFrameworkCore.Extensions` (đã `global using`).
- Provider Oracle lỗi khi lấy khóa migration trên MariaDB và lớp lịch sử của nó là internal → dùng
  `Data/MariaDbHistoryRepository.cs` (bảng `__EFMigrationsHistory` chuẩn + khóa `GET_LOCK` timeout 600s), đăng ký bằng
  `ReplaceService<IHistoryRepository, MariaDbHistoryRepository>()` trong `Program.cs`. Không hỗ trợ `migrations script --idempotent`.
- Migration đã áp lên DB thật hiện tại (`db73445`, 2026-10-14, DB trống): `InitialCreate` và `EnglishNames` (trên DB cũ `db73331` được áp 2026-10-13):
  xóa bảng `photos` (web chỉ dùng văn bản), tạo `notify_settings`, đổi tên cột sang tiếng Anh và đổi giá trị enum tiếng Việt
  trong dữ liệu sang tên tiếng Anh (SQL `UPDATE … CASE` trong migration — mục "Đổi tên sang tiếng Anh" bên dưới).
  (Migration `TextOnlyAndNotify` chưa từng áp đã được gộp vào `EnglishNames` rồi xóa.)
- **Độ trễ DB**: máy chủ DB ở xa — mỗi vòng mạng ~250 ms, mỗi lần lấy lại kết nối từ pool của MySql.Data thêm ~500 ms.
  Vì vậy: `KeepOpenInterceptor` giữ một kết nối suốt request; bảng nhỏ + dữ liệu theo khoảng ngày đọc qua bộ đệm dùng chung
  `Services/DataCache.cs` (tự làm mới sau mọi lệnh ghi qua EF; nạp song song tối đa 8 kết nối). Ghi thẳng vào DB từ ngoài app
  thì app thấy chậm tối đa 60 giây (TTL của cache).
- Nhà cung cấp hosting (databaseasp.net) có thể **chặn IP tạm thời** sau nhiều lần đăng nhập DB thất bại (đã gặp 2026-10-13:
  "Access denied" rồi không mở được cổng 3306 từ IP máy dev). Khi DB lỗi: dừng app để không thử lại liên tục, kiểm tra trang quản trị hosting.
- Cột `DateOnly` dùng bộ chuyển đổi DateOnly ↔ DateTime (AppDbContext.ConfigureConventions) vì MySql.Data không đọc được
  DateOnly ("Unable to cast DateTime to DateOnly" khi bảng có dữ liệu). Cột DB vẫn là `date`.
- Mọi bảng `utf8mb4`. Enum lưu chuỗi (varchar 20) = **tên enum tiếng Anh** (`Data/Enums.cs`, vd `Done`, `PaidLeave`, `Manager`).
  Ngày làm việc kiểu `date` (DateOnly); mốc thời gian `datetime(6)` **UTC** — "hôm nay"/"bây giờ" luôn tính theo giờ Việt Nam
  qua `Services/VnTime.cs` (máy chủ đặt ở nước ngoài). Giờ trong ngày là chuỗi `HH:mm` (cho phép `24:00` làm giờ kết thúc).

## Quy tắc vận hành (bắt buộc — xem agents.md)

- Connection string: `ConnectionStrings:Default`. **Hiện đặt trong `QMDept/appsettings.json` và commit lên git theo
  quyết định của chủ dự án (2026-10-13), chủ dự án sẽ tự gỡ sau** — không tự ý chuyển đi.
  Khi muốn gỡ khỏi git: chuyển sang `QMDept/appsettings.Local.json` (gitignore, ưu tiên cao hơn appsettings.json),
  user-secrets hoặc biến môi trường `ConnectionStrings__Default`. Ví dụ:
  ```json
  { "ConnectionStrings": { "Default": "server=HOST;port=3306;database=qmdept;user=USER;password=PASS;SslMode=Preferred" } }
  ```
- Khi đã có connection string: **chỉ làm trên DB thật**, không tạo DB test/InMemory/SQLite.
- Ứng dụng tự chạy migration khi khởi động (`Database:AutoMigrate`, mặc định `true`). Lệnh tay:
  ```
  cd QMDept
  dotnet ef migrations add <TenMigration> -o Data/Migrations
  dotnet ef database update
  ```
- Chưa có connection string: app chỉ hiện trang hướng dẫn cấu hình; vẫn build và tạo migration bình thường
  (provider Oracle không cần kết nối để tạo migration).
- Đổi model ⇒ luôn tạo migration mới (không sửa migration đã áp lên DB thật).

## Bảng

| Bảng | Entity | Khóa / ràng buộc chính | Ghi chú |
|---|---|---|---|
| `people` | Person | `Code` unique; `LeaderId` → people (SetNull) | Role, Level, PasswordHash (null = chưa đặt), SecurityStamp, IsDeleted (xóa mềm) |
| `projects` | Project | | Need (nhu cầu nhân sự/ngày), StatusOverride, Closed (đã bàn giao), SortOrder |
| `plan_items` | PlanItem | → projects (Cascade) | đầu mục kế hoạch; Status NotStarted/InProgress/Done; DoneDate/DoneBy |
| `assignments` | Assignment | (ProjectId, EmployeeId) unique | Role Lead/Support |
| `work_tasks` | WorkTask | idx (Date, EmployeeId), idx FromDate, idx ExtraId | Source, Status, Selected, CarryOk, CarriedFromId/CarriedToId (tham chiếu mềm, không FK), GivenBy |
| `day_plans` | DayPlan | (Date, EmployeeId) unique | trạng thái làm việc, báo cơm, nghỉ, công tác, hỗ trợ, quyết định Leader (`Leader*`) / TP (`ManagerDecision/ManagerBy/ManagerAt/ManagerNote`) |
| `day_plan_tasks` | DayPlanTask | PK (DayPlanId, WorkTaskId), cả hai Cascade | = dayPlans.taskIds |
| `day_reports` | DayReport | (Date, EmployeeId) unique | Blocked, Note, quyết định Leader |
| `leaves` | Leave | (Date, EmployeeId) unique | Leader báo nghỉ |
| `project_needs` | ProjectNeed | (Date, ProjectId) unique | ghi đè nhu cầu nhân sự theo ngày |
| `extras` | Extra | idx Date; TaskId → work_tasks (SetNull) | phát sinh; Status lưu Pending/Approved/Rejected |
| `leader_reports` | LeaderReport | (Date, LeaderId) unique | SummaryJson = ảnh chụp số liệu lúc gửi (longtext, JSON camelCase của `LeaderReportSummary`; dự án: `name, pct, need, onSite, status`) |
| `overtimes` | Overtime | (Date, PersonId) unique | đăng ký làm thêm; Role = vai trò lúc đăng ký |
| `overtime_tasks` | OvertimeTask | → overtimes (Cascade); WorkTaskId (SetNull) | việc làm tiếp (snapshot tiêu đề) |
| `ot_sends` | MealOrder | Date unique | TP gửi tổng hợp suất ăn cho HCNS: `RiceCount` (cơm, gồm bún), `NoodleCount`, `SnackCount`, `AllowanceCount`, `People` |
| `notify_settings` | NotifySetting | PK PersonId → people (Cascade) | bật/tắt thông báo đẩy + DisabledTypes (CSV tên NotifyType bị tắt) |

Sơ đồ khóa của artifact (Firestore-like) → bảng: `id = ${date}_${employee}` được thay bằng unique index (Date, EmployeeId).

## Đổi tên sang tiếng Anh (2026-10-13)

Theo quy tắc "code bằng tiếng Anh" (agents.md §1), tên bảng giữ nguyên nhưng **tên cột và giá trị enum đã đổi** — migration
tương ứng phải đổi tên cột (giữ dữ liệu) và cập nhật dữ liệu cũ:

- Cột `day_plans`: `TpDecision/TpBy/TpAt/TpNote` → `ManagerDecision/ManagerBy/ManagerAt/ManagerNote`.
- Cột `ot_sends` (entity `OtSend` → `MealOrder`): `Com/Bun/Nhe/Pc` → `RiceCount/NoodleCount/SnackCount/AllowanceCount`.
- Giá trị enum (cột varchar) đổi từ tên tiếng Việt sang tên tiếng Anh, vd `Nv/Tp` → `Staff/Manager`;
  `ChuaLam/DangLam/HoanThanh/Cham` → `NotStarted/InProgress/Done/Late`; `KeHoach/NgoaiKeHoach/PhatSinh/ChuyenTiep/GiaoViec`
  → `Planned/Unplanned/Extra/CarryOver/Assigned`; `CongTy/CtNgay/CtDai/NghiPhep/NghiKl` → `AtOffice/DayTrip/LongTrip/PaidLeave/UnpaidLeave`;
  `Cat/Com/Bun` → `None/Rice/Noodle`; `Phep/Kl` → `Paid/Unpaid`; `Sang/Chieu/CaNgay` → `Morning/Afternoon/FullDay`;
  `Khong/Nhom/NhomKhac/PhongKhac` → `None/Team/OtherTeam/OtherDept`; `ChoDuyet/DaDuyet/TuChoi` → `Pending/Approved/Rejected`;
  `DuTru/ChinhThuc/TreHan` → `Early/OnTime/Late`; `Lv/Nghi` → `Workday/Holiday`; `Khong/Nhe/Com/Bun` (suất tăng ca) →
  `None/Snack/Rice/Noodle`; `Dat/DangLam/Cham/NguyCo/HoanThanh` (dự án) → `OnTrack/InProgress/Behind/AtRisk/Completed`.
  Cột enum có dữ liệu: `people.Role`, `people.Level` (không đổi), `projects.StatusOverride`, `plan_items.Status`, `assignments.Role` (không đổi),
  `work_tasks.Status/Source`, `day_plans.Status/Meal/LeaveType/LeaveSession/SupportScope`, `extras.Status/DecidedRole`,
  `overtimes.Role/DayType/Meal/Status/Reg`; quyết định `Ok/No` không đổi.
- `notify_settings.DisabledTypes` (CSV tên `NotifyType`): `KeHoachMoi` → `PlanSubmitted`, `KeHoachKetQua` → `PlanDecided`,
  `HoTroCanTp` → `SupportNeedsManager`, `BaoCaoMoi` → `ReportSubmitted`, `BaoCaoKetQua` → `ReportDecided`, `PhatSinhMoi` → `ExtraProposed`,
  `PhatSinhKetQua` → `ExtraDecided`, `ChuyenTiep` → `CarryOver`, `GiaoViec` → `TaskAssigned`, `NghiPhep` → `LeaveMarked`,
  `LamThemMoi` → `OvertimeSubmitted`, `LamThemKetQua` → `OvertimeDecided`, `BaoCaoLeader` → `LeaderReportSent`.
- `leader_reports.SummaryJson`: khóa dự án `kh/tt/st` → `need/onSite/status`.

## Bảng đề xuất lịch (migration `CalendarProposals`, 2026-10-15)

- `calendar_proposals`: Id, Date, ProposerId (NV đề xuất), ProjectId, PlanItemId?, Title, Start, End, Status (Pending/Approved/Rejected),
  Reason (lý do từ chối), DecidedBy, DecidedAt, CreatedAt.
- `calendar_proposal_people`: (ProposalId, EmployeeId) — những người sẽ có việc nếu được duyệt (đề xuất việc của mình: chỉ người đề xuất).
- `calendar_proposals.Departments` (migration `ProposalDepartments`): phòng ban hỗ trợ đề xuất, cách nhau ";".
- Cột mới `work_tasks.Supported` (migration `TaskSupported`): người hỗ trợ đã tích "đã hỗ trợ".
