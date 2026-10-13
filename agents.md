# agents.md — Hướng dẫn bàn giao cho AI / người tiếp quản dự án QMDept

> File này là điểm vào bắt buộc cho MỌI phiên làm việc (bất kể AI nào: Claude, Codex, Gemini, Copilot…).
> Đọc hết file này + `docs/handoff.md` trước khi sửa code. Cuối phiên phải cập nhật `docs/handoff.md`.

## 1. Quy tắc bắt buộc của chủ dự án (KHÔNG được vi phạm)

1. **Tài liệu bàn giao**: luôn duy trì `agents.md` (tên viết thường, ở thư mục gốc repo) và các file trong `docs/`
   để phiên AI sau đọc là làm tiếp được. Mỗi phiên kết thúc phải ghi nhật ký vào `docs/handoff.md`
   (đã làm gì, đang dở gì, việc tiếp theo, quyết định quan trọng). Thay đổi kiến trúc/DB/nghiệp vụ → cập nhật doc tương ứng.
2. **Chỉ dùng DB thật**: khi chủ dự án đã ghép connection string, mọi thao tác (migration, kiểm tra dữ liệu, chạy app)
   đều làm trực tiếp trên DB thật đó. **Không tạo DB test**, không dùng InMemory/SQLite/DB tạm để thay thế.
   Chưa có connection string thì chỉ build + tạo migration, không tự dựng DB.
3. **Nguồn yêu cầu nghiệp vụ**: artifact "Quản lý công việc phòng QLCL&BD"
   (https://claude.ai/artifact/1EVpUiV9HUGFyWTHz2XJH1) và đoạn chat "QLCL" (đoạn chat đã tạo ra artifact đó).
   Mọi màn hình / luật nghiệp vụ phải bám theo artifact. Bản tóm tắt: `docs/nghiep-vu.md`.
   Bản sao mã nguồn artifact (để đọc khi không truy cập được claude.ai): `docs/artifact/qlcl-artifact.html`.
   Artifact thay đổi trên claude.ai → đọc lại bằng công cụ Artifact `read`, cập nhật bản sao + `docs/nghiep-vu.md`.
4. **Git**: chỉ làm việc trên nhánh `master` duy nhất (không tạo nhánh khác, không worktree, không PR).
   **Cuối mỗi phiên tự động `git commit` và `git push origin master`.**
5. **Quy tắc phải ghi vào file**: mọi quy tắc / yêu cầu làm việc chủ dự án đưa ra phải được ghi vào `agents.md`
   (hoặc file `docs/` tương ứng), không chỉ lưu trong bộ nhớ riêng của AI — phiên / AI khác chỉ đọc được file.

## 2. Tổng quan kỹ thuật

- ASP.NET Core MVC (.NET 10), Razor Views (server-render, mobile-first), EF Core 10 + MySQL
  (provider `MySql.EntityFrameworkCore` 10.0.9 — Pomelo chưa hỗ trợ EF Core 10), cookie authentication.
- Mã nguồn web trong `QMDept/` (project `QMDept/QMDept.csproj`, solution `QMDept.slnx`).
- Kiến trúc chính: `ViewScope` (ai đang xem gì) + `Dept` (ảnh chụp dữ liệu + toàn bộ hàm truy vấn port từ artifact)
  + `Blocks`/`Ui` (khối HTML port từ artifact) + controller theo vai trò. Chi tiết: `docs/kien-truc.md`.

## 3. Lệnh thường dùng

```bash
cd QMDept
dotnet build                                   # build
dotnet run --launch-profile https              # chạy (cần connection string — xem docs/database.md)
dotnet ef migrations add <Ten> -o Data/Migrations   # tạo migration khi đổi model
dotnet ef database update                      # áp migration lên DB thật (app cũng tự migrate khi khởi động)
```

Connection string DB thật đang nằm trong `QMDept/appsettings.json` và được commit lên git — **quyết định của chủ dự án
ngày 2026-10-13** ("cứ làm vậy đi, sau này tôi gỡ sau"). Không tự ý chuyển/xóa khi chưa được chủ dự án yêu cầu.
(`appsettings.Local.json` vẫn được hỗ trợ, gitignore, ưu tiên cao hơn — dùng khi chủ dự án muốn gỡ mật khẩu khỏi git.)
DB thật hiện tại là **MariaDB 10.11** (xem `docs/database.md`). Tài khoản đăng nhập thử trên DB thật: `docs/tai-khoan.local.md`
(file local, gitignore — chỉ có trên máy chủ dự án; máy khác thì hỏi chủ dự án hoặc nhờ tài khoản `admin` đặt mật khẩu).
Không ghi mật khẩu tài khoản người dùng vào file được commit.

## 4. Mục lục tài liệu

| File | Nội dung |
|---|---|
| `docs/handoff.md` | Nhật ký các phiên, trạng thái hiện tại, việc đang dở, việc tiếp theo — **đọc trước tiên** |
| `docs/nghiep-vu.md` | Đặc tả nghiệp vụ rút ra từ artifact (vai trò, quy trình, luật tính, khác biệt có chủ đích) |
| `docs/man-hinh.md` | Bản đồ màn hình: route ↔ hàm artifact ↔ file code ↔ trạng thái |
| `docs/kien-truc.md` | Cấu trúc thư mục, luồng request, quy ước Razor/JS/C#, cách dùng `Dept` |
| `docs/database.md` | Bảng, quy ước kiểu dữ liệu, connection string, migration |
| `docs/artifact/qlcl-artifact.html` | Bản sao mã nguồn artifact gốc (đặc tả chuẩn) |

## 5. Lưu ý khi sửa code

- Luật nghiệp vụ có bản C# (`Services/Rules.cs`, `Services/Dept.cs`) và bản JS xem trước trên form
  (`wwwroot/js/*.js`) — sửa luật phải sửa cả hai.
- Form POST có `action` viết tay phải có `asp-antiforgery="true"` (xem `docs/kien-truc.md`).
- Không tin client: mọi POST tự kiểm quyền (`RequireSelfNv/RequireLead/RequireTp/RequireInScopeAsync`).
- `ScopeResolver` lấy ngày đang chọn (`Scope.Date`) từ query `?d=`; với POST không có `?d=` thì lấy field form `d`.
  `@Ui.ReturnField(Context.Request)` tự chèn field ẩn `d` → mọi form POST thao tác theo ngày đang xem phải có
  `@Ui.ReturnField(...)` (hoặc ghi `?d=yyyy-MM-dd` trong `action`), nếu không `Scope.Date` sẽ là hôm nay.
  Không đặt field nào khác tên `d` trong form POST.
- Thực thể trong `Dept` là chỉ đọc (AsNoTracking); ghi qua `AppDbContext`.
- Đổi entity ⇒ tạo migration mới; không sửa migration đã áp lên DB thật.
