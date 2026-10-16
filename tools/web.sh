#!/usr/bin/env bash
# Quản lý web chạy thử trên máy (http://localhost:5125, môi trường Development) — dùng sau MỖI yêu cầu (AGENTS.md quy tắc 22).
#   bash tools/web.sh restart   dừng app → build → chạy lại (tự đặt khóa để web-watch.sh không hồi sinh app giữa chừng)
#   bash tools/web.sh start     dừng app cũ (nếu có) → chạy lại bằng DLL đã build, không build
#   bash tools/web.sh stop      dừng app
#   bash tools/web.sh health    kiểm tra web trả 200 (dùng thanh DEV: /Dev/As rồi /Manager/Overview); exit 0 = khỏe
# Log app: tools/.web.log. Chạy ở thư mục gốc repo.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOCK="$ROOT/tools/.web-lock"; LOG="$ROOT/tools/.web.log"; B=http://localhost:5125
export DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK=1 DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
stop_app() {
  powershell.exe -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\" | Where-Object { \$_.CommandLine -like '*QMDept.dll*' } | ForEach-Object { Stop-Process -Id \$_.ProcessId -Force }" >/dev/null 2>&1
}
start_app() {
  stop_app
  ( cd "$ROOT/QMDept" && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5125 nohup dotnet bin/Debug/net10.0/QMDept.dll > "$LOG" 2>&1 & )
  for i in $(seq 1 60); do c=$(curl -s -o /dev/null -w "%{http_code}" --max-time 3 "$B/Account/Login"); [ "$c" != "000" ] && { echo "web chạy sau ${i}s (HTTP $c)"; return 0; }; sleep 1; done
  echo "web KHÔNG khởi động được — xem $LOG"; return 1
}
health() {
  local J; J=$(mktemp)
  curl -s -o /dev/null -c "$J" --max-time 15 "$B/Dev/As?id=8"
  local code; code=$(curl -s -o /dev/null -w "%{http_code}" -b "$J" --max-time 40 "$B/Manager/Overview")
  rm -f "$J"; [ "$code" = "200" ]
}
case "${1:-restart}" in
  stop) touch "$LOCK"; stop_app; rm -f "$LOCK" ;;
  start) touch "$LOCK"; start_app; r=$?; rm -f "$LOCK"; exit $r ;;
  restart)
    touch "$LOCK"; stop_app
    ( cd "$ROOT/QMDept" && dotnet build 2>&1 | grep -E "error|Error\(s\)|Build succeeded" | sort -u )
    start_app; r=$?; rm -f "$LOCK"; exit $r ;;
  health) health && echo "web khỏe" || { echo "web LỖI"; exit 1; } ;;
  *) echo "dùng: web.sh restart|start|stop|health"; exit 2 ;;
esac
