# Nhật ký bàn giao

Ghi mới nhất ở trên. Mỗi phiên: ngày, AI/người làm, đã làm, đang dở, việc tiếp theo, quyết định.

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
- Connection string để ở `QMDept/appsettings.Local.json` (gitignore). Chưa có → app hiện trang hướng dẫn.
- Các khác biệt có chủ đích so với artifact: `docs/nghiep-vu.md` mục 9.

### Việc tiếp theo
- Khi chủ dự án ghép connection string: chạy app (tự migrate) trên DB thật, tạo tài khoản TP đầu tiên, kiểm thử từng màn hình.
