#!/usr/bin/env bash
# Smoke test: đăng nhập từng tài khoản và GET mọi trang; in mã HTTP + phát hiện lỗi trong HTML.
# Dùng: QM_PW_USER=... QM_PW_ADMIN=... bash smoke.sh [base]
BASE="${1:-http://localhost:5125}"
DIR="${TMPDIR:-/tmp}/qmdept_smoke"; rm -rf "$DIR"; mkdir -p "$DIR"
fail=0

login() { # $1=code $2=pw $3=jar
  local jar="$3"; rm -f "$jar"
  local html; html=$(curl -s -c "$jar" -b "$jar" "$BASE/Account/Login")
  local tok; tok=$(echo "$html" | grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' | head -1 | sed 's/.*value="//;s/"$//')
  if [ -z "$tok" ]; then echo "!! no antiforgery token on login page"; fail=1; return 1; fi
  local code; code=$(curl -s -o /dev/null -w "%{http_code} %{redirect_url}" -c "$jar" -b "$jar" --data-urlencode "code=$1" --data-urlencode "password=$2" --data-urlencode "__RequestVerificationToken=$tok" "$BASE/Account/Login")
  echo "login $1 -> $code"
}

get() { # $1=jar $2=path
  local f="$DIR/$(echo "$2" | tr '/?=&' '____').html"
  local r; r=$(curl -s -o "$f" -w "%{http_code} %{redirect_url}" -b "$1" -c "$1" "$BASE$2")
  local err=""
  grep -q 'An unhandled exception\|InvalidOperationException\|NullReferenceException\|Exception:' "$f" && err=" EXCEPTION"
  grep -q 'class="empty">Không tìm thấy' "$f" && err="$err (not-found)"
  echo "  $r  $2$err"
  [[ "$r" == 2* || "$r" == 302* ]] || fail=1
  [ -n "$err" ] && [[ "$err" == *EXCEPTION* ]] && { fail=1; grep -o '<title>[^<]*</title>' "$f" | head -1; grep -m3 -o '[A-Za-z.]*Exception: [^<]*' "$f"; }
}

J="$DIR/jar_nv"; login tp.leader1.nv1 "$QM_PW_USER" "$J"
for p in / /Staff/Month /Staff/Projects "/Staff/Projects?pt=done" /Staff/Morning /Staff/Evening /Overtime "/Overtime?tab=summary" /Settings "/Manager/Overview" /Admin/Accounts; do get "$J" "$p"; done

J="$DIR/jar_ld"; login tp.leader1 "$QM_PW_USER" "$J"
for p in / /Leader/Overview /Planning/Approvals "/Planning/Approvals?ap=pending" /Planning/Reports /Planning/Week /Planning/Projects "/Planning/Projects?pj=new" /Planning/People /Planning/Assign   /Leader/Tracking "/Leader/Tracking?mode=project" /Leader/Reports "/Leader/Reports?tab=week" /Overtime "/Overtime?tab=approve" "/Overtime?tab=summary"   "/Staff/Month?as=all" "/Staff/Month?as=3" "/Staff/Morning?as=3" "/Staff/Evening?as=3" /Settings /Manager/Overview; do get "$J" "$p"; done

J="$DIR/jar_tp"; login tp "$QM_PW_USER" "$J"
for p in / /Manager/Overview /Manager/Approvals "/Manager/Approvals?mode=extra&f=all" /Manager/Projects /Manager/Reports "/Manager/Reports?tab=week" /Overtime "/Overtime?tab=approve" "/Overtime?tab=summary"   /Planning/People "/Leader/Overview?as=2" "/Planning/Week?as=2" "/Staff/Month?as=all" "/Staff/Month?as=3" /Settings /Admin/Accounts; do get "$J" "$p"; done

J="$DIR/jar_ad"; login admin "$QM_PW_ADMIN" "$J"
for p in / /Admin/Accounts "/Manager/Overview?as=1" "/Leader/Overview?as=2" "/Staff/Month?as=3" "/Staff/Month?as=all" "/Overtime?as=1" /Settings; do get "$J" "$p"; done

echo "RESULT: $([ $fail = 0 ] && echo OK || echo FAIL)"
