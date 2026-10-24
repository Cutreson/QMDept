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
   **Mỗi khi code xong (và cuối mỗi phiên) tự động `git commit` và `git push origin master`** (xác nhận lại 2026-10-14).
5. **Quy tắc phải ghi vào file**: mọi quy tắc / yêu cầu làm việc chủ dự án đưa ra phải được ghi vào `agents.md`
   (hoặc file `docs/` tương ứng), không chỉ lưu trong bộ nhớ riêng của AI — phiên / AI khác chỉ đọc được file.
6. **Giao diện dùng tốt cả trên máy tính và điện thoại** (yêu cầu 2026-10-13): mọi màn hình mới/sửa phải kiểm tra ở
   ~375px (điện thoại), ~768px (máy tính bảng) và ≥1280px (máy tính) — quy ước ở `docs/kien-truc.md` mục "Giao diện responsive".
7. **Chỉ dùng văn bản** (yêu cầu 2026-10-13): web không có bất kỳ chỗ đính kèm file / hình ảnh nào (đã gỡ ảnh hiện trường
   của artifact, bảng `photos`). Không thêm `<input type=file>`, upload, lưu file người dùng.
8. **Cấp trên xem màn hình cấp dưới — chỉ xem** (yêu cầu 2026-10-13): combobox "Xem màn hình" trên header cho phép
   Quản trị → TP/Leader/NV, TP → Leader/NV, Leader → NV trong nhóm xem đúng các màn hình của người đó, **không thao tác gì**
   (server chặn mọi POST — `AppController`). Mỗi người chỉ thao tác trong giao diện của chính mình. Xem `docs/kien-truc.md`.
   Nút **"Màn hình của tôi"** và **"Lịch nhóm"** (TP / Quản trị: **"Lịch của phòng"**) nằm cạnh combobox (yêu cầu 2026-10-14).
   **Riêng Leader xem NV chỉ thấy lịch tháng của NV đó** (yêu cầu 2026-10-14; `AppController` + `ChromeBuilder`).
   Combobox chỉ gồm "Màn hình của tôi" + cấp dưới; **lịch tháng cả nhóm không nằm trong combobox** mà là nút "Lịch nhóm" cạnh
   combobox (`?as=all`, `ChromeModel.TeamHref`) — yêu cầu 2026-10-13.
9. **Quản trị (admin) cao hơn Trưởng phòng, bao trùm mọi chức năng TP** (yêu cầu 2026-10-13): Quản trị dùng toàn bộ giao diện
   và quyền thao tác của TP (tổng quan, phê duyệt, dự án, báo cáo, làm thêm, nhân sự, nhận thông báo của TP) + thêm tab "Tài khoản"
   (toàn quyền tài khoản: tạo, sửa vai trò/nhóm/cấp bậc, đặt mật khẩu, xóa, khôi phục) + xem được màn hình của mọi người (chỉ xem).
   Cơ chế: `ScopeResolver.UiRoleOf(Admin) = Manager`, `ViewScope.IsManager` gồm cả Admin, trang `[UiRole(Admin)]` chỉ mở ở màn hình
   của chính tài khoản Quản trị.
10. **Thông báo đẩy bằng SignalR** (yêu cầu 2026-10-13): mọi sự kiện cần người khác xử lý / có kết quả đều đẩy thông báo
    (`Services/Notifier.cs`); người dùng bật/tắt và chọn loại thông báo ở trang Cài đặt (`/Settings`). Thao tác mới có
    người liên quan ⇒ thêm lời gọi `Notify.…Async` sau `SaveChangesAsync`.
11. **Nhận diện**: favicon / icon ứng dụng (lịch có dấu tích, xanh → tím thương hiệu SORA) trong `wwwroot` — giữ đồng bộ
    `favicon.svg`, `favicon.ico`, `apple-touch-icon.png`, `icon-192/512.png`, `site.webmanifest`.
12. **Code bằng tiếng Anh (định danh, route, khóa query, tên field, tên file), UI tiếng Việt, comment được phép tiếng Việt**
    (yêu cầu 2026-10-13): lớp / hàm / thuộc tính / biến / giá trị enum, route & URL, khóa và giá trị query, tên field form và giá trị
    option, định danh JS, id/class HTML-CSS, tên file đều tiếng Anh. Chữ hiển thị cho người dùng vẫn tiếng Việt.
    Chi tiết: `docs/kien-truc.md` mục "Quy ước C#"; bảng route: `docs/man-hinh.md`.
13. **Giờ luôn theo giờ Việt Nam qua `VnTime` (server đặt ở nước ngoài)** (yêu cầu 2026-10-13): mọi "hôm nay / bây giờ" nghiệp vụ
    dùng `Services/VnTime.cs` (UTC+7); không dùng `DateTime.Now` / `DateTime.Today` / giờ máy chủ; JS không tự tính ngày nghiệp vụ
    từ đồng hồ trình duyệt.
14. **Giao diện gọn** (yêu cầu 2026-10-13): **không dùng placeholder** (gợi ý nhập) trong ô nhập liệu / textarea (kể cả đặt bằng JS);
    không thêm dòng giới thiệu dưới tiêu đề trang, chú thích hướng dẫn, chú giải "bấm vào…", giải thích ai duyệt…
    Chỉ giữ chữ mang dữ liệu (giờ gửi, người duyệt, trạng thái, danh sách trống…) và nhãn cần thiết.
15. **Tài khoản đã xóa không hiện trên giao diện nghiệp vụ** (yêu cầu 2026-10-13): mọi danh sách / combobox / nhóm chỉ dùng tài khoản
    đang hoạt động. Riêng trang Quản trị → Tài khoản là một danh sách duy nhất, mỗi tài khoản có trạng thái
    "Đang hoạt động" / "Đã xóa" (lọc `?st=all|active|deleted`), tài khoản đã xóa chỉ có nút "Khôi phục".
16. **Trao đổi với chủ dự án bằng tiếng Việt** (yêu cầu 2026-10-13): mọi câu trả lời, báo cáo, câu hỏi gửi chủ dự án viết bằng
    tiếng Việt (tài liệu docs/ và comment vẫn tiếng Việt như hiện tại; định danh trong code vẫn tiếng Anh theo quy tắc 12).
17. **Tự chủ hoàn toàn** (yêu cầu 2026-10-13): tuân thủ tài liệu trong repo, tự quyết định cho phù hợp, tự động code → test →
    hoàn thiện liên tục cho đến khi thấy ổn; không dừng lại hỏi chủ dự án (trừ việc không thể tự làm, ví dụ mở IP DB).
    Ghi các quyết định đã tự đưa ra vào `docs/handoff.md`.
18. **Không dùng model `fable`** (yêu cầu 2026-10-14): subagent / workflow chỉ dùng `sonnet` (hoặc model mặc định của phiên);
    `opus` hiện không truy cập được. Tiết kiệm hạn mức: E2E chạy tuần tự, ưu tiên ít agent.
19. **Tài khoản & mật khẩu** (yêu cầu 2026-10-14): **mã tài khoản nhân viên = mã nhân viên của phòng** (vd. `00171`, danh sách chuẩn ở `docs/nhan-su.md`);
    mã gợi ý theo tên (Nguyễn Văn Huề → `hue.nv`) chỉ dùng khi chưa có mã nhân viên. **Mật khẩu của 10 tài khoản nhân sự chuẩn: `1`** (chủ dự án yêu cầu,
    nên mật khẩu tối thiểu chỉ 1 ký tự — `AccountRules.MinPassword`); mật khẩu mặc định của tài khoản mới / sau khi đặt lại là `1` (đổi 2026-10-20; form tạo tài khoản có ô Mật khẩu điền sẵn `1`).
    Người dùng **tự đổi được mật khẩu** của mình (trang Cài đặt). Tài khoản quản trị: `admin` / `Admin@123`.
20. **Publish web do chủ dự án làm** (yêu cầu 2026-10-14): AI không tự publish / deploy. Khi cần đưa bản mới lên web
    (qlcl.runasp.net), nhắc chủ dự án tự chạy Publish trong Visual Studio. Quy tắc 18 (không dùng `fable`) và 4 (commit + push thẳng `master`)
    được chủ dự án xác nhận lại ở phiên này.
21. **Thanh DEV chọn giao diện (chỉ chạy thử trên máy)** (yêu cầu 2026-10-14): khi `ASPNETCORE_ENVIRONMENT=Development` và `DevSwitch:Enabled=true`
    (`appsettings.Development.json`) app bỏ màn hình đăng nhập (vào luôn bằng TP) và hiện thanh vàng chọn tài khoản Nhân viên / Leader / Trưởng phòng /
    Quản trị (`Services/DevSwitch.cs`, `Controllers/DevController.cs`, đầu `_Layout.cshtml`). Production không bao giờ bật (`/Dev/*` trả 404) — **không
    được bật cờ này trên web thật**.

22. **Tự chạy lại web, không để chủ dự án phải nhắc** (yêu cầu 2026-10-15, bổ sung 2026-10-16): xử lý xong MỖI yêu cầu → `bash tools/web.sh restart`
    (dừng → build → chạy lại `http://localhost:5125`) rồi mở lại trình duyệt (khung Browser). Web lỗi / chết / trả 500 cũng tự chạy lại ngay
    (`bash tools/web.sh start`), rồi báo ngắn gọn. Trình canh `tools/web-watch.sh` chạy nền (20 giây/lần, lỗi 2 lần liên tiếp thì tự chạy lại; tự tạm nghỉ khi
    `tools/.web-lock` có mặt lúc đang build): đầu mỗi phiên làm việc kiểm tra `bash tools/web.sh health`, nếu chưa có trình canh thì chạy
    `nohup bash tools/web-watch.sh &`. Sau khi xóa dữ liệu thử bằng SQL phải chạy lại web (bộ nhớ đệm ~60 giây).

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

Script kiểm thử / bảo trì ở `tools/` (chạy ở thư mục gốc repo; mật khẩu tài khoản test truyền qua biến môi trường `QM_PW_USER`,
`QM_PW_ADMIN` — không ghi vào file):

```bash
bash tools/smoke.sh                            # đăng nhập từng vai trò, GET mọi trang, báo lỗi 500 / exception
node tools/perf.mjs                            # đo thời gian tải trang
bash tools/web.sh restart                      # dừng → build → chạy lại web chạy thử (health / start / stop) — xem quy tắc 22
dotnet run tools/dbquery.cs <file.sql>         # chạy SQL (mỗi câu một dòng) trên DB thật
dotnet run tools/dblatency.cs                  # đo độ trễ DB / kiểm tra DB có kết nối được không
# tools/e2e-lib.mjs: thư viện Node cho kịch bản E2E (Client.login/get/post, forms(), toast(), sql()) — xem chú thích đầu file
```

**Máy dev Windows (2026-10-14)**: SDK .NET 10 trên máy này có thể **treo** khi chạy `dotnet run` / `dotnet ef` (nó kiểm tra / cài workload
MAUI và chờ quyền admin; log không in gì). Đặt `DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1`
trước mọi lệnh `dotnet`, và chạy app bằng DLL đã build, không qua SDK:
`cd QMDept && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5125 dotnet bin/Debug/net10.0/QMDept.dll`
(app tự `Migrate()` khi khởi động nên không cần `dotnet ef database update`). Muốn build lại phải dừng app trước (DLL bị khóa).

`tools/cleanup-test-data.sql` xóa toàn bộ dữ liệu test (giữ 5 tài khoản chính) — đọc chú thích đầu file trước khi chạy.
**Không chạy nhiều tiến trình test song song tới DB** (2026-10-13 nhà cung cấp đã chặn IP máy dev sau đợt test 4 agent song song).

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
| `docs/nhan-su.md` | Danh sách nhân sự chuẩn (mã nhân viên, vai trò, nhóm, cấp bậc) đã cập nhật lên DB thật |
| `docs/artifact/qlcl-artifact.html` | Bản sao mã nguồn artifact gốc (đặc tả chuẩn) |

## 5. Lưu ý khi sửa code

- Luật nghiệp vụ có bản C# (`Services/Rules.cs`, `Services/Dept.cs`) và bản JS xem trước trên form
  (`wwwroot/js/*.js`) — sửa luật phải sửa cả hai.
- Form POST có `action` viết tay phải có `asp-antiforgery="true"` (xem `docs/kien-truc.md`).
- Không tin client: mọi POST tự kiểm quyền (`RequireSelfStaff/RequireLead/RequireManager/RequireInScopeAsync`); ngoài ra
  `AppController` tự chặn mọi POST khi đang xem màn hình cấp dưới (`Scope.IsViewingOther`).
- Mỗi controller khai báo giao diện bằng `[UiRole(...)]` (một vai trò), không khai báo = dùng chung NV/Leader/TP,
  `[AnyUiRole]` = mọi vai trò kể cả Quản trị; muốn chia theo action thì override `UiRoles(ctx)` (xem `PlanningController.cs`).
- `ScopeResolver` lấy ngày đang chọn (`Scope.Date`) từ query `?d=`; với POST không có `?d=` thì lấy field form `d`.
  `@Ui.ReturnField(Context.Request)` tự chèn field ẩn `d` → mọi form POST thao tác theo ngày đang xem phải có
  `@Ui.ReturnField(...)` (hoặc ghi `?d=yyyy-MM-dd` trong `action`), nếu không `Scope.Date` sẽ là hôm nay.
  Không đặt field nào khác tên `d` trong form POST.
- Thực thể trong `Dept` là chỉ đọc (AsNoTracking); ghi qua `AppDbContext`.
- Đổi entity ⇒ tạo migration mới; không sửa migration đã áp lên DB thật.

23. **Định dạng ngày & cách gọi người** (yêu cầu 2026-10-16): mọi ngày hiển thị cho người dùng đều **dd/MM/yyyy** (`Fmt.Dmy`; `Fmt.Dm` / `Fmt.Short` / `Fmt.DayLabel` cũng ra đủ năm;
    ô chọn ngày dùng `Ui.DateInput` — ô chữ dd/MM/yyyy + nút lịch, **không dùng `<input type=date>`** vì trình duyệt tự đổi theo ngôn ngữ máy). Server đọc ngày nhập bằng
    `Fmt.ParseInput` (dd/MM/yyyy hoặc yyyy-MM-dd). Khóa ISO `yyyy-MM-dd` chỉ dùng trong URL / field ẩn. Danh sách gồm cả Leader nhóm và NV gọi là **"nhân sự"**
    (không gọi "nhân viên", vì Leader không phải nhân viên) và Leader nhóm hiện pill **"Leader"** thay cho cấp bậc Tech (`Ui.LevelPill`).
24. **Leader có nút "Kế hoạch"** (yêu cầu 2026-10-16, sửa lại cùng ngày): một nút "Kế hoạch" cạnh "Màn hình của tôi" (thay "Lịch của tôi" + "Lịch nhóm") → `/Staff/Month?as=mine` với thanh chuyển
    "Lịch của tôi | Lịch nhóm"; **Trạng thái làm việc** là tab của "Màn hình của tôi" nằm giữa Tổng quan và Kế hoạch & duyệt (`/Staff/Morning?as=mine`, `ChromeBuilder.TabDefs[Leader]` id `status`) (| Lịch nhóm" và 2 tab: **Kế hoạch tháng**, **Trạng thái làm việc** (= form báo cáo đầu ngày như nhân viên: nội dung công việc — chọn dự án + nội dung; Leader không có nhân sự hỗ trợ / ghi chú — rồi trạng thái làm việc & báo cơm; **không có báo cáo cuối ngày**).
    `ViewScope.OwnCalendar` = chế độ cá nhân của Leader, cookie `qm_view=mine`, chỉ ở đường dẫn `/Staff/*`; sang trang Leader tự về `me`; chỉ `SubmitPlan` cho phép Leader (`RequireSelfStaff(leaderOk: true)`).
    **Người duyệt trạng thái làm việc của Leader là Trưởng phòng** (Quản trị cũng được): `Manager/Approvals?mode=leader` ("Của Leader"), quyết định ghi vào `DayPlan.Leader*`; phần duyệt của Leader (`Dept.IsTeamStaff`) chỉ tính nhân viên.
25. **Leader: bỏ hàng nút trên thanh trên, "Kế hoạch tháng" thành tab con của "Kế hoạch & duyệt"** (yêu cầu 2026-10-17; thay phần nút "Kế hoạch" của quy tắc 24): Leader không còn hàng
    "Màn hình của tôi | Kế hoạch" (`ChromeModel.ViewInPlan` ẩn cả form `viewas`); tab con của Kế hoạch & duyệt: **Kế hoạch**, **Kế hoạch tháng** (`/Staff/Month?as=mine`, thanh "Lịch của tôi | Lịch nhóm" +
    ô "Xem màn hình" nhân sự; đã có "Giao thêm việc" nên **bỏ tab Lịch tuần + Phân công** — trang `/Planning/Week`, `/Planning/Assign` vẫn còn nhưng không có lối vào), Báo cáo, Đề xuất lịch, Dự án & đầu mục, Leader dự án, Nhân sự.
    Tab trái "Kế hoạch & duyệt" sáng cả khi ở Kế hoạch tháng (`ChromeBuilder.onPlanMonth`); số chờ duyệt ở `_PlanningTabs` đếm theo tài khoản Leader (`Dept.CarriesFromTeam`).
26. **Kết quả làm thêm (nghiệm thu)** (yêu cầu 2026-10-19): đăng ký làm thêm được Leader (NV) / TP (Leader) duyệt → người làm thêm **cập nhật công việc đã triển khai + giờ thực tế** (`POST /Overtime/ReportDone`,
    chỉ từ ngày làm thêm trở đi) → người duyệt **duyệt kết quả** (lưu trữ, giờ được tính) hoặc **từ chối** (bắt buộc lý do; giờ làm thêm **không được tính**, phải cập nhật lại) —
    `DecideDone` / `UndoDone`, enum `OtCompletion`, cột `overtimes.Completion/Actual*/Done*`, partial `Views/Overtime/_OtDone.cshtml`. Sửa / hủy đăng ký khi kết quả đã duyệt bị chặn;
    sửa đăng ký hoặc hoàn tác duyệt đăng ký thì kết quả làm lại. Tab Duyệt + badge đếm cả kết quả chờ duyệt (`Overtime.NeedsDecision`); Tổng hợp có "Giờ làm thêm được tính" (chỉ kết quả đã duyệt).
27. **Leader: "Kế hoạch đề xuất"** (yêu cầu 2026-10-19): tab con đầu của Kế hoạch & duyệt (`/Planning/Proposals`, thay "Kế hoạch" + "Đề xuất lịch"; `/Planning/Approvals` chỉ còn redirect) gộp
    kế hoạch đầu ngày của NV (`_PlanCard`) + phát sinh chờ duyệt + đề xuất lịch + **đăng ký làm thêm kèm kết quả** (`Views/Shared/_OtCard`), chia 2 mục riêng **Chờ duyệt / Đã duyệt** (`?f=pending|approved`, `PlanningInboxVm`).
    Leader không còn tab "Duyệt" ở trang Làm thêm (`/Overtime?tab=approve` redirect; chỉ TP còn "Duyệt Leader"). Số chờ duyệt: `Dept.LeaderInbox` (tab con) + badge tab "Kế hoạch & duyệt" (`ChromeBuilder.Badge`).
    Thứ tự tab con Leader: Kế hoạch đề xuất, Kế hoạch tháng, Báo cáo, Dự án & đầu mục, Leader dự án, Nhân sự.
28. **Nhân sự: sơ đồ quản lý hình tháp** (yêu cầu 2026-10-19): `/Planning/People` (Leader và TP) vẽ sơ đồ Trưởng phòng → Leader → nhân viên từng nhóm (+ cột "Chưa xếp nhóm"), thẻ `Views/Planning/_OrgCard.cshtml`,
    CSS `.org*`, trang này bỏ khỏi `gridPages`. Leader xem cả sơ đồ phòng (chỉ thao tác Báo nghỉ / Bỏ nghỉ với nhân viên nhóm mình). **TP / Quản trị sửa sơ đồ**: kéo thả thẻ nhân viên sang cột Leader khác
    (`wwwroot/js/site.js` "sơ đồ nhân sự") hoặc dùng ô chọn nhóm trên thẻ → `POST /Planning/MoveToTeam` (đổi `Person.LeaderId`; nhân viên tự vào nhóm mới: Leader mới duyệt / thấy, Leader cũ không còn) +
    thông báo `NotifyType.TeamChanged` (NV, Leader cũ, Leader mới). Tạo / sửa / xóa tài khoản giữ nguyên (nút "Sửa" trên thẻ + form "Tạo tài khoản").
29. **Kế hoạch đề xuất = 2 mục chính × 2 mục con** (yêu cầu 2026-10-19, sửa quy tắc 27): vị trí tab con **giữa "Kế hoạch tháng" và "Báo cáo"**; hai mục chính `?k=work` **Đề xuất công việc** (kế hoạch đầu ngày + phát sinh + đề xuất lịch)
    và `?k=ot` **Đề xuất làm thêm tại công ty** (đăng ký làm thêm + kết quả); mỗi mục có 2 mục con **Chờ duyệt / Đã duyệt** (`?f=pending|approved`). `PlanningInboxVm.Kind/WorkPending/OtPending`; link cũ
    `/Planning/Approvals` → `?f=pending`, `/Overtime?tab=approve` (Leader) → `?k=ot`.
30. **Nhân sự: bỏ Báo nghỉ trên sơ đồ, thêm Xóa; tên vai trò** (yêu cầu 2026-10-19): thẻ sơ đồ (`_OrgCard`) không còn nút Báo nghỉ / Bỏ nghỉ (chỉ còn dòng "Nghỉ …" nếu có); TP thấy **Sửa** + **Xóa**
    (Xóa gửi form ẩn `epDel{id}` có hỏi xác nhận, không xóa chính mình). Vai trò hiển thị chỉ là **Nhân viên / Leader / Trưởng phòng** (`Labels.Title`; bỏ chữ "Setup"); Quản trị là "Quản trị hệ thống".
31. **Leader chỉ thấy dự án được giao; thống kê Leader phụ trách dự án** (yêu cầu 2026-10-20): `Dept.ActivePj()/AllPj()/TableProjects()` lọc theo tài khoản — tài khoản Leader chỉ thấy dự án có `OwnerLeaderId` = mình
    (danh sách dự án, ô chọn dự án, bảng tình hình, tab "Dự án & đầu mục", báo cáo TP…); TP / Quản trị / nhân viên không đổi. Trang TP `/Planning/ProjectOwners`: thống kê tổng quan (tổng dự án, đã có / chưa có Leader, từng Leader đang quản lý bao nhiêu dự án
    + danh sách) và 2 mục **Đã có Leader / Chưa có Leader** (`?f=has|none`, bỏ "Tất cả").
32. **TP: Tổng quan gồm 3 mục, bỏ hàng nút trên thanh trên** (yêu cầu 2026-10-20): thanh trên của TP / Quản trị không còn ô "Xem màn hình" + nút "Màn hình của tôi" / "Lịch của phòng" (`ChromeModel.ViewInPlan` cho mọi tài khoản không phải NV).
    Tab **Tổng quan** (`Views/Shared/_OverviewTabs.cshtml`): **Dự án** (`/Manager/Overview`: KPI, tiến độ, biểu đồ cuộn ngang theo mã dự án, vấn đề), **Nhân sự** (`?s=people`: số đang làm / công tác / nghỉ / chưa báo, từng người làm ở đâu + việc trong ngày,
    nhóm theo Leader), **Kế hoạch tháng** (`/Staff/Month?as=all` — lịch của phòng gồm Leader + nhân viên, bố trí như Kế hoạch tháng của Leader, có "Giao thêm việc"); ô "Xem màn hình" (xem Leader / NV, chỉ xem) nằm ở đầu Tổng quan.
33. **Xem màn hình người khác = chỉ xem KẾ HOẠCH THÁNG của họ** (yêu cầu 2026-10-20, thay phần "xem đủ màn hình" của quy tắc 8): ô "Xem màn hình" chỉ có ở mục **Kế hoạch tháng** (TP / Quản trị: Tổng quan → Kế hoạch tháng; Leader: Kế hoạch & duyệt → Kế hoạch tháng),
    chọn Leader / nhân viên nào thì mở `/Staff/Month?as=<id>` (chỉ xem; `ScopeResolver.viewMonth` cho người được xem dùng giao diện kiểu nhân viên ở trang này). Mọi trang GET khác khi đang xem người khác tự chuyển về
    `/Staff/Month?as=<id>` (`AppController`, áp dụng cho TP / Quản trị / Leader). Thanh tab bên trái vẫn là của tài khoản đang đăng nhập (`ChromeBuilder.away`), banner "Đang xem…" có nút về màn hình của mình.
34. **Tổng quan TP (yêu cầu 2026-10-20, bổ sung quy tắc 32)**: **Dự án** = tổng hợp nhanh: KPI, thanh tỉ lệ trạng thái, "Cần chú ý" (Chậm / Nguy cơ), bảng "Theo Leader", danh sách gọn lọc `?pst=risk|late|ok|done` + `?pl=<leaderId|none>`
    (rủi ro lên đầu). **Nhân sự** = bảng + thống kê (cả TP): công tác / công ty, nghỉ, báo cơm / cắt cơm, ai chưa báo / chờ duyệt / bị từ chối để nhắc — **chỉ hiện địa điểm, nghỉ, báo cơm của kế hoạch đã được Leader duyệt**
    (Leader: TP duyệt; Leader ghi nghỉ hiện riêng). **Kế hoạch tháng** có 2 mục **Lịch của tôi** (`?as=mine`, TP thêm / xóa việc cá nhân như Leader) và **Lịch phòng** (`?as=all`).
    **Không giao việc vượt cấp**: ở Lịch phòng TP / Quản trị chỉ giao cho Leader (`AssignTask` chặn nhân viên); Leader giao tiếp cho nhân viên nhóm mình.
35. **Tổng quan TP → Nhân sự chỉ là MỘT bảng** (yêu cầu 2026-10-20, thay phần thống kê của quy tắc 34): cột Họ tên, Nhóm, **Trạng thái làm việc** (Làm việc tại công ty / Công tác trong ngày / Công tác dài ngày / Nghỉ phép / Nghỉ không lương
    kèm thời gian nghỉ), **Địa chỉ công tác** (tỉnh / thành + khách hàng), **Nội dung dự án** (dự án + việc + giờ của kế hoạch đã duyệt), Báo cơm, Thông tin (Đã duyệt / Chờ duyệt / Bị từ chối / Chưa báo). Không còn ô số và khung "Cần nhắc nhở".
36. **Đề xuất công việc / Xin nghỉ gộp một nút; Báo cáo chia Chờ duyệt / Đã duyệt** (yêu cầu 2026-10-20): ở Kế hoạch tháng (Lịch của tôi) nhân viên có nút **"＋ Đề xuất công việc / xin nghỉ"**
    (Leader: **"＋ Cập nhật kế hoạch cá nhân / xin nghỉ"**) mở một bảng có hai thẻ **Công việc | Xin nghỉ** (`_SheetTabs`, bấm thẻ kia thì bảng đổi template). **Xin nghỉ bắt buộc nêu lý do**; nghỉ phép theo buổi
    (sáng / chiều / cả ngày), nghỉ không lương theo giờ (≥ 1 giờ). Quy trình: NV → Leader nhóm duyệt (`/Planning/DecideLeave`, mục "Xin nghỉ" ở Kế hoạch đề xuất); Leader → Trưởng phòng duyệt (`/Manager/DecideLeave`, Phê duyệt → Của Leader);
    từ chối bắt buộc ghi lý do; duyệt thì ghi `Leave` vào lịch (bảng `leave_requests` → `leaves`) và hiện "Nghỉ …" trên lịch. **Nghỉ một phần ngày (nửa ngày / vài giờ) KHÔNG coi là nghỉ cả ngày**: `Dept.OnLeave` chỉ trả nghỉ cả ngày
    (`Dept.IsFullDayLeave`), `Dept.PartLeave` trả nghỉ một phần — người đó vẫn có việc / vẫn báo cáo, lịch hiện ghi chú nghỉ. Thông báo: `NotifyType.LeaveRequested/LeaveDecided`.
    **Planning/Reports** (Leader): bỏ mục đỏ "Đề xuất chuyển kế hoạch" riêng (đề xuất chuyển nằm ngay trong thẻ báo cáo của nhân sự, có nút Duyệt / Sửa); báo cáo chia 2 mục **Chờ duyệt | Đã duyệt** (`?f=pending|approved`).
37. **Sheet "Đề xuất công việc / xin nghỉ" là MỘT form; Tổng quan của Leader có 3 mục** (yêu cầu 2026-10-20, sửa quy tắc 36 phần "hai thẻ" và quy tắc 25): nút mở một sheet duy nhất; ô **"Loại đề xuất"** (NV: Đề xuất công việc | Xin nghỉ;
    Leader: Cập nhật kế hoạch cá nhân | Xin nghỉ; TP / Quản trị không xin nghỉ). Chọn **Xin nghỉ** → hiện loại nghỉ + buổi / giờ + lý do (bắt buộc); **chỉ khi nghỉ nửa ngày hoặc vài giờ** (< 8 giờ) mới hiện thêm phần nội dung công việc làm trong
    thời gian còn lại (không bắt buộc; không được trùng khung giờ nghỉ), nghỉ cả ngày thì ẩn. Một lần gửi = đề xuất nghỉ + (nếu có) đề xuất công việc / việc cá nhân: `POST /Staff/ProposeCalendar` (NV) và `/Staff/AddOwnTask` (Leader) với field `kind=work|leave`;
    JS `site.js` (syncPropForm: `[data-kind]`, `[data-leave-fields]`, `[data-work-block]`), partial `_LeaveFields`, `_ProposalSheet`. Nhãn là "Nội dung công việc" (bỏ chữ "phát sinh").
    **Leader: tab "Tổng quan" có 3 mục Dự án / Nhân sự / Kế hoạch tháng** như TP (`_OverviewTabs`): Dự án = `/Leader/Overview`, Nhân sự = `?s=people` (bảng nhóm mình, `_PeopleTable` dùng chung với TP),
    Kế hoạch tháng = `/Staff/Month?as=mine` (có "Lịch của tôi | Lịch nhóm" + ô "Xem màn hình" nhân sự). **Kế hoạch & duyệt của Leader không còn tab con "Kế hoạch tháng"**. Kiểm thử toàn phòng: `bash tools/leave-test.sh`.
38. **Báo cáo cuối ngày: chờ duyệt → khóa; duyệt → sao chép gửi Zalo; từ chối → mở lại** (yêu cầu 2026-10-20): nhân viên gửi báo cáo ⇒ trang `/Staff/Evening` hiện khối **"Báo cáo tổng hợp"** theo dự án (`Services/EodText.cs`) với nhãn **Chờ duyệt**
    và **khóa chỉnh sửa** (Cập nhật / tích hỗ trợ / phát sinh / gửi lại bị chặn cả ở server — `StaffController.EnsureReportOpenAsync`); có nút **"Rút lại để sửa"** (`POST /Staff/WithdrawReport`, chỉ khi chưa duyệt).
    Leader **duyệt** ⇒ nhãn **Đã duyệt** + nút **"Sao chép dự án này"** (từng dự án, để dán vào nhóm Zalo dự án đó) và **"Sao chép toàn bộ báo cáo"** (JS `[data-copy="id1 id2…"]` trong `site.js`; văn bản chọn / copy tay được,
    CSS `.rpt-text`). Leader **từ chối** ⇒ báo cáo mở lại để nhân viên cập nhật theo yêu cầu (hiện ghi chú của Leader), gửi lại thì thành Chờ duyệt. Leader hoàn tác duyệt ⇒ về Chờ duyệt. Kiểm thử toàn phòng: `bash tools/report-test.sh`.
    Nút gửi của sheet Đề xuất công việc / xin nghỉ luôn ghi **"Gửi đề xuất"** (không ghi "xin nghỉ" vì có thể gửi nghỉ, công việc hoặc cả hai).
39. **Leader: "Xin nghỉ" là tab con riêng của Kế hoạch & duyệt** (yêu cầu 2026-10-20, tách khỏi "Kế hoạch đề xuất" của quy tắc 27 / 29): `/Planning/Leave` ngay sau "Kế hoạch đề xuất", hiện số chờ duyệt, 2 mục **Chờ duyệt / Đã duyệt** (`?f=pending|approved`,
    `PlanningLeaveVm`); duyệt → ghi Nghỉ vào lịch, từ chối bắt buộc lý do (`POST /Planning/DecideLeave`). "Kế hoạch đề xuất" không còn chứa xin nghỉ (`Dept.LeaderInbox` không đếm xin nghỉ; badge tab "Kế hoạch & duyệt" vẫn cộng cả hai).
    Thông báo xin nghỉ của nhân viên dẫn tới `/Planning/Leave`.
40. **Leader / TP xóa hoặc chuyển lịch công việc** (yêu cầu 2026-10-20): dưới mỗi dòng việc ở "Lịch của tôi" và "Lịch nhóm / Lịch phòng" có nút **Chuyển lịch** (ngày từ hôm nay trở đi + người nhận) và **Xóa**
    (`Views/Shared/_TaskActions.cshtml`, `POST /Staff/DeleteTask` / `/Staff/MoveTask`, `StaffCalendar.OwnsTask`). Quyền: việc của chính mình; Leader thêm việc của nhân viên nhóm mình; TP / Quản trị thêm việc của Leader (không vượt cấp xuống nhân viên);
    người nhận chuyển lịch cũng chỉ trong quyền đó. Khóa khi việc đã Hoàn thành / Chậm hoặc nhân sự đã gửi báo cáo cuối ngày; việc đã được chuyển tiếp đi nơi khác thì không chuyển lịch được (việc chuyển tiếp thì chuyển được, ngày chuyển sang trên việc gốc đổi theo). Người nhận nghỉ cả ngày thì chặn; người nhận khác mình được thông báo.
41. **TP / Quản trị: tab "Trạng thái làm việc" chỉ có phần của mình; phê duyệt làm thêm nằm ở Phê duyệt** (yêu cầu 2026-10-21): tab thứ 2 của TP / Quản trị (sau Tổng quan) = `/Staff/Morning?as=mine` (như Trạng thái làm việc của Leader;
    **TP không có người duyệt nên trạng thái tự được duyệt**, vẫn sửa và gửi lại được — `StaffController.SelfApproves`, không gửi thông báo duyệt) với 2 mục con **Trạng thái làm việc | ĐK làm thêm tại CTy** (đăng ký làm thêm của chính TP,
    `_MgrStatusTabs`, `/Overtime?tab=mine`). Tab **Phê duyệt** có thêm mục lớn thứ 4 **Làm thêm tại CTy** (`_MgrApprovalTabs`, dùng chung với `/Manager/Approvals`) gồm **Duyệt đăng ký làm thêm của Leader** và **Tổng hợp suất ăn**
    (`/Overtime?tab=approve|summary`, tab Phê duyệt sáng, badge Phê duyệt cộng cả số làm thêm chờ duyệt). Bỏ tab riêng "ĐK làm thêm tại CTy" của TP. Bảng Nhân sự ở Tổng quan hiện hàng của TP nếu TP đã khai trạng thái.
42. **Leader / TP: chỉ Sửa + Xóa công việc (chuyển lịch nằm trong Sửa); tổng hợp ngày ở bảng Nhân sự** (yêu cầu 2026-10-21, sửa quy tắc 40): mỗi việc trong quyền của mình (mình; Leader + NV nhóm; TP + Leader) chỉ có hai nút **Sửa** và **Xóa**
    (lịch tháng: `_TaskActions`; trang Trạng thái làm việc: hai nút nằm dưới từng dòng việc, không nằm trong dòng — tránh chồng chữ). Sheet **Sửa** (`Views/Shared/_TaskEdit.cshtml`, `POST /Staff/EditTask`) gồm dự án, nội dung, giờ, **ngày** và **"Lịch của"** (người nhận:
    Leader → mình + NV nhóm; TP / Quản trị → mình + Leader): đổi ngày / người = chuyển lịch (từ hôm nay trở đi; chặn người nhận nghỉ cả ngày; sửa + chuyển chạy trong một transaction, lỗi không ghi dở; người nhận được thông báo). Không còn `POST /Staff/MoveTask`.
    Khóa khi việc đã Hoàn thành / Chậm hoặc đã có báo cáo cuối ngày (`StaffCalendar.CanManage`). Bảng Nhân sự (`_PeopleTable`, Tổng quan TP và Leader) có 5 ô tổng hợp ngày: **Tổng nhân sự** (số dòng của bảng), **Đã duyệt / xác nhận**
    (kế hoạch đã duyệt; hàng của TP ghi **"Đã xác nhận"** vì TP tự xác nhận), **Nhân sự làm việc**, **Nhân sự nghỉ**, **Suất cơm** — cùng tiêu chí với bảng (chỉ kế hoạch đã duyệt).
43. **Đăng ký làm thêm tại công ty: nhân viên kết thúc SAU 20:00 cần thêm Trưởng phòng duyệt** (yêu cầu 2026-10-21): (1) NV làm thêm **kết thúc ≤ 20:00** → Leader duyệt → **Đã duyệt**, thông tin cập nhật lên Trưởng phòng (TP chỉ xem, không có nút duyệt);
    (2) NV làm thêm **kết thúc > 20:00** (`Overtime.NeedsManager`, hằng `Overtime.LateFrom`) → Leader duyệt → trạng thái **`OtStatus.PendingManager`** ("Chờ Trưởng phòng") → Trưởng phòng duyệt → **Đã duyệt** (Leader từ chối thì kết thúc, TP không thấy).
    Cột mới `overtimes.LeaderBy / LeaderAt` (migration `20261021100000_OvertimeManagerStep`). Leader đăng ký → TP duyệt (như cũ); TP tự ghi nhận. Người duyệt: `OvertimeController.Decide` (bước TP: `LoadForDecisionAsync`, `tpStep`); **Hoàn tác**: TP hoàn tác quyết định của TP → về chờ TP;
    Leader chỉ hoàn tác khi đang chờ TP hoặc do mình quyết định (không hoàn tác quyết định của TP). NV sửa đăng ký → duyệt lại từ Leader (xóa LeaderBy/At). Duyệt **kết quả** làm thêm: Leader (NV) / TP (Leader) — TP không duyệt kết quả của NV (`forResult`).
    Số chờ duyệt: Leader `Overtime.NeedsDecision` (không đếm đăng ký chờ TP), TP `Overtime.NeedsTpDecision`. Danh sách TP (`Dept.OtPending`) gồm đăng ký của Leader + đăng ký NV mà Leader đã duyệt. Thông báo: `Notify.OvertimeToManagerAsync` (Leader duyệt xong → TP); hủy đăng ký đã qua Leader cũng báo TP.
    Thẻ `Views/Shared/_OtCard.cshtml` (Leader: Kế hoạch đề xuất → Làm thêm; TP: Phê duyệt → Làm thêm tại CTy) hiện dòng "Cấp duyệt".
44. **Form dự án + tìm dự án** (yêu cầu 2026-10-21): form tạo / sửa dự án (`/Planning/Projects`, mọi nút "＋ Dự án mới" dẫn tới đây) gọi **"Khách hàng"** (không còn chữ "Nhà máy"; cột `Project.Factory`), ô **Khách hàng** và **Địa điểm** là ô nhập có gợi ý (`<datalist>` lấy từ các dự án đã có —
    nhập mới hoặc chọn sẵn); **bỏ** ô "Địa chỉ khách hàng" (cột `projects.CustomerAddress` giữ nguyên dữ liệu nhưng không còn hiển thị / sửa; `SaveProject` không ghi đè khi không gửi) và ô "Nhu cầu nhân sự/ngày" (sửa dự án giữ nguyên `Need`, dự án mới mặc định 2).
    Trang **Leader phụ trách dự án** (`/Planning/ProjectOwners`, `Views/Planning/_OwnerCard.cshtml`) có **ô tìm dự án**: gõ chữ (không cần dấu; khớp tên, mã, khách hàng, địa điểm, Leader) → hiện ngay các dự án khớp (cả dự án đã hoàn thành) kèm Leader phụ trách + ô đổi Leader;
    không khớp = dự án chưa có trên hệ thống (`PlanningManage.SearchKey / Norm`, script cuối view). **Lưu ý khi kiểm thử bằng curl trên Windows**: `curl --data-urlencode` làm hỏng chữ Việt có dấu trong form (lưu thành `?`) — đừng gửi tiếng Việt thật lên DB thật bằng curl; dùng ASCII hoặc SQL qua `tools/dbquery.cs` (UTF-8 đúng).
45. **Cách lọc / chọn dự án (và người, khách hàng, địa điểm) — DUY NHẤT một kiểu** (yêu cầu 2026-10-21, "từ lần sau chỉ làm phương pháp lọc như thế"): dùng **ô chọn có tìm kiếm của trang "Giao thêm việc"** = `Ui.Select(...)` (`<select class="in">`) — `site.js` `enhanceSelect` biến thành một ô duy nhất:
    gõ chữ (không cần dấu) → danh sách thu gọn → bấm chọn / ↑ ↓ Enter; từ 7 mục trở lên tự có, ít hơn thì thêm `data-combo`. Bộ lọc tự gửi / tự chuyển trang (`data-autosubmit`, `data-nav`) phải thêm `data-combo` (đã áp: Theo dõi của Leader — lọc dự án, "Xem màn hình" — lọc người).
    Chọn xong thì lọc / mở ngay (trang Leader phụ trách dự án: chọn dự án → hiện đúng dự án đó + Leader phụ trách, "— Tất cả dự án —" → danh sách theo mục). **Không dùng** ô tìm riêng (`<input type=search>`), `<datalist>` thuần, hay tự viết JS lọc kiểu khác.
    Ô vừa chọn có sẵn vừa nhập mới (Khách hàng, Địa điểm của dự án): `<input data-combo-free list="…">` + `<datalist>` — `enhanceFree` vẽ danh sách gợi ý cùng giao diện combo, cứ gõ là nhập mới.
46. **Dự án & đầu mục: Đầu mục lên trên, Thông tin dự án xuống dưới; mỗi phần có Sửa / Hủy sửa RIÊNG** (yêu cầu 2026-10-21, sửa bản trước cùng ngày): trang `/Planning/Projects` (Leader và TP / Quản trị) xếp **Đầu mục kế hoạch** ở trên (bảng: mục, thời gian, ô đổi trạng thái → tích Hoàn thành)
    rồi **Thông tin dự án** (hiển thị khóa). Mỗi phần có nút **Sửa** riêng, hai cờ độc lập: đầu mục `?ei=1`, thông tin dự án `?edit=1` (xử lý phía server, `Ui.Here` giữ cờ kia). Sửa đầu mục: ô **Bắt đầu / Kết thúc từng đầu mục** (nút "Lưu thời gian đầu mục" → `POST /Planning/SaveItems`,
    kiểm hết rồi mới ghi, lưu xong thoát chế độ sửa), nút ✕ xóa đầu mục, form **Thêm đầu mục** (thêm xong vẫn ở chế độ sửa), nút **Hủy sửa** (thoát không lưu). Sửa thông tin dự án: form (Lưu thông tin → thoát chế độ sửa / **Hủy sửa**). Dự án mới (`?pj=new`) hiện form ngay.
    **Bỏ** ô "Trạng thái" và "Đã bàn giao" khỏi form: dự án **Hoàn thành khi tích hết đầu mục** (`Dept.PjStatus`); `SaveProject` không gửi `statusOverride` / `closed` thì giữ nguyên giá trị cũ (dự án đã bàn giao như GC0125-02 giữ nguyên).
47. **Leader phụ trách nhân sự / báo cơm / làm thêm (cờ `Person.HrDuty`) có MỤC RIÊNG "Nhân sự phòng"** (yêu cầu 2026-10-21): TP / Quản trị tick **"Phụ trách nhân sự, báo cơm, làm thêm tại công ty (với Leader)"** ở sheet Sửa tài khoản (`/Planning/People`; `EditAccount`, cột `people.HrDuty`,
    migration `20261021120000_PersonHrDuty`; chỉ lưu cho vai trò Leader; thẻ sơ đồ hiện pill "Phụ trách nhân sự"). **Hiện được giao cho Leader Lê Thế Hùng (id 26).** Leader có cờ có thêm **tab riêng "Nhân sự phòng"** ở thanh trái (cuối thanh trái, sau "ĐK làm thêm tại CTy"; `ChromeBuilder`, `/Leader/Department`,
    `Views/Leader/Department.cshtml`, `_HrTabs`) gồm 2 mục: **Nhân sự phòng** (bảng nhân sự CẢ PHÒNG: Trưởng phòng + mọi Leader + nhân viên + chưa xếp nhóm, cùng bảng + 5 ô tổng hợp như TP) và **Báo cơm & làm thêm** (đăng ký làm thêm CẢ PHÒNG; đã BỎ nút Gửi HCNS ở quy tắc 49 —
    trang `/Overtime?tab=summary` của Leader có cờ cũng hiện cả phòng). **Tổng quan → Nhân sự của Leader vẫn là nhóm mình** như mọi Leader. Quyền duyệt khác không đổi (vẫn chỉ duyệt nhân viên nhóm mình). Tên người ngoài nhóm trong bảng không là liên kết xem lịch. Trang Nhân sự của TP giữ nguyên.
48. **TP: Tổng quan → Nhân sự có 2 mục con** (yêu cầu 2026-10-21): **Làm việc hành chính** (`?s=people`, bảng nhân sự cả phòng + 5 ô tổng hợp như quy tắc 35 / 42) và **Làm thêm tại công ty** (`?s=people&k=ot`: đăng ký làm thêm cả phòng trong ngày — số người làm thêm,
    suất cơm / ăn nhẹ / phụ cấp, chi phí dự kiến, danh sách kèm duyệt / kết quả / hạn đăng ký — ; dùng lại `Views/Overtime/_Summary.cshtml`, `ManagerController.Overview(s, k)`, `ManagerOverviewVm.PeopleKind / Ot`). Thanh ngày dùng chung cho cả hai mục.
49. **Bỏ "Gửi đăng ký suất ăn cho HCNS" + "Chi phí dự kiến"; hạn đăng ký làm thêm cài được** (yêu cầu 2026-10-21): tổng hợp làm thêm (`Views/Overtime/_Summary.cshtml`, dùng ở Tổng hợp suất ăn, TP → Nhân sự → Làm thêm tại công ty, Leader phụ trách nhân sự) **không còn** khối "Gửi đăng ký suất ăn cho Phòng HCNS"
    (đã xóa `SendMealOrder`; bảng `ot_sends` giữ nguyên dữ liệu cũ, không dùng) và khối "Chi phí dự kiến" (còn "Giờ làm thêm": giờ được tính + cảnh báo chờ duyệt / trễ hạn). **Hạn đăng ký làm thêm cho TOÀN BỘ nhân sự** (NV, Leader, TP) **mặc định 15:00** (trước đây 14:00):
    dự trù = trước hạn ngày làm việc liền trước, chính thức = trước hạn cùng ngày, sau đó "Trễ hạn" (`OtRules.RegType / DeadlineState`). **Trưởng phòng / Quản trị đặt hạn linh động (08:00–20:00)** ở khối **"Hạn đăng ký làm thêm"** cuối trang tổng hợp (`POST /Overtime/SetDeadline`,
    lưu bảng `app_settings` khóa `OtDeadline`, migration `20261021130000_AppSettings`, nạp lúc khởi động vào `OtRules.DeadlineHhmm`); đổi ngay cho mọi người, đăng ký đã tạo giữ nhãn cũ.
50. **Tổng quan của Leader: "Nhân sự nhóm" + "Nhân sự phòng"** (yêu cầu 2026-10-21, sửa quy tắc 47): với MỌI Leader tab **"Nhân sự" đổi tên thành "Nhân sự nhóm"** (bảng nhóm mình; TP vẫn "Nhân sự"). Leader có cờ `HrDuty` (hiện là Lê Thế Hùng) có thêm tab **"Nhân sự phòng"** ngay sau "Nhân sự nhóm"
    trong Tổng quan (`_OverviewTabs`, `/Leader/Department`; **bỏ khỏi thanh trái**, tab Tổng quan sáng khi ở trang này) với 2 mục con **Làm việc hành chính | Làm thêm tại công ty** (`_HrTabs`, giống TP → Nhân sự). Thứ tự tab: Dự án | Nhân sự nhóm | Nhân sự phòng | Kế hoạch tháng.
51. **Không dùng số thứ tự trước tiêu đề** (yêu cầu 2026-10-21): bỏ mọi số tròn (bước 1, 2, 3…) trước tiêu đề trang (`Ui.Head` không còn in số; tham số `n` giữ để khỏi sửa nơi gọi; CSS `.step-n` đã xóa) và số đầu tiêu đề khối ("1. Nội dung công việc", "2. Trạng thái & báo cơm" → bỏ số). Đừng thêm lại.
    Cột "#" đếm dòng trong bảng dữ liệu (bảng nhân sự, bảng đầu mục) vẫn giữ vì là số dòng, không phải bước.
52. **Đăng ký làm thêm: "Công việc khác" = dự án + tên công việc (như đề xuất phát sinh)** (yêu cầu 2026-10-21): ô "Nội dung khác (nếu có)" (textarea) đã thay bằng khối **Công việc khác**: chọn **Dự án** (ô chọn có tìm kiếm) + nhập **Tên công việc** + **＋ Thêm** (hoặc Enter) → thẻ có ✕ để bỏ;
    thêm được nhiều mục (≤ 20), gửi kèm khi **Gửi đăng ký** (field `otherTitle[]` / `otherProject[]`, `OvertimeController.Save`; lưu thành `OvertimeTask` với `WorkTaskId = null`, hiển thị như các việc làm tiếp ở `_OtDetail`; dự án đầu tiên làm `Overtime.ProjectId` nếu không tích việc nào).
    Phải có ít nhất một việc tích hoặc một công việc khác. Đăng ký cũ có nội dung chữ (`ExtraContent` / `Content`) hiện thành một thẻ khi sửa (field `extra`). `wwwroot/js/overtime.js` (`addOther`) + chip `.pick-chip` dùng chung `site.js`.
53. **Mọi ô chọn / lọc dự án gồm CẢ dự án đã hoàn thành (kể cả đã bàn giao `Closed`)** (yêu cầu 2026-10-21 — dự án xong vẫn phải triển khai khi khách hàng có sự cố): dùng `D.AllPj()` + `Ui.PjOptions(...)` (nhãn **"Mã · Tên"** để phân biệt dự án trùng tên), **không dùng `D.ActivePj()`** cho ô chọn / lọc.
    Đã áp: đăng ký làm thêm (Công việc khác), đề xuất phát sinh (báo cáo cuối ngày), thêm việc ngoài kế hoạch, giao thêm việc (Lịch nhóm / phòng), việc cá nhân, Sửa công việc, đề xuất công việc của nhân viên (dự án mình là Leader dự án), lọc dự án ở Theo dõi, Leader dự án (`/Planning/ProjectLeads`),
    `StaffCalendar.DefaultProject`. **Server không còn chặn dự án đã bàn giao** khi giao / đề xuất / thêm / chuyển việc, duyệt phát sinh, chọn Leader dự án (`WorkflowService`, `StaffReports.RequireActiveProjectAsync`, `TaskFieldsInput`, `StaffController.AssignTask`, `LeaderController`, `PlanningController.SetProjectLead`).
    `ActivePj()` chỉ dành cho thống kê "đang triển khai" (Tổng quan, báo cáo tiến độ). Leader vẫn chỉ thấy dự án được giao cho mình (quy tắc 31); nhân viên và TP thấy mọi dự án. Ô chọn dự án mới PHẢI dùng `D.AllPj()` — đừng thêm lại bộ lọc `!Closed`.
54. **Leader: ĐK làm thêm gộp vào tab "Trạng thái làm việc"; không có "Tổng hợp suất ăn"** (yêu cầu 2026-10-21, theo mẫu của TP — quy tắc 41): Leader không còn tab riêng "ĐK làm thêm tại CTy" ở thanh trái; tab **Trạng thái làm việc** có 2 mục con
    **Trạng thái làm việc | ĐK làm thêm tại CTy** (`Views/Shared/_MgrStatusTabs.cshtml`, dùng chung TP / Leader; tab sáng cả ở `/Overtime`, `ChromeBuilder.onOvertime`). **Leader không có mục Tổng hợp suất ăn** (đường cũ `/Overtime?tab=summary` chuyển về ĐK làm thêm; Leader có cờ HrDuty xem ở
    Tổng quan → Nhân sự phòng → Làm thêm tại công ty). Duyệt đăng ký làm thêm của nhân viên vẫn ở Kế hoạch & duyệt → Kế hoạch đề xuất. Nhân viên giữ tab ĐK làm thêm riêng; tổng hợp suất ăn của TP nằm ở Phê duyệt → Làm thêm tại CTy.
55. **Leader: trang Xin nghỉ có bộ lọc ngày** (yêu cầu 2026-10-21): `/Planning/Leave` có thanh ngày `_DayBar` như Kế hoạch đề xuất / Báo cáo; danh sách Chờ duyệt / Đã duyệt chỉ của ngày xin nghỉ đang chọn;
    đề xuất còn chờ ở ngày khác hiện thành liên kết "Còn chờ duyệt ở ngày khác" (`PlanningLeaveVm.OtherPending`); số chờ duyệt ở tab / badge vẫn đếm tất cả.
56. **Số đỏ trên thanh tab đếm MỌI ngày** (yêu cầu 2026-10-21): badge của "Kế hoạch & duyệt" (Leader), "Phê duyệt" (TP), "Báo cáo cuối ngày" (NV) không còn phụ thuộc ngày đang xem — `Dept.LeaderPendingAll / OtPendingAll`, `ChromeBuilder` nạp thêm ±30 ngày quanh hôm nay.
57. **Giao việc → thông báo → người nhận xác nhận → báo ngược người giao** (yêu cầu 2026-10-21): việc do cấp quản lý giao / chuyển lịch cho người khác (`Giao thêm việc`, `AddWeekTask`, Sửa → đổi người) ghi `WorkTask.GivenById` + `GivenBy`, `AckAt` = null (migration `TaskAck`);
    người nhận (NV hoặc Leader) nhận thông báo `TaskAssigned` (Leader mở `/Staff/Month?as=mine`), thấy nút **"Xác nhận đã nhận việc"** (`_TaskAck`, `POST /Staff/AckTask`, chỉ người nhận, một lần) → người giao nhận thông báo `NotifyType.TaskAcked`;
    mọi dòng việc (`Blocks.TaskRow`) hiện "Giao bởi … · Chờ xác nhận / ✓ Đã xác nhận hh:mm dd/MM/yyyy" cho cả người giao và người nhận.
    Số đỏ tính theo TÀI KHOẢN đăng nhập (không theo scope) nên không mất khi vào Trạng thái làm việc (?as=mine) — `LeaderPendingAll(lid)`, `OtPendingAll(role, me)`.
58. **TP: Phê duyệt → Phát sinh có thanh lọc ngày** (yêu cầu 2026-10-21): `/Manager/Approvals?mode=extra` có `_DayBar` như Hỗ trợ nhân sự / Của Leader; danh sách phát sinh lọc theo ngày đang chọn.
59. **Chấm đỏ ngày chờ xử lý** (yêu cầu 2026-10-22): `Dept.PendingDays(role, me)` (theo tài khoản đăng nhập, ±30 ngày + đề xuất / xin nghỉ mọi ngày) → thanh ngày `_DayBar` hiện "Chờ xử lý ngày khác: dd/MM/yyyy [n]" (bấm để chuyển ngày) và ô ngày lịch tháng có chấm đỏ `.pdot`; không cần dò từng ngày để tìm việc cần duyệt.
60. **Lịch tháng: số xong/tổng + 3 màu; trạng thái RIÊNG từng việc** (yêu cầu 2026-10-22): ô ngày hiện `xong/tổng` (vd 8/9) — xám = không việc, xanh dương nhạt = có việc, xanh dương đậm = hoàn thành hết, đỏ nhạt = chưa hoàn thành hết (ngày đã qua còn việc chưa xong / chuyển tiếp, hoặc có việc Chậm) — `StaffCalendar.DayCell`; áp cho Lịch của tôi, Lịch nhóm / phòng của mọi nhân sự (ô cả nhóm gộp mọi người, dòng dưới là số người có việc). Mỗi dòng việc (`Blocks.TaskRow`) có nhãn ở cuối: **Hoàn thành / Chưa hoàn thành / Chuyển sang dd/MM/yyyy / Đang làm / Chưa làm** (`StaffCalendar.TaskResult`) + ghi chú lý do nếu có; tiêu đề từng người ghi `xong/tổng việc`. Việc đã chuyển tiếp không tính là xong ở ngày gốc.
    Trạng thái từng việc nằm ở **cột riêng** bên phải cột giờ (`.trow-st` / `.st`; điện thoại ≤640px thì xuống dòng dưới nội dung).
    **Sửa / Xóa nằm ở cột cuối cùng của dòng việc** (`_TaskBtns` → `Blocks.TaskRow(actions:)`, `.trow-act`; sheet + form xóa vẫn ở `_TaskActions` / `_TaskEdit` đặt dưới dòng). Điện thoại ≤640px thì xuống dòng dưới.
    **Sửa lại 2026-10-22: phần chờ duyệt CHỈ hiện ở các trang duyệt riêng** — chấm đỏ trên lịch tháng và khối "Đề xuất chờ duyệt" trên lịch đã BỎ; chip "Chờ xử lý ngày khác" của `_DayBar` chỉ hiện ở Kế hoạch đề xuất / Xin nghỉ / Báo cáo (Leader), Phê duyệt (TP), Làm thêm tab duyệt. Số đỏ trên thanh tab trái vẫn giữ.
62. **Công tác dài ngày tô vàng** (yêu cầu 2026-10-22): pill "Công tác dài ngày" màu vàng (`p-warn`, `Labels`) (chỉ chữ, không tô cả hàng) để phân biệt với công tác trong ngày (xanh dương).
63. **Leader có Báo cáo cuối ngày kiểu người hỗ trợ; bỏ nút Hoàn thành / Chưa hoàn thành ở lịch** (yêu cầu 2026-10-22, thay quy tắc 61 và phần "Leader không có báo cáo cuối ngày" của quy tắc 24): tab **Trạng thái làm việc** của Leader có 3 mục con **Trạng thái làm việc | Báo cáo cuối ngày | ĐK làm thêm tại CTy**
    (`_MgrStatusTabs`; `/Staff/Evening?as=mine`). Việc của Leader ở dự án đã có Leader dự án là việc hỗ trợ: Leader chỉ tích **"Đã hỗ trợ"** (kèm giờ), **Leader dự án báo cáo kết quả** hoàn thành / chưa (+ kế hoạch tiếp) — như nhân viên hỗ trợ.
    Gửi báo cáo → **tự xác nhận, không qua duyệt** (`LeaderBy = "Tự xác nhận"`, không gửi thông báo). Đã bỏ `POST /Staff/LeaderResult` và nút xác nhận trên lịch.
64. **Lỗi trang trắng + tải file "Decide.txt" trên điện thoại** (yêu cầu 2026-10-23): nguyên nhân khả dĩ là mã chống giả mạo (antiforgery) hết hạn khi trang mở sẵn lâu / máy chủ khởi động lại (khóa mã hóa trước đây chỉ nằm trong bộ nhớ) → POST trả 400 rỗng. Đã sửa: (1) khóa mã hóa lưu file `App_Data/keys` (gitignore; `SetApplicationName("QMDept")`, fallback bỏ qua nếu thư mục không ghi được); (2) `Services/AntiforgeryFailureFilter` biến lỗi này thành toast "Trang đã hết hạn — thao tác chưa được lưu, vui lòng làm lại" + quay lại trang vừa xem. Nếu còn lỗi trên web thật: lấy log máy chủ của POST `/Overtime/Decide` (hoặc action tương ứng).
    **Nguyên nhân THẬT (2026-10-23)**: action `POST /Manager/Decide` (TP duyệt / từ chối kế hoạch cần hỗ trợ nhân sự — Phê duyệt → Hỗ trợ nhân sự) đã bị xóa nhầm ở commit f747e1a, nên bấm Duyệt / Từ chối là POST tới action không tồn tại → trình duyệt điện thoại hiện trang trắng + tải "Decide.txt". Đã khôi phục. Đã rà mọi form / URL POST trong Views + JS: không còn action thiếu. Lần sau xóa action phải kiểm view còn gọi không.
65. **Hộp thông báo lưu lại + chuông; mọi từ chối đều báo người liên quan** (yêu cầu 2026-10-23): thông báo đẩy SignalR chỉ tới người đang online nên người offline / mở điện thoại sau không thấy → mọi thông báo (`Notifier.SendAsync`) nay được **lưu** vào bảng `inbox_items`
    (migration `InboxItems`, entity `InboxItem`, người đã tắt loại đó ở Cài đặt thì không lưu). Chuông 🔔 cạnh ⚙ trên thanh trên hiện số chưa xem (`ChromeModel.Unread`, tăng ngay khi có thông báo trực tiếp — site.js), bấm vào `/Inbox` (`InboxController`, danh sách 100 mới nhất, mở là đánh dấu đã xem;
    thông báo từ chối / yêu cầu bổ sung / hoàn tác hiện chữ đỏ). Đã rà MỌI luồng từ chối / không duyệt (kế hoạch, báo cáo, phát sinh, đề xuất lịch, xin nghỉ, làm thêm + kết quả, hỗ trợ nhân sự của TP, kế hoạch Leader) — tất cả đều gọi `Notify.…Async`; thêm luồng từ chối mới PHẢI gọi thông báo.
66. **Nhắc nhở báo cáo màu cam** (yêu cầu 2026-10-23): dải cam đầu mọi trang (`ChromeModel.Reminders`, `_Layout`): **từ 07:00** nếu hôm nay chưa báo cáo đầu ngày (trạng thái làm việc); **từ 17:00** nếu đã có kế hoạch (không phải ngày nghỉ) mà chưa báo cáo cuối ngày (nhân viên + Leader; TP không có báo cáo cuối ngày).
    Áp cho nhân viên, Leader, Trưởng phòng (không Quản trị); bỏ qua Chủ nhật, ngày nghỉ cả ngày và khi đang xem màn hình người khác; giờ theo `VnTime`. Nút "Báo cáo ngay ›" dẫn tới trang báo cáo của chính mình.
67. **Xin người hỗ trợ từ NHÓM KHÁC trong phòng: Leader nhóm được xin xác nhận, không qua Trưởng phòng** (yêu cầu 2026-10-23): quy trình **Leader dự án đề xuất** (báo cáo đầu ngày, ô "Nhân sự hỗ trợ" → "Nhóm khác" × số lượng) → **Leader trực tiếp duyệt** kế hoạch (`DecidePlan` → `CreateSupportRequestsAsync` tạo mỗi nhóm một `SupportRequest`, bảng `support_requests`, migration `SupportRequests`)
    → **Leader nhóm được xin "xác nhận" và chọn 1..SL nhân viên của nhóm mình** (tab con **Hỗ trợ nhân sự** của Kế hoạch & duyệt, `/Planning/Support` + `POST /Planning/ConfirmSupport`, chờ xác nhận / đã xác nhận theo ngày, số chờ cộng vào badge tab + tab con) → thông báo đẩy `SupportConfirmed` cho **người đề xuất, Leader trực tiếp của họ và nhân viên được chọn**
    (`SupportRequested` báo Leader nhóm được xin khi kế hoạch được duyệt). Người đề xuất thấy trạng thái từng nhóm ngay trên trang Trạng thái làm việc. `Dept.NeedsManager` nay chỉ còn **Phòng ban khác** (`OtherDept`) — vẫn qua TP; chỉ định người trong nhóm mình vẫn chỉ Leader duyệt.
    Hoàn tác duyệt / gửi lại kế hoạch → hủy các yêu cầu hỗ trợ của kế hoạch đó. **Lưu ý kỹ thuật**: `ExecuteDeleteAsync` sinh SQL sai trên MariaDB (`DELETE AS s`) — xóa bằng `RemoveRange` + `SaveChanges`.
68. **Giờ kết thúc mặc định 17:00** (yêu cầu 2026-10-23): mọi form thêm công việc mới (báo cáo đầu ngày — ngoài kế hoạch, đề xuất công việc, giao thêm việc, việc cá nhân, Phân công) mặc định Bắt đầu 08:00 / Kết thúc **17:00** (trước là 12:00).
69. **Badge CAM = hạng mục MÌNH đã đề xuất nhưng chưa được duyệt** (yêu cầu 2026-10-23): cạnh badge đỏ (việc người khác cần mình duyệt) trên thanh tab chính có thêm badge cam (`TabLink.Wait`, `ChromeBuilder.Wait`, `_Layout` `.bgs` / `.badge.wait`, rê chuột hiện "n mục bạn đã đề xuất đang chờ duyệt"), tính theo TÀI KHOẢN đăng nhập, mọi ngày đã nạp:
    **Nhân viên** — Báo cáo đầu ngày (kế hoạch chờ Leader / TP), Báo cáo cuối ngày (báo cáo chờ duyệt + việc chuyển tiếp chờ duyệt), Kế hoạch tháng (đề xuất lịch + xin nghỉ chờ duyệt), ĐK làm thêm (đăng ký / kết quả chờ duyệt);
    **Leader** — Trạng thái làm việc (kế hoạch + làm thêm chờ TP), Tổng quan (xin nghỉ chờ TP); **TP / Quản trị** — Trạng thái làm việc (làm thêm chờ duyệt). Việc đã bị từ chối không tính (đã có thông báo riêng).
70. **Badge ĐỎ = việc MÌNH phải làm mà chưa làm** (yêu cầu 2026-10-23, bổ sung quy tắc 69): trên thanh tab chính và mục con "Trạng thái làm việc" của Leader / TP (`ChromeBuilder.TodoMorning / TodoEvening / TodoOt`, `ChromeModel.Todo*`, `_MgrStatusTabs` ghi "(n)"):
    **Báo cáo đầu ngày** — hôm nay từ 07:00 chưa gửi, hoặc kế hoạch (hôm nay trở đi) bị từ chối cần gửi lại; **Báo cáo cuối ngày** (nhân viên + Leader) — mỗi việc phải xử lý như điều kiện khi gửi (việc hỗ trợ chưa tích "Đã hỗ trợ", việc của mình / của dự án mình phụ trách chưa xong chưa có kế hoạch tiếp; báo cáo chưa gửi ≥ 1; bị yêu cầu bổ sung), 7 ngày gần nhất, hôm nay chỉ tính từ 17:00;
    **ĐK làm thêm** — đã được duyệt, tới giờ làm mà chưa cập nhật kết quả (hoặc kết quả bị từ chối). Leader / TP: các số cộng vào tab **Trạng thái làm việc**. Phát sinh mình đề xuất chờ duyệt chuyển sang badge cam của Báo cáo cuối ngày (không còn đỏ).
    **Mỗi thành phần của số đỏ phải truy được (yêu cầu 2026-10-23)**: số đỏ tab "Kế hoạch & duyệt" của Leader = kế hoạch + phát sinh + báo cáo + **việc chuyển tiếp chờ duyệt** + làm thêm + đề xuất lịch + xin nghỉ + yêu cầu hỗ trợ nhóm khác, MỌI ngày. Mỗi loại phải hiện ở một mục con có "(n)" (Kế hoạch đề xuất / Xin nghỉ / Hỗ trợ nhân sự / Báo cáo — Báo cáo nay tính cả việc chuyển tiếp) và ngày khác hiện ở chip "Chờ xử lý ngày khác".
    Thêm loại mới vào số đỏ thì PHẢI thêm mục con có số + chip ngày tương ứng, nếu không người dùng thấy số mà không biết ở đâu.
71. **Số đỏ không được "mồ côi" + mọi việc cần XÁC NHẬN đều là đỏ** (yêu cầu 2026-10-23): (1) nguyên nhân số đỏ 7 của Leader Tân ở ngày không có gì: một **việc chuyển tiếp của chính Leader** (do Leader dự án là nhân viên báo thay) có `CarryOk = false` mà không ai có thẩm quyền duyệt (danh sách Báo cáo của Leader chỉ có nhân viên nhóm mình) —
    nay việc chuyển tiếp của Leader / TP **tự duyệt** (`StaffController.UpdateTask` `ownerSelfOk`), số đỏ Leader chỉ đếm việc chuyển tiếp của **nhân viên nhóm mình** (`Dept.LeaderPendingAll / PendingDays` dùng `Team`). (2) Việc được giao mà mình chưa "Xác nhận đã nhận việc" (từ 7 ngày trước) là **số đỏ** trên mục **Kế hoạch tháng** của nhân viên
    (Leader / TP: tab Tổng quan) — `ChromeBuilder.TodoAck` — kèm dải đỏ liệt kê ngày có việc chờ xác nhận ở đầu trang Kế hoạch tháng. Nguyên tắc chung: mọi thứ cần ai đó duyệt / xác nhận / làm mà chưa làm = đỏ (quy tắc 56 / 70 / 71); đề xuất của mình chưa được duyệt = cam (quy tắc 69).
72. **Số trong mục con = chấm ĐỎ; việc chuyển tiếp chờ duyệt luôn có chỗ hiện** (yêu cầu 2026-10-23): `Ui.Subtabs` / `Ui.Seg` tự đổi đuôi " (n)" (n > 0) của nhãn thành chấm đỏ `.cnt` (trừ mục "Đã …"; n = 0 giữ chữ thường) — áp cho MỌI mục con của mọi vai trò.
    Trang Leader → Báo cáo: việc chuyển tiếp chờ duyệt mà nhân viên **chưa có thẻ báo cáo** (Leader dự án báo thay kết quả → tạo việc chuyển tiếp) hiện thành thẻ riêng "việc chuyển tiếp từ …" có nút **Duyệt / Sửa** ở mục Chờ duyệt, và được đếm vào "Chờ duyệt (n)" (`orphanCarries`).
73. **Việc chưa hoàn thành: bắt buộc lý do + chuyển sang ngày SAU ngày gửi báo cáo mới gửi được** (yêu cầu 2026-10-23): ở Báo cáo cuối ngày, việc không "Hoàn thành" phải có **lý do** và **kế hoạch tiếp** (nội dung, giờ, ngày). Ngày chuyển sang phải **sau cả ngày báo cáo lẫn ngày gửi (hôm nay)** — báo cáo bù cho ngày đã qua không được chuyển về quá khứ
    (`StaffReports.NextDateOptions`: 14 ngày kế tiếp sau `max(ngày của việc, hôm nay)`; kiểm ở `UpdateTask` và ô chọn ngày). Khi bấm **Gửi báo cáo** (`SubmitReport`) kiểm lại MỌI việc chưa xong của mình và của dự án mình phụ trách: thiếu lý do hoặc `CarryDate` ≤ ngày gửi → chặn kèm tên việc (cả dữ liệu cũ). Việc hỗ trợ chỉ cần tích "Đã hỗ trợ".
74. **Dự án "Công việc chung"** (yêu cầu 2026-10-24): dự án đặc biệt (id 164, mã `CHUNG`, `Category = "Công việc chung"` → `Project.IsCommon`) để mọi nhân sự ghi nội dung họp triển khai / họp quản lý / họp tổng thể công ty… Mọi vai trò đều chọn được ở các ô chọn dự án (Leader cũng thấy dù không phải Leader phụ trách — `Dept.VisiblePj`;
    nhân viên đề xuất công việc ở lịch tháng được chọn dự án này dù không là Leader dự án — `WorkflowService.ProposeAsync`). **Không tính vào thống kê dự án** (`Dept.ActivePj` loại), không hiện ở trang Leader phụ trách dự án / Leader dự án, không hiện dòng "Leader dự án: …" (`Blocks.TaskRow`, Evening); việc thuộc dự án này tự báo cáo đầy đủ (không là việc hỗ trợ).
75. **Mọi thông báo / số đỏ phải nói rõ NGÀY** (yêu cầu 2026-10-24): (1) thông báo đẩy + hộp thông báo: tiêu đề luôn có "ngày dd/MM/yyyy" và liên kết mở đúng ngày đó (`Notifier`; phát sinh, việc chuyển tiếp đã được bổ sung); dải nhắc báo cáo cam ghi ngày hôm nay. (2) Việc MÌNH phải làm ở ngày khác hiện chip đỏ **"Cần làm ngày khác: dd/MM/yyyy [n]"** ở thanh ngày
    của Báo cáo đầu ngày, Báo cáo cuối ngày, ĐK làm thêm (`_DayBar` đọc `ChromeModel.MorningDays / EveningDays / OtDays`); việc cần DUYỆT ở ngày khác hiện chip "Chờ xử lý ngày khác" ở các trang duyệt (quy tắc 59); việc được giao chờ xác nhận liệt kê ngày ở đầu Kế hoạch tháng (quy tắc 71). Thêm loại thông báo / số đỏ mới PHẢI có ngày trong chữ và chip / liên kết tới ngày.
76. **Số đỏ / chip ngày nào cũng phải có NỘI DUNG việc cần làm trên trang đích** (yêu cầu 2026-10-24): vào ngày trên chip thì trang phải ghi rõ cần làm gì — Báo cáo cuối ngày hiện khung đỏ "dd/MM/yyyy: cần làm để gửi báo cáo cuối ngày" liệt kê từng việc (việc hỗ trợ chưa tích, việc chưa xong cần lý do + ngày chuyển, việc dự án mình phụ trách chưa báo, và nút Gửi báo cáo;
    kèm liên kết tới nút gửi); ĐK làm thêm hiện khung đỏ "cần cập nhật kết quả làm thêm" (cả khi kết quả bị từ chối); Báo cáo đầu ngày bị từ chối hiện khung đỏ có lý do; việc được giao chờ xác nhận có dải đỏ liệt kê ngày + nút Xác nhận. Thêm loại số đỏ mới thì trang đích PHẢI có khung nội dung tương ứng (đỏ = phải làm).
77. **Mức độ ưu tiên khi giao việc** (yêu cầu 2026-10-24): Leader / Trưởng phòng chọn **Mức độ ưu tiên** (Bình thường / **Ưu tiên** / **Khẩn cấp**) ở form **Giao thêm việc** và sheet **Sửa** công việc (`WorkTask.Priority`, enum `TaskPriority`, migration `TaskPriority`; `StaffController.AssignTask / EditTask`, `WorkflowService.AssignTaskAsync / EditTaskAsync`).
    Người được giao biết ngay: nhãn **Ưu tiên** (vàng) / **Khẩn cấp** (đỏ) cạnh tên việc ở mọi nơi hiện dòng việc (`Blocks.TaskRow` — lịch của tôi, lịch nhóm / phòng, trạng thái làm việc, theo dõi, báo cáo; `Blocks.PriorityTag`) và ở danh sách chọn việc của Báo cáo đầu ngày; thông báo đẩy + hộp thông báo có tiền tố **[ƯU TIÊN] / [KHẨN CẤP]**.
    Việc Bình thường không hiện nhãn. Việc do nhân viên tự thêm / đề xuất luôn Bình thường.
78. **Số lần chuyển tiếp + giải thích trạng thái việc** (yêu cầu 2026-10-24): mỗi việc chuyển tiếp ghi **"Chuyển tiếp lần N"** (N = số việc gốc trong chuỗi `CarriedFromId` — `Dept.CarryTimes`; từ lần 2 chữ **đỏ** để thấy việc bị delay nhiều lần); việc gốc bị chuyển đi ghi dưới nhãn "Chuyển sang dd/MM/yyyy" dòng "Chuyển tiếp lần N+1"
    (thêm "· chờ Leader duyệt" nếu bản chuyển của nhân viên chưa được duyệt nên chưa hiện ở lịch ngày đích); việc chưa xong ở ngày đã qua mà chưa đề xuất chuyển ghi đỏ **"Chưa có kế hoạch chuyển"** (`Blocks.TaskRow` cột trạng thái, `.st-note`). Việc chuyển tiếp của Leader / TP **luôn tự duyệt** (`Dept.OkCal` + `ownerSelfOk`) nên luôn hiện ở ngày được chuyển sang
    — các việc cũ thiếu duyệt đã được sửa dữ liệu (`CarryOk = 1`). Việc chuyển tiếp của nhân viên chỉ hiện ở lịch ngày đích sau khi Leader duyệt (Kế hoạch & duyệt → Báo cáo).
79. **Bảng tổng hợp làm thêm THÁNG** (yêu cầu 2026-10-24): cuối trang tổng hợp làm thêm (`Views/Overtime/_Summary.cshtml` → `_OtMonth.cshtml`, `OtMonthVm`) có bảng toàn bộ nhân sự (Leader + nhân viên, **trừ Trưởng phòng / Quản trị**) trong tháng: số ngày (đăng ký đã duyệt), giờ đăng ký, giờ được tính (kết quả đã duyệt), chờ duyệt đăng ký, chờ duyệt kết quả + dòng tổng; chọn tháng `?om=yyyy-MM`.
    Hiện ở: Trưởng phòng → Tổng quan → Nhân sự → Làm thêm tại công ty; Trưởng phòng → Phê duyệt → Làm thêm tại CTy → Tổng hợp suất ăn; Leader phụ trách nhân sự → Nhân sự phòng → Làm thêm tại công ty. Nhân viên không thấy.
80. **Bịt lỗ hổng quy trình (yêu cầu 2026-10-24, trừ "người thay thế khi vắng")**: (2) Leader nhóm được xin hỗ trợ có nút **"Không đáp ứng được"** (bắt buộc lý do — `POST /Planning/DeclineSupport`, `SupportRequest.Declined / DeclineReason`, thông báo `SupportDeclined` cho người đề xuất + Leader trực tiếp; người đề xuất thấy "Không đáp ứng được: lý do" ở Trạng thái làm việc).
    (3) Dải nhắc cam cho người duyệt: **việc chờ duyệt quá 2 ngày** (kèm ngày cũ nhất); Trưởng phòng thấy thêm **Leader nào đang tồn việc quá hạn** (leo thang). (4) Bảng **"Chưa báo cáo cuối ngày · 7 ngày gần nhất"** (`_EodMissing`): Leader (nhóm mình, ở Báo cáo) và Trưởng phòng (cả phòng gồm Leader, ở Tổng quan → Nhân sự).
    (5) Trưởng phòng xem **báo cáo cuối ngày của Leader** (chỉ xem) ở Phê duyệt → Của Leader. (6) Trưởng phòng ghi nghỉ bằng trạng thái "Nghỉ phép / không lương" ở Trạng thái làm việc (tự duyệt, hiện ở bảng Nhân sự). (7) Nhân viên **chưa xếp nhóm** bị chặn gửi kế hoạch / báo cáo / phát sinh / đăng ký làm thêm với lời nhắc xếp nhóm; Trưởng phòng có dải nhắc liệt kê người chưa xếp nhóm.
    (8) Người giao việc thấy dải nhắc khi việc giao **chưa được xác nhận quá 24 giờ**. (9) Leader có nút **"Từ chối"** (bắt buộc lý do) ở đề xuất chuyển tiếp của nhân viên — `POST /Planning/RejectCarry`: xóa việc chuyển tiếp, mở lại báo cáo cuối ngày của nhân viên + Leader dự án báo thay, thông báo kèm lý do.
    **Bảng 'Chưa báo cáo cuối ngày' của Trưởng phòng nằm TRÊN bảng nhân sự** (Tổng quan → Nhân sự, ngay dưới thanh ngày, yêu cầu 2026-10-24).
81. **Xuất Excel "Bảng đăng ký làm thêm giờ tháng"** (yêu cầu 2026-10-24, theo mẫu `Tháng 9.1.xlsx` của phòng): nút **⬇ Xuất Excel (theo đăng ký)** và **(theo kết quả đã duyệt)** ở bảng tổng hợp làm thêm tháng (`_OtMonth`) → `GET /Overtime/ExportMonth?om=yyyy-MM&basis=reg|actual` (chỉ Trưởng phòng / Quản trị và Leader phụ trách nhân sự `HrDuty`).
    File .xlsx tự dựng bằng SpreadsheetML (`Services/OtXlsx.cs`, không thêm thư viện; logo SORA `wwwroot/img/ot-logo.png`): sheet **"Bảng OT nhập liệu"** — tiêu đề + "Bộ phận: Quản lý chất lượng và bảo dưỡng", cột STT | ID (mã nhân viên) | Họ và Tên | Ngày làm thêm | Thứ | Nội dung công việc | Từ | Đến | **150% | 200% | 210% | 270%** | Tổng giờ làm thêm | Ghi chú, dòng Tổng cộng,
    phần "Ghi chú" (Người kiểm tra: TP; Người duyệt: GĐ; gửi duyệt trước 16h) và ô ký Người lập / Người kiểm tra / Người duyệt; sheet **"Danh sách CBCNV"** (STT, ID, Họ tên, Tên công ty). Không gồm Trưởng phòng / Quản trị. Hệ số: ngày thường 150% đến 22:00, sau 22:00 210%; Chủ nhật / ngày nghỉ 200% đến 22:00, sau 22:00 270%; trừ nghỉ trưa 12:00–13:00
    (`OtXlsx.Split`). Lưu ý khác file mẫu: công thức Excel cũ của cột 270% (`24-H`) và trừ nghỉ trưa ngày Chủ nhật làm việc sau 22:00 tính sai nên bản này tính đúng theo giờ thực tế. `reg` = đăng ký đã duyệt theo giờ đăng ký; `actual` = chỉ đăng ký có kết quả đã duyệt, theo giờ thực tế + công việc đã triển khai.
82. **Báo cáo cuối ngày: nút "Đã cập nhật", bỏ "Chi tiết"; thông báo lỗi giữ tới khi bấm** (yêu cầu 2026-10-24): (1) việc đã cập nhật (trạng thái khác "Chưa làm" hoặc đã có kế hoạch chuyển) hiện nút xanh **"✓ Đã cập nhật"** (vẫn bấm để sửa khi báo cáo còn mở), chưa cập nhật là **"Cập nhật"**; nút **"Chi tiết" đã bỏ**, nội dung chi tiết
    (Dự án, Đầu mục, Kết quả / Lý do chưa hoàn thành, giờ cập nhật) luôn hiện ngay trong thẻ việc, ngay trên hàng nút. (2) **Mọi thông báo lỗi / thiếu điều kiện** (`AppController.ToastErr`, kiểm tra phía trình duyệt `staff.js` / `overtime.js` / `site.js`) là khung đỏ **giữ lại cho tới khi người dùng bấm vào màn hình** (hoặc gõ phím);
    thông báo thành công vẫn tự tắt sau ~3 giây (`qmToast(msg, sticky)`, `TempData["toastSticky"]`, CSS `.toast.sticky`). Thông báo lỗi mới PHẢI dùng `ToastErr`, không dùng `Toast`.
    **Cập nhật 2026-10-24 (thay bảng theo người của quy tắc 79)**: bảng làm thêm tháng trên màn hình dựng lại **đúng mẫu Excel** — `_OtMonth.cshtml` dùng chung dữ liệu `OtXlsx.LoadAsync`: mỗi dòng một lần làm thêm, **chỉ người có làm thêm tại công ty** (không gồm Trưởng phòng / Quản trị), cột STT | ID | Họ và Tên | Ngày làm thêm | Thứ | Nội dung công việc | Từ | Đến | 150% | 200% | 210% | 270% | Tổng giờ làm thêm | Ghi chú + dòng Tổng cộng;
    chọn tháng `?om=yyyy-MM`, cơ sở `?ob=reg` (theo đăng ký) | `actual` (theo kết quả đã duyệt); nút "⬇ Xuất Excel" xuất đúng bảng đang xem (quy tắc 81). `OtMonthVm` không còn dùng.
    **Sửa lại 2026-10-24 (quy tắc 81)**: file Excel xuất ra **dựng từ chính file mẫu** `QMDept/Assets/ot-template.xlsx` (= `Tháng 9.1.xlsx`, copy khi build / publish — `QMDept.csproj`): giữ NGUYÊN font (Times New Roman), cỡ chữ, khung, cách ẩn dòng trống, logo, ghi chú, chữ ký, sheet "Danh sách CBCNV"; `OtXlsx.BuildFromTemplate` chỉ thay tiêu đề tháng và các dòng dữ liệu (dòng 5.., giữ kiểu ô của mẫu),
    **không thêm dòng tổng cộng** và không thêm nội dung nào khác mẫu. Muốn đổi bố cục → sửa file mẫu rồi build lại (không sửa code định dạng). Bảng trên màn hình cũng không có dòng tổng.
    Ô **Suất cơm** của bảng Nhân sự (`_PeopleTable`) ghi rõ **Cơm n · Bún m** ngay dưới con số (yêu cầu 2026-10-24).
83. **Nhập NHIỀU công việc của cùng một dự án trong một bảng; không cho trùng giờ** (yêu cầu 2026-10-24): Giao thêm việc (Lịch nhóm / phòng), Đề xuất công việc (nhân viên), Cập nhật kế hoạch cá nhân (Leader / TP) và Thêm công việc ngoài kế hoạch (Báo cáo đầu ngày) dùng chung partial `Views/Shared/_TaskLines.cshtml`:
    mỗi dòng = nội dung + giờ bắt đầu + giờ kết thúc (giờ nằm cuối dòng), nút "＋ Thêm công việc" / ✕ bớt dòng (field `ltitle` / `lstart` / `lend` lặp theo dòng, đọc bằng `ViewModels/TaskLines.cs`; field `title/start/end` cũ vẫn được chấp nhận). **Hai việc trùng khung giờ thì bị chặn**:
    trùng nhau trong bảng, hoặc trùng việc đã có của người thực hiện trong ngày (`TaskLines.Validate / EnsureFreeAsync`; liền kề 08–10 và 10–12 là không trùng) — client tô đỏ dòng + dòng cảnh báo + toast giữ lại, server kiểm lại cả khi Sửa công việc (`EditTask`, loại trừ chính nó). Nhiều dòng ghi trong một transaction.
    Đề xuất công việc phát sinh ở Báo cáo cuối ngày (`_ExtraSheet`, có mô tả + giờ kết thúc không bắt buộc) giữ nguyên một việc / lần.
84. **Báo cáo cuối ngày: "Đã cập nhật" là nhãn, "Sửa" là nút riêng** (yêu cầu 2026-10-24, sửa quy tắc 82 mục 1): việc chưa cập nhật = nút xanh dương **"Cập nhật"**; việc đã cập nhật = nhãn xanh lá **"✓ Đã cập nhật"** (không bấm được) + nút viền **"Sửa"** đứng riêng bên cạnh để mở lại bảng cập nhật
    (`Views/Shared/_UpdateBtn.cshtml`, CSS `.upd-row / .upd-done / .upd-edit`) — áp cho mọi nhân sự (việc của mình, việc của người hỗ trợ trong dự án mình phụ trách; nhân viên, Leader).
