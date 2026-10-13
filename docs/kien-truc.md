# Kiến trúc & quy ước code

## Cấu trúc thư mục (`QMDept/`)

```
Program.cs                 DI, cookie auth, MySQL, auto-migrate, antiforgery toàn cục
Data/
  Enums.cs                 Mọi enum, tên tiếng Anh (lưu DB dạng chuỗi = tên enum). Comment ghi khóa gốc của artifact ('chua_lam'…)
  Labels.cs                Nhãn tiếng Việt + class pill + màu cho enum: Labels.Of(x), Labels.PillHtml(x)
  Entities.cs              Toàn bộ entity (bảng)
  AppDbContext.cs          Cấu hình bảng / index / FK / utf8mb4
  MariaDbHistoryRepository.cs  Bảng lịch sử + khóa migration chạy được trên MariaDB (thay bản lỗi của provider)
  Migrations/              EF Core migrations (MySql.EntityFrameworkCore)
Services/
  VnTime.cs                Giờ VN (UTC+7): Today, Now, Hm(utc), UtcAt(date,"HH:mm") — mọi "hôm nay/bây giờ" nghiệp vụ đi qua đây
  Fmt.cs                   Định dạng & tiện ích ngày/giờ: Dm, Dmy, DayLabel, Monday, T24, Min, Lt/Le/Gt/Ge, Provinces…
  Rules.cs                 OtRules (làm thêm), LeaveRules (nghỉ) — luật thuần, có bản JS tương ứng ở wwwroot/js
  ViewScope.cs             Ngữ cảnh request: Account, Role (giao diện = vai trò người đang xem), Me, AllStaff, Date, Pool, Subordinates
  ScopeResolver.cs         Điền ViewScope từ cookie đăng nhập + ?as= (cookie qm_view) + ?d=; quyết định trang có hợp giao diện
  Notifier.cs              Thông báo đẩy: NotifyHub (SignalR), NotifyCatalog (loại + người nhận), Notifier (gửi sau khi lưu)
  Dept.cs                  "Ảnh chụp" dữ liệu + toàn bộ hàm truy vấn của artifact (TasksOn, PlanOf, PjStatus, IssuesOf…)
  Blocks.cs                Khối HTML dùng chung (TaskRow, ExtraRow, ProjectCard, PlanTable, PlanMeta, PjTable, Donut…)
  Ui.cs                    Helper HTML nhỏ (Pill, Bar, Head, Subtabs, Seg, TimeSelect, Select, Here/To URL…)
  ChromeBuilder.cs         Thanh trên + thanh tab + badge cho _Layout
  AccountRules.cs          Chuẩn hóa mã (chữ thường), kiểm tra mã/mật khẩu, đặt mật khẩu + SecurityStamp
  LeaderReportSummary.cs   Ảnh chụp số liệu báo cáo ngày Leader gửi TP (JSON)
  WorkflowService.cs       Thao tác ghi dùng chung (duyệt/từ chối phát sinh, tạo phát sinh, giao việc, xóa việc) + gửi thông báo
Controllers/
  AppController.cs         Base: [Authorize], phân giải scope, Toast, Back, Do(), Require*()
  AccountController.cs     Đăng nhập / thiết lập TP / đăng xuất
  HomeController.cs        "/" → trang mặc định theo vai trò; Error
  StaffController.*.cs     Giao diện Nhân viên        (Month, Projects | Morning, Evening) — partial: .Calendar.cs, .Reports.cs
  LeaderController.cs      Giao diện Leader           (Overview, Tracking, Reports)
  PlanningController.*.cs  Leader → Kế hoạch & duyệt  (Approvals, Reports | Week, Projects, People, Assign) — partial: .Approval.cs, .Manage.cs
  ManagerController.cs     Giao diện Trưởng phòng     (Overview, Approvals, Projects, Reports)
  OvertimeController.cs    Làm thêm (dùng chung NV/Leader/TP; giao diện theo vai trò người đang xem)
  AdminController.cs       Quản trị tài khoản (/Admin/Accounts) — bổ sung, không có trong artifact
  SettingsController.cs    Cài đặt thông báo đẩy (/Settings, mọi vai trò) — bổ sung
ViewModels/                View-model theo màn hình (Staff*, Leader, Planning*, Manager, Overtime + Shared.cs: Nav, ProjectDetailVm)
Views/                     Staff/, Leader/, Planning/, Manager/, Overtime/, Admin/, Settings/, Account/
Views/Shared/              _Layout, _DayBar, _WeekBar, _MonthBar, _ProjectDetail, _PlanningTabs, _TaskFields, _ExtraSheet, _ItemsJson
wwwroot/css/site.css       CSS port từ artifact + phần bổ sung MVC ở cuối
wwwroot/js/site.js         Hành vi chung bằng data-attribute (sheet, confirm, prompt, autosubmit…), chế độ chỉ xem, thông báo đẩy SignalR
wwwroot/js/staff.js        Báo cáo đầu/cuối ngày: hộp chọn giờ nghỉ (applyLeave), nháp form (sessionStorage), sheet cập nhật việc
wwwroot/lib/microsoft-signalr  Thư viện client SignalR chính thức (@microsoft/signalr 10.0.11, MIT)
wwwroot/favicon.*, icon-*.png, apple-touch-icon.png, site.webmanifest  Bộ icon ứng dụng (dựng từ favicon.svg)
wwwroot/js/overtime.js     Làm thêm: bản JS của otRule/otMealOpts/otLive để xem trước mức tăng ca
GlobalUsings.cs            global using MySql.EntityFrameworkCore.Extensions (migration provider sinh ra cần)
```

## Luồng một request

1. `AppController.OnActionExecutionAsync` → `ScopeResolver` điền `ViewScope` (tài khoản; người đang xem từ `?as=` hoặc cookie
   `qm_view`; giao diện = vai trò người đó; ngày từ `?d=`). Trang không hợp giao diện → chuyển về trang mặc định của giao diện đó.
   Đang xem màn hình cấp dưới (`Scope.IsViewingOther`) → mọi POST bị chặn ngay tại đây (toast "chỉ xem").
2. Action GET: `await D.EnsureAsync(from, to)` cho khoảng ngày cần → `return View(vm)`.
3. View dùng `@inject Dept D` và `@inject ViewScope Scope` (đã khai báo trong `_ViewImports`) để gọi các hàm port từ
   artifact, ví dụ `D.TasksOn(Scope.Date, Scope.MeId)`, `D.TaskRow(t)`, `Ui.Pill(st)`.
4. Action POST (luôn `[HttpPost]`, antiforgery tự kiểm tra): kiểm quyền (`RequireSelfStaff`, `RequireLead`, `RequireManager`,
   `RequireInScopeAsync`), sửa DB qua `Db` (truy vấn có tracking, KHÔNG sửa entity của `D`), `SaveChangesAsync`,
   rồi `return Back()` (PRG). Dùng `return await Do(async () => {...}, "Đã lưu")` để tự bắt `BizException` → toast lỗi.

## `Dept` — quy tắc dùng

- Dữ liệu lấy qua `DataCache` (singleton, dùng chung mọi request — xem mục "Hiệu năng" bên dưới), nên entity của `D`
  dùng chung giữa các request: **tuyệt đối không sửa** thuộc tính / danh sách của chúng.
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
  `<form method="post" action="/Staff/SubmitPlan" asp-antiforgery="true">` (hoặc dùng `asp-controller`/`asp-action`).
  Form nằm trong `<template>` cũng vậy (hoặc gọi `@Html.AntiForgeryToken()` bên trong).
- Form POST: thêm `@Ui.ReturnField(Context.Request)` để quay lại đúng trang/tham số sau khi lưu.
- Hộp thoại kiểu bảng trượt (sheet) — xem đầu file `wwwroot/js/site.js`:
  - `<form data-confirm="Xóa …?" data-confirm-label="Xóa">` hỏi xác nhận.
  - `<form data-prompt="Từ chối kế hoạch của X" data-prompt-label="Lý do" data-prompt-required data-prompt-name="reason">`
    mở ô nhập lý do rồi gửi kèm field `reason`.
  - `<button type="button" data-sheet="tplId">` mở `<template id="tplId"><form method="post" …>…</form></template>`
    (form bên trong có nút `type="button" data-x="close"` để đóng). Form trong template vẫn có antiforgery token.
  - `<select data-items-for="idSelectDauMuc">` + `window.QM_ITEMS = {projectId: [[itemId, title], ...]}` để đổi danh sách đầu mục.
  - `<select data-autosubmit>` tự gửi form GET (lọc).
  - `<select data-show-target="#supportWrap" data-show-values="Team,OtherTeam,OtherDept">` hiện/ẩn phần tử.
- Chế độ chỉ xem (`Scope.IsReadOnly`): view tự ẩn nút/khóa form; controller vẫn phải kiểm quyền (không tin client).
- Tiêu đề màn hình: `@Ui.Head(n, "Tiêu đề")` (không thêm dòng giới thiệu — agents.md §1 mục 14; chỉ dùng phụ đề khi nó là dữ liệu,
  vd "12 nhân viên"); thanh ngày: `<partial name="_DayBar" />`.
- Không dùng `placeholder` cho ô nhập; không thêm chú thích hướng dẫn (agents.md §1 mục 14).
- Chuỗi người dùng nhập: Razor tự encode khi dùng `@x`; trong C# tạo HTML phải `WebUtility.HtmlEncode`.

## Xem màn hình cấp dưới (chỉ xem)

- `ViewScope.Account` = người đăng nhập; `ViewScope.Me` = người đang được xem; `ViewScope.Role` = giao diện =
  `ScopeResolver.UiRoleOf(Me.Role)` (Quản trị → Manager). `IsViewingOther` (Me ≠ Account) ⇒ chỉ xem; `AllStaff` ⇒ lịch tháng
  cả nhóm (giao diện NV, chỉ form giao việc ghi được).
- **Quản trị bao trùm TP**: `UiRoleOf(Admin) = Manager` → Quản trị dùng mọi trang `[UiRole(Manager)]` với đủ quyền
  (`ViewScope.IsManager` gồm Admin; `Notifier` gửi thông báo của TP cho cả Quản trị); tab "Tài khoản" thêm vào thanh tab khi
  `Scope.IsAdmin` và không xem người khác; trang `[UiRole(Admin)]` chỉ mở ở màn hình của chính tài khoản Quản trị.
- Combobox trên header (`ChromeBuilder.ViewOptions`) chỉ gồm "Màn hình của tôi" + cấp dưới, gửi GET `?as=<id>|me` tới trang hiện
  tại. Lịch tháng cả nhóm là **nút "Lịch nhóm"** cạnh combobox (`ChromeModel.TeamHref` = `/Staff/Month?as=all`). Lựa chọn nhớ trong cookie
  `qm_view`. Mở trang thuộc giao diện của chính mình mà không có `?as=` ⇒ tự quay về màn hình của mình, **trừ khi** người trong
  cookie dùng cùng giao diện với trang (Quản trị đang xem TP rồi bấm tab TP → vẫn xem TP). Link thông báo kèm `as=me`.
  Form POST của trang dùng chung (Làm thêm: `OvertimeUi.PostUrl`) và form "Giao thêm việc" ở lịch nhóm luôn ghi rõ `as=`
  trong `action` (`as=me` / `as=all`) để đổi combobox ở tab khác không làm form bị chặn.
- Mỗi trang khai báo giao diện: `[UiRole(Role.X)]`, không khai báo (dùng chung NV/Leader/TP — Làm thêm), `[AnyUiRole]` (Cài đặt);
  `PlanningController` override `UiRoles` để Nhân sự + tài khoản + báo nghỉ dùng chung Leader/TP.
- Khi `IsViewingOther`: `<body class="readonly">` — CSS ẩn nút trong form POST, nút `data-sheet`, `button[form]`; site.js khóa ô nhập.
  Server vẫn chặn mọi POST khi IsViewingOther. Ở lịch nhóm (`Scope.AllStaff`) server chặn mọi POST trừ `Staff/AssignTask`.
  POST bị `Forbidden` (trang không hợp giao diện) có toast báo lỗi thay vì chuyển trang im lặng.

## Hiệu năng (DB ở xa)

- Đo 2026-10-13: mỗi vòng mạng tới DB ~250 ms; EF mở/đóng kết nối quanh mỗi truy vấn và MySql.Data tốn ~2 vòng khi lấy lại kết nối
  từ pool ⇒ ~750 ms/truy vấn, ~13 truy vấn/trang ⇒ **~10 giây/trang**.
- `KeepOpenInterceptor` (Services/DataCache.cs): chặn EF đóng kết nối giữa các truy vấn — một kết nối suốt request, trả về pool khi
  DbContext bị dispose.
- `DataCache` (singleton): ảnh chụp bảng nhỏ (people, projects, plan_items, assignments, notify_settings) + dữ liệu theo khoảng ngày
  của `Dept`. `ScopeResolver`, `ChromeBuilder`, `HomeController`, `Dept` đều đọc từ đây ⇒ trang xem lại không cần truy vấn DB.
  `DataCacheInterceptor` tăng phiên bản sau mọi lệnh không phải SELECT, sau COMMIT và ROLLBACK ⇒ cache cũ bị bỏ ngay. TTL 60 giây.
  Lúc thiếu cache: nạp song song trên nhiều DbContext (IDbContextFactory), tối đa 8 kết nối, mỗi khóa chỉ nạp một lần.
- Hệ quả: **mọi ghi phải qua EF** (SaveChanges / ExecuteUpdate / ExecuteDelete). Không sửa entity lấy từ `D` / `DataCache`.
  Truy vấn riêng của trang (`Db.…`) vẫn chạy trực tiếp trên DB — hạn chế thêm truy vấn mới trong GET, ưu tiên dữ liệu của `D`.

## Bảo mật đăng nhập

- Phiên bị thu hồi khi đổi mật khẩu / vai trò / mã hoặc xóa tài khoản (claim `qm:stamp` ≠ `Person.SecurityStamp`):
  cookie auth `OnValidatePrincipal` (Program.cs, đọc từ `DataCache`) từ chối và đăng xuất ở **mọi endpoint, kể cả hub SignalR**;
  `ScopeResolver`, `HomeController.Index`, `Account/Login` GET cũng kiểm tra (không lặp chuyển hướng).
- Kết nối SignalR đang mở được ghi lại kèm stamp (`NotifyHub.Open`); `Notifier` chỉ gửi tới kết nối có stamp còn khớp và
  ngắt kết nối của phiên đã thu hồi (tab mở sẵn không nhận thông báo nữa).
- Tài khoản Quản trị / TP không bao giờ ở trạng thái "chưa có mật khẩu" (ai đăng nhập trước sẽ đặt mật khẩu và chiếm quyền):
  Quản trị tạo TP/Quản trị phải kèm mật khẩu, không xóa mật khẩu của họ, không đổi vai trò tài khoản chưa có mật khẩu thành
  TP/Quản trị; TP ở Nhân sự không tạo được TP và không xóa mật khẩu / đổi mã của tài khoản TP.
- Giới hạn đăng nhập: policy `login` (Program.cs) — 10 POST `Login`/`Bootstrap` mỗi phút cho mỗi IP (`RemoteIpAddress`), vượt quá → 429.
  **Khi triển khai sau reverse proxy** (nginx, load balancer…) mọi người dùng sẽ chung IP của proxy → phải bật
  `UseForwardedHeaders` với `KnownProxies` đúng địa chỉ proxy (không tin `X-Forwarded-For` từ nguồn bất kỳ — sẽ bị giả mạo để né giới hạn).

## Thông báo đẩy (SignalR)

- Server: `AddSignalR()`, `MapHub<NotifyHub>("/hubs/notify")`; gửi bằng `Clients.Users(personIds)` (UserIdentifier = claim NameIdentifier).
- `Notifier` (scoped, thuộc tính `Notify` của AppController; WorkflowService cũng dùng): hàm theo sự kiện (`PlanSubmittedAsync`,
  `ReportDecidedAsync`, …) — gọi SAU `SaveChangesAsync`, không bao giờ ném lỗi; tự bỏ qua người thao tác và người đã tắt loại đó.
- Thêm loại mới: giá trị `NotifyType` (Data/Enums.cs) + dòng `NotifyCatalog.All` (nhãn, mô tả, vai trò nhận) + hàm trong
  `Notifier` + lời gọi ở controller. Cài đặt cũ tự coi loại mới là "bật".
- Client (site.js): kết nối khi `<body data-notify="1">`, tự kết nối lại; hiện thẻ trong `#notifs`; thông báo của hệ điều hành
  khi tab ẩn và trình duyệt đã cho phép (nút ở /Settings).

## Giao diện responsive (điện thoại / máy tính bảng / máy tính)

Theo yêu cầu chủ dự án (2026-10-13) giao diện phải dùng tốt cả trên máy tính và điện thoại — CSS ở cuối `site.css`:
- **< 700px (điện thoại)**: y như artifact — header 2–3 hàng, thanh tab dưới đáy, 1 cột.
- **700–1023px (máy tính bảng)**: cột nội dung 760px, thẻ dự án 2 cột, lịch tháng ô lớn hơn, vẫn thanh tab dưới.
- **≥ 1024px (máy tính)**: header một hàng (logo · chọn giao diện · xem người · tài khoản); thanh tab thành **menu dọc bên trái**
  (`body.has-side .tabbar`, bám dưới header nhờ biến `--hdr-h` do `site.js` đo); nội dung rộng tối đa 1180px (1320px khi ≥1440px).
  Trang trong danh sách `gridPages` ở `_Layout.cshtml` (tổng quan, duyệt, báo cáo, nhân sự, dự án & đầu mục, tài khoản) có
  `main.grid-page`: các thẻ `section.card` / `form.card` là con trực tiếp của `<main>` xếp **2 cột**; phần tử khác chiếm cả hàng.
- Khi viết view mới: đặt thẻ nội dung là con trực tiếp của `<main>` (không bọc thêm div) để lưới 2 cột hoạt động; bảng rộng luôn
  bọc `.tbl-wrap` (cuộn ngang trong thẻ, không làm tràn trang). Kiểm tra ở 375px, 768px và 1280px.

## Quy ước rút ra khi triển khai (bắt buộc theo)

- **Ngày của POST**: `Scope.Date` lấy từ `?d=`; POST không có `?d=` thì lấy field form `d` (do `Ui.ReturnField` chèn).
  Giao diện Leader/Làm thêm còn ghim `?d=&as=` ngay trong `action` (`LeaderUi.Post`, `OvertimeUi.PostUrl`) để thao tác đúng
  ngày/nhóm dù cookie "Xem …" đổi ở tab khác. Không đặt field form nào khác tên `d`.
- **Không đặt `<form>` trong `<p>`/`<span>`** (trình duyệt tự đóng `<p>` khi gặp `<form>`): dùng `<div class="note">` + `form.inline`,
  hoặc nút `<button form="idForm">` trỏ tới form ẩn ở chỗ khác.
- **Một `<template>` sheet chỉ chứa đúng một `<form>`**. Thao tác phá hủy bên trong sheet (vd. "Xóa tài khoản này") dùng
  `<button type="submit" form="hiddenFormId">` trỏ tới một `<form hidden asp-antiforgery="true" data-confirm="…">` đặt ngoài template.
- **`<partial model=…>` có object initializer chứa chuỗi** → viết `model="@(new X { A = "…" })"` hoặc dựng biến trong `@{ }` trước.
- **Controller partial** (`StaffController.*.cs`, `PlanningController.*.cs`): helper riêng đặt trong lớp static của khu vực
  (`StaffReports`, `StaffCalendar`, `PlanningManage`, `PlanningApproval`…) hoặc đặt tên có tiền tố khu vực để không trùng giữa các file partial.
- **Script trang chặn submit** (kiểm tra phía client) chỉ cần `preventDefault()` — `site.js` bỏ qua submit đã bị chặn.
- **Chế độ chỉ xem** (applyRO): chỉ ẩn nút/form ghi; vẫn hiện chữ trạng thái (vd. "Leader đã duyệt lúc …").
- **Cờ sửa** dùng `?edit=<ngày>`; `_DayBar` tự bỏ `edit`, `open`, `w`, `m` khi đổi ngày.
- **Không có file / ảnh**: web chỉ dùng văn bản (agents.md quy tắc 7) — không thêm upload.
- **Giới hạn độ dài** theo cột DB (tiêu đề 300, ghi chú/lý do 2000, mã NV 20) — kiểm tra phía server, `maxlength` phía client.
- **Mã tài khoản**: luôn qua `AccountRules.NormalizeCode` (chữ thường) + `AccountRules.ValidateCodeAsync`; đổi mật khẩu/vai trò/mã →
  `AccountRules.NewStamp()` (đăng xuất phiên cũ).

## Quy ước C#

- .NET 10, nullable bật, file-scoped namespace, primary constructor cho service.
- **Code bằng tiếng Anh** (quy tắc chủ dự án 2026-10-13): mọi định danh (lớp, hàm, thuộc tính, biến, giá trị enum), route/URL,
  khóa và giá trị query (`?tab=week`, `?mode=staff`, `?f=pending`…), tên field form (`status`, `province`, `picks`…), giá trị
  option (`yes`/`no`, `workday`/`holiday`, `rice`/`snack`…), định danh JS, id/class HTML-CSS, tên file. **Giao diện (chữ hiển thị)
  tiếng Việt**; comment được phép tiếng Việt (comment trích tên hàm / khóa của artifact như `nvMorning`, `'chua_lam'` giữ nguyên).
  Bảng tên tiếng Anh ↔ màn hình: `docs/man-hinh.md`; giá trị enum: `Data/Enums.cs`.
- **Giờ Việt Nam**: máy chủ đặt ở nước ngoài → mọi "hôm nay / bây giờ" nghiệp vụ dùng `VnTime` (`VnTime.Today`, `VnTime.Now`,
  `VnTime.NowHm`, `VnTime.UtcNow` để lưu mốc UTC, `VnTime.UtcAt` cho mốc giờ VN như hạn 14:00). Cấm `DateTime.Now`, `DateTime.Today`,
  `DateTimeOffset.Now` cho ngày/giờ nghiệp vụ. JS phía trình duyệt không tự tính ngày nghiệp vụ từ đồng hồ máy người dùng
  (ngày luôn lấy từ server qua `?d=` / dữ liệu trang); chỉ dùng `new Date()` cho việc thuần hiển thị (vd giờ của thông báo thử).
- Thông báo người dùng bằng tiếng Việt, giữ nguyên câu chữ của artifact khi có.
- Không thêm thư viện JS/CSS ngoài. Không dùng Bootstrap trong view (file lib/ của template còn để nguyên, không tham chiếu).
