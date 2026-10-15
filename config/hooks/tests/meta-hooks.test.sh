#!/usr/bin/env bash
# Tests for config/hooks/meta-stop-gate.sh and meta-outbox.sh against a stub aw-meta.
# Run: bash config/hooks/tests/meta-hooks.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOKS="$(cd "$DIR/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done   # the caller may be a meta session: its team config, bin and claude must not reach the stubs
export HOME="$WORK/home"; mkdir -p "$HOME"
export AW_STATE_DIR="$WORK/state"
unset AW_JUDGE_CHILD
export CALLS="$WORK/calls" STDIN_SEEN="$WORK/stdin"
STUB="$WORK/aw-meta"
cat > "$STUB" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$CALLS"
[ "$1" = outbox-record ] && cat > "$STDIN_SEEN"
[ -n "${STUB_OUT:-}" ] && echo "$STUB_OUT"
exit "${STUB_RC:-0}"
EOF
chmod +x "$STUB"
export AW_META_BIN="$STUB"
stop() { printf '%s' "$1" | bash "$HOOKS/meta-stop-gate.sh" 2> "$WORK/err"; }
P='{"session_id":"s-1","transcript_path":"/t/s-1.jsonl","stop_hook_active":false}'

: > "$CALLS"
stop "$P"; RC=$?
check "nothing owed: exit 0" "$RC" "0"
check "nothing owed: calls stop-check with session and transcript" "$(cat "$CALLS")" "stop-check --session s-1 --transcript /t/s-1.jsonl"

: > "$CALLS"
STUB_RC=1 STUB_OUT="OWED reply to sindri-api: docs check" stop "$P"; RC=$?
check "obligation open: blocks with exit 2" "$RC" "2"
check "obligation open: the list goes to stderr" "$(cat "$WORK/err")" "OWED reply to sindri-api: docs check"

: > "$CALLS"
STUB_RC=1 stop '{"session_id":"s-1","transcript_path":"/t/x","stop_hook_active":true}'; RC=$?
check "hook active: still asks, with --hook-active" "$(cat "$CALLS")" "stop-check --session s-1 --transcript /t/x --hook-active"
check "hook active: aw-meta decides (blocks)" "$RC" "2"

OUT="$(STUB_OUT="aw-meta: stop allowed after 3 identical blocks; still open: reply" stop "$P")"; RC=$?
check "loop guard let it through: exit 0, the warning shown as a systemMessage" "$RC:$(jq -r .systemMessage <<< "$OUT")" "0:aw-meta: stop allowed after 3 identical blocks; still open: reply"
OUT="$(stop "$P")"
check "nothing owed: prints nothing" "$OUT" ""

STUB_RC=2 stop "$P"; RC=$?
check "tool error: fails open" "$RC" "0"
grep -q '"hook":"meta-stop-gate","cause":"meta-exit-2"' "$AW_STATE_DIR/logs/failopen.log" 2> /dev/null
check "tool error: logged as HOOK-FAILOPEN meta-exit-2" "$?" "0"

stop ""; RC=$?
check "empty input: fails open" "$RC" "0"
stop '{"session_id":"s-1"}'; RC=$?
check "no transcript_path: fails open" "$RC" "0"
grep -q '"cause":"field-missing"' "$AW_STATE_DIR/logs/failopen.log"
check "no transcript_path: logged field-missing" "$?" "0"

: > "$CALLS"
AW_META_BIN="$WORK/missing" stop "$P"; RC=$?
check "aw-meta not installed: exit 0, nothing called" "$RC:$(cat "$CALLS")" "0:"

: > "$CALLS"
AW_JUDGE_CHILD=1 stop "$P"; RC=$?
check "judge child: exit 0, nothing called" "$RC:$(cat "$CALLS")" "0:"

# meta-outbox.sh
: > "$CALLS"
SEND='{"session_id":"s-1","tool_name":"SendMessage","tool_input":{"to":"sindri-api","message":"done"},"tool_response":{"msg_id":"m-9"}}'
OUT="$(printf '%s' "$SEND" | bash "$HOOKS/meta-outbox.sh")"; RC=$?
check "outbox: exit 0, silent" "$RC:$OUT" "0:"
check "outbox: calls outbox-record" "$(cat "$CALLS")" "outbox-record"
check "outbox: passes the hook payload on stdin" "$(cat "$STDIN_SEEN")" "$SEND"
OUT="$(printf '%s' "$SEND" | STUB_RC=2 bash "$HOOKS/meta-outbox.sh")"; RC=$?
check "outbox: aw-meta failure never blocks" "$RC:$OUT" "0:"
grep -q '"hook":"meta-outbox","cause":"meta-exit-2"' "$AW_STATE_DIR/logs/failopen.log"
check "outbox: failure logged" "$?" "0"

exit $fail
