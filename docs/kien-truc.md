# Kiến trúc & quy ước code

## Cấu trúc thư mục (`QMDept/`)

```
Program.cs                 DI, cookie auth, MySQL, auto-migrate, antiforgery toàn cục
Data/
  Enums.cs                 Mọi enum (lưu DB dạng chuỗi). Comment ghi khóa gốc của artifact ('chua_lam'…)
  Labels.cs                Nhãn tiếng Việt + class pill + màu cho enum: Labels.Of(x), Labels.PillHtml(x)
  Entities.cs              Toàn bộ entity (bảng)
  AppDbContext.cs          Cấu hình bảng / index / FK / utf8mb4
  Migrations/              EF Core migrations (MySql.EntityFrameworkCore)
Services/
  VnTime.cs                Giờ VN (UTC+7): Today, Now, Hm(utc), UtcAt(date,"HH:mm")
  Fmt.cs                   Định dạng & tiện ích ngày/giờ: Dm, Dmy, DayLabel, Monday, T24, Min, Lt/Le/Gt/Ge, Provinces…
  Rules.cs                 OtRules (làm thêm), LeaveRules (nghỉ) — luật thuần, có bản JS tương ứng ở wwwroot/js
  ViewScope.cs             Ngữ cảnh request: Account, Role (giao diện), Me (người đang xem), AllNv, Date, Pool
  ScopeResolver.cs         Điền ViewScope từ cookie đăng nhập + ?as= + ?d=
  Dept.cs                  "Ảnh chụp" dữ liệu + toàn bộ hàm truy vấn của artifact (TasksOn, PlanOf, PjStatus, IssuesOf…)
  Blocks.cs                Khối HTML dùng chung (TaskRow, ExtraRow, ProjectCard, PlanTable, PlanMeta, PjTable, Donut…)
  Ui.cs                    Helper HTML nhỏ (Pill, Bar, Head, Subtabs, Seg, TimeSelect, Select, Here/To URL…)
  ChromeBuilder.cs         Thanh trên + thanh tab + badge cho _Layout
  WorkflowService.cs       Thao tác ghi dùng chung (duyệt/từ chối phát sinh, tạo phát sinh, giao việc, xóa việc) + PhotoStore
Controllers/
  AppController.cs         Base: [Authorize], phân giải scope, Toast, Back, Do(), Require*()
  AccountController.cs     Đăng nhập / thiết lập TP / đăng xuất
  HomeController.cs        "/" → trang mặc định theo vai trò; /photo/{id}; Error
  NvController.cs          Giao diện Nhân viên        (Thang, DuAn, DauNgay, CuoiNgay)
  LeaderController.cs      Giao diện Leader           (TongQuan, TheoDoi, BaoCao)
  KeHoachController.*.cs   Leader → Kế hoạch & duyệt  (Duyet, BaoCao, Tuan, DuAn, NhanSu, PhanCong) — partial class
  TpController.cs          Giao diện Trưởng phòng     (TongQuan, PheDuyet, DuAn, BaoCao)
  LamThemController.cs     Làm thêm (dùng chung, ?r=nv|leader|tp)
ViewModels/                View-model theo màn hình (+ Shared.cs: Nav, ProjectDetailVm)
Views/Shared/              _Layout, _DayBar, _WeekBar, _MonthBar, _ProjectDetail
wwwroot/css/site.css       CSS port từ artifact + phần bổ sung MVC ở cuối
wwwroot/js/site.js         Hành vi chung bằng data-attribute (sheet, confirm, prompt, autosubmit, thu nhỏ ảnh…)
```

## Luồng một request

1. `AppController.OnActionExecutionAsync` → `ScopeResolver` điền `ViewScope` (tài khoản, giao diện từ `[UiRole]`,
   người đang xem từ `?as=` hoặc cookie `qm_as_{role}`, ngày từ `?d=`). Sai quyền giao diện → chuyển về trang mặc định.
2. Action GET: `await D.EnsureAsync(from, to)` cho khoảng ngày cần → `return View(vm)`.
3. View dùng `@inject Dept D` và `@inject ViewScope Scope` (đã khai báo trong `_ViewImports`) để gọi các hàm port từ
   artifact, ví dụ `D.TasksOn(Scope.Date, Scope.MeId)`, `D.TaskRow(t)`, `Ui.Pill(st)`.
4. Action POST (luôn `[HttpPost]`, antiforgery tự kiểm tra): kiểm quyền (`RequireSelfNv`, `RequireLead`, `RequireTp`,
   `RequireInScopeAsync`), sửa DB qua `Db` (truy vấn có tracking, KHÔNG sửa entity của `D`), `SaveChangesAsync`,
   rồi `return Back()` (PRG). Dùng `return await Do(async () => {...}, "Đã lưu")` để tự bắt `BizException` → toast lỗi.

## `Dept` — quy tắc dùng

- Bảng nhỏ (people, projects, items, assignments) nạp toàn bộ ở lần `EnsureAsync` đầu.
- Bảng theo ngày nạp theo khoảng; gọi `EnsureAsync` nhiều lần được (gộp). Truy vấn ngày chưa nạp → `InvalidOperationException`
  (cố ý, để lộ lỗi khi quên nạp). `IssuesOf(d)` cần cả `d` và `d+1`; báo cáo tuần cần cả tuần; lịch tháng cần cả tháng;
  `ProjectDetailVm.LoadAsync` tự nạp tuần + ngày.
- Thực thể của `D` là AsNoTracking → **chỉ đọc**.
- Hàm phụ thuộc phạm vi (`Staff()`, `InScope()`, `ExtrasOn()`, `CarriesFrom()`, `Kpis()`, `OtPending()`…) dựa trên
  `ViewScope.Role` và `ViewScope.Me` — giống hệt artifact.

## Quy ước giao diện (Razor)

- Điều hướng bằng `<a href>` (không SPA). Giữ trạng thái qua query: `d` (ngày), `w` (thứ 2 của tuần), `m` (tháng),
  `as` (người đang xem), `tab`/`sub`/`pj`/... theo màn hình. Dùng `Ui.Here(Context.Request, ("key", value))` để đổi
  1 tham số của trang hiện tại; `Ui.To(Context.Request, "/Path", ...)` để sang trang khác mà giữ `d`.
- Form POST: **bắt buộc** có antiforgery token (filter toàn cục `AutoValidateAntiforgeryToken` sẽ trả 400 nếu thiếu).
  Form có `action="/Path"` viết tay thì tag helper KHÔNG tự chèn token → luôn viết
  `<form method="post" action="/Nv/GuiKeHoach" asp-antiforgery="true">` (hoặc dùng `asp-controller`/`asp-action`).
  Form nằm trong `<template>` cũng vậy (hoặc gọi `@Html.AntiForgeryToken()` bên trong).
- Form POST: thêm `@Ui.ReturnField(Context.Request)` để quay lại đúng trang/tham số sau khi lưu.
- Hộp thoại kiểu bảng trượt (sheet) — xem đầu file `wwwroot/js/site.js`:
  - `<form data-confirm="Xóa …?" data-confirm-label="Xóa">` hỏi xác nhận.
  - `<form data-prompt="Từ chối kế hoạch của X" data-prompt-label="Lý do" data-prompt-required data-prompt-name="reason">`
    mở ô nhập lý do rồi gửi kèm field `reason`.
  - `<button type="button" data-sheet="tplId">` mở `<template id="tplId"><form method="post" …>…</form></template>`
    (form bên trong có nút `type="button" data-x="close"` để đóng). Form trong template vẫn có antiforgery token.
  - `<input type="file" name="photos" multiple accept="image/*" data-shrink>` thu nhỏ ảnh trước khi gửi
    (form cần `enctype="multipart/form-data"`).
  - `<select data-items-for="idSelectDauMuc">` + `window.QM_ITEMS = {projectId: [[itemId, title], ...]}` để đổi danh sách đầu mục.
  - `<select data-autosubmit>` tự gửi form GET (lọc).
  - `<select data-show-target="#supWrap" data-show-values="Nhom,NhomKhac,PhongKhac">` hiện/ẩn phần tử.
- Chế độ chỉ xem (`Scope.IsReadOnly`): view tự ẩn nút/khóa form; controller vẫn phải kiểm quyền (không tin client).
- Tiêu đề màn hình: `@Ui.Head(n, "Tiêu đề", "phụ đề")`; thanh ngày: `<partial name="_DayBar" />`.
- Chuỗi người dùng nhập: Razor tự encode khi dùng `@x`; trong C# tạo HTML phải `WebUtility.HtmlEncode`.

## Quy ước C#

- .NET 10, nullable bật, file-scoped namespace, primary constructor cho service.
- Tên tiếng Việt không dấu cho action/route theo tab của artifact (Thang, DuAn, DauNgay…), tên tiếng Anh cho kỹ thuật.
- Thông báo người dùng bằng tiếng Việt, giữ nguyên câu chữ của artifact khi có.
- Không thêm thư viện JS/CSS ngoài. Không dùng Bootstrap trong view (file lib/ của template còn để nguyên, không tham chiếu).
