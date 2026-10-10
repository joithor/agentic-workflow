#!/usr/bin/env bash
# Tests for config/lib/failopen.sh — the HOOK-FAILOPEN logger every fail-open
# hook sources (spec 2026-10-09-agent-readiness-design.md §4c, B5).
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIB="$DIR/../failopen.sh"

fail=0
check() {
  if [ "$2" == "$3" ]; then echo "PASS: $1"; else
    echo "FAIL: $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}

# Runs aw_failopen in a fresh bash with `set -euo pipefail` (the strictest
# caller) and prints "<stdout>|<exit>".
run_lib() {
  local state="$1"; shift
  local out rc
  set +e
  out="$(AW_STATE_DIR="$state" bash -c 'set -euo pipefail; . "$0"; aw_failopen "$@"; echo "rc=$?"' "$LIB" "$@" 2>&1)"
  rc=$?
  set -e
  printf '%s|%s' "$out" "$rc"
}

state="$(mktemp -d)"
check "logs nothing to stdout or stderr, returns 0" "$(run_lib "$state" send-gate judge-exit-127)" "rc=0|0"
log="$state/logs/failopen.log"
check "appends one line" "$(wc -l < "$log" | tr -d ' ')" "1"
check "line is a HOOK-FAILOPEN event with hook and cause" \
  "$(jq -c '{event, hook, cause, provider}' "$log")" \
  '{"event":"HOOK-FAILOPEN","hook":"send-gate","cause":"judge-exit-127","provider":"claude"}'
check "ts is ISO-8601 UTC" "$(jq -r '.ts | test("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$")' "$log")" "true"

AW_PROVIDER=codex run_lib "$state" scope-gate judge-decision-missing > /dev/null
check "records the host provider" "$(tail -n 1 "$log" | jq -r .provider)" "codex"

run_lib "$state" 'bad"hook' 'cause with "quotes" and \ backslash' > /dev/null
check "sanitizes hook and cause so the line stays valid JSON" \
  "$(tail -n 1 "$log" | jq -c '{hook, cause}')" '{"hook":"bad_hook","cause":"cause_with__quotes__and___backslash"}'

long="$(printf 'x%.0s' $(seq 1 200))"
run_lib "$state" h "$long" > /dev/null
check "caps the cause at 80 characters" "$(tail -n 1 "$log" | jq -r '.cause | length')" "80"

# Input classification: "input <raw>" logs why the payload could not be read,
# and logs nothing for a readable JSON object.
state2="$(mktemp -d)"; log2="$state2/logs/failopen.log"
run_lib "$state2" h input '' > /dev/null
check "input: empty payload -> empty-input" "$(tail -n 1 "$log2" | jq -r .cause)" "empty-input"
run_lib "$state2" h input 'not json SENTINEL-SECRET' > /dev/null
check "input: invalid JSON -> input-unparsable" "$(tail -n 1 "$log2" | jq -r .cause)" "input-unparsable"
check "input: payload content never reaches the log" "$(grep -c SENTINEL-SECRET "$log2" || true)" "0"
run_lib "$state2" h input '{"tool_input":{}}' > /dev/null
check "input: readable JSON object logs nothing" "$(wc -l < "$log2" | tr -d ' ')" "2"
BASH_BIN="$(command -v bash)"
nojq="$(mktemp -d)"; for t in date mkdir; do ln -s "$(command -v "$t")" "$nojq/$t"; done
AW_STATE_DIR="$state2" PATH="$nojq" "$BASH_BIN" -c '. "$0"; aw_failopen h input "{}"' "$LIB"
check "input: jq missing -> jq-missing" "$(tail -n 1 "$log2" | jq -r .cause)" "jq-missing"

# field-missing: a readable payload that names the hook's own tool but lacks the
# field the hook checks (a host schema change) is logged; other payloads are not.
state3="$(mktemp -d)"; log3="$state3/logs/failopen.log"
run_lib "$state3" h input '{"tool_name":"Bash","tool_input":{"cmd":"x"}}' '.tool_name == "Bash"' > /dev/null
check "input: own tool without the checked field -> field-missing" "$(tail -n 1 "$log3" | jq -r .cause)" "field-missing"
run_lib "$state3" h input '{"tool_name":"Read","tool_input":{}}' '.tool_name == "Bash"' > /dev/null
check "input: another tool's payload logs nothing" "$(wc -l < "$log3" | tr -d ' ')" "1"

# No state dir and no HOME: nowhere to log, so skip quietly (never write under /).
check "HOME and AW_STATE_DIR unset: silent, returns 0" \
  "$(env -u HOME -u AW_STATE_DIR bash -c 'set -euo pipefail; . "$0"; aw_failopen h c; echo "rc=$?"' "$LIB" 2>&1)" "rc=0"

# date missing: still returns 0 and prints nothing, even under set -e.
nodate="$(mktemp -d)"; for t in mkdir wc mv; do ln -s "$(command -v "$t")" "$nodate/$t"; done
check "date missing: silent, returns 0" \
  "$(AW_STATE_DIR="$(mktemp -d)" PATH="$nodate" "$BASH_BIN" -c 'set -euo pipefail; . "$0"; aw_failopen h c; echo "rc=$?"' "$LIB" 2>&1)" "rc=0"

# A symlinked log is never followed.
state4="$(mktemp -d)"; mkdir -p "$state4/logs"; target="$(mktemp)"
ln -s "$target" "$state4/logs/failopen.log"
run_lib "$state4" h c > /dev/null
check "symlinked log: target untouched" "$(wc -c < "$target" | tr -d ' ')" "0"

# The log rotates once it passes AW_FAILOPEN_MAX_BYTES (default 1 MiB).
state5="$(mktemp -d)"; mkdir -p "$state5/logs"; head -c 300 /dev/zero | tr '\0' 'x' > "$state5/logs/failopen.log"
AW_FAILOPEN_MAX_BYTES=200 run_lib "$state5" h c > /dev/null
check "rotation: old log moved to failopen.log.1" "$(wc -c < "$state5/logs/failopen.log.1" | tr -d ' ')" "300"
check "rotation: new log holds only the new event" "$(jq -r .cause "$state5/logs/failopen.log")" "c"
check "new log is private to the user" "$(stat -f %Lp "$state5/logs/failopen.log" 2>/dev/null || stat -c %a "$state5/logs/failopen.log")" "600"

# Logging must never fail or slow the hook down when the log can't be written.
ro="$(mktemp -d)"; chmod 500 "$ro"
check "unwritable state dir: silent, returns 0" "$(run_lib "$ro/nested" h c)" "rc=0|0"
chmod 700 "$ro"

exit $fail
