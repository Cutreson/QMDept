# Nhật ký bàn giao

Ghi mới nhất ở trên. Mỗi phiên: ngày, AI/người làm, đã làm, đang dở, việc tiếp theo, quyết định.

---

## Phiên 4 — 2026-10-14 (Claude Code, Sonnet 5.5) — hướng dẫn cho người mới

- Chủ dự án xác nhận lại: commit + push thẳng `master`, không tự publish (chủ dự án chạy Publish trong Visual Studio), không dùng `fable` → đã ghi vào agents.md (quy tắc 4, 18, 20).
- Tạo tài liệu hướng dẫn người không rành kỹ thuật làm tiếp dự án bằng Claude + Publish bằng Visual Studio:
  https://claude.ai/artifact/M28pntTwxHCwQQxZq4kygv (riêng tư, chủ dự án tự chia sẻ nếu cần).
- Build + chạy thử trên http://localhost:5125 đạt.
- Thêm 5 dự án vào DB thật (Dây truyền dây/Musashi, Máy kiểm tra tấm/Rozer, Băng tải 1/Kefico, Chiller/Chiyoda, Máy ép vòng bi/Valqua) —
  "khách hàng" = ô Nhà máy của dự án.
- **Nhân sự hỗ trợ / Leader dự án** (yêu cầu chủ dự án 2026-10-14): báo cáo cuối ngày của người chỉ Hỗ trợ dự án = Hoàn thành / Chưa hoàn thành +
  vấn đề cần chú ý (không kế hoạch tiếp); Leader dự án báo cáo đầy đủ; mỗi dự án một Leader dự án; mọi dòng việc hiện "Leader dự án: …".
  Chi tiết: `docs/nghiep-vu.md` §3.4. Đã thử trên DB thật bằng dữ liệu tạm (tài khoản `zz.*`, đã xóa sạch).
  Bổ sung cùng ngày: Leader xem NV chỉ thấy lịch tháng của NV (các trang khác chuyển về `/Staff/Month?as=`); lịch nhóm chỉ hiện "Leader dự án"
  ở dòng của người được chọn làm Leader dự án (`TaskRow(leadOnly: true)`).
  Leader có tab con mới "Leader dự án" (`/Planning/ProjectLeads`, POST `SetProjectLead`) để chọn người làm Leader dự án cho từng dự án; TP / Quản trị
  có nút "Chọn Leader dự án" ở trang Dự án; thông báo mới `NotifyType.ProjectLead`.
  Đề xuất lịch của NV chỉ cho dự án mình là Leader dự án (server kiểm tra trong `WorkflowService.ProposeAsync`).
  Tab "Leader dự án": 2 mục nhỏ Đã có / Chưa chọn Leader dự án.
  TP / Quản trị KHÔNG chọn Leader dự án (đã bỏ nút + chặn); chỉ Leader nhóm chọn.
  TP / Quản trị: nút "Leader phụ trách dự án" (/Planning/ProjectOwners) giao dự án cho nhóm Leader (docs/nghiep-vu.md §19).
  **2026-10-16: nạp lại file thông tin DA.xlsx — CHỈ sheet "Dự án chế tạo": 48 dự án / 355 hạng mục, kiểm khớp 100% (docs/nghiep-vu.md §18); dự án demo + nội bộ đã xóa.**
  **2026-10-16: chủ dự án yêu cầu XÓA toàn bộ dữ liệu dự án + giao việc của mọi nhân sự trên DB thật** (`tools/delete-project-data.sql`, đã chạy): DB còn 11 tài khoản (admin + 10 nhân sự chuẩn), 0 dự án / phân công / việc / đề xuất / kế hoạch ngày / báo cáo. Muốn có dự án phải tạo lại (Trưởng phòng / Quản trị → Dự án → ＋ Dự án mới).
  Ô chọn có tìm kiếm trong chính ô (1 ô thay vì 2) — docs/nghiep-vu.md §17.
  Số lượng dự án (N · đang triển khai · hoàn thành) ở mọi màn hình thông tin dự án (docs/nghiep-vu.md §16).
  Công cụ `tools/web.sh` (restart/start/stop/health) + `tools/web-watch.sh` (tự chạy lại web khi lỗi) — AGENTS.md quy tắc 22.
  2026-10-16: thứ tự tab con Leader: Kế hoạch, Lịch tuần, Phân công, Báo cáo, Đề xuất lịch, Dự án & đầu mục, Leader dự án, Nhân sự.
  2026-10-16: form đề xuất chỉ liệt kê dự án NV được giao làm Leader dự án (đã kiểm tra 6 NV).
  Mọi NV có cùng tính năng đề xuất + xin hỗ trợ (không còn gắn với việc là Leader dự án); đã so sánh 6 NV bằng script (id 11/12/13/27/28/29) — giống nhau.
  Báo cáo đầu ngày: "Nhân sự hỗ trợ" thiết kế giống đề xuất công việc (docs/nghiep-vu.md §14).
  Đề xuất công việc mở cho mọi NV có nhóm (dự án bất kỳ cho việc của mình; xin hỗ trợ chỉ ở dự án mình là Leader dự án).
  Lịch nhóm của NV gồm cả Leader của nhóm (Leader tự đặt việc hiện cho NV thấy) — giống giao diện Leader; đã kiểm tra mọi tài khoản NV / Leader / TP (script audit: GET mọi trang) đều chạy.
  Leader giao việc: bỏ đầu mục, nhãn "Leader" cho trưởng nhóm; đề xuất của NV hiện ở lịch nhóm + huy hiệu (`docs/nghiep-vu.md` §13).
  Đề xuất công việc: bỏ đầu mục, thêm lý do + số lượng xin hỗ trợ theo phòng ban / nhóm khác, chỉ chỉ định người trong nhóm mình (`docs/nghiep-vu.md` §12).
  Tab "Làm thêm" đổi tên thành "ĐK làm thêm tại CTy" (tiêu đề trang: "Đăng ký làm thêm tại công ty"). Đề xuất công việc dùng được cả ở Lịch nhóm (partial `_StaffProposals`).
  **2026-10-15 (5 hạng mục)**: NV xem Lịch nhóm + đề xuất lịch (bảng `calendar_proposals`, Leader duyệt ở tab "Đề xuất lịch"); Leader tự đặt lịch cho mình ở lịch nhóm;
  ô tìm nhanh cho danh sách chọn (site.js); NV chỉnh giờ làm ở báo cáo cuối ngày. Chi tiết `docs/nghiep-vu.md` §3.1, §3.4, §11.
  Header: nút "Màn hình của tôi" tách khỏi combobox, cạnh nút Lịch nhóm (TP / Quản trị: "Lịch của phòng").
  Thanh DEV chọn giao diện + bỏ màn hình đăng nhập khi chạy thử trên máy (quy tắc 21; không bật trên web thật).
  Cập nhật nhân sự chuẩn (`docs/nhan-su.md`): 10 người, mã tài khoản = mã nhân viên, mật khẩu `1` (MinPassword hạ còn 1); đã sửa trên DB thật qua trang Quản trị + SQL.
  NV → Thông tin dự án chỉ hiện dự án mình là Leader dự án (`StaffController.Calendar.cs`, `Projects`).
  **Cập nhật cùng ngày**: người hỗ trợ chỉ tích "Đã hỗ trợ" (cột mới `work_tasks.Supported`, migration `TaskSupported`, đã áp lên DB thật); Leader dự án báo cáo
  cả việc của người hỗ trợ; giao việc không còn chọn vai trò (nhãn tự động theo dự án; ô tích người tham gia để trắng).
  Quyết định tự đưa ra: dự án chưa có Leader dự án thì mọi người báo cáo đầy đủ như trước.

---

## Phiên 3 — 2026-10-14 (Claude Code, Sonnet 5.5) — dựng lại DB mới + sửa lỗi khởi động

### Đã làm
- Chủ dự án đổi sang DB mới **db73445** (MariaDB 10.11.15, trống). App tự `Migrate()` → 16 bảng + `__EFMigrationsHistory` (InitialCreate + EnglishNames).
  Tạo lại 5 tài khoản chính (tp qua Bootstrap; tp.leader1, nv1, nv2, admin qua SQL + đặt mật khẩu ở lần đăng nhập đầu) — mật khẩu ở
  `docs/tai-khoan.local.md` (local, gitignore).
- **Lỗi khởi động đã sửa**: DI deadlock (`AddDbContextFactory` → `DataCacheInterceptor` → `DataCache` → `IDbContextFactory`): app treo, không log, không
  kết nối DB. `DataCacheInterceptor` nay lấy `DataCache` chậm qua `IServiceProvider`. (Code DataCache của phiên 1 trước đó chưa từng chạy thật.)
- Smoke test (`tools/smoke.sh`) 5 tài khoản × ~65 trang: đạt. Hiệu năng: trang đã cache ~5–20 ms, nạp mới ~0,7–1,7 s (trước ~10 s).
  Còn 4 trang ~0,8–1 s ở lần lặp: Manager/Overview, Leader/Reports, Manager/Reports, Overtime?tab=summary — chưa tìm nguyên nhân.
- Rà soát code bằng agent (5 reviewer + verifier) — đã sửa: TP không sửa / hạ vai trò / đặt lại mật khẩu / xóa tài khoản TP khác
  (`PlanningController.Manage.cs`); `Dept.InScope` loại tài khoản đã xóa với mọi vai trò (quy tắc 15); bỏ câu hướng dẫn trái quy tắc 14
  (Blocks.cs, Week/Assign.cshtml); CSS 375px (nhãn "Xem màn hình", "Trạng thái", nhãn phụ lịch nhóm).
- Ghi khác biệt chủ ý vào nghiep-vu.md §9 (việc ngoài KH khi sửa kế hoạch; tài khoản TP chỉ Quản trị sửa).
- TP / Quản trị tạo + sửa dự án và đầu mục: nút "＋ Dự án mới" / "Sửa dự án & đầu mục" ở `/Manager/Projects` → `/Planning/Projects` (dùng chung với Leader).
- **Trạng thái DB cuối phiên 3**: chủ dự án yêu cầu "coi như dựng lại DB, chỉ còn admin" → DB `db73445` chỉ có tài khoản `admin` (Admin@123); mọi tài khoản test đã xóa hẳn.
  Tạo TP / Leader / NV ở Quản trị → Tài khoản (mã theo quy tắc `hue.nv`, mật khẩu mặc định `Test@123`).
- Mật khẩu test: `admin` = Admin@123, tp / tp.leader1 / nv1 / nv2 = Test@123 (chủ dự án chọn; ghi ở `docs/tai-khoan.local.md`).
- Thêm `tools/e2e-lib.mjs`; quy tắc 18 (không dùng model fable); ghi cách chạy app trên máy dev Windows vào AGENTS.md §3.

### Chưa làm / việc tiếp theo
1. ~~Dọn dữ liệu test~~ — **đã xong** (chủ dự án chạy `tools/cleanup-test-data.sql`): DB còn đúng 5 tài khoản chính, 0 dự án / việc.
2. E2E 9 khu vực (carry, extras, pages, staff, overtime, access, notify, perf, ui) **chưa chạy** (chủ dự án chỉ cần test đơn giản).
3. Phát hiện chưa xử lý: chuyển tiếp trên dự án đã bàn giao (verifier chia ý kiến); `DeleteTaskAsync` không giao dịch (không gây lỗi người dùng);
   Bootstrap TOCTOU (mức thấp, giống artifact).
4. Bản web trên qlcl.runasp.net cần publish lại với connection string mới.

### Quyết định
- Workflow chỉ dùng `sonnet` (`opus` 404; `fable` bị cấm). Test chỉ ở mức smoke theo yêu cầu chủ dự án.

---

## Phiên 2 — 2026-10-13 khuya (Claude Code trên claude.ai, môi trường cloud) — rà soát code + sửa lỗi

> Chủ dự án yêu cầu tự chủ hoàn toàn (agents.md §1 mục 17), làm trực tiếp trên `master`.

### Bối cảnh
- Máy cloud của phiên này **không kết nối được DB thật** (không phân giải được `db73331.public.databaseasp.net`, không có TCP 3306
  ra ngoài) → theo quy tắc 2 chỉ build, không chạy app / smoke / E2E. Đã cài .NET SDK 10.0.112 (apt) — build sạch 0 lỗi 0 cảnh báo.
- Việc 1–4 của phiên trước (chờ DB mở, xóa dữ liệu test, smoke/perf, E2E) **vẫn chưa làm** — phải chạy từ máy có DB.

### Đã làm: việc 5 (rà soát code 3 mảng, đọc code — 3 agent) và sửa mọi lỗi xác nhận được
- Chuyển tiếp: duyệt / duyệt tất cả / sửa và duyệt / duyệt báo cáo khi NV **đã gửi kế hoạch** ngày chuyển sang → việc được chọn
  sẵn + gắn vào kế hoạch (`WorkflowService.SyncCarryToPlanAsync`). Trước đây kế hoạch đã duyệt thì NV không cập nhật / báo cáo được
  việc chuyển tiếp duyệt muộn. Hoàn tác / NV sửa đề xuất / đổi ngày → gỡ khỏi kế hoạch cũ. ApproveCarry / UndoCarry bỏ qua nếu đã ở trạng thái đích.
- Xóa tài khoản Leader (TP Nhân sự + Quản trị) → NV trong nhóm thành "Chưa xếp nhóm" (`AccountRules.ClearTeamAsync`, ghi cùng
  SaveChanges — cũng dùng cho đổi vai trò Leader, nay nguyên tử).
- Duyệt phát sinh: chặn dự án đã bàn giao; giành quyền bằng UPDATE có điều kiện trong transaction (2 người duyệt cùng lúc chỉ tạo 1 việc);
  kết thúc "Chưa rõ" + bắt đầu ≥ 17:00 → bắt đầu + 30'.
- Thông báo mới (quy tắc 10): hoàn tác quyết định kế hoạch / báo cáo → NV; hủy đăng ký làm thêm → người duyệt (`OvertimeCancelled`);
  NV đề xuất / sửa chuyển tiếp khi báo cáo đã duyệt → Leader (`CarryProposed`). Hai loại mới tự hiện ở trang Cài đặt.
- Xem màn hình: Quản trị đang xem TP bấm tab TP không còn bị đưa về màn hình của mình (`ScopeResolver` giữ người trong cookie nếu cùng
  giao diện); form "Giao thêm việc" ở lịch nhóm ghi `as=all`; form Làm thêm luôn ghi `as=`; server chặn mọi POST ở lịch nhóm trừ
  `Staff/AssignTask`; POST bị Forbidden có toast.
- `SetNeed` chỉ nhận dự án đang triển khai. `DataCache`: chờ đủ truy vấn (`Task.WhenAll`), lần nạp cũ không ghi đè ảnh chụp mới hơn.
- Quy tắc 14: bỏ phụ đề "biết trước việc cần làm…" (Kế hoạch tháng), các dòng "Bấm vào một ngày…", "Bấm để mở màn hình phê duyệt ›".
- Tài liệu: nghiep-vu.md §9 (khác biệt mới) + §10 (bảng thông báo), kien-truc.md (Xem màn hình).

### Chưa làm / việc tiếp theo
1. Việc 1–4 của phiên trước (cần DB). Khi E2E nhớ test thêm: chuyển tiếp duyệt sau khi kế hoạch đã duyệt (NV cập nhật được việc),
   xóa Leader → NV "Chưa xếp nhóm", 2 loại thông báo mới, Quản trị xem TP chuyển tab.
2. NV chưa xếp nhóm không có ai duyệt kế hoạch / báo cáo / phát sinh / chuyển tiếp (artifact cũng vậy: phạm vi Leader chỉ là nhóm).
   **Đã quyết định (phiên 2, "Tiếp tục xử lý đi")**: không cho TP duyệt thay; TP Tổng quan hiện cảnh báo đỏ "N nhân viên chưa xếp nhóm"
   (tên + link tab Nhân sự) để TP xếp nhóm. NV gắn với Leader đã xóa hiện "Chưa xếp nhóm" ở mọi nơi (`Dept.LeaderOf/GroupText/Unassigned`).
3. Còn vài chú thích ranh giới quy tắc 14 giữ lại vì mang luật nghiệp vụ: "Nghỉ phép tối thiểu theo buổi", "Nghỉ không lương tối thiểu
   1 giờ" (StaffReports.cs, staff.js), "Tăng ca từ 3 giờ bắt buộc dùng suất cơm." (Overtime.cs).

### Vòng 2 (sau "Tiếp tục xử lý đi")
- Rà soát lại toàn bộ thay đổi của phiên (1 agent): sửa thêm — ngoại lệ giữ người trong cookie của `ScopeResolver` chỉ áp dụng cho GET
  (form POST không có `?as=` luôn là màn hình của mình); phát sinh không nhận giờ bắt đầu 24:00; chuyển tiếp duyệt muộn không chèn vào
  ngày NV đã gửi báo cáo cuối ngày.

### Quyết định tự đưa ra
- Việc chuyển tiếp được duyệt vào ngày NV đã gửi kế hoạch thì tự gắn vào kế hoạch (giống việc được giao / phát sinh).
- Không cho TP duyệt thay NV chưa xếp nhóm trong phiên này (thay đổi mô hình giao diện lớn, chưa test được vì không có DB).

---

## Phiên 1 (tiếp) — 2026-10-13 tối (Claude Code, Opus 5.5) — DỪNG THEO YÊU CẦU CHỦ DỰ ÁN LÚC ~23:15

> Phiên sau: đọc `agents.md` (quy tắc §1 mục 1–16) → mục này → `docs/kien-truc.md` ("Hiệu năng", "Bảo mật đăng nhập", "Xem màn hình").
> Trao đổi với chủ dự án bằng **tiếng Việt** (quy tắc 16).

### Trạng thái tổng quát
- Code ở `master` **build sạch** (0 lỗi, 0 cảnh báo). Commit cuối của phiên: xem `git log`.
- Web chủ dự án đang dùng: **http://qlcl.runasp.net/** (MonsterASP, publish bằng Visual Studio — hồ sơ
  `QMDept/Properties/PublishProfiles/site98406-WebDeploy.pubxml`, gitignore). Bản trên đó là bản publish lúc ~20:08,
  **chưa có** các thay đổi của phiên này → chủ dự án cần publish lại.
- **DB thật chặn IP máy dev `42.1.77.17` từ ~21:45**: lúc đầu "Access denied for user db73331@42.1.77.17", sau đó không mở
  được cổng 3306, ping mất 100%. Connection string KHÔNG đổi (chủ dự án xác nhận). Đến 23:11 vẫn bị chặn. Có thể do nhà cung cấp
  chặn IP sau nhiều lần đăng nhập DB thất bại / quá nhiều kết nối từ 4 agent test chạy song song. → Mọi việc cần DB ở dưới **chưa làm được**.
  Lúc ~22:05 qlcl.runasp.net cũng trả 503 rồi 404 (có thể hosting đang sự cố hoặc chủ dự án đang publish).
- **Lưu ý khi test lại**: không chạy nhiều agent / nhiều kết nối song song tới DB; khi DB lỗi phải dừng app ngay (app thử kết nối
  lại liên tục có thể kéo dài lệnh chặn).

### Đã làm trong phiên
- Hoàn tất toàn bộ màn hình (commit `30f5781`), rồi theo yêu cầu chủ dự án: chỉ dùng văn bản (gỡ ảnh, bảng `photos`),
  thông báo đẩy SignalR + `/Settings`, xem màn hình cấp dưới (chỉ xem), favicon, code tiếng Anh (định danh/route/file/enum),
  giờ VN qua `VnTime`, giao diện responsive.
- Migration `EnglishNames` **đã áp lên DB thật** (21:12): đổi tên cột, chuyển giá trị enum tiếng Việt → tiếng Anh, bỏ `photos`,
  thêm `notify_settings`; đã truy vấn kiểm tra không còn giá trị tiếng Việt. Smoke test 65 trang × 5 tài khoản đạt (bản chưa có cache).
- Yêu cầu giao diện mới (agents.md §1 mục 8, 9, 14, 15, 16): bỏ mọi placeholder; lược chú thích / phụ đề trang;
  **Quản trị bao trùm TP** (`ScopeResolver.UiRoleOf(Admin)=Manager`, tab Tài khoản thêm vào tab TP); trang Tài khoản một danh sách
  có trạng thái Đang hoạt động / Đã xóa (`?st=`); lịch cả nhóm là nút "Lịch nhóm" cạnh combobox; trả lời bằng tiếng Việt.
- **Tối ưu tốc độ** (chưa đo lại được vì DB bị chặn): đo trước ~10 s/trang = ~13 truy vấn × ~750 ms (RTT ~250 ms + MySql.Data lấy lại
  kết nối từ pool ~500 ms). Thêm `Services/DataCache.cs`: `DataCache` (ảnh chụp bảng nhỏ + dữ liệu theo khoảng ngày, dùng chung mọi
  request, tự làm mới sau mọi lệnh ghi qua `DataCacheInterceptor`, TTL 60 s, nạp song song tối đa 8 kết nối), `KeepOpenInterceptor`
  (một kết nối suốt request). `Dept`, `ScopeResolver`, `ChromeBuilder`, `HomeController`, `Notifier`, cookie `OnValidatePrincipal`
  đều đọc từ cache. DbContext đăng ký bằng `AddDbContextFactory` + scoped. Chi tiết: `docs/kien-truc.md` mục "Hiệu năng".
- Sửa lỗi E2E (đợt 1 + đợt 2):
  - vòng chuyển hướng khi phiên bị thu hồi; giới hạn đăng nhập 10/phút/IP (policy `login`);
  - việc ngoài KH khi KH đã gửi; dự án đã bàn giao; làm thêm: lưới 30 phút, 0 giờ, quyết định lại, giờ sai định dạng (`Fmt.IsHhmm` chặt);
  - gửi trùng chạm unique index → toast thay vì 500 (`AppController` bắt `DbUpdateException` cho mọi POST);
  - chi tiết dự án → "Phát sinh gần đây" nạp việc đúng ngày của phát sinh (trạng thái Hoàn thành / Đang xử lý);
  - **bảo mật**: Quản trị không đổi tài khoản chưa có mật khẩu thành Quản trị/TP; TP (Nhân sự) không tạo TP, không xóa mật khẩu / đổi mã
    tài khoản TP; Admin không sửa tài khoản đã xóa; phiên bị thu hồi bị từ chối ở mọi endpoint kể cả hub SignalR (`OnValidatePrincipal`),
    kết nối SignalR cũ không nhận thông báo và bị ngắt (`NotifyHub.Open`); Logout xóa cookie `qm_view`; `?as=all` khi không có NV bị bỏ qua.
- Script kiểm thử / bảo trì đưa vào repo `tools/` (mật khẩu truyền qua biến môi trường, xem `docs/tai-khoan.local.md` trên máy chủ dự án;
  chạy ở thư mục gốc repo): `smoke.sh` (GET mọi trang theo vai trò), `perf.mjs` (đo thời gian tải trang), `dbquery.cs` (chạy SQL trên
  DB thật), `dblatency.cs` (đo độ trễ DB), `cleanup-test-data.sql` (xóa dữ liệu test).

### Kết quả E2E đợt 2 (trước khi DB bị chặn) — đã kiểm OK
- NV đầu ngày / cuối ngày + Leader duyệt KH + TP duyệt hỗ trợ: mọi thông báo kiểm tra và thứ tự khớp artifact, không lỗi.
- Làm thêm: C# `OtRules.Calc` và JS `otRule` khớp nhau trên 4.608 tổ hợp giờ; đăng ký / sửa / duyệt / hoàn tác / tổng hợp / gửi HCNS;
  chế độ chỉ xem chặn POST.
- Dự án, đầu mục, % kế hoạch / thực tế, trạng thái dự án (biên chênh 25), lịch tuần, phân công, nhân sự (TP), phạm vi Leader;
  SignalR "Việc mới trong lịch" / "Việc bị xóa khỏi lịch".
- Tài khoản: mọi kiểm tra khi tạo / đặt mật khẩu / xóa / khôi phục; phiên cũ trên MVC; không có upload file; định danh tiếng Anh;
  không dùng giờ máy chủ.

### CHƯA LÀM (việc tiếp theo, theo thứ tự)
1. Chờ DB mở lại cho IP máy dev (kiểm tra: `dotnet run tools/dblatency.cs`). Nếu vẫn bị chặn: chủ dự án vào trang quản trị
   databaseasp.net / MonsterASP → database `db73331` → Remote access / IP whitelist.
2. **Xóa dữ liệu test** (chủ dự án đã đồng ý): `dotnet run tools/dbquery.cs tools/cleanup-test-data.sql` — đọc chú thích đầu file:
   nếu chủ dự án đã nhập dữ liệu thật trên qlcl.runasp.net thì xóa có chọn lọc theo tiền tố `[E2E` / mã `zz.`.
   Dữ liệu test hiện có: dự án id 1–8 (`[E2E-…]`, `[E2E2-daily]`, `[E2E2-ot]`, `[E2E2-plan] P1/P2neg`) và việc / KH / báo cáo /
   phát sinh / làm thêm (2026-10-13 … 2026-11-03) liên quan; tài khoản Id > 5 (`zz.acct.*` đã xóa mềm, `zz.e2e2ac.s1/s2/l1`,
   `zz.e2e2pl.x1/l1` còn hoạt động — `zz.e2e2ac.s2` đang ở nhóm tp.leader1); 1 đăng ký làm thêm sai định dạng "2 :00–9 :30" (23/10).
3. Chạy app bản mới: `cd QMDept && ASPNETCORE_ENVIRONMENT=Development dotnet run --launch-profile http` (cổng 5125) →
   `QM_PW_USER=… QM_PW_ADMIN=… bash tools/smoke.sh` → `node tools/perf.mjs` (so với ~10 s/trang trước tối ưu; trang xem lại kỳ vọng < 1 s).
4. E2E các luồng **chưa chạy được** (dừng giữa chừng vì DB):
   - Leader `/Planning/Reports`: DecideReport (ok/no, tự duyệt chuyển tiếp), UndoReportDecision, ApproveCarry / ApproveAllCarries /
     UndoCarry / EditCarry; việc chuyển tiếp hiện ở ngày sau.
   - Phát sinh: ApproveExtra / RejectExtra (Planning/Approvals, Leader/Tracking AddExtra) → việc "Phát sinh".
   - `/Leader/Overview`, `/Leader/Tracking`, `/Leader/Reports` (SetNeed, SubmitReport, tab tuần); `/Manager/Overview`, `/Manager/Reports`,
     `/Manager/Approvals` (plan/extra × bộ lọc), `/Manager/Projects` tab overview / people.
   - Nghỉ không lương biên giờ, nghỉ phép cả ngày không chọn việc, công tác dài ngày, ngày không có việc; `/Staff/Month` + lịch nhóm AssignTask.
   - Làm thêm: Cancel (kể cả sau duyệt), chọn "Công việc làm tiếp", gửi lại HCNS, badge tab.
   - Xem màn hình: ma trận `?as=` (Leader → NV nhóm khác, TP → admin, NV → đồng nghiệp phải bị từ chối), mọi POST khi đang xem người khác.
   - **Tính năng mới của phiên chưa test chạy thật**: Quản trị dùng giao diện TP (duyệt, làm thêm tự ghi nhận, gửi HCNS, nhận thông báo TP),
     nút "Lịch nhóm", trang Tài khoản lọc trạng thái, DataCache (dữ liệu mới thấy ngay sau khi ghi), thông báo SignalR sau khi bị thu hồi phiên.
   - Giao diện 375 / 768 / 1280 px: header có nút "Lịch nhóm" + combobox; thanh tab dưới của Quản trị có 7 tab.
5. Rà soát code (3 agent đọc code bị dừng giữa chừng theo yêu cầu chủ dự án — **chưa có kết quả**): Leader/TP báo cáo + chuyển tiếp +
   phát sinh; bảo mật xem màn hình + lịch tháng + DataCache; NV đầu/cuối ngày + làm thêm + thông báo + responsive.
6. Commit + push cuối phiên (máy dev chưa cấu hình credential helper cho git → push có thể cần chủ dự án đăng nhập GitHub:
   `git config --global credential.helper manager` rồi `git push origin master`).

### Quyết định trong phiên
- Quản trị cao hơn TP, dùng giao diện + quyền TP (đăng ký làm thêm của Quản trị được ghi nhận ngay như TP).
- Tài khoản TP / Quản trị không bao giờ ở trạng thái "chưa có mật khẩu"; TP mới do Quản trị tạo kèm mật khẩu (khác artifact — nghiep-vu.md §9).
- Mọi ghi phải qua EF (để `DataCache` tự làm mới); entity của `Dept` / cache là chỉ đọc, dùng chung giữa các request.
- Giới hạn đăng nhập theo IP: nếu sau này chạy sau reverse proxy phải cấu hình ForwardedHeaders (`docs/kien-truc.md`).

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

- (2026-10-16) Form "Giao thêm việc" ở lịch nhóm/lịch phòng chuyển thành nút + bảng trượt (như Đề xuất công việc của NV) — Month.cshtml, site.css. Đã kiểm tra Leader 9/10/26, TP, mobile 375px, giao việc thật rồi xóa dữ liệu thử.

- (2026-10-16) Đồng bộ ngày dd/MM/yyyy toàn web: `Fmt.Dm` = đủ năm, `Fmt.ParseInput`, `Ui.DateInput` thay mọi `<input type=date>` (Assign, Projects), biểu đồ tuần dự án 2 dòng, thanh tuần `dd/MM/yyyy – dd/MM/yyyy`.
  Đã quét ~1.000 trang (NV 11/27, Leader 9/10, TP 8, Admin 5) không còn ngày dd/MM hoặc yyyy-MM-dd hiển thị. Lịch nhóm: "Tất cả nhân sự", "N nhân sự", Leader có pill "Leader" (`Ui.LevelPill(p, leader)`).
- (2026-10-16) Bàn phím điện thoại che danh sách dự án trong bảng trượt: bảng bám visualViewport + danh sách ô chọn nằm trong bảng (site.js/site.css). Kiểm tra bằng mô phỏng --vvh=420px.
- (2026-10-16) Báo cáo đầu ngày: ô Khách hàng (công tác) đổi từ ô gõ + datalist sang ô chọn có tìm kiếm giống Tỉnh / thành (`Ui.Select` + `data-combo`, danh sách = khách hàng của các dự án; khách hàng đã lưu ngoài danh sách vẫn hiện). Áp dụng cho mọi NV (cùng một view).
- (2026-10-16) Dưới mọi ô chọn dự án (thêm việc ngoài kế hoạch, báo cáo cuối ngày phát sinh, đề xuất công việc, giao việc Leader, lịch tuần, lịch nhóm) hiện dòng "Leader dự án: …" đổi theo dự án đang chọn (`Ui.ProjectLeadHint` + site.js, class `.pj-lead`).
- (2026-10-16) Báo cáo đầu ngày: đảo thứ tự 2 mục; công tác tự điền địa điểm + khách hàng theo dự án của việc đã chọn (bỏ ô Tỉnh / Khách hàng). Đã thử gửi kế hoạch thật cho NV 11 ngày 27/10 rồi xóa day_plans thử.
- (2026-10-16) Leader: thêm nút / thanh chọn "Lịch của tôi" (?as=mine) xem lịch tháng riêng như NV; hàng nút Xem màn hình tự xuống dòng trên điện thoại. Đã kiểm tra Leader 9/10/26.
- (2026-10-16) Leader có báo cáo đầu ngày / cuối ngày (chế độ ?as=mine, cookie mine, /Staff/*); Trưởng phòng duyệt ở Phê duyệt → Của Leader. Đã thử trọn luồng với Leader Tân (kế hoạch → TP duyệt → cập nhật việc → báo cáo → TP duyệt) rồi xóa dữ liệu thử.
- (2026-10-16) Sửa lại: Leader chỉ có nút "Kế hoạch" (kế hoạch tháng + Trạng thái làm việc trong ngày), bỏ báo cáo cuối ngày của Leader; TP duyệt trạng thái ở Phê duyệt → Của Leader. Đã thử luồng với Leader Huề (gửi → TP duyệt) rồi xóa dữ liệu thử.
- (2026-10-16) Trạng thái làm việc của Leader: trả lại phần Nội dung công việc (chọn dự án + nội dung, hỗ trợ, ghi chú) như nhân viên; AddUnplannedTask / SubmitPlan cho phép Leader; TP thấy bảng việc ở Của Leader. Đã thử với Leader Tân (thêm việc → gửi công tác → địa điểm/khách hàng tự điền) rồi xóa dữ liệu thử.
- (2026-10-16) Leader: bỏ phần Nhân sự hỗ trợ + Ghi chú ở trang Trạng thái làm việc (giữ ô ẩn cho staff.js). Còn lại: nội dung công việc (chọn dự án + nội dung) và trạng thái & báo cơm. Đã thử gửi thật với Leader Tân rồi xóa dữ liệu thử.
- (2026-10-16) Leader: "Trạng thái làm việc" chuyển thành tab của Màn hình của tôi (giữa Tổng quan và Kế hoạch & duyệt); trong chế độ cá nhân ?as=mine vẫn hiện tab Leader; nút Kế hoạch chỉ sáng ở lịch tháng. Đã kiểm tra Leader 9/10/26 (desktop + mobile).
- (2026-10-16) Leader: ô "Xem màn hình" nhân viên chuyển từ thanh trên vào trang Kế hoạch (lịch tháng; ChromeModel.ViewInPlan + Month.cshtml); xem NV vẫn chỉ xem. Đã kiểm tra Leader 9/10/26 với từng NV trong nhóm.
- (2026-10-17) Leader: bỏ nút Màn hình của tôi / Kế hoạch ở thanh trên; Kế hoạch tháng thành tab con của Kế hoạch & duyệt (thay Lịch tuần + Phân công); xem NV vẫn ở trong Kế hoạch tháng. Đã kiểm tra Leader 9/10/26 (Kế hoạch, Kế hoạch tháng, Lịch nhóm, xem NV).
- (2026-10-17) Leader ở Kế hoạch tháng → Lịch của tôi: nút "＋ Cập nhật kế hoạch cá nhân" (dự án + nội dung + giờ, nguồn Planned, vào lịch ngay) + nút Xóa cho việc đã thêm (POST /Staff/AddOwnTask, /Staff/DeleteOwnTask, chỉ Leader ở ?as=mine). Đã thử với Leader Tân (thêm → hiện → xóa), xóa luôn dòng assignments phát sinh khi thử.
- (2026-10-19) Đăng ký làm thêm: sửa lỗi ô "Báo cơm tăng ca" bị kẹt ở "Không đăng ký" sau khi đổi giờ qua mức không có suất ăn; nay đổi mức (ăn nhẹ ↔ cơm ↔ không) thì quay về suất của mức mới (hiển thị / chọn sẵn trước), cùng mức giữ lựa chọn người dùng (wwwroot/js/overtime.js). Áp dụng cho NV, Leader, TP (cùng form).
- (2026-10-19) Làm thêm: thêm bước nghiệm thu kết quả (OtCompletion, migration 20261019100000_OvertimeCompletion): NV cập nhật giờ thực tế + công việc → Leader/TP duyệt (lưu trữ, tính giờ) hoặc từ chối (không tính giờ, cập nhật lại). Đã chạy trọn luồng NV Tài ↔ Leader Huề (cập nhật → từ chối → cập nhật lại → duyệt) rồi xóa dữ liệu thử.
- (2026-10-19) Leader: gộp Kế hoạch + Đề xuất lịch + duyệt đăng ký làm thêm thành tab "Kế hoạch đề xuất" (Chờ duyệt / Đã duyệt); Approvals + Overtime?tab=approve (Leader) redirect về đây; _OtCard/_OtDetail/_OtDone chuyển sang Views/Shared. Đã thử với Leader Huề + NV Tài (đăng ký làm thêm → duyệt ở Kế hoạch đề xuất) rồi xóa dữ liệu thử.
- (2026-10-19) Nhân sự: sơ đồ hình tháp (TP → Leader → NV); TP kéo thả / chọn nhóm để chuyển NV (MoveToTeam + TeamChanged). Đã thử chuyển NV Hùng (Huề → Tân → chưa xếp → Huề) và kiểm tra Leader cũ / mới thấy đúng, đã khôi phục như ban đầu.
- (2026-10-19) Kế hoạch đề xuất: chuyển tab con vào giữa Kế hoạch tháng và Báo cáo; chia 2 mục chính (Đề xuất công việc / Đề xuất làm thêm tại công ty) × 2 mục con (Chờ duyệt / Đã duyệt). Đã thử đăng ký làm thêm của NV Tài hiện đúng ở mục làm thêm rồi xóa.
- (2026-10-19) Sơ đồ nhân sự: bỏ Báo nghỉ ở thẻ, thêm Xóa tài khoản cạnh Sửa; Labels.Title: Nhân viên / Leader / Trưởng phòng (bỏ Setup). Đã thử tạo + xóa tài khoản zz (đã xóa hẳn khỏi DB).
- (2026-10-20) Leader chỉ thấy dự án được giao (VisiblePj theo OwnerLeaderId); trang TP chọn Leader phụ trách có thống kê + 2 mục Đã có / Chưa có Leader. Hiện 48/48 dự án đã có Leader (Huề 20, Tân 27, Hùng 1).
- (2026-10-20) TP: gộp hàng nút trên thanh trên vào Tổng quan (3 mục Dự án / Nhân sự / Kế hoạch tháng + ô Xem màn hình); TeamPeople của TP gồm cả Leader; biểu đồ tiến độ cuộn ngang. Đã kiểm tra TP + Quản trị.
- (2026-10-20) Xem màn hình người khác chỉ còn Kế hoạch tháng (TP / Quản trị / Leader); ô Xem màn hình chỉ ở mục Kế hoạch tháng. Đã kiểm tra TP + Quản trị xem Leader 9/26 và NV 11: mọi trang khác chuyển về Month.
