# Bản đồ màn hình

Route ↔ hàm artifact (`docs/artifact/qlcl-artifact.html`) ↔ file code. Trạng thái: ✅ xong · 🚧 đang làm · ⬜ chưa làm.
Tham số query dùng chung: `d` (ngày, yyyy-MM-dd), `w` (thứ 2 của tuần), `m` (ngày 1 của tháng), `as` (người đang xem: id, `all` hoặc `me`).
Mọi POST: antiforgery + `returnUrl` (+ field ẩn `d`) → xử lý → chuyển về trang trước với toast (PRG).
Toàn bộ route, khóa query, giá trị query và tên field là tiếng Anh (quy tắc 2026-10-13, agents.md §1); nhãn hiển thị vẫn tiếng Việt.

| Giao diện | Route GET | POST | Hàm artifact | Code | TT |
|---|---|---|---|---|---|
| Chung | `/Account/Login` | `Login`, `Bootstrap`, `Logout` | loginView, loginForm, bootForm | AccountController, Views/Account | ✅ |
| Chung | `/` → trang mặc định theo vai trò; `/{Controller}` → trang mặc định | — | — | HomeController, `Index` của từng controller | ✅ |
| Chung | Thanh trên, combobox "Xem màn hình" cấp dưới (chỉ xem), nút "Lịch nhóm" (`/Staff/Month?as=all`), thanh tab / menu trái + badge | — | renderChrome (đã đổi theo yêu cầu) | _Layout, ChromeBuilder, ScopeResolver | ✅ |
| Chung | `/Settings` (`?as=me`) — cài đặt thông báo đẩy | `Settings/Save` | — (bổ sung) | SettingsController, Views/Settings, Services/Notifier.cs | ✅ |
| Quản trị | Toàn bộ màn hình TP (Quản trị dùng giao diện TP) + tab `/Admin/Accounts` (`?st=all\|active\|deleted` — trạng thái Đang hoạt động / Đã xóa) + xem mọi màn hình qua combobox | `Create`, `Edit`, `SetPassword` (`clear=true` = xóa mật khẩu), `Delete`, `Restore` | — (bổ sung theo yêu cầu chủ dự án) | AdminController, Views/Admin | ✅ |
| NV | `/Staff/Month` (`?m=`; `?as=all` = lịch cả nhóm của cấp trên) | `AssignTask` (giao thêm việc ở lịch cả nhóm) | nvMonth, teamDayList, calAssignForm | StaffController.Calendar.cs, Views/Staff/Month | ✅ |
| NV | `/Staff/Projects` (`?pt=active\|done`, `?pj=&tab=overview\|plan\|people`) | — | nvProjects, projectCard, projectDetail | StaffController.Calendar.cs, _ProjectDetail | ✅ |
| NV | `/Staff/Morning` (`?edit=<ngày>` sửa kế hoạch) | `SubmitPlan`, `AddUnplannedTask` | nvMorning, openLeave, applyLeave, addTaskFields | StaffController.Reports.cs, Views/Staff/Morning, wwwroot/js/staff.js | ✅ |
| NV | `/Staff/Evening` (`?open=<id>` mở chi tiết) | `UpdateTask`, `ProposeExtra`, `SubmitReport` | nvEvening, openUpdate, openExtra | StaffController.Reports.cs, Views/Staff/Evening, staff.js | ✅ |
| Chung | `/Overtime` (`?tab=mine\|approve\|summary`, `?edit=<ngày>`; giao diện theo vai trò người đang xem) | `Save`, `Cancel`, `Decide` (v=ok\|no, reason), `Undo`, `SendMealOrder` | otScreen, otMine, otApprove, otSummary | OvertimeController, Views/Overtime, wwwroot/js/overtime.js | ✅ |
| Leader | `/Leader/Overview` | — | ldOverview | LeaderController, Views/Leader | ✅ |
| Leader | `/Planning/Approvals` (`?ap=all\|pending`, `?pj=`) | `DecidePlan`, `UndoPlanDecision`, `CommentPlan`, `ApproveExtra`, `RejectExtra` | ldApprove | PlanningController.Approval.cs | ✅ |
| Leader | `/Planning/Reports` | `DecideReport`, `UndoReportDecision`, `ApproveCarry`, `ApproveAllCarries`, `UndoCarry`, `EditCarry` | ldEodApprove, carrySection | PlanningController.Approval.cs | ✅ |
| Leader | `/Planning/Week` (`?w=`, `?emp=`) | `AddWeekTask`, `DeleteTask` | ldWeek | PlanningController.Manage.cs | ✅ |
| Leader | `/Planning/Projects` (`?pj=<id>\|new`) | `SaveProject`, `AddItem`, `SetItemStatus`, `DeleteItem` | ldItems, projForm | PlanningController.Manage.cs | ✅ |
| Leader + TP | `/Planning/People` (TP: tab "Nhân sự") | `ToggleLeave`; TP: `CreateAccount`, `EditAccount`, `DeleteAccount` | ldPeople, openPerson | PlanningController.Manage.cs | ✅ |
| Leader | `/Planning/Assign` | `AssignTask`, `Unassign` | ldAssign (artifact không có tab — bản này thêm) | PlanningController.Manage.cs | ✅ |
| Leader | `/Leader/Tracking` (`?mode=staff\|project`, `?pj=`) | `AddExtra`, `ApproveExtra`, `RejectExtra` | ldTrack | LeaderController | ✅ |
| Leader | `/Leader/Reports` (`?tab=day\|week`) | `SetNeed`, `SubmitReport` | ldReport, weekReport | LeaderController | ✅ |
| TP | `/Manager/Overview` | — | tpOverview | ManagerController, Views/Manager | ✅ |
| TP | `/Manager/Approvals` (`?mode=plan\|extra`, `?f=pending\|approved\|rejected\|all`) | `Decide` | tpApprove | ManagerController | ✅ |
| TP | `/Manager/Projects` (`?pj=&tab=overview\|plan\|people`) | — | tpProjects | ManagerController | ✅ |
| TP | `/Manager/Reports` (`?tab=day\|week`) | — | tpReports, weekReport | ManagerController | ✅ |

Tab con của Leader → Kế hoạch & duyệt (partial `_PlanningTabs`, model = khóa tab): `approvals`, `reports`, `week`, `projects`,
`people`, `assign`. Id tab thanh dưới / menu trái (`ChromeBuilder.TabDefs`): NV `month`, `projects`, `morning`, `overtime`,
`evening`; Leader `overview`, `planning`, `tracking`, `reports`, `overtime`; TP `overview`, `approvals`, `projects`, `reports`,
`overtime`, `people`; Quản trị = tab của TP + `accounts`.

Giá trị form đáng chú ý: báo cáo đầu ngày `status`/`meal`/`support`/`leaveType`/`leaveSession` = tên enum (`WorkStatus`, `Meal`,
`SupportScope`, `LeaveType`, `LeaveSession`), `province`, `customer`, `supportText`, `picks`; cập nhật việc `state` (tên `TaskState`);
báo cáo cuối ngày `blocked=yes|no`; làm thêm `dayType=workday|holiday`, `meal=rice|snack|noodle|none`.

Không port (code chết trong artifact): `nvWeek`, `teamMonthTable`, dữ liệu mẫu (`clearSample`).
