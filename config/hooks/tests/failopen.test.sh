#!/usr/bin/env bash
# HOOK-FAILOPEN coverage for every hook that can fail open
# (spec 2026-10-09-agent-readiness-design.md §4c, B5).
#
# Each case runs the real hook twice from an installed-style layout (hooks next
# to lib/): once with lib/failopen.sh present and once without it. Logging is
# the only allowed difference, so:
#   - stdout and exit code must be byte-identical between the two runs;
#   - the run with the lib appends exactly one HOOK-FAILOPEN line naming the
#     hook and the expected cause ("-" = a normal path that must log nothing);
#   - no log line ever carries payload content.
# Run: bash config/hooks/tests/failopen.test.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HOOKS="$ROOT/config/hooks"
LIBDIR="$ROOT/config/lib"
fail=0
SECRET="SENTINEL-PAYLOAD-7f3a"

# stage <with-failopen 0|1> <with-locks 0|1> — a temp hooks dir laid out the way
# the installers lay it out (hooks, adapters/, lib/).
stage() {
  local d; d="$(mktemp -d)"
  cp "$HOOKS"/*.sh "$d/"
  mkdir -p "$d/lib" "$d/adapters"
  cp "$HOOKS/adapters/"*.sh "$d/adapters/"
  [ "$2" = 1 ] && cp "$LIBDIR/locks.sh" "$d/lib/locks.sh"
  [ "$1" = 1 ] && cp "$LIBDIR/failopen.sh" "$d/lib/failopen.sh"
  printf '%s' "$d"
}

FAKE_BIN="$(mktemp -d)"
JQ_DIR="$(dirname "$(command -v jq)")"
BASE_PATH="/usr/bin:/bin:$JQ_DIR"
# judge stub: JUDGE_MODE picks the behavior for every subcommand.
cat > "$FAKE_BIN/judge" <<'EOF'
#!/usr/bin/env bash
case "${JUDGE_MODE:-}" in
  exit1) exit 1 ;;
  garbage) echo 'not-json'; exit 0 ;;
  empty) echo '{}'; exit 0 ;;
  *) echo "{\"decision\":\"$JUDGE_MODE\"}"; exit 0 ;;
esac
EOF
chmod +x "$FAKE_BIN/judge"
NO_JUDGE_BIN="$(mktemp -d)"

# run_case <name> <hook path inside the stage dir> <stdin> <expected cause|-> [VAR=value ...]
# Env: CASE_PATH (default fake judge on PATH), LOCKS (default 1).
run_case() {
  local name="$1" hook="$2" input="$3" want="$4"; shift 4
  local with without st_with st_without out_with out_without rc_with rc_without lines got
  with="$(stage 1 "${LOCKS:-1}")"; without="$(stage 0 "${LOCKS:-1}")"
  st_with="$(mktemp -d)"; st_without="$(mktemp -d)"
  out_with="$(printf '%s' "$input" | env PATH="${CASE_PATH:-$FAKE_BIN:$BASE_PATH}" AW_STATE_DIR="$st_with" "$@" bash "$with/$hook" 2>/dev/null)"; rc_with=$?
  out_without="$(printf '%s' "$input" | env PATH="${CASE_PATH:-$FAKE_BIN:$BASE_PATH}" AW_STATE_DIR="$st_without" "$@" bash "$without/$hook" 2>/dev/null)"; rc_without=$?

  if [ "$out_with" != "$out_without" ] || [ "$rc_with" != "$rc_without" ]; then
    echo "FAIL: $name — logging changed the decision"
    echo "  with lib:    rc=$rc_with out=$out_with"
    echo "  without lib: rc=$rc_without out=$out_without"
    fail=1; return
  fi
  if [ -s "$st_without/hooks/failopen.log" ]; then
    echo "FAIL: $name — logged without the lib installed"; fail=1; return
  fi
  lines=0
  [ -f "$st_with/hooks/failopen.log" ] && lines="$(wc -l < "$st_with/hooks/failopen.log" | tr -d ' ')"
  if [ "$want" = "-" ]; then
    [ "$lines" = 0 ] || { echo "FAIL: $name — expected no HOOK-FAILOPEN, got: $(cat "$st_with/hooks/failopen.log")"; fail=1; return; }
  else
    got="$(jq -r 'select(.event == "HOOK-FAILOPEN") | .hook + " " + .cause' "$st_with/hooks/failopen.log" 2>/dev/null)"
    if [ "$lines" != 1 ] || [ "$got" != "$want" ]; then
      echo "FAIL: $name — expected one line '$want', got $lines: $got"; fail=1; return
    fi
    if grep -q "$SECRET" "$st_with/hooks/failopen.log"; then
      echo "FAIL: $name — payload content reached the log"; fail=1; return
    fi
  fi
  echo "PASS: $name (rc=$rc_with)"
}

SEND="{\"session_id\":\"s1\",\"tool_input\":{\"to\":\"main\",\"message\":\"$SECRET progress\"}}"

# --- send-gate.sh (PreToolUse SendMessage) ---
CASE_PATH="$NO_JUDGE_BIN:$BASE_PATH" run_case "send-gate: judge missing" send-gate.sh "$SEND" "send-gate judge-exit-127"
run_case "send-gate: judge exits 1" send-gate.sh "$SEND" "send-gate judge-exit-1" JUDGE_MODE=exit1
run_case "send-gate: judge prints garbage" send-gate.sh "$SEND" "send-gate judge-output-unparsable" JUDGE_MODE=garbage
run_case "send-gate: judge gives no decision" send-gate.sh "$SEND" "send-gate judge-decision-missing" JUDGE_MODE=empty
run_case "send-gate: empty stdin" send-gate.sh "" "send-gate empty-input"
run_case "send-gate: unparsable stdin" send-gate.sh "$SECRET {" "send-gate input-unparsable"
LOCKS=0 run_case "send-gate: locks lib missing" send-gate.sh "$SEND" "send-gate locks-lib-missing"
run_case "send-gate: no message is not a failure" send-gate.sh '{"session_id":"s1","tool_input":{"type":"shutdown_request"}}' "-"
run_case "send-gate: send decision logs nothing" send-gate.sh "$SEND" "-" JUDGE_MODE=send
run_case "send-gate: drop decision logs nothing" send-gate.sh "$SEND" "-" JUDGE_MODE=drop

# --- scope-gate.sh (PreToolUse Agent) ---
AGENT="{\"session_id\":\"s1\",\"tool_use_id\":\"tu1\",\"tool_input\":{\"subagent_type\":\"x\",\"description\":\"g\",\"prompt\":\"$SECRET\"}}"
CASE_PATH="$NO_JUDGE_BIN:$BASE_PATH" run_case "scope-gate: judge missing" scope-gate.sh "$AGENT" "scope-gate judge-exit-127"
run_case "scope-gate: judge prints garbage" scope-gate.sh "$AGENT" "scope-gate judge-output-unparsable" JUDGE_MODE=garbage
run_case "scope-gate: judge gives no decision" scope-gate.sh "$AGENT" "scope-gate judge-decision-missing" JUDGE_MODE=empty
run_case "scope-gate: unknown decision" scope-gate.sh "$AGENT" "scope-gate judge-decision-unknown" JUDGE_MODE=maybe
run_case "scope-gate: unparsable stdin" scope-gate.sh "nope" "scope-gate input-unparsable"
run_case "scope-gate: empty stdin" scope-gate.sh "" "scope-gate empty-input"
run_case "scope-gate: missing decision denies, logs nothing" scope-gate.sh "$AGENT" "-" JUDGE_MODE=missing

# --- external-write-guard.sh (PreToolUse gh pr merge|comment|review, Linear, Slack) ---
SESS="$(mktemp -d)"; printf 'not json' > "$SESS/s9.json"
run_case "external-write-guard: matched write without session id" external-write-guard.sh \
  '{"tool_name":"Bash","tool_input":{"command":"gh pr merge 1"}}' "external-write-guard session-id-missing"
run_case "external-write-guard: session state unreadable" external-write-guard.sh \
  '{"session_id":"s9","tool_name":"Bash","tool_input":{"command":"gh pr merge 1"}}' "external-write-guard session-state-unreadable" AW_JUDGE_SESSIONS_DIR="$SESS"
run_case "external-write-guard: unparsable stdin" external-write-guard.sh "$SECRET" "external-write-guard input-unparsable"
run_case "external-write-guard: unmatched tool logs nothing" external-write-guard.sh '{"tool_name":"Bash","tool_input":{"command":"ls"}}' "-"

# --- done-gate.sh (Stop) ---
NOASSIST="$(mktemp)"; echo '{"type":"user","message":{"content":"hi"}}' > "$NOASSIST"
run_case "done-gate: transcript unreadable" done-gate.sh '{"session_id":"s1","transcript_path":"/nonexistent/t.jsonl"}' "done-gate transcript-unreadable"
run_case "done-gate: no assistant line" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$NOASSIST\"}" "done-gate no-assistant-line"
run_case "done-gate: unparsable stdin" done-gate.sh "$SECRET" "done-gate input-unparsable"
run_case "done-gate: re-invocation logs nothing" done-gate.sh '{"stop_hook_active":true}' "-"
run_case "done-gate: no claim source logs nothing" done-gate.sh '{"session_id":"s1"}' "-"

# --- outbox-flush.sh (TeammateIdle) ---
OUTBOX="$(mktemp -d)"; printf 'not json\n' > "$OUTBOX/s1.jsonl"
run_case "outbox-flush: outbox unreadable" outbox-flush.sh '{"session_id":"s1","teammate_name":"t"}' "outbox-flush outbox-unreadable" AW_OUTBOX_DIR="$OUTBOX"
run_case "outbox-flush: unparsable stdin" outbox-flush.sh "$SECRET" "outbox-flush input-unparsable"

# --- Bash safety hooks (PreToolUse Bash) ---
for h in block-destructive block-push-main detect-secrets; do
  run_case "$h: unparsable stdin" "$h.sh" "$SECRET rm -rf /" "$h input-unparsable"
  run_case "$h: empty stdin" "$h.sh" "" "$h empty-input"
  run_case "$h: normal command logs nothing" "$h.sh" '{"tool_input":{"command":"ls"}}' "-"
done
run_case "block-destructive: deny logs nothing" block-destructive.sh '{"tool_input":{"command":"rm -rf /tmp/x"}}' "-"

# --- Provider adapters ---
EXIT3="$(mktemp -d)/exit3.sh"; printf '#!/usr/bin/env bash\nexit 3\n' > "$EXIT3"

# The adapters take the hook script as an argument, so run them directly.
adapter_case() {
  local name="$1" adapter="$2" want="$3"; shift 3
  local with without st_with st_without out_with out_without rc_with rc_without got input
  input='{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"ls"}}'
  with="$(stage 1 1)"; without="$(stage 0 1)"; st_with="$(mktemp -d)"; st_without="$(mktemp -d)"
  out_with="$(printf '%s' "$input" | env PATH="$FAKE_BIN:$BASE_PATH" AW_STATE_DIR="$st_with" bash "$with/adapters/$adapter" "$@" 2>/dev/null)"; rc_with=$?
  out_without="$(printf '%s' "$input" | env PATH="$FAKE_BIN:$BASE_PATH" AW_STATE_DIR="$st_without" bash "$without/adapters/$adapter" "$@" 2>/dev/null)"; rc_without=$?
  if [ "$out_with" != "$out_without" ] || [ "$rc_with" != "$rc_without" ]; then
    echo "FAIL: $name — logging changed the decision ($rc_with '$out_with' vs $rc_without '$out_without')"; fail=1; return
  fi
  [ -s "$st_without/hooks/failopen.log" ] && { echo "FAIL: $name — logged without the lib"; fail=1; return; }
  got="$(jq -r '.hook + " " + .cause' "$st_with/hooks/failopen.log" 2>/dev/null)"
  if [ "$want" = "-" ]; then
    [ -z "$got" ] || { echo "FAIL: $name — expected no line, got $got"; fail=1; return; }
  elif [ "$got" != "$want" ]; then
    echo "FAIL: $name — expected '$want', got '$got'"; fail=1; return
  fi
  echo "PASS: $name (rc=$rc_with out=$out_with)"
}
for p in codex cursor; do
  adapter_case "$p adapter: hook script missing" "$p.sh" "$p-adapter hook-script-missing" /nonexistent/hook.sh
  adapter_case "$p adapter: hook exits 3" "$p.sh" "$p-adapter hook-exit-3" "$EXIT3"
  adapter_case "$p adapter: no hook script argument" "$p.sh" "$p-adapter args-invalid"
  adapter_case "$p adapter: allowing hook logs nothing" "$p.sh" "-" block-destructive.sh
done

exit $fail
