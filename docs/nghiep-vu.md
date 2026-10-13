# Đặc tả nghiệp vụ — Quản lý công việc phòng QLCL&BD (SORA)

Nguồn: artifact "Quản lý công việc phòng QLCL&BD" (https://claude.ai/artifact/1EVpUiV9HUGFyWTHz2XJH1) — một SPA
điện thoại (HTML + JS, dữ liệu dùng chung). Bản ASP.NET này port **toàn bộ** màn hình và luật của artifact.
Tên hàm JS gốc được ghi trong ngoặc `()` để tra lại khi cần (mã nguồn artifact: đọc bằng công cụ Artifact `read`).
Những điểm khác artifact được ghi rõ ở mục 9.

## 1. Vai trò & quyền

| Vai trò | Mã | Thấy giao diện | Ghi chú |
|---|---|---|---|
| Nhân viên Setup | `Nv` | Nhân viên (của chính mình) | có cấp bậc Tech 1/2/3, thuộc nhóm của 1 Leader |
| Leader Setup | `Leader` | Leader + "Lịch nhân viên" (NV trong nhóm, chỉ xem + giao việc) | |
| Trưởng phòng | `Tp` | TP + Leader (chọn Leader để xem theo nhóm) + Lịch nhân viên (mọi NV) | quản lý tài khoản |

- Đăng nhập: mã NV + mật khẩu (≥ 4 ký tự). Lần đầu: mật khẩu nhập vào thành mật khẩu. Quên: TP đặt lại.
- Chưa có TP nào → màn hình "Thiết lập tài khoản Trưởng phòng" tạo TP đầu tiên.
- Chỉ TP tạo/sửa/xóa tài khoản, cấp mã, xếp nhóm (`leader`), cấp bậc.
- Leader/TP xem trang NV ở chế độ **chỉ xem** (`isRO`); riêng chế độ "Tất cả nhân viên" có form **Giao thêm việc**.
- `inScope(emp)`: ở giao diện Leader chỉ tính Leader đang xem + NV trong nhóm; các giao diện khác: tất cả.

## 2. Đối tượng dữ liệu (xem docs/database.md cho cột)

people · projects (prj) · plan_items (items: đầu mục kế hoạch dự án) · assignments (assign: vai trò NV trong dự án
Leader dự án / Hỗ trợ dự án) · work_tasks (tasks: việc theo ngày của NV) · day_plans (dayPlans: báo cáo đầu ngày) ·
day_reports (dayReports: báo cáo cuối ngày) · leaves (Leader báo nghỉ cho NV) · project_needs (needs: nhu cầu nhân sự
theo ngày) · extras (phát sinh) · leader_reports · overtimes (ot) · ot_sends · photos.

### Công việc (task)
- Trạng thái: Chưa làm / Đang làm / Hoàn thành / Chậm.
- Nguồn: `KeHoach` (Leader xếp lịch tuần), `NgoaiKeHoach` (NV tự thêm đầu ngày), `PhatSinh` (phát sinh được duyệt),
  `ChuyenTiep` (chuyển từ việc chưa xong), `GiaoViec` (Leader/TP giao; nhãn "Leader giao").
- `Selected`: NV chọn làm trong ngày ở báo cáo đầu ngày.
- **Hiện trên lịch tháng (`okCal`)**: Ngoài kế hoạch → chỉ khi Leader đã duyệt kế hoạch đầu ngày có chứa việc đó;
  Chuyển tiếp → chỉ khi `CarryOk` (Leader duyệt chuyển); nguồn khác → luôn hiện.

## 3. Quy trình hằng ngày của Nhân viên (4 tab + Làm thêm)

### 3.1 Kế hoạch tháng (`nvMonth`)
- Lưới tháng (T2→CN). Mỗi ô: số việc đã duyệt (`okCal`) trong ngày. Màu: không việc = xám; xong hết = xanh lá;
  có việc Chậm = đỏ nhạt; hôm nay = xanh đậm; còn lại = xanh nhạt. Bấm ô → chọn ngày, hiện danh sách việc ngày đó.
- Leader/TP xem một NV: chỉ xem. Chế độ "Tất cả nhân viên": ô = số NV có việc / tổng việc / số người nghỉ (N);
  ô đỏ nếu có việc Chậm; chọn ngày → danh sách việc từng người (`teamDayList`) + form **Giao thêm việc** (`calAssignForm`):
  dự án, đầu mục (hoặc tự nhập), giờ bắt đầu/kết thúc (bước 30'), tích người tham gia + vai trò (Hỗ trợ / Leader dự án).

### 3.2 Thông tin dự án (`nvProjects`, `projectDetail`)
- 2 nhóm: Đang triển khai / Đã hoàn thành (đóng hoặc 100%). Thẻ dự án: màu, tên, trạng thái, nhà máy – địa điểm,
  vai trò của NV (theo assign, hoặc "Bạn đang tham gia" nếu có việc trong tuần), thanh tiến độ, ngày.
- Chi tiết: tab Tổng quan (KPI đầu mục, nhân sự TT/KH, phát sinh gần đây) · Kế hoạch (thông tin, danh sách đầu mục
  theo giai đoạn, biểu đồ tiến độ theo tuần) · Nhân sự (người có việc trong tuần).

### 3.3 Báo cáo đầu ngày (`nvMorning`) — gửi Leader duyệt
Form gồm:
1. **Trạng thái làm việc** (bắt buộc): Làm việc tại công ty / Công tác trong ngày / Công tác dài ngày / Xin nghỉ phép /
   Xin nghỉ không lương. **Báo cơm** (bắt buộc): Cắt cơm / Cơm / Bún.
   - Nghỉ phép → chọn buổi: Sáng 08–12 (0,5 ngày, 4h) / Chiều 13–17 (0,5) / Cả ngày 08–17 (1 ngày, 8h).
   - Nghỉ không lương → chọn từ giờ / đến giờ (mốc giờ chẵn 08..17, bỏ 12:00 bắt đầu, bỏ 13:00 kết thúc), tối thiểu
     1 giờ làm việc (không tính 12:00–13:00).
   - Khi chọn thời gian nghỉ: tự **bỏ chọn** các việc nằm trọn trong khung nghỉ; nghỉ ≥ 8 giờ → báo cơm tự thành
     "Cắt cơm" (đổi lại thì khôi phục lựa chọn cũ).
   - Công tác → bắt buộc Tỉnh/thành (34 tỉnh) + Khách hàng (gợi ý theo danh sách nhà máy).
2. **Nội dung công việc**: tích các việc trong ngày (mặc định tích hết; việc chuyển tiếp chỉ hiện khi đã duyệt);
   "＋ Thêm công việc ngoài kế hoạch" (dự án, đầu mục hoặc tự nhập, giờ) → tạo việc `NgoaiKeHoach` (đã chọn).
   **Nhân sự hỗ trợ**: Không / Trong nhóm (Leader duyệt) / Nhóm khác / Phòng ban khác (cần thêm TP duyệt) + nội dung (bắt buộc nếu chọn).
   Ghi chú.
- Kiểm tra khi gửi: chọn trạng thái; nghỉ phải có thời gian nghỉ đúng loại; công tác phải có tỉnh + khách hàng;
  chọn báo cơm; ít nhất 1 việc (trừ nghỉ ≥ 8 giờ); hỗ trợ phải có nội dung.
- Gửi = ghi đè kế hoạch ngày (xóa các quyết định duyệt cũ) và cập nhật `Selected` của mọi việc trong ngày.
- Sau khi gửi: hiện việc đã chọn, trạng thái & báo cơm, **Trạng thái duyệt** (Leader; TP nếu cần hỗ trợ ngoài nhóm),
  góp ý Leader/TP; chưa duyệt xong → nút "Sửa kế hoạch và gửi lại".
- Trạng thái kế hoạch (`planSt`): Chưa gửi → Chờ duyệt → (Chờ TP duyệt nếu cần) → Đã duyệt; Leader hoặc TP từ chối → Từ chối.

### 3.4 Báo cáo cuối ngày (`nvEvening`)
- Chưa gửi kế hoạch đầu ngày → cảnh báo. Danh sách các việc đã chọn (dòng thời gian), mỗi việc: **Cập nhật**, **Chi tiết**.
- **Cập nhật** (`openUpdate`): trạng thái (Đang làm / Hoàn thành / Chậm / Chưa làm); nếu việc gắn đầu mục chưa xong:
  tích "Đầu mục … đã xong toàn bộ" (tự tích khi chọn Hoàn thành và giờ kết thúc ≥ 15:00); ghi chú — **bắt buộc** khi
  chưa hoàn thành ("Lý do chưa hoàn thành"); ảnh hiện trường (≤ 4, thu nhỏ 1600px).
  Chưa hoàn thành → bắt buộc **Kế hoạch tiếp theo**: ngày (14 ngày kế tiếp, CN ghi "ngày nghỉ"), giờ, nội dung →
  tạo/cập nhật việc `ChuyenTiep` (`CarryOk=false`, chờ Leader duyệt); đổi lại thành Hoàn thành → xóa việc chuyển tiếp
  nếu nó còn "Chưa làm".
  Đầu mục: tích "đã xong" + Hoàn thành → đầu mục Hoàn thành (ngày xong, người xong); ngược lại nếu việc
  Đang làm/Hoàn thành/Chậm mà đầu mục đang Chưa làm → đầu mục Đang làm.
- **＋ Đề xuất công việc phát sinh** (`openExtra`): dự án, tên, mô tả/lý do, giờ bắt đầu (mặc định giờ hiện tại làm tròn
  30'), kết thúc (có thể "Chưa rõ"), ảnh. Leader duyệt → tự vào danh sách việc hôm nay.
- Danh sách phát sinh trong ngày. Cảnh báo "Còn N việc chưa hoàn thành chưa có kế hoạch tiếp theo".
- **Tình hình chung**: Có vướng mắc? (Không/Có), ghi chú → Gửi báo cáo cuối ngày (chặn nếu còn việc chưa xong chưa
  có kế hoạch tiếp). Gửi lại = cần Leader duyệt lại. Trạng thái: Chờ duyệt / Đã duyệt / Yêu cầu bổ sung (kèm nội dung).

### 3.5 Làm thêm (dùng chung 3 vai trò) — quy định áp dụng từ 01/10/2026 (`otScreen`)
- Hộp hạn đăng ký: Dự trù trước 14:00 ngày làm việc liền trước (bỏ CN) · Chính thức trước 14:00 cùng ngày · quá hạn
  → "không đảm bảo bố trí suất ăn".
- Form: Loại ngày (Ngày làm việc — ngoài giờ HC / Ngày nghỉ, lễ; mặc định CN = nghỉ), giờ bắt đầu, giờ kết thúc
  (mặc định 17:00–20:00, ngày nghỉ 08:00–17:00), xem trước mức tăng ca trực tiếp (`otRule`), báo cơm theo mức,
  công việc làm tiếp (tích các việc trong ngày chưa xong), nội dung khác. Phải có ít nhất 1 việc hoặc nội dung.
- Luật `otRule`: ngày làm việc chỉ ngoài 08:00–17:00 (kết thúc ≤ 08:00 hoặc bắt đầu ≥ 17:00); ngày nghỉ trừ 12–13.
  < 2h: "Làm thêm X giờ", không suất ăn · 2–<3h: "Tăng ca 2 giờ", ăn nhẹ 15.000đ · 3–<4h: cơm 35.000đ ·
  ≥ 4h: cơm 35.000đ + phụ cấp 20.000đ (trả vào lương). Nghỉ giải lao: 12:00–13:00 (nếu trừ trưa); ≥3h và bắt đầu
  ≤17:30, kết thúc ≥18:00 → "17:30–18:00 (1 lần)"; 2–<3h: bắt đầu 17:00 → "17:00–17:10", hoặc 07:35–07:45; còn lại
  "Theo bố trí của bộ phận". Báo cơm: ≥3h → Cơm/Không; 2h → Ăn nhẹ/Không; <2h → Không.
- Hạn đăng ký (`regType`) tính theo thời điểm tạo; sửa mà đổi suất ăn → tính lại thời điểm.
- Duyệt: NV → Leader duyệt; Leader → TP duyệt; TP tự ghi nhận (đã duyệt). Từ chối bắt buộc lý do. Có hoàn tác.
- Leader/TP có tab: Của tôi · Duyệt (NV/Leader) · Tổng hợp suất ăn (số người, suất cơm, ăn nhẹ, phụ cấp, chi phí dự
  kiến, danh sách, chờ duyệt, trễ hạn). TP: **Gửi đăng ký suất ăn cho Phòng HCNS** (lưu số liệu; gửi lại cập nhật).

## 4. Leader (5 tab)

1. **Tổng quan** (`ldOverview`): KPI (Dự án nhóm, Nhân sự làm việc/tổng, Hoàn thành, Đang làm, Chậm — tính trên việc
   đã chọn trong phạm vi), thông báo số mục chờ duyệt (bấm → Kế hoạch & duyệt / Chờ duyệt), bảng tình hình dự án
   (`pjTable`), donut công việc hôm nay, phát sinh hôm nay.
2. **Kế hoạch & duyệt** (`ldPlan`) — tab con:
   - *Kế hoạch* (`ldApprove`): lọc Nhân viên đề xuất / Chờ duyệt, lọc dự án; DS chưa gửi; mỗi kế hoạch: meta, bảng việc,
     ghi chú, góp ý; Duyệt / Từ chối (bắt buộc lý do) / Góp ý; Hoàn tác (khi TP chưa quyết). Phát sinh chờ duyệt: Duyệt/Từ chối.
   - *Báo cáo* (`ldEodApprove`): đề xuất chuyển kế hoạch (`carrySection`: Duyệt, Duyệt tất cả, Sửa = sửa + duyệt luôn,
     Hoàn tác); DS chưa gửi báo cáo; mỗi báo cáo: hoàn thành x/y, chậm, vướng mắc, phát sinh, việc + kết quả + đề xuất
     chuyển; Duyệt (đồng thời duyệt mọi đề xuất chuyển của NV đó) / Yêu cầu bổ sung (bắt buộc nội dung) / Hoàn tác.
   - *Lịch tuần* (`ldWeek`): bảng NV × 7 ngày (số việc / Nghỉ), chọn NV → việc từng ngày (xóa ✕), form **Thêm việc**
     (dự án, đầu mục, nội dung, ngày trong tuần, giờ) → việc `KeHoach` chưa chọn.
   - *Dự án & đầu mục* (`ldItems`): chip dự án + "＋ Dự án mới"; form dự án (tên, nhà máy, địa điểm, bắt đầu, kết thúc,
     nhu cầu nhân sự/ngày, trạng thái tự động/ghi đè, "Đã bàn giao"); bảng đầu mục (đổi trạng thái trực tiếp, xóa) +
     thêm đầu mục (tên, bắt đầu, kết thúc).
   - *Nhân sự* (`ldPeople`): Leader → nhóm mình, nút Báo nghỉ/Bỏ nghỉ (lý do) theo ngày, bấm tên NV → xem lịch tháng;
     TP → mọi tài khoản + **Sửa** (họ tên, mã, vai trò, nhóm, cấp bậc, đặt lại mật khẩu, xóa) + **Tạo tài khoản**.
   - *Phân công* (`ldAssign` — trong artifact không có tab, bản này thêm tab con): giao việc 1 NV, việc đã giao 7 ngày
     qua (xóa), phân công theo NV (bỏ giao), theo dự án.
3. **Theo dõi** (`ldTrack`): theo nhân viên (lọc dự án; tiến độ x/y; trạng thái kế hoạch; vướng mắc; mở xem việc) /
   theo dự án; phát sinh hôm nay + "＋ Thêm phát sinh" (Leader thêm = duyệt ngay) + Duyệt/Từ chối.
4. **Báo cáo TP** (`ldReport`): Báo cáo ngày (KPI, tiến độ dự án, vấn đề nổi bật `issuesOf`, kế hoạch ngày mai: nhu
   cầu — sửa trực tiếp — / đã bố trí / chênh lệch, ghi chú, **Gửi báo cáo cho TP** lưu ảnh chụp số liệu) / Báo cáo tuần (`weekReport`).
5. **Làm thêm** (mục 3.5).

## 5. Trưởng phòng (5 tab)
1. **Tổng quan** (`tpOverview`): KPI (dự án, nhân sự, tiến độ TB, vấn đề, phát sinh), báo cáo Leader đã gửi chưa,
   bảng tiến độ, biểu đồ kế hoạch vs thực tế theo dự án, donut nhân sự (đang làm / chờ phân công / nghỉ), vấn đề.
2. **Phê duyệt** (`tpApprove`): Hỗ trợ nhân sự (kế hoạch cần TP; lọc Chờ duyệt/Đã duyệt/Từ chối/Tất cả; Duyệt / Từ chối
   bắt buộc lý do — chỉ khi Leader đã duyệt) / Phát sinh (chỉ theo dõi, Leader duyệt).
3. **Dự án**: chip dự án + chi tiết dự án (như NV).
4. **Báo cáo**: danh sách báo cáo ngày Leader gửi (KPI, bảng dự án, vấn đề, nhân sự ngày mai, ghi chú) / Báo cáo tuần.
5. **Làm thêm**.

## 6. Luật tính

- `pjPct` = % đầu mục Hoàn thành. `pjPlanPct(date)` = % đầu mục có ngày kết thúc **trước** `date`.
- `pjStatus`: đóng hoặc (có đầu mục và 100%) → Hoàn thành; có override → override; chênh = KH − TT:
  ≤ 0 Đạt · ≤ 10 Đang làm · ≤ 25 Chậm · > 25 Nguy cơ.
- `need(date, pid)` = nhu cầu ghi đè theo ngày, mặc định `Project.Need`. `onSite(date, pid)` = số người có việc ở dự
  án trong ngày (mọi việc, không chỉ việc đã chọn) và không bị ghi nghỉ.
- `dayCounts(date)` đếm việc **đã chọn** trong phạm vi. `working(date)` = NV (staff) không nghỉ và có việc.
- `issuesOf(d)`: phát sinh chờ duyệt; NV báo vướng mắc; dự án Nguy cơ; dự án thiếu nhân sự ngày mai.
- `exSt` (phát sinh): Chờ duyệt / Từ chối / đã duyệt → theo việc tạo ra: Hoàn thành → "Hoàn thành", Chưa làm → "Đã duyệt",
  khác → "Đang xử lý".
- Badge thanh tab: Leader "Kế hoạch & duyệt" = kế hoạch chờ + phát sinh chờ + báo cáo chờ + đề xuất chuyển chờ;
  TP "Phê duyệt" = kế hoạch chờ TP; "Làm thêm" (Leader/TP) = đăng ký chờ mình duyệt; NV "Báo cáo cuối ngày" = phát sinh chờ.

## 7. Giờ & ngày
- Múi giờ Việt Nam (UTC+7). Giờ hành chính 08:00–12:00, 13:00–17:00. Chọn giờ công việc: 00:00–24:00 bước 30 phút.
- Tuần bắt đầu Thứ 2. Chủ nhật là ngày nghỉ (gợi ý OT ngày nghỉ; ngày làm việc liền trước bỏ qua CN).

## 8. Giao diện
- Mobile-first, giống artifact: thanh trên (logo SORA, tài khoản, nút Thoát, nút chọn giao diện, ô "Xem nhân viên/Leader"),
  thanh tab dưới có badge, màu nhấn xanh (TP: tím). CSS port nguyên trong `wwwroot/css/site.css`.

## 9. Khác biệt có chủ đích so với artifact
- Trường "…By" (người duyệt/giao) ghi **tên tài khoản thật sự thao tác** (artifact ghi tên người đang được xem).
- Báo cáo Leader gửi TP khóa theo (ngày, Leader) — artifact chỉ 1 báo cáo/ngày cho cả phòng.
- `issuesOf`: vướng mắc của NV chỉ tính trong phạm vi đang xem.
- Mật khẩu băm PBKDF2 (artifact: SHA-256 có muối cố định). Cookie đăng nhập 30 ngày.
- Xóa tài khoản = xóa mềm (lịch sử giữ nguyên). Không có dữ liệu mẫu / nút "Xóa dữ liệu mẫu".
- Không đồng bộ trực tiếp (realtime): dữ liệu cập nhật khi tải lại trang / sau mỗi thao tác.
- Leader/TP thao tác ở giao diện Leader của người khác (TP chọn Leader X) thì phạm vi = nhóm X; "Làm thêm → Của tôi"
  chỉ sửa được khi người đang xem là chính mình.
