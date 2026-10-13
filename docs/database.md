# Cơ sở dữ liệu

- **DB thật hiện tại: MariaDB 10.11** (host databaseasp.net, database `db73331`, `lower_case_table_names=1`, utf8mb4_general_ci).
  Kết nối qua **MySql.EntityFrameworkCore 10.0.9** (provider chính thức của Oracle; Pomelo chưa hỗ trợ EF Core 10).
  API: `options.UseMySQL(conn)`; extension cấu hình ở namespace `MySql.EntityFrameworkCore.Extensions` (đã `global using`).
- Provider Oracle lỗi khi lấy khóa migration trên MariaDB và lớp lịch sử của nó là internal → dùng
  `Data/MariaDbHistoryRepository.cs` (bảng `__EFMigrationsHistory` chuẩn + khóa `GET_LOCK` timeout 600s), đăng ký bằng
  `ReplaceService<IHistoryRepository, MariaDbHistoryRepository>()` trong `Program.cs`. Không hỗ trợ `migrations script --idempotent`.
- Đã áp migration `InitialCreate` lên DB thật ngày 2026-10-13 (DB trước đó trống).
- Mọi bảng `utf8mb4`. Enum lưu chuỗi (varchar 20). Ngày làm việc kiểu `date` (DateOnly); mốc thời gian `datetime(6)` **UTC**.
  Giờ trong ngày là chuỗi `HH:mm` (cho phép `24:00` làm giờ kết thúc).

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
| `plan_items` | PlanItem | → projects (Cascade) | đầu mục kế hoạch; Status ChuaLam/DangLam/HoanThanh; DoneDate/DoneBy |
| `assignments` | Assignment | (ProjectId, EmployeeId) unique | Role Lead/Support |
| `work_tasks` | WorkTask | idx (Date, EmployeeId), idx FromDate, idx ExtraId | Source, Status, Selected, CarryOk, CarriedFromId/CarriedToId (tham chiếu mềm, không FK), GivenBy |
| `day_plans` | DayPlan | (Date, EmployeeId) unique | trạng thái làm việc, báo cơm, nghỉ, công tác, hỗ trợ, quyết định Leader/TP |
| `day_plan_tasks` | DayPlanTask | PK (DayPlanId, WorkTaskId), cả hai Cascade | = dayPlans.taskIds |
| `day_reports` | DayReport | (Date, EmployeeId) unique | Blocked, Note, quyết định Leader |
| `leaves` | Leave | (Date, EmployeeId) unique | Leader báo nghỉ |
| `project_needs` | ProjectNeed | (Date, ProjectId) unique | ghi đè nhu cầu nhân sự theo ngày |
| `extras` | Extra | idx Date; TaskId → work_tasks (SetNull) | phát sinh; Status lưu ChoDuyet/DaDuyet/TuChoi |
| `photos` | Photo | TaskId/ExtraId (SetNull) | file trên đĩa (`Uploads:Path`, mặc định `QMDept/App_Data/uploads`) |
| `leader_reports` | LeaderReport | (Date, LeaderId) unique | SummaryJson = ảnh chụp số liệu lúc gửi (longtext) |
| `overtimes` | Overtime | (Date, PersonId) unique | đăng ký làm thêm; Role = vai trò lúc đăng ký |
| `overtime_tasks` | OvertimeTask | → overtimes (Cascade); WorkTaskId (SetNull) | việc làm tiếp (snapshot tiêu đề) |
| `ot_sends` | OtSend | Date unique | TP gửi tổng hợp suất ăn cho HCNS |

Sơ đồ khóa của artifact (Firestore-like) → bảng: `id = ${date}_${employee}` được thay bằng unique index (Date, EmployeeId).
