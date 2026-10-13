# Nhật ký bàn giao

Ghi mới nhất ở trên. Mỗi phiên: ngày, AI/người làm, đã làm, đang dở, việc tiếp theo, quyết định.

---

## Phiên 1 (tiếp) — 2026-10-13 tối (Claude Code, Opus 5.5)

### Đã làm
- Hoàn tất toàn bộ màn hình (commit `30f5781`), rồi theo yêu cầu chủ dự án:
  - **Chỉ dùng văn bản**: gỡ ảnh hiện trường, bảng `photos`, PhotoStore.
  - **Thông báo đẩy SignalR** + trang `/Settings`.
  - **Xem màn hình cấp dưới (chỉ xem)**.
  - **Favicon**.
  - **Code tiếng Anh**: đổi toàn bộ định danh, route, file, giá trị enum sang tiếng Anh.
  - **Giờ VN** qua `VnTime`.
  - **Giao diện responsive**.
- Migration `EnglishNames` áp lên DB thật (đổi tên cột + chuyển giá trị enum cũ sang tiếng Anh, xem `docs/database.md`); đã truy vấn
  kiểm tra không còn giá trị tiếng Việt.
- Sửa lỗi từ đợt E2E trước:
  - Vòng chuyển hướng vô hạn khi phiên cũ bị thu hồi (`HomeController.Index`, `Account/Login` GET tự đăng xuất).
  - Giới hạn đăng nhập 10 lần/phút/IP (`AddRateLimiter`, policy `login`).
  - Việc ngoài KH thêm khi KH đã gửi.
  - Dự án đã bàn giao.
  - Lưới 30 phút / 0 giờ của làm thêm.
  - Quyết định lại làm thêm.
  - Các kiểm tra trong trang Quản trị.
- Yêu cầu giao diện mới (ghi ở agents.md §1 mục 8, 9, 14, 15, 16):
  - Bỏ mọi placeholder.
  - Lược chú thích và phụ đề trang.
  - **Quản trị bao trùm TP** (`ScopeResolver.UiRoleOf`).
  - Tài khoản đã xóa chỉ hiện dạng trạng thái ở trang Tài khoản.
  - Lịch cả nhóm chuyển từ combobox sang nút "Lịch nhóm".
  - Trao đổi bằng tiếng Việt.
- **Tối ưu tốc độ**: đo ~10 s/trang (mỗi truy vấn ~750 ms vì DB ở xa). Thêm `Services/DataCache.cs` (bộ đệm dùng chung tự
  làm mới khi ghi + `KeepOpenInterceptor` + nạp song song), đăng ký `AddDbContextFactory` + DbContext scoped. Chi tiết: `docs/kien-truc.md` mục "Hiệu năng".
- Smoke test 65 trang × 5 tài khoản: đạt (trước khi thêm cache).

### Đang dở
- Khoảng 21:40 DB thật báo "Access denied" cho IP máy dev rồi chặn cổng 3306 (connection string không đổi) → app dừng,
  4 agent E2E (NV hằng ngày, làm thêm, kế hoạch + TP, tài khoản / xem màn hình / thông báo) dừng giữa chừng.
- Chưa chạy bản có `DataCache` trên DB thật, chưa đo lại tốc độ.
- Chưa xóa dữ liệu test (`[E2E-…]`, `[E2E2-…]`, tài khoản `zz.*` Id > 5) — chủ dự án đã đồng ý xóa; giữ 5 tài khoản chính
  (tp, tp.leader1, tp.leader1.nv1, tp.leader1.nv2, admin).

### Việc tiếp theo
- DB mở lại → xóa dữ liệu test → chạy app → smoke test → đo tốc độ (`perf.mjs`) → E2E lại các luồng còn thiếu
  (đặc biệt Quản trị dùng giao diện TP, nút Lịch nhóm, trang Tài khoản lọc trạng thái).

---

## Phiên 1 — 2026-10-13 (Claude Code, Opus 5.5)

### Đã làm
- Đọc toàn bộ artifact "Quản lý công việc phòng QLCL&BD" (dữ liệu trong artifact đều là dữ liệu mẫu). Lưu bản sao
  mã nguồn vào `docs/artifact/qlcl-artifact.html`. Đoạn chat "QLCL" trên claude.ai **không truy cập được** từ phiên này
  (trình duyệt tích hợp chưa đăng nhập claude.ai) → đặc tả dựa hoàn toàn vào artifact.
- Nền tảng: EF Core + MySQL (MySql.EntityFrameworkCore 10.0.9), entity + migration `InitialCreate`, cookie auth
  (đăng nhập mã NV + mật khẩu, thiết lập TP đầu tiên), `ViewScope`/`ScopeResolver`, `Dept` (port toàn bộ hàm truy vấn),
  `Blocks`/`Ui` (port khối HTML), layout + CSS của artifact, `site.js` (sheet/confirm/prompt/thu nhỏ ảnh…),
  `WorkflowService` (duyệt phát sinh, giao việc, xóa việc), `PhotoStore`, partial dùng chung.
- Tài liệu: `agents.md`, `docs/nghiep-vu.md`, `docs/kien-truc.md`, `docs/database.md`, `docs/man-hinh.md`.

### Đang làm
- Triển khai các màn hình NV / Leader / Kế hoạch & duyệt / TP / Làm thêm (xem `docs/man-hinh.md`).

### Quyết định
- Provider `MySql.EntityFrameworkCore` (Oracle) vì Pomelo chưa có bản cho EF Core 10. File migration provider sinh
  ra thiếu `using` → đã thêm `GlobalUsings.cs` (`global using MySql.EntityFrameworkCore.Extensions;`).
- Thời gian lưu UTC, hiển thị giờ VN (UTC+7) qua `VnTime`. Giờ trong ngày lưu chuỗi "HH:mm" (cho phép 24:00).
- Connection string DB thật (MariaDB 10.11) nằm trong `QMDept/appsettings.json`, commit lên git theo quyết định của chủ dự án
  (chủ dự án sẽ tự gỡ sau). Chưa có connection string → app hiện trang hướng dẫn.
- Các khác biệt có chủ đích so với artifact: `docs/nghiep-vu.md` mục 9.

### Việc tiếp theo
- Khi chủ dự án ghép connection string: chạy app (tự migrate) trên DB thật, tạo tài khoản TP đầu tiên, kiểm thử từng màn hình.
