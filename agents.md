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
    nên mật khẩu tối thiểu chỉ 1 ký tự — `AccountRules.MinPassword`); mật khẩu mặc định của tài khoản mới / sau khi đặt lại vẫn `Test@123`.
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
