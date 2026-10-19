#!/usr/bin/env bash
# Kiểm thử luồng báo cáo cuối ngày cho TOÀN BỘ nhân viên (DB thật, dữ liệu thử có chữ "zz", dọn cuối script):
# gửi → chờ duyệt (khóa sửa, có báo cáo tổng hợp, chưa cho sao chép) → rút lại → gửi lại → Leader từ chối (mở lại để sửa) → gửi lại → Leader duyệt (hiện nút Sao chép, khóa hẳn).
export DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
B=http://localhost:5125; T=${TMPDIR:-/tmp}/rt; mkdir -p $T; fail=0; D=2026-10-30
ok(){ echo "  ok  $*"; }; bad(){ echo "  !!  $*"; fail=1; }
login(){ J=$T/j$1; rm -f $J; curl -s -c $J -b $J -L "$B/Dev/As?id=$1" -o /dev/null; }
tok(){ curl -s -b $J -c $J "$1" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed 's/.*value="//;s/"//'; }
post(){ local url=$1 turl=$2 t; shift 2; t=$(tok "$turl"); curl -s -b $J -c $J -L -o $T/out.html -w "%{http_code}" -X POST "$url" --data-urlencode "__RequestVerificationToken=$t" "$@" >/dev/null; }
sql(){ printf "%s\n" "$@" > $T/q.sql; dotnet run tools/dbquery.cs $T/q.sql 2>&1; }
ev(){ curl -s -b $J -c $J "$B/Staff/Evening?d=$D" | perl -CS -pe 's/&#x([0-9A-Fa-f]+);/chr(hex($1))/ge' > $T/ev.html; }
has(){ grep -q "$1" $T/ev.html; }
check(){ if "$@"; then ok "$*"; else bad "$*"; fi; }
declare -A LEAD=([11]=9 [12]=9 [29]=9 [13]=10 [27]=10 [28]=26) PJ=([11]=110 [12]=112 [13]=114 [27]=130 [29]=144 [28]=128)
STAFF="11 12 13 27 28 29"
sql "DELETE FROM day_reports WHERE Note LIKE 'zz%'" "DELETE FROM work_tasks WHERE Title LIKE 'zz%'" >/dev/null
for id in $STAFF; do sql "INSERT INTO work_tasks (Date,EmployeeId,ProjectId,Title,Start,End,Status,Source,Selected,Supported,CarryOk,CreatedAt,Note) VALUES ('$D',$id,${PJ[$id]},'zz viec thu $id','08:00','10:00','Done','Planned',1,1,0,NOW(),'zz ket qua $id')" >/dev/null; done
RID=$T/rid
for id in $STAFF; do
  echo "== NV $id (Leader ${LEAD[$id]}) =="
  login $id; ev; check has 'id="eodForm"'; ! has 'eodSummary' && ok "chưa có báo cáo → chưa có tổng hợp" || bad "tổng hợp xuất hiện sớm"
  post "$B/Staff/SubmitReport?d=$D" "$B/Staff/Evening?d=$D" -d "d=$D&blocked=no&note=zz ghi chu $id"
  ev; check has 'eodSummary'; check has 'Chờ duyệt'; check has "zz viec thu $id"; check has "zz ket qua $id"
  ! has 'data-copy' && ok "chờ duyệt: chưa có nút sao chép" || bad "có nút sao chép khi chờ duyệt"
  ! has 'data-sheet="upd' && ok "chờ duyệt: đã khóa nút Cập nhật" || bad "chưa khóa nút Cập nhật"
  check has 'WithdrawReport'
  post "$B/Staff/SubmitReport?d=$D" "$B/Staff/Evening?d=$D" -d "d=$D&blocked=yes&note=zz doi $id"
  r=$(sql "SELECT Blocked FROM day_reports WHERE EmployeeId=$id AND Date='$D'" | tail -1); [ "$r" = "False" ] && ok "gửi lại khi chờ duyệt bị chặn" || bad "gửi lại không bị chặn ($r)"
  # rút lại → mở lại → gửi lại
  post "$B/Staff/WithdrawReport?d=$D" "$B/Staff/Evening?d=$D" -d "d=$D"; ev; ! has 'eodSummary' && ok "rút lại → mở lại để sửa" || bad "rút lại không mở lại"
  post "$B/Staff/SubmitReport?d=$D" "$B/Staff/Evening?d=$D" -d "d=$D&blocked=no&note=zz ghi chu $id"
  rid=$(sql "SELECT Id FROM day_reports WHERE EmployeeId=$id AND Date='$D'" | tail -1 | tr -d ' ')
  # Leader từ chối
  L=${LEAD[$id]}; login $L; post "$B/Planning/DecideReport?d=$D" "$B/Planning/Reports?d=$D" -d "id=$rid&v=no&note=zz bo sung them"
  login $id; ev; check has 'yêu cầu bổ sung'; check has 'zz bo sung them'; ! has 'eodSummary' && ok "bị từ chối: báo cáo mở lại" || bad "bị từ chối vẫn khóa"
  check has 'data-sheet="upd'
  post "$B/Staff/SubmitReport?d=$D" "$B/Staff/Evening?d=$D" -d "d=$D&blocked=yes&note=zz da bo sung $id"
  ev; check has 'Chờ duyệt'; check has 'Vướng mắc: Có'
  # Leader duyệt
  login $L; post "$B/Planning/DecideReport?d=$D" "$B/Planning/Reports?d=$D" -d "id=$rid&v=ok"
  login $id; ev; check has 'Đã duyệt'; check has 'data-copy="eodHead eodP0"'; check has 'Sao chép toàn bộ báo cáo'; check has "zz da bo sung $id"
  post "$B/Staff/WithdrawReport?d=$D" "$B/Staff/Evening?d=$D" -d "d=$D"; n=$(sql "SELECT COUNT(*) FROM day_reports WHERE EmployeeId=$id AND Date='$D'" | tail -1 | tr -d ' '); [ "$n" = 1 ] && ok "đã duyệt: không rút lại được" || bad "rút lại được báo cáo đã duyệt"
done
sql "DELETE FROM day_reports WHERE Note LIKE 'zz%'" "DELETE FROM work_tasks WHERE Title LIKE 'zz%'" | tail -2
echo "KẾT QUẢ: $([ $fail = 0 ] && echo ĐẠT || echo CÓ LỖI)"
