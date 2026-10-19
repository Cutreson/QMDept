#!/usr/bin/env bash
# Kiểm thử xin nghỉ + đề xuất công việc cho TOÀN BỘ nhân sự phòng (DB thật, dữ liệu thử có lý do bắt đầu bằng "zz" và được dọn cuối script).
# Chạy ở thư mục gốc repo khi web đang chạy ở http://localhost:5125 (thanh DEV bật). Xem docs/handoff.md (phiên 5).
export DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
B=http://localhost:5125; T=${TMPDIR:-/tmp}/lt; mkdir -p $T; fail=0
ok(){ echo "  ok  $*"; }; bad(){ echo "  !!  $*"; fail=1; }
login(){ J=$T/j$1; rm -f $J; curl -s -c $J -b $J -L "$B/Dev/As?id=$1" -o /dev/null; }
tok(){ curl -s -b $J -c $J "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"//'; }
post(){ local url=$1 turl=$2 t; shift 2; t=$(tok "$turl"); curl -s -b $J -c $J -L -o $T/out.html -w "%{http_code}" -X POST "$url" --data-urlencode "__RequestVerificationToken=$t" "$@"; }
sql(){ printf "%s\n" "$@" > $T/q.sql; dotnet run tools/dbquery.cs $T/q.sql 2>&1; }
page(){ local c; c=$(curl -s -b $J -c $J -L -o $T/p.html -w "%{http_code}" "$B$1"); if [ "$c" = 200 ] && ! grep -qi "exception\|Lỗi hệ thống" $T/p.html; then ok "GET $1"; else bad "GET $1 -> $c"; fi; }

echo "== A. Mở trang theo vai trò (toàn bộ phòng) =="
for id in 11 12 13 27 28 29; do login $id; echo "NV $id"; for p in "/Staff/Month?d=2026-11-03" "/Staff/Morning" "/Staff/Projects" "/Overtime" "/Settings"; do page "$p"; done; done
for id in 9 10 26; do login $id; echo "Leader $id"; for p in "/Leader/Overview" "/Leader/Overview?s=people" "/Staff/Month?as=mine&d=2026-11-03" "/Staff/Month?as=all" "/Planning/Proposals" "/Planning/Proposals?k=ot" "/Planning/Reports" "/Planning/Reports?f=approved" "/Planning/Projects" "/Planning/ProjectLeads" "/Planning/People" "/Staff/Morning?as=mine" "/Leader/Tracking" "/Overtime"; do page "$p"; done; done
for id in 8 5; do login $id; echo "TP/QT $id"; for p in "/Manager/Overview" "/Manager/Overview?s=people" "/Staff/Month?as=all" "/Staff/Month?as=mine" "/Manager/Approvals" "/Manager/Approvals?mode=leader" "/Planning/People" "/Planning/ProjectOwners" "/Overtime"; do page "$p"; done; done
login 5; page "/Admin/Accounts"

echo "== B. Nhân viên gửi xin nghỉ / đề xuất =="
declare -A LEAD=([11]=9 [12]=9 [29]=9 [13]=10 [27]=10 [28]=26) PJ=([11]=110 [12]=112 [13]=114 [27]=130 [29]=144)
i=0
for id in 11 12 13 27 28 29; do
  i=$((i+1)); login $id; d1=2026-11-0$i; d2=2026-11-1$i; d3=2026-11-2$i
  U() { echo "$B/Staff/ProposeCalendar?d=$1"; }
  M() { echo "$B/Staff/Month?d=$1"; }
  # 1) không có lý do -> bị chặn
  post "$(U $d1)" "$(M $d1)" -d "d=$d1&kind=leave&type=Paid&session=FullDay&reason=" >/dev/null
  # 2) nghỉ cả ngày có lý do
  post "$(U $d1)" "$(M $d1)" -d "d=$d1&kind=leave&type=Paid&session=FullDay&reason=zz nghi ca ngay $id" >/dev/null
  if [ -n "${PJ[$id]}" ]; then
    # 3) nghỉ buổi sáng + công việc trùng giờ nghỉ -> bị chặn
    post "$(U $d2)" "$(M $d2)" -d "d=$d2&kind=leave&type=Paid&session=Morning&reason=zz nghi sang $id&projectId=${PJ[$id]}&title=zz viec trung gio&start=08:00&end=12:00&self=1" >/dev/null
    # 4) nghỉ buổi sáng + công việc buổi chiều -> cả hai vào
    post "$(U $d2)" "$(M $d2)" -d "d=$d2&kind=leave&type=Paid&session=Morning&reason=zz nghi sang $id&projectId=${PJ[$id]}&title=zz viec buoi chieu $id&start=13:00&end=17:00&self=1" >/dev/null
  else
    post "$(U $d2)" "$(M $d2)" -d "d=$d2&kind=leave&type=Paid&session=Afternoon&reason=zz nghi chieu $id" >/dev/null
  fi
  # 5) nghỉ không lương 1 giờ, không công việc
  post "$(U $d3)" "$(M $d3)" -d "d=$d3&kind=leave&type=Unpaid&from=09:00&to=10:00&reason=zz di kham $id" >/dev/null
  # 6) đề xuất công việc thường (không nghỉ) vẫn chạy
  if [ -n "${PJ[$id]}" ]; then post "$(U 2026-11-30)" "$(M 2026-11-30)" -d "d=2026-11-30&kind=work&projectId=${PJ[$id]}&title=zz viec thuong $id&start=08:00&end=10:00&self=1" >/dev/null; fi
done
echo "== C. Leader xin nghỉ (kế hoạch cá nhân) =="
for id in 9 10 26; do
  login $id; curl -s -b $J -c $J "$B/Staff/Month?as=mine" -o /dev/null
  p=$( [ $id = 9 ] && echo 110 || ([ $id = 10 ] && echo 108 || echo 128) )
  d=2026-12-0$((id%9+1))
  post "$B/Staff/AddOwnTask?as=mine&d=$d" "$B/Staff/Month?as=mine&d=$d" -d "d=$d&kind=leave&type=Paid&session=Afternoon&reason=&projectId=$p&title=zz" >/dev/null
  post "$B/Staff/AddOwnTask?as=mine&d=$d" "$B/Staff/Month?as=mine&d=$d" -d "d=$d&kind=leave&type=Paid&session=Afternoon&reason=zz leader nghi chieu $id&projectId=$p&title=zz viec leader sang $id&start=08:00&end=12:00" >/dev/null
  d2=2026-12-1$((id%9+1))
  post "$B/Staff/AddOwnTask?as=mine&d=$d2" "$B/Staff/Month?as=mine&d=$d2" -d "d=$d2&kind=leave&type=Paid&session=FullDay&reason=zz leader nghi ca ngay $id" >/dev/null
done
echo "== D. Kết quả đã ghi (leave_requests / leaves / việc / đề xuất) =="
sql "SELECT Id,PersonId,Date,Status,LeaveType,LeaveSession,Reason FROM leave_requests WHERE Reason LIKE 'zz%' ORDER BY Id" | tee $T/lr.txt
sql "SELECT COUNT(*) FROM calendar_proposals WHERE Title LIKE 'zz%'" "SELECT COUNT(*) FROM work_tasks WHERE Title LIKE 'zz%'" | tail -2
echo "== E. Duyệt: Leader duyệt NV (cái đầu duyệt, cái thứ hai từ chối, còn lại duyệt), TP duyệt Leader =="
n=0
while IFS='|' read -r rid pid rest; do
  rid=$(echo $rid); pid=$(echo $pid); [ -z "$rid" ] && continue
  case "$pid" in 11|12|13|27|28|29) lead=${LEAD[$pid]}; login $lead; n=$((n+1)); v=ok; extra=""; [ $((n%4)) = 0 ] && { v=no; extra="&note=zz tu choi"; }
     post "$B/Planning/DecideLeave" "$B/Planning/Proposals" -d "id=$rid&v=$v$extra" >/dev/null ;;
     9|10|26) login 8; post "$B/Manager/DecideLeave" "$B/Manager/Approvals?mode=leader" -d "id=$rid&v=ok" >/dev/null ;;
  esac
done < <(grep -E '^[0-9]+ \|' $T/lr.txt)
sleep 2
sql "SELECT Id,PersonId,Date,Status,DecidedBy FROM leave_requests WHERE Reason LIKE 'zz%' ORDER BY Id" "SELECT EmployeeId,Date,LeaveType,LeaveSession,LeaveHours,Reason FROM leaves WHERE Reason LIKE 'zz%' ORDER BY EmployeeId,Date"
echo "== F. Dọn dữ liệu thử =="
sql "DELETE FROM calendar_proposal_people WHERE ProposalId IN (SELECT Id FROM calendar_proposals WHERE Title LIKE 'zz%')" "DELETE FROM calendar_proposals WHERE Title LIKE 'zz%'" "DELETE FROM leave_requests WHERE Reason LIKE 'zz%'" "DELETE FROM leaves WHERE Reason LIKE 'zz%'" "DELETE FROM work_tasks WHERE Title LIKE 'zz%'" | tail -3
echo "KẾT QUẢ: $([ $fail = 0 ] && echo ĐẠT || echo CÓ LỖI)"
