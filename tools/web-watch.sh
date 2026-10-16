#!/usr/bin/env bash
# Canh web chạy thử: cứ 20 giây kiểm tra; lỗi 2 lần liên tiếp thì tự chạy lại (yêu cầu chủ dự án 2026-10-16: "khi web lỗi tự chạy lại").
# Không làm gì khi có khóa tools/.web-lock (đang build / chạy lại). Chạy nền: nohup bash tools/web-watch.sh &
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
bad=0
while true; do
  sleep 20
  [ -f "$ROOT/tools/.web-lock" ] && { bad=0; continue; }
  if bash "$ROOT/tools/web.sh" health >/dev/null 2>&1; then bad=0; else
    bad=$((bad+1))
    if [ $bad -ge 2 ]; then
      echo "$(date '+%F %T') web lỗi → chạy lại" >> "$ROOT/tools/.web-watch.log"
      bash "$ROOT/tools/web.sh" start >> "$ROOT/tools/.web-watch.log" 2>&1
      bad=0
    fi
  fi
done
