# Bản đồ màn hình

Route ↔ hàm artifact (`docs/artifact/qlcl-artifact.html`) ↔ file code. Trạng thái: ✅ xong · 🚧 đang làm · ⬜ chưa làm.

| Giao diện | Route | Hàm artifact | Code | Trạng thái |
|---|---|---|---|---|
| Chung | `/Account/Login`, `/Account/Bootstrap`, `/Account/Logout` | loginView, loginForm, bootForm | AccountController, Views/Account | ✅ |
| Chung | `/` → trang mặc định theo vai trò; `/photo/{id}` | — | HomeController | ✅ |
| Chung | Thanh trên, chọn giao diện, "Xem nhân viên/Leader", thanh tab + badge | renderChrome | _Layout, ChromeBuilder, ScopeResolver | ✅ |
| Quản trị | `/Admin/TaiKhoan` (tạo/sửa/xóa/khôi phục, đặt mật khẩu) | — (bổ sung theo yêu cầu chủ dự án) | AdminController, Views/Admin | ✅ |
| NV | `/Nv/Thang` (+ `?as=all` cả nhóm, giao thêm việc) | nvMonth, teamDayList, calAssignForm | NvController, Views/Nv | 🚧 |
| NV | `/Nv/DuAn` (+ `?pj=&tab=`) | nvProjects, projectCard, projectDetail | NvController, _ProjectDetail | 🚧 |
| NV | `/Nv/DauNgay` | nvMorning, openLeave, applyLeave, addTaskFields | NvController, wwwroot/js/leave.js | 🚧 |
| NV | `/Nv/CuoiNgay` | nvEvening, openUpdate, openExtra | NvController | 🚧 |
| Chung | `/LamThem?r=nv\|leader\|tp` (+ `tab=me\|duyet\|th`) | otScreen, otMine, otApprove, otSummary | LamThemController, wwwroot/js/ot.js | 🚧 |
| Leader | `/Leader/TongQuan` | ldOverview | LeaderController | 🚧 |
| Leader | `/KeHoach/Duyet` | ldApprove | KeHoachController.Duyet.cs | 🚧 |
| Leader | `/KeHoach/BaoCao` | ldEodApprove, carrySection | KeHoachController.Duyet.cs | 🚧 |
| Leader | `/KeHoach/Tuan` | ldWeek | KeHoachController.Manage.cs | 🚧 |
| Leader | `/KeHoach/DuAn` | ldItems, projForm | KeHoachController.Manage.cs | 🚧 |
| Leader | `/KeHoach/NhanSu` | ldPeople, openPerson (TP: quản lý tài khoản) | KeHoachController.Manage.cs | 🚧 |
| Leader | `/KeHoach/PhanCong` | ldAssign (artifact không có tab — bản này thêm) | KeHoachController.Manage.cs | 🚧 |
| Leader | `/Leader/TheoDoi` | ldTrack | LeaderController | 🚧 |
| Leader | `/Leader/BaoCao` (+ `tab=tuan`) | ldReport, weekReport | LeaderController | 🚧 |
| TP | `/Tp/TongQuan` | tpOverview | TpController | 🚧 |
| TP | `/Tp/PheDuyet` | tpApprove | TpController | 🚧 |
| TP | `/Tp/DuAn` | tpProjects | TpController | 🚧 |
| TP | `/Tp/BaoCao` (+ `tab=tuan`) | tpReports, weekReport | TpController | 🚧 |

Không port (code chết trong artifact): `nvWeek`, `teamMonthTable`, dữ liệu mẫu (`clearSample`).
