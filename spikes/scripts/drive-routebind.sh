#!/usr/bin/env bash
# Drive the RouteBind probe: bind Vogen hybrid ids from route and query values under NativeAOT.
# Valid values must bind (200); invalid ones must be 400, never 500 and never an uninitialized id.
# Usage: drive-routebind.sh <RouteBind-binary> [port]
set -u
bin="$1"; port="${2:-5210}"
"$bin" "$port" </dev/null >/dev/null 2>&1 &
pid=$!
trap 'kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null' EXIT
for _ in $(seq 1 100); do curl -s -o /dev/null "http://127.0.0.1:${port}/budget/1" && break; sleep 0.05; done
fails=0
check() { # path expected-status [expected-body-substring]
  local out code body
  out=$(curl -s -m 5 -w '\n%{http_code}' "http://127.0.0.1:${port}$1")
  code=${out##*$'\n'}; body=${out%$'\n'*}
  printf '%-22s -> %s %s\n' "$1" "$code" "$(echo "$body" | head -c 120)"
  [ "$code" = "$2" ] || { echo "FAIL $1 expected $2"; fails=$((fails + 1)); }
  [ -z "${3:-}" ] || echo "$body" | grep -q "$3" || { echo "FAIL $1 body"; fails=$((fails + 1)); }
}
check /items/item-1 200 "item item-1 initialized=True"
check /items/%20 400
check /budget/1200 200 "budget 1200"
check /budget/-1 400
check /budget/abc 400
check "/query?id=q-1" 200 "query q-1"
check "/query?id=" 400
check /query 400
check "/qbudget?b=" 400
check "/qbudget?b=5" 200 "qbudget 5"
[ "$fails" -eq 0 ] && echo OK || echo "FAILED $fails"
[ "$fails" -eq 0 ]
