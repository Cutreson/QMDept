# Đặc tả nghiệp vụ — Quản lý công việc phòng QLCL&BD (SORA)

Nguồn: artifact "Quản lý công việc phòng QLCL&BD" (https://claude.ai/artifact/1EVpUiV9HUGFyWTHz2XJH1) — một SPA
điện thoại (HTML + JS, dữ liệu dùng chung). Bản ASP.NET này port **toàn bộ** màn hình và luật của artifact.
Tên hàm JS gốc được ghi trong ngoặc `()` để tra lại khi cần (mã nguồn artifact: đọc bằng công cụ Artifact `read`).
Những điểm khác artifact được ghi rõ ở mục 9.

## 1. Vai trò & quyền

| Vai trò | Mã | Thấy giao diện | Ghi chú |
|---|---|---|---|
| Nhân viên Setup | `Staff` | Nhân viên (của chính mình) | có cấp bậc Tech 1/2/3, thuộc nhóm của 1 Leader |
| Leader Setup | `Leader` | Leader + "Lịch nhân viên" (NV trong nhóm, chỉ xem + giao việc) | |
| Trưởng phòng | `Manager` | TP + Leader (chọn Leader để xem theo nhóm) + Lịch nhân viên (mọi NV) | quản lý tài khoản |
| Quản trị (bổ sung) | `Admin` | **toàn bộ giao diện TP** (đủ quyền TP) + tab **Tài khoản** (`/Admin/Accounts`) + **xem màn hình của mọi người** (chỉ xem) | cao hơn TP: mọi chức năng TP + tạo/sửa/xóa/khôi phục tài khoản, **đặt mật khẩu trực tiếp** |

- Vai trò Quản trị KHÔNG có trong artifact — chủ dự án yêu cầu ngày 2026-10-13 ("admin có thể tạo và đặt mật khẩu các tài khoản";
  "admin là tài khoản cao hơn trưởng phòng… bao trùm các chức năng trưởng phòng"). Trang Tài khoản: một danh sách, mỗi tài khoản
  có trạng thái Đang hoạt động / Đã xóa; tài khoản đã xóa không hiện ở màn hình nghiệp vụ nào khác.
- Quy tắc đặt mã/tên của chủ dự án: tên = mã, dạng `tp`, `tp.leaderN`, `tp.leaderN.nvM`. Mã lưu **chữ thường**
  (artifact dùng chữ HOA), đăng nhập không phân biệt hoa/thường (`AccountRules.NormalizeCode`). Tài khoản đã tạo trên DB
  thật: xem `docs/handoff.md` (mật khẩu ở `docs/tai-khoan.local.md`, chỉ có trên máy chủ dự án).
- Đăng nhập: mã NV + mật khẩu (tối thiểu 1 ký tự — yêu cầu 2026-10-14). Lần đầu: mật khẩu nhập vào thành mật khẩu. Quên: TP đặt lại.
- Chưa có TP nào (và chưa có Quản trị) → màn hình "Thiết lập tài khoản Trưởng phòng" tạo TP đầu tiên.
- TP tạo/sửa/xóa tài khoản, cấp mã, xếp nhóm (`leader`), cấp bậc (như artifact; "đặt lại mật khẩu" = người dùng tự đặt
  ở lần đăng nhập tới). Quản trị làm được mọi việc đó + đặt mật khẩu trực tiếp + khôi phục tài khoản đã xóa.
- **Xem màn hình cấp dưới (yêu cầu 2026-10-13, thay cơ chế "chọn giao diện" của artifact)**: header có combobox
  "Xem màn hình" — Quản trị chọn được TP / Leader / NV, TP chọn Leader / NV, Leader chọn NV trong nhóm. Chọn một người →
  thấy đúng các màn hình (đủ các tab) của người đó, **chỉ xem**, mọi thao tác bị chặn (cả phía server). Nút **"Lịch nhóm"**
  cạnh combobox (không nằm trong combobox) = lịch tháng cả nhóm của artifact (`nvMonth` all mode) với form **Giao thêm việc**
  (Leader / TP / Quản trị).
  **Ngoại lệ (yêu cầu 2026-10-14): Leader chọn một NV chỉ thấy LỊCH THÁNG của NV đó** (không có báo cáo đầu / cuối ngày, thông tin dự án,
  làm thêm; mở trang khác → chuyển về lịch tháng); Leader còn lại chỉ có lịch tháng cả nhóm. TP / Quản trị vẫn xem đủ màn hình.
  Ở lịch tháng cả nhóm, việc nhiều người cùng làm chỉ hiện "Leader dự án: …" ở dòng của người được chọn làm Leader dự án (người hỗ trợ không hiện).
  Mỗi người chỉ thao tác trong giao diện của chính mình: TP không còn thao tác thay Leader trong giao diện Leader như artifact;
  TP có thêm tab **Nhân sự** (tài khoản, nhóm, cấp bậc, báo nghỉ — trước đây nằm trong giao diện Leader).
- `inScope(emp)`: ở giao diện Leader chỉ tính Leader đang xem + NV trong nhóm; các giao diện khác: tất cả.

## 2. Đối tượng dữ liệu (xem docs/database.md cho cột)

people · projects (prj) · plan_items (items: đầu mục kế hoạch dự án) · assignments (assign: vai trò NV trong dự án
Leader dự án / Hỗ trợ dự án) · work_tasks (tasks: việc theo ngày của NV) · day_plans (dayPlans: báo cáo đầu ngày) ·
day_reports (dayReports: báo cáo cuối ngày) · leaves (Leader báo nghỉ cho NV) · project_needs (needs: nhu cầu nhân sự
theo ngày) · extras (phát sinh) · leader_reports · overtimes (ot) · ot_sends · photos.

### Công việc (task)
- Trạng thái: Chưa làm / Đang làm / Hoàn thành / Chậm.
- Nguồn (`TaskSource`): `Planned` (Leader xếp lịch tuần), `Unplanned` (NV tự thêm đầu ngày — "Ngoài kế hoạch"),
  `Extra` (phát sinh được duyệt), `CarryOver` (chuyển từ việc chưa xong), `Assigned` (Leader/TP giao; nhãn "Leader giao").
- `Selected`: NV chọn làm trong ngày ở báo cáo đầu ngày.
- **Hiện trên lịch tháng (`okCal`)**: Ngoài kế hoạch → chỉ khi Leader đã duyệt kế hoạch đầu ngày có chứa việc đó;
  Chuyển tiếp → chỉ khi `CarryOk` (Leader duyệt chuyển); nguồn khác → luôn hiện.

## 3. Quy trình hằng ngày của Nhân viên (4 tab + "ĐK làm thêm tại CTy" — tên tab đổi 2026-10-15, tiêu đề trang "Đăng ký làm thêm tại công ty")

### 3.1 Kế hoạch tháng (`nvMonth`)
- Lưới tháng (T2→CN). Mỗi ô: số việc đã duyệt (`okCal`) trong ngày. Màu: không việc = xám; xong hết = xanh lá;
  có việc Chậm = đỏ nhạt; hôm nay = xanh đậm; còn lại = xanh nhạt. Bấm ô → chọn ngày, hiện danh sách việc ngày đó.
- Leader/TP xem một NV: chỉ xem. Chế độ "Tất cả nhân viên": ô = số NV có việc / tổng việc / số người nghỉ (N);
  ô đỏ nếu có việc Chậm; chọn ngày → danh sách việc từng người (`teamDayList`) + form **Giao thêm việc** (`calAssignForm`):
  dự án, đầu mục (hoặc tự nhập), giờ bắt đầu/kết thúc (bước 30'), tích người tham gia (để trắng, Leader tự tích; nhãn vai trò tự động theo dự án, bị khóa).
- **NV xem lịch nhóm + đề xuất lịch (yêu cầu 2026-10-15)**: NV có nút "Lịch của tôi / Lịch nhóm" (lịch tháng của các NV cùng nhóm, chỉ xem,
  `?as=all` — chỉ hiệu lực khi bấm rõ). Chọn một ngày ở lịch của mình → **＋ Đề xuất công việc** (một nút duy nhất, **chỉ chọn được các dự án mình làm Leader dự án** — yêu cầu 2026-10-15, kể cả việc của chính mình; kèm ô chọn **người hỗ trợ** NV / Leader dạng ô chọn mở ra như ô chọn dự án (chọn nhiều người → hiện thành thẻ ✕; không có ô gõ lọc). Danh sách có thêm nhóm **Phòng ban**: Thiết kế cơ, Thiết kế điện, Lắp ráp cơ, Lắp ráp điện (`Fmt.SupportDepartments`, lưu ở `calendar_proposals.Departments`; khi duyệt ghi vào tên việc "· Phòng hỗ trợ: …" vì phòng ban không có tài khoản); nút chỉ hiện khi mình là Leader dự án của ít nhất một dự án). Với
  dự án mình là Leader dự án. Đề xuất (`calendar_proposals` + `calendar_proposal_people`) ở
  trạng thái Chờ duyệt, hiện trong "Đề xuất của tôi" (nút đề xuất có cả ở "Lịch của tôi" và "Lịch nhóm"); **Leader nhóm duyệt (tab "Đề xuất lịch") thì việc mới được ghi vào lịch NV + lịch nhóm**
  (nguồn "Đề xuất", `WorkflowService.DecideProposalAsync`), từ chối bắt buộc lý do. Thông báo `CalendarProposed` / `CalendarDecided`.
- **Leader tự đặt lịch**: ở lịch nhóm, danh sách người (và form Giao thêm việc) có cả Leader — Leader tích chính mình để tự thêm việc vào lịch của leader
  và lịch nhóm, không cần duyệt.

### 3.2 Thông tin dự án (`nvProjects`, `projectDetail`)
- **Chỉ hiện dự án mà NV được chọn làm Leader dự án** (yêu cầu 2026-10-14; artifact hiện mọi dự án). Dự án chỉ hỗ trợ / không tham gia
  không có trong danh sách, mở thẳng `?pj=` cũng bị chuyển về danh sách.
- 2 nhóm: Đang triển khai / Đã hoàn thành (đóng hoặc 100%). Thẻ dự án: màu, tên, trạng thái, nhà máy – địa điểm,
  nhãn "Leader dự án", thanh tiến độ, ngày.
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
   "＋ Thêm công việc ngoài kế hoạch" (dự án, đầu mục hoặc tự nhập, giờ) → tạo việc `Unplanned` (đã chọn).
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
  chưa hoàn thành ("Lý do chưa hoàn thành"). (Không có ảnh hiện trường — web chỉ dùng văn bản, yêu cầu 2026-10-13.)
  Chưa hoàn thành → bắt buộc **Kế hoạch tiếp theo**: ngày (14 ngày kế tiếp, CN ghi "ngày nghỉ"), giờ, nội dung →
  tạo/cập nhật việc `CarryOver` (`CarryOk=false`, chờ Leader duyệt); đổi lại thành Hoàn thành → xóa việc chuyển tiếp
  nếu nó còn "Chưa làm".
  Đầu mục: tích "đã xong" + Hoàn thành → đầu mục Hoàn thành (ngày xong, người xong); ngược lại nếu việc
  Đang làm/Hoàn thành/Chậm mà đầu mục đang Chưa làm → đầu mục Đang làm.
- **Chỉnh giờ làm (yêu cầu 2026-10-15)**: ở "Cập nhật" mỗi hạng mục NV (và Leader dự án khi báo cáo thay người hỗ trợ) sửa được giờ bắt đầu / kết thúc
  thực tế của việc; người hỗ trợ sửa giờ ngay cạnh nút "Đã hỗ trợ". Kết thúc phải sau bắt đầu.
- **Vai trò trong dự án (yêu cầu 2026-10-14)** — Leader dự án do Leader / TP chọn ở tab "Leader dự án"; mọi người còn lại có việc ở dự án đó là **người hỗ trợ**:
  - **Người hỗ trợ** (dự án đã có Leader dự án, người làm không phải Leader dự án): ở Báo cáo cuối ngày chỉ **tích "Đã hỗ trợ"** cho từng việc
    (`work_tasks.Supported`, POST `/Staff/TickSupport`); không có Cập nhật / kế hoạch tiếp theo. Gửi báo cáo bị chặn nếu còn việc chưa tích.
  - **Leader dự án** chịu trách nhiệm mọi vấn đề / nội dung của dự án: báo cáo đầy đủ (hoàn thành / chưa / đổi kế hoạch = kế hoạch tiếp theo, tích đầu mục
    xong) cho việc của mình **và** cho việc của người hỗ trợ trong dự án mình phụ trách (mục "Việc của người hỗ trợ trong dự án bạn phụ trách");
    chưa báo hết thì không gửi được báo cáo cuối ngày. Kết quả ghi lên chính việc của người hỗ trợ (kế hoạch tiếp theo tạo cho người đó).
  - Dự án **chưa chọn Leader dự án**: mọi người báo cáo đầy đủ như trước.
  - Mỗi dự án chỉ **một** Leader dự án: chọn người mới thì người cũ về "Hỗ trợ" (`WorkflowService.UpsertAssignmentAsync`).
  - Giao việc (lịch nhóm "Giao thêm việc", Phân công): **không còn ô chọn vai trò** — chọn dự án thì nhãn vai trò tự đổi và bị khóa (Leader dự án / "Hỗ trợ"), còn ô tích người tham gia để trắng — Leader tự tích người được giao;
    server gán vai trò theo dự án (`WorkflowService.AssignTaskAsync`).
  - Mọi dòng việc (`Blocks.TaskRow`: lịch tháng của NV, Theo dõi, Báo cáo) và thẻ việc ở Báo cáo cuối ngày hiện **"Leader dự án: <tên>"** (hoặc "chưa chọn");
    ở lịch nhóm của Leader chỉ dòng của Leader dự án hiện nhãn này.
- **＋ Đề xuất công việc phát sinh** (`openExtra`): dự án, tên, mô tả/lý do, giờ bắt đầu (mặc định giờ hiện tại làm tròn
  30'), kết thúc (có thể "Chưa rõ"). Leader duyệt → tự vào danh sách việc hôm nay.
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
     (dự án, đầu mục, nội dung, ngày trong tuần, giờ) → việc `Planned` chưa chọn.
   - *Dự án & đầu mục* (`ldItems`): chip dự án + "＋ Dự án mới"; form dự án (tên, nhà máy, địa điểm, bắt đầu, kết thúc,
     nhu cầu nhân sự/ngày, trạng thái tự động/ghi đè, "Đã bàn giao"); bảng đầu mục (đổi trạng thái trực tiếp, xóa) +
     thêm đầu mục (tên, bắt đầu, kết thúc).
   - *Nhân sự* (`ldPeople`): Leader → nhóm mình, nút Báo nghỉ/Bỏ nghỉ (lý do) theo ngày, bấm tên NV → xem lịch tháng;
     TP → mọi tài khoản + **Sửa** (họ tên, mã, vai trò, nhóm, cấp bậc, đặt lại mật khẩu, xóa) + **Tạo tài khoản**.
   - *Leader dự án* (`/Planning/ProjectLeads` — bổ sung 2026-10-14): mỗi dự án đang triển khai có một ô chọn NV trong nhóm làm Leader
     dự án (hoặc "Chưa chọn"). Dự án có Leader dự án mới hiện ở "Thông tin dự án" của người đó (NV chỉ thấy dự án mình làm Leader dự án);
     người cũ về "Hỗ trợ"; cả hai nhận thông báo. TP / Quản trị: nút "Chọn Leader dự án" ở trang Dự án (chọn trong mọi NV).
   - *Phân công* (`ldAssign` — trong artifact không có tab, bản này thêm tab con): giao việc 1 NV, việc đã giao 7 ngày
     qua (xóa), phân công theo NV (bỏ giao), theo dự án.
3. **Theo dõi** (`ldTrack`): theo nhân viên (lọc dự án; tiến độ x/y; trạng thái kế hoạch; vướng mắc; mở xem việc) /
   theo dự án; phát sinh hôm nay + "＋ Thêm phát sinh" (Leader thêm = duyệt ngay) + Duyệt/Từ chối.
4. **Báo cáo TP** (`ldReport`): Báo cáo ngày (KPI, tiến độ dự án, vấn đề nổi bật `issuesOf`, kế hoạch ngày mai: nhu
   cầu — sửa trực tiếp — / đã bố trí / chênh lệch, ghi chú, **Gửi báo cáo cho TP** lưu ảnh chụp số liệu) / Báo cáo tuần (`weekReport`).
5. **Làm thêm** (mục 3.5).

## 5. Trưởng phòng (5 tab của artifact + Nhân sự)
1. **Tổng quan** (`tpOverview`): KPI (dự án, nhân sự, tiến độ TB, vấn đề, phát sinh), báo cáo Leader đã gửi chưa,
   bảng tiến độ, biểu đồ kế hoạch vs thực tế theo dự án, donut nhân sự (đang làm / chờ phân công / nghỉ), vấn đề.
2. **Phê duyệt** (`tpApprove`): Hỗ trợ nhân sự (kế hoạch cần TP; lọc Chờ duyệt/Đã duyệt/Từ chối/Tất cả; Duyệt / Từ chối
   bắt buộc lý do — chỉ khi Leader đã duyệt) / Phát sinh (chỉ theo dõi, Leader duyệt).
3. **Dự án**: chip dự án + chi tiết dự án (như NV).
4. **Báo cáo**: danh sách báo cáo ngày Leader gửi (KPI, bảng dự án, vấn đề, nhân sự ngày mai, ghi chú) / Báo cáo tuần.
5. **Làm thêm**.
6. **Nhân sự** (bổ sung): danh sách mọi tài khoản (trừ Quản trị), Sửa / Tạo tài khoản, báo nghỉ — chính là ldPeople/openPerson
   của artifact (artifact đặt trong giao diện Leader → Kế hoạch & duyệt → Nhân sự).

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
- Yêu cầu thêm của chủ dự án (2026-10-13): **phù hợp cả máy tính và điện thoại**. Máy tính (≥1024px): header một hàng,
  thanh tab thành menu dọc bên trái, nội dung rộng, các trang tổng quan/duyệt/báo cáo xếp thẻ 2 cột; máy tính bảng: cột rộng
  hơn, vẫn thanh tab dưới. Chi tiết: `docs/kien-truc.md` mục "Giao diện responsive".

## 9. Khác biệt có chủ đích so với artifact
- Trường "…By" (người duyệt/giao) ghi **tên tài khoản thật sự thao tác** (artifact ghi tên người đang được xem).
- Báo cáo Leader gửi TP khóa theo (ngày, Leader) — artifact chỉ 1 báo cáo/ngày cho cả phòng.
- `issuesOf`: vướng mắc của NV chỉ tính trong phạm vi đang xem.
- Mật khẩu băm PBKDF2 (artifact: SHA-256 có muối cố định). Cookie đăng nhập 30 ngày.
- Xóa tài khoản = xóa mềm (lịch sử giữ nguyên). Không có dữ liệu mẫu / nút "Xóa dữ liệu mẫu".
- Không đồng bộ dữ liệu trực tiếp (realtime) như artifact: dữ liệu cập nhật khi tải lại trang / sau mỗi thao tác; thay vào đó
  có **thông báo đẩy SignalR** (mục 10) báo ngay khi có việc liên quan.
- Không có ảnh hiện trường / đính kèm (web chỉ dùng văn bản — yêu cầu chủ dự án).
- Leader/TP thao tác ở giao diện Leader của người khác (TP chọn Leader X) thì phạm vi = nhóm X; "Làm thêm → Của tôi"
  chỉ sửa được khi người đang xem là chính mình.
- Server chặn những thao tác artifact chỉ "ẩn nút": quyết định lại kế hoạch/báo cáo đã có quyết định Leader; Hoàn tác khi TP
  đã quyết định; TP duyệt kế hoạch chưa ở trạng thái "Chờ TP duyệt"; sửa / gửi lại / thêm việc ngoài kế hoạch cho kế hoạch
  **đã duyệt** ("Kế hoạch đã được duyệt, không sửa được nữa"); giao việc / thêm việc / phát sinh cho dự án **đã bàn giao**.
- "Kế hoạch tháng" chế độ một người khi nhóm trống hiện "Nhóm chưa có nhân viên." (artifact liệt kê nhầm việc của mọi người).
- TP → Phê duyệt → Phát sinh liệt kê tối đa 200 phát sinh mới nhất; TP → Báo cáo liệt kê tối đa 60 báo cáo mới nhất.
- TP Tổng quan hiện một thông báo cho mỗi Leader đã gửi báo cáo ngày + danh sách Leader "Chưa gửi".
- Duyệt làm thêm của NV chỉ liệt kê NV trong nhóm của Leader đang xem (NV chưa xếp nhóm không có ai duyệt — cần xếp nhóm).
- Nhân sự (TP): không tạo được tài khoản Trưởng phòng và không "đặt lại mật khẩu" / đổi mã của tài khoản TP (artifact cho phép) —
  tài khoản TP do Quản trị tạo kèm mật khẩu, tránh người khác đăng nhập trước để chiếm quyền.
- Mã tài khoản lưu chữ thường; có vai trò Quản trị (mục 1). **Quản trị bao trùm mọi chức năng TP** (yêu cầu 2026-10-13): dùng giao diện
  TP với đủ quyền (duyệt hỗ trợ nhân sự, duyệt làm thêm của Leader, gửi suất ăn HCNS, nhân sự…; đăng ký làm thêm của Quản trị được ghi
  nhận ngay như TP) + tab Tài khoản.
- Làm thêm: server bắt buộc giờ theo bước 30 phút (artifact chỉ giới hạn qua danh sách chọn) và báo lỗi khi khoảng giờ nằm trọn
  trong nghỉ trưa (artifact chấp nhận 0 giờ). Giờ phải đúng dạng HH:mm. Hai lần gửi cùng lúc trùng khóa → thông báo "Dữ liệu vừa được
  cập nhật ở nơi khác" thay vì lỗi.
- Việc chuyển tiếp được duyệt (duyệt / duyệt tất cả / sửa và duyệt / duyệt báo cáo) khi NV **đã gửi kế hoạch** ngày chuyển sang →
  việc được chọn sẵn và gắn vào kế hoạch ngày đó (như việc được giao / phát sinh đã duyệt; trừ khi NV đã gửi báo cáo cuối ngày
  đó — khi ấy việc chỉ hiện trong lịch), để NV vẫn cập nhật / báo cáo được kể cả
  khi kế hoạch đã được duyệt (artifact: việc hiện trong lịch nhưng NV không thao tác được). Hoàn tác / NV sửa đề xuất / đổi ngày →
  gỡ khỏi kế hoạch cũ.
- Duyệt phát sinh: dự án đã bàn giao → báo lỗi; hai người duyệt cùng lúc chỉ tạo một việc; giờ kết thúc "Chưa rõ" → 17:00, nếu bắt
  đầu từ 17:00 trở đi thì kết thúc = bắt đầu + 30 phút (artifact luôn 17:00, có thể kết thúc trước bắt đầu); giờ bắt đầu phát sinh không được là 24:00.
- Xóa tài khoản Leader → NV trong nhóm thành "Chưa xếp nhóm" (như khi Leader đổi vai trò). NV chưa xếp nhóm không có Leader
  duyệt kế hoạch / báo cáo — TP cần xếp lại nhóm: TP Tổng quan hiện cảnh báo "N nhân viên chưa xếp nhóm" (kèm tên, bấm mở Nhân sự).
  NV gắn với Leader đã xóa / không còn là Leader cũng hiện "Chưa xếp nhóm" (`Dept.GroupText`).
- Nhu cầu nhân sự (Leader → Kế hoạch ngày mai) chỉ nhận dự án đang triển khai.
- Việc ngoài kế hoạch thêm khi NV đang "Sửa kế hoạch và gửi lại" (kế hoạch đã gửi, chưa duyệt) chỉ được chọn khi NV gửi lại kế hoạch có tích việc đó
  (artifact: luôn chọn ngay) — để Leader duyệt đúng danh sách việc. Chưa gửi lại mà Leader đã duyệt thì việc đó không vào báo cáo cuối ngày.
- Tài khoản & mật khẩu (yêu cầu 2026-10-14): mã = tên + chữ cái đầu họ và đệm, không dấu, chữ thường (Nguyễn Văn Huề → `hue.nv`);
  mật khẩu mặc định của tài khoản mới / sau khi đặt lại = `Test@123` (artifact: để trống, đặt ở lần đăng nhập đầu); người dùng tự đổi mật khẩu ở
  trang Cài đặt (artifact: chỉ TP đặt lại).
- Tài khoản TP chỉ Quản trị sửa / hạ vai trò / đặt lại mật khẩu / xóa được (TP ở Nhân sự chỉ sửa tên chính mình) — chặn TP chiếm tài khoản TP khác.

## 10. Thông báo đẩy (bổ sung — yêu cầu 2026-10-13)

- Cơ chế SignalR (hub `/hubs/notify`): khi một thao tác cần người khác xử lý hoặc có kết quả, người liên quan nhận ngay thẻ
  thông báo trên trang (và thông báo của hệ điều hành nếu trình duyệt cho phép và tab đang ẩn). Bấm "Xem ›" để mở đúng màn hình.
- Trang **Cài đặt** (nút ⚙ trên header, `/Settings`): bật / tắt thông báo đẩy; chọn từng loại muốn nhận (chỉ hiện loại liên quan
  vai trò); cho phép thông báo của hệ điều hành; gửi thử. Mặc định: bật tất cả.

| Loại | Khi nào | Người nhận |
|---|---|---|
| Kế hoạch đầu ngày mới | NV gửi / gửi lại kế hoạch | Leader nhóm |
| Kết quả duyệt kế hoạch | Leader duyệt / từ chối / góp ý / hoàn tác; TP duyệt / từ chối hỗ trợ | NV (+ Leader khi TP quyết định) |
| Yêu cầu hỗ trợ nhân sự | Leader duyệt kế hoạch cần hỗ trợ nhóm khác / phòng ban khác | mọi TP |
| Báo cáo cuối ngày mới | NV gửi báo cáo cuối ngày | Leader nhóm |
| Kết quả duyệt báo cáo | Leader duyệt / yêu cầu bổ sung / hoàn tác | NV |
| Đề xuất phát sinh mới | NV đề xuất phát sinh | Leader nhóm |
| Kết quả phát sinh | phát sinh được duyệt / từ chối; Leader thêm phát sinh cho NV | NV |
| Kế hoạch chuyển tiếp | duyệt / duyệt tất cả / sửa / hoàn tác chuyển tiếp (kể cả khi duyệt báo cáo) | NV |
| Giao việc | giao việc (lịch cả nhóm, Phân công), thêm việc lịch tuần, xóa việc | NV |
| Báo nghỉ | Leader / TP ghi nghỉ, bỏ nghỉ | NV |
| Đăng ký làm thêm cần duyệt | NV đăng ký (→ Leader nhóm), Leader đăng ký (→ mọi TP) | Leader / TP |
| Kết quả làm thêm | duyệt / từ chối / hoàn tác | người đăng ký |
| Hủy đăng ký làm thêm | NV / Leader hủy đăng ký (kể cả đã duyệt) | Leader nhóm / mọi TP |
| Đề xuất chuyển tiếp mới | NV đề xuất / sửa việc chuyển tiếp khi báo cáo cuối ngày đã được duyệt | Leader nhóm |
| Leader dự án | được chọn / không còn là Leader dự án | NV |
| Báo cáo ngày của Leader | Leader gửi / gửi lại báo cáo ngày | mọi TP |

Người tự thao tác không nhận thông báo của chính mình. Link trong thông báo kèm `as=me` để luôn mở màn hình của chính người nhận.

## 11. Ô tìm nhanh trong danh sách chọn (bổ sung — yêu cầu 2026-10-15)

Mọi `<select>` có từ 7 mục trở lên (dự án, nhân viên, tỉnh…) và danh sách tích chọn nhiều người (`.optbox`) tự có ô gõ chữ cái phía trên:
gõ → chỉ còn các mục chứa chữ đó (không phân biệt hoa / dấu, "dao" ra "Đào Minh Tài"). Mã ở `wwwroot/js/site.js` (`enhanceSearch`); bỏ qua select
giờ (`.mono`), `data-autosubmit`, `data-nosearch`.

## 12. Đề xuất công việc phát sinh ở lịch tháng — chỉnh lần 2 (yêu cầu 2026-10-15)

- Form chỉ có: Dự án (các dự án mình làm Leader dự án), **nội dung công việc phát sinh**, giờ bắt đầu / kết thúc — **không chọn đầu mục kế hoạch**.
- Xin người hỗ trợ: (a) **chỉ định người chỉ trong nhóm của mình** (NV cùng Leader + Leader của mình, ô chọn → thẻ ✕; server chặn người nhóm khác);
  (một ô chọn duy nhất: người trong nhóm / phòng ban / nhóm khác; chọn phòng ban hoặc nhóm khác → nhập **số lượng** rồi Thêm → thành thẻ "Phòng … ×N", lại chọn tiếp);
  (b) **xin theo số lượng**: từng phòng ban (Thiết kế cơ, Thiết kế điện, Lắp ráp cơ, Lắp ráp điện) và từng nhóm khác ("Nhóm <tên Leader>") — không chỉ
  định người; (c) **lý do xin hỗ trợ** (bắt buộc khi có xin người / số lượng, ≤ 500 ký tự).
- Lưu: `calendar_proposals.Departments` = "Thiết kế cơ=2;Nhóm Nguyễn Văn Huề=1", `SupportReason`. Leader duyệt thấy số lượng + lý do; khi duyệt, số lượng ghi vào
  tên việc "· Xin hỗ trợ: Thiết kế cơ ×2, Nhóm … ×1" (`Fmt.QuotaText`).

## 13. Giao thêm việc của Leader + đề xuất chuyển sang trưởng nhóm (yêu cầu 2026-10-15)

- Form "Giao thêm việc" (lịch nhóm) và "Giao việc" (tab Phân công): **không còn chọn đầu mục / "Việc khác"** — chỉ dự án + **nội dung công việc phát sinh** + giờ + người tham gia.
- Nhãn vai trò ở danh sách người tham gia: **trưởng nhóm (Leader) hiện "Leader"**, Leader dự án hiện "Leader dự án", còn lại "Hỗ trợ" (nhãn khóa, tự đổi theo dự án).
- Đề xuất lịch của NV **chuyển sang trưởng nhóm của người đề xuất để duyệt**: hiện ở thẻ "Đề xuất chờ duyệt (N)" ngay đầu lịch nhóm của Leader (Duyệt / Từ chối tại chỗ),
  tab con "Đề xuất lịch (N)", huy hiệu số trên tab "Kế hoạch & duyệt" và thông báo đẩy. Danh sách chờ duyệt nằm trong `DataCache` (`Dept.PendingProposals`).
- **Công việc của chính Leader dự án (bổ sung)**: form đề xuất có ô **"Tôi thực hiện (với vai trò Leader dự án)"**, mặc định tích. Tích → người đề xuất là người thực hiện (cùng
  những người hỗ trợ chọn thêm); bỏ tích thì chỉ những người được chỉ định trong nhóm làm — bỏ tích mà không chọn ai thì báo lỗi. Danh sách đề xuất hiện "Thực hiện: …".
- **Mở rộng cho mọi thành viên trong nhóm (yêu cầu 2026-10-15, thay yêu cầu "chỉ dự án mình làm Leader dự án")**: **mọi NV có nhóm** đều có nút "＋ Đề xuất công việc" và chọn
  được **mọi dự án đang triển khai** cho công việc của chính mình (Leader nhóm duyệt). Phần **"Tôi thực hiện" + xin người hỗ trợ / phòng ban / nhóm khác + lý do**
  chỉ hiện (và chỉ được server chấp nhận) với dự án mình làm Leader dự án (`WorkflowService.ProposeAsync`); dự án khác chỉ đề xuất việc của chính mình.
- **NV thêm việc ngoài kế hoạch (Báo cáo đầu ngày)**: bỏ ô "Đầu mục kế hoạch" — chỉ chọn dự án + nhập nội dung + giờ (yêu cầu 2026-10-15; `TaskFieldsVm.ShowItem = false`).
  Form Thêm việc ở Lịch tuần của Leader vẫn còn ô đầu mục.

## 14. Nhân sự hỗ trợ ở Báo cáo đầu ngày — cùng thiết kế với đề xuất công việc (yêu cầu 2026-10-15)

- Thay ô "Không cần hỗ trợ / Trong nhóm / Nhóm khác / Phòng ban khác" bằng **một ô chọn** (như ô người hỗ trợ ở đề xuất công việc): người trong nhóm của mình (+ Leader của mình),
  phòng ban (Thiết kế cơ / điện, Lắp ráp cơ / điện) và nhóm khác **theo số lượng** → thẻ ✕; bên dưới là **lý do xin người hỗ trợ** (bắt buộc khi có xin).
- Phạm vi duyệt **suy ra từ lựa chọn** (`StaffController.ResolveSupportAsync`): chỉ người trong nhóm → `Team` (Leader duyệt); có nhóm khác → `OtherTeam`; có phòng ban → `OtherDept`
  (hai loại sau cần thêm TP duyệt như cũ). Chỉ định người nhóm khác bị chặn.
- Lưu: `day_plans.SupportScope`, `SupportText` (câu ghép để hiển thị: "Người hỗ trợ: … · Phòng ban: … ×N · Nhóm khác: … — Lý do: …"), `SupportPick` (JSON để dựng lại thẻ khi sửa kế hoạch).
- **Cập nhật (yêu cầu 2026-10-15, "mọi NV có tính năng như Vũ Văn Lực")**: bỏ ràng buộc "chỉ Leader dự án mới xin hỗ trợ". Mọi NV có nhóm, ở dự án đang triển khai bất kỳ, đều dùng đủ:
  "Tôi thực hiện", xin người hỗ trợ trong nhóm / phòng ban / nhóm khác theo số lượng + lý do; Leader nhóm duyệt. (Phần phụ thuộc DỮ LIỆU — dự án hiện ở "Thông tin dự án",
  mục "Việc của người hỗ trợ trong dự án bạn phụ trách" — vẫn theo việc ai được chọn làm Leader dự án.)
- **Đảo lại (yêu cầu 2026-10-16)**: NV **chỉ xem và đề xuất những dự án mình được giao làm Leader dự án** — form đề xuất chỉ liệt kê các dự án đó, NV không là Leader dự án nào thì
  không có nút "＋ Đề xuất công việc"; máy chủ cũng chặn (`WorkflowService.ProposeAsync`). Trong dự án được giao, mọi NV đều dùng đủ "Tôi thực hiện" + xin người hỗ trợ / phòng ban / nhóm khác + lý do.
  (Thay cho mục "mở rộng cho mọi thành viên" phía trên về phạm vi dự án.) Đã kiểm tra 6 NV: Lực [Dây truyền dây, Máy kiểm tra tấm, Băng tải 1], Tài [Chiller], Hùng [Máy ép vòng bi],
  Hòa / Đoàn / Vinh không có dự án → không có nút.

## 15. Thứ tự tab con "Kế hoạch & duyệt" của Leader (yêu cầu 2026-10-16)

Kế hoạch → Lịch tuần → Phân công → Báo cáo → Đề xuất lịch → Dự án & đầu mục → Leader dự án → Nhân sự (`Views/Shared/_PlanningTabs.cshtml`).

## 16. Số lượng dự án ở mọi màn hình thông tin dự án (yêu cầu 2026-10-16)

Dòng "N dự án · X đang triển khai · Y đã hoàn thành" (`Dept.PjCountText`; đang triển khai = chưa bàn giao và chưa 100%): NV → Thông tin dự án (dưới tiêu đề; chỉ đếm dự án mình là Leader dự án),
TP → Dự án, Leader / TP → Dự án & đầu mục, bảng "Tình hình dự án" (Leader Tổng quan) và "Tiến độ dự án" (TP Tổng quan).

## 17. Ô chọn có tìm kiếm trong chính ô (sửa mục 11, yêu cầu 2026-10-16)

Không còn ô gõ riêng phía trên ô chọn (trước đây hiện 2 ô, vd. "Tỉnh / thành công tác"). Mọi `<select>` có ≥ 7 mục (tỉnh / thành, danh sách nhân viên…) thành **một ô duy nhất**:
bấm / gõ chữ cái ngay trong ô → danh sách chỉ còn mục chứa chữ đó (không phân biệt hoa / dấu), chọn bằng chuột hoặc ↑ ↓ Enter, Esc để đóng (`site.js: enhanceSelect`, lớp `.combo`).
`<select>` gốc vẫn nằm trong form (ẩn) nên giá trị / kiểm tra bắt buộc không đổi; danh sách < 7 mục vẫn là ô chọn thường; ô chọn người hỗ trợ (`data-pick-into`) giữ ô chọn thường.
Danh sách tích chọn nhiều người (`.optbox` ≥ 7 dòng) vẫn có ô lọc phía trên vì là danh sách tích.

## 18. Dữ liệu dự án nạp từ file "thông tin DA.xlsx" (yêu cầu 2026-10-16)

Công cụ: `tools/import-da.cs` (`gen` sinh SQL, `verify` so DB với file, `report` thống kê + điểm bất thường); nạp bằng `tools/dbquery.cs`. Đã nạp **88 dự án / 576 hạng mục** vào DB thật
và kiểm tra khớp 100% (so từng dự án + hạng mục; thêm một lượt đối chiếu độc lập bằng trình đọc khác).

- **Sheet "Dự án chế tạo"** (48 dự án, 355 hạng mục): Dự án → tên; Mã dự án → `Project.Code`; Khách hàng → Nhà máy (`Factory`); Tỉnh thành → Địa điểm (`Location`); Số lượng → `Quantity`;
  Leader → **Leader phụ trách** (`OwnerLeaderId`, tài khoản Leader trùng tên); Hạng mục → đầu mục; Ngày bắt đầu / kết thúc **TT (thực tế)** → ngày đầu mục.
- **[Đã BỎ 2026-10-16 — chủ dự án yêu cầu xóa dữ liệu dự án demo; chỉ còn sheet chế tạo, nạp bằng `import-da.cs gen … chetao`]** Sheet "Dự án demo + Nội bộ" (40 dự án có tên, 221 hạng mục): Trạng thái "Đã hoàn thành" → dự án **Đã bàn giao**; Dự án → tên (nguyên văn, gồm mã); Cấp độ → `Level`; Số lượng; Ngày PO / Ngày giao hàng theo PO
  (`PoDate` / `DeliveryDate`; nếu ghi dạng chữ như "02/04" thì đưa vào Ghi chú); Hạng mục → đầu mục; PIC → `PlanItem.Pic`; ngày **KH** → `PlanStart/PlanEnd`, ngày **TT** → ngày đầu mục (không có TT thì lấy KH);
  Tình trạng / Nguyên nhân chậm / NOTE → `PlanItem.Note`.
- Trạng thái đầu mục: sheet 2 theo cột Trạng thái hạng mục (hoặc Tiến độ thực tế = 1; dự án đã hoàn thành → mọi hạng mục xong); sheet 1 (không có cột trạng thái) suy theo ngày TT so với ngày nạp
  (kết thúc đã qua → Hoàn thành, đã bắt đầu → Đang làm, còn lại → Chưa làm). Ngày dự án = ngày sớm nhất / muộn nhất của các đầu mục (không có thì ngày PO / giao hàng). Nhu cầu nhân sự mặc định 2.
- **Điểm bất thường trong file** (giữ nguyên hoặc bỏ qua, đã báo chủ dự án): 4 hạng mục kết thúc trước bắt đầu (sheet 1 dòng 64, 142, 167, 169 — giữ nguyên); 3 ô ngày bắt đầu lỗi `#REF!`
  (dòng 66, 162, 196 — để trống); 4 dự án sheet 2 STT 41–44 không có tên → **không nạp**.
- Hiển thị: mã · nhà máy – địa điểm ở thẻ / tiêu đề dự án; mục "Thông tin dự án" có Mã, Số lượng, Leader phụ trách, Loại, Cấp độ, PO, Giao hàng, Ghi chú; hạng mục hiện PIC, ngày KH, ghi chú; danh sách > 12 dự án dùng
  ô chọn có tìm kiếm thay hàng nút; Leader chỉ thấy ở tab "Leader dự án" các dự án do mình phụ trách (+ dự án chưa có Leader phụ trách).
- **Nạp lại 2026-10-16**: xóa 88 dự án cũ rồi nạp lại **chỉ sheet "Dự án chế tạo"** (48 dự án, 355 hạng mục), đã kiểm khớp 100% (`verify … chetao` + đối chiếu độc lập). Dự án demo / nội bộ đã xóa khỏi DB.

## 19. Trưởng phòng chọn Leader phụ trách dự án (yêu cầu 2026-10-16)

TP / Quản trị: trang Dự án → nút **"Leader phụ trách dự án"** (`/Planning/ProjectOwners`, giống tab "Leader dự án" của Leader): mỗi dự án đang triển khai có một ô chọn Leader (nhóm) phụ trách (hoặc "Chưa chọn"),
bộ lọc "Chưa có Leader" + đếm. Lưu `projects.OwnerLeaderId`; Leader được chọn / người cũ nhận thông báo `ProjectOwner` ("Dự án giao nhóm phụ trách"). Leader được giao sẽ thấy dự án ở tab "Leader dự án"
(Leader chỉ thấy dự án do mình phụ trách + dự án chưa có Leader phụ trách) để chọn người làm Leader dự án trong nhóm mình. Chỉ TP / Quản trị đổi được (Leader gọi POST bị chặn).
- **Đính chính (2026-10-16)**: Trưởng phòng / Quản trị **không** chọn Leader dự án — đã bỏ nút "Chọn Leader dự án" ở trang Dự án và chặn `/Planning/ProjectLeads` + `SetProjectLead` với TP / Quản trị;
  việc chọn Leader dự án chỉ của Leader nhóm. TP / Quản trị chỉ chọn **Leader (nhóm) phụ trách** dự án (§19).
- **Tab "Leader dự án" chia 2 mục nhỏ (2026-10-16)**: "Đã có Leader dự án (N)" (`?f=has`) và "Chưa chọn Leader dự án (M)" (`?f=none`); mặc định mở mục đầu có dự án (đã có → chưa chọn).
  Chọn xong một dự án thì dự án chuyển từ mục "Chưa chọn" sang "Đã có".

### 19b. Giao thêm việc của Leader/TP thu gọn (yêu cầu 2026-10-16)
Ở "Lịch nhóm" / "Lịch của phòng" form "Giao thêm việc" không còn hiện sẵn: chỉ có nút **＋ Giao thêm việc**; bấm mới mở bảng trượt
(`<template id="calAssignTpl">` trong `Views/Staff/Month.cshtml`, cùng cơ chế `data-sheet` với "Đề xuất công việc" của NV). Nội dung form và
POST `/Staff/AssignTask` giữ nguyên (nhãn Leader dự án/Hỗ trợ tự đổi theo dự án, ô tích để trắng). CSS chỉ-xem: `[data-sheet]:not(.ro-ok)`.

### 19c. Báo cáo đầu ngày: đảo thứ tự + công tác tự điền (yêu cầu 2026-10-16)
Form kế hoạch đầu ngày: **1. Nội dung công việc** (việc trong lịch, thêm việc ngoài kế hoạch, nhân sự hỗ trợ, ghi chú) rồi **2. Trạng thái & báo cơm**.
NV chỉ chọn trạng thái (kể cả "Công tác trong ngày / dài ngày") và báo cơm; **Địa điểm công tác + Khách hàng không phải chọn nữa** — tự lấy từ
`Project.Location` / `Project.Factory` của các dự án có việc đang tích (staff.js đổ vào #tripPlace / #tripCustomer; `SubmitPlan` ghi lại vào
`TripProvince` / `TripCustomer` bằng `StaffReports.TripPlace`, nhiều dự án thì nối bằng "; " / ", ").

### 19d. Leader có báo cáo đầu ngày / cuối ngày như nhân viên (yêu cầu 2026-10-16)
- Nút "Lịch & báo cáo của tôi" (header Leader) → `/Staff/Month?as=mine` + thanh tab: Kế hoạch tháng, Báo cáo đầu ngày, Báo cáo cuối ngày — dùng lại đúng các trang / form của nhân viên (StaffController), `RequireSelfStaff` cho phép Leader ở chế độ này.
- Người duyệt là **Trưởng phòng**: tab "Phê duyệt" → "Của Leader" (`?mode=leader`): kế hoạch đầu ngày (Duyệt / Từ chối + lý do) và báo cáo cuối ngày (Duyệt / Yêu cầu bổ sung) của các Leader; có Hoàn tác. Thông báo đẩy `PlanSubmitted` / `ReportSubmitted` gửi tới Trưởng phòng.
- Chỉ định người hỗ trợ: Leader chọn được NV trong nhóm của mình (không chọn chính mình); nhóm khác / phòng ban xin theo số lượng như NV. Việc chuyển tiếp và phát sinh của Leader tự được duyệt (không có cấp duyệt riêng).
- Phần duyệt của Leader (kế hoạch, báo cáo, số chờ duyệt) không còn tính Leader — chỉ NV trong nhóm (`Dept.IsTeamStaff`).

### 19e. Sửa lại mục 19d (yêu cầu 2026-10-16)
Leader **không còn báo cáo cuối ngày**; "báo cáo đầu ngày" của Leader rút gọn thành **"Trạng thái làm việc trong ngày"** (trạng thái làm việc + nghỉ / công tác + báo cơm; không chọn việc, không xin hỗ trợ).
Header Leader: nút "Kế hoạch" duy nhất (gộp "Lịch của tôi" và "Lịch nhóm" — trong trang vẫn có thanh "Lịch của tôi | Lịch nhóm"). Trưởng phòng duyệt ở Phê duyệt → "Của Leader" (chỉ còn trạng thái làm việc). Đã bỏ phần duyệt báo cáo cuối ngày của Leader.

### 19f. Trạng thái làm việc của Leader có lại phần nội dung công việc (yêu cầu 2026-10-16)
Trang "Trạng thái làm việc" của Leader (`/Staff/Morning?as=mine`) dùng đúng form của nhân viên: 1. Nội dung công việc (việc trong lịch, "＋ Thêm công việc ngoài kế hoạch" chọn dự án + nội dung + giờ, nhân sự hỗ trợ trong nhóm mình, ghi chú), 2. Trạng thái & báo cơm (công tác tự điền địa điểm + khách hàng theo dự án). Vẫn không có báo cáo cuối ngày. Trưởng phòng duyệt ở Phê duyệt → Của Leader (có bảng việc).

Cập nhật 2026-10-16: trang Trạng thái làm việc của Leader bỏ phần Nhân sự hỗ trợ và Ghi chú (chỉ còn nội dung công việc + trạng thái & báo cơm).

Cập nhật 2026-10-16: tab "Trạng thái làm việc" của Leader nằm trong thanh tab của Màn hình của tôi (giữa Tổng quan và Kế hoạch & duyệt); nút "Kế hoạch" chỉ còn lịch tháng (Lịch của tôi | Lịch nhóm).

Cập nhật 2026-10-16: Leader chọn nhân viên để xem màn hình ở trang Kế hoạch (ô "Xem màn hình" dưới thanh Lịch của tôi | Lịch nhóm), không còn ở thanh trên. TP / Quản trị giữ ô ở thanh trên.

Cập nhật 2026-10-17: Leader — Kế hoạch tháng là tab con của Kế hoạch & duyệt (sau tab Kế hoạch); bỏ tab Lịch tuần và Phân công (giao việc làm ở Kế hoạch tháng → Lịch nhóm → Giao thêm việc); thanh trên của Leader không còn nút.

Cập nhật 2026-10-17: Leader ở Lịch của tôi có "＋ Cập nhật kế hoạch cá nhân" — thêm việc của chính mình vào ngày đang chọn (như Giao thêm việc nhưng chỉ cho mình; `TaskSource.Planned`), có thể xóa việc đã thêm.

### 19g. Làm thêm: nghiệm thu kết quả (yêu cầu 2026-10-19)
Luồng: NV đăng ký làm thêm → Leader duyệt → NV làm thêm, **cập nhật công việc đã triển khai + giờ thực tế** → Leader duyệt kết quả → **lưu trữ** (giờ được tính).
Làm chưa đến giờ đăng ký / vượt giờ đăng ký → hệ thống cảnh báo chênh lệch cho người duyệt; người duyệt **từ chối kết quả** (kèm lý do) → giờ làm thêm của người đó **không được tính** và phải cập nhật lại (gửi lại → chờ duyệt lại).
Leader làm thêm: TP duyệt cả đăng ký lẫn kết quả; TP tự ghi nhận (không cần duyệt). Tổng hợp suất ăn có thêm cột "Kết quả (giờ tính)" và dòng "Giờ làm thêm được tính" (chỉ kết quả đã duyệt).

Cập nhật 2026-10-19: Leader duyệt kế hoạch đầu ngày, đề xuất lịch và đăng ký làm thêm (kèm kết quả làm thêm) ở một chỗ: Kế hoạch & duyệt → Kế hoạch đề xuất, chia Chờ duyệt / Đã duyệt.

Cập nhật 2026-10-19: mục Nhân sự là sơ đồ quản lý hình tháp; Trưởng phòng đổi sơ đồ (chuyển nhân viên giữa các nhóm) thì nhân viên tự vào nhóm tương ứng và Leader liên quan nhận thông báo.

Cập nhật 2026-10-19: Kế hoạch đề xuất chia 2 mục chính (Đề xuất công việc; Đề xuất làm thêm tại công ty), mỗi mục có Chờ duyệt / Đã duyệt; tab nằm giữa Kế hoạch tháng và Báo cáo.

Cập nhật 2026-10-20: Leader chỉ thấy các dự án được TP giao cho nhóm phụ trách; TP có thống kê số dự án theo từng Leader và 2 mục Đã có Leader / Chưa có Leader.

Cập nhật 2026-10-20: Tổng quan của Trưởng phòng có 3 mục: Dự án, Nhân sự (ai đang làm, làm ở đâu, việc từng người, nghỉ), Kế hoạch tháng của phòng.

Cập nhật 2026-10-20: ô Xem màn hình chỉ có ở mục Kế hoạch tháng và chỉ xem được kế hoạch tháng của nhân sự được chọn (Leader hoặc nhân viên).

Cập nhật 2026-10-20: Trưởng phòng — Dự án tổng hợp nhanh; Nhân sự bảng + thống kê (thông tin Leader đã duyệt); Kế hoạch tháng gồm Lịch của tôi và Lịch phòng; giao việc chỉ xuống Leader (không vượt cấp).

## 20. Xin nghỉ trong Đề xuất công việc (yêu cầu 2026-10-20)
- Một nút "Đề xuất công việc / xin nghỉ" trên Lịch của tôi, hai thẻ Công việc | Xin nghỉ. Xin nghỉ **bắt buộc có lý do**; nghỉ phép theo buổi, nghỉ không lương theo giờ (≥ 1 giờ).
- NV → Leader nhóm duyệt; Leader → Trưởng phòng duyệt; từ chối phải nêu lý do. Duyệt ⇒ ghi `Leave` (chi tiết loại / buổi / giờ) hiện trên lịch.
- Nghỉ vài giờ / nửa ngày (đã duyệt) không loại người đó khỏi công việc trong ngày (`Dept.PartLeave`); chỉ nghỉ cả ngày mới là "Nghỉ" (`Dept.OnLeave`).
- Báo cáo cuối ngày của Leader chia 2 mục Chờ duyệt / Đã duyệt; đề xuất chuyển kế hoạch hiện ngay trong thẻ báo cáo.
