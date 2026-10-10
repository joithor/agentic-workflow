#!/usr/bin/env bash
# HOOK-FAILOPEN coverage for every hook that can fail open
# (spec 2026-10-09-agent-readiness-design.md §4c, B5).
#
# Each case runs the real hook twice from an installed-style layout (hooks next
# to lib/): once with lib/failopen.sh present and once without it. Logging is
# the only allowed difference, so:
#   - stdout, stderr (stage path normalized) and the exit code must be
#     byte-identical between the two runs;
#   - the run with the lib appends exactly the expected HOOK-FAILOPEN lines
#     ("-" = a normal path that must log nothing, "?" = don't check the log);
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

# path_without <tool...> — a PATH dir holding every tool in /usr/bin and /bin
# (and jq's dir) except the named ones.
path_without() {
  local d f name skip; d="$(mktemp -d)"
  for f in /usr/bin/* /bin/* "$(dirname "$(command -v jq)")"/jq; do
    name="$(basename "$f")"; skip=0
    for t in "$@"; do [ "$name" = "$t" ] && skip=1; done
    [ "$skip" = 1 ] || [ -e "$d/$name" ] || ln -s "$f" "$d/$name"
  done
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
  nobrief)
    [ "$1" = brief ] && { echo "no brief for tool_use_id: $3" >&2; exit 1; }
    exit 0 ;;
  *) echo "{\"decision\":\"$JUDGE_MODE\"}"; exit 0 ;;
esac
EOF
chmod +x "$FAKE_BIN/judge"
NO_JUDGE_BIN="$(mktemp -d)"
# jq wrapper that fails one chosen invocation (FAIL_JQ_ON = a substring of its args).
JQ_FAIL_BIN="$(mktemp -d)"
cat > "$JQ_FAIL_BIN/jq" <<EOF
#!/usr/bin/env bash
case "\$*" in *"\${FAIL_JQ_ON:-@@never@@}"*) exit 5 ;; esac
exec "$(command -v jq)" "\$@"
EOF
chmod +x "$JQ_FAIL_BIN/jq"

# check_pair <name> <want> <with-dir> <without-dir> <state-with> <state-without> <out/err/rc files…>
compare_runs() {
  local name="$1" want="$2" with="$3" without="$4" st_with="$5" st_without="$6"
  local o1="$7" e1="$8" r1="$9" o2="${10}" e2="${11}" r2="${12}" log got lines
  sed "s#$with#<stage>#g" "$e1" > "$e1.n"; sed "s#$without#<stage>#g" "$e2" > "$e2.n"
  if ! cmp -s "$o1" "$o2" || ! cmp -s "$e1.n" "$e2.n" || [ "$r1" != "$r2" ]; then
    echo "FAIL: $name — logging changed the decision"
    echo "  with lib:    rc=$r1 out=$(cat "$o1") err=$(cat "$e1.n")"
    echo "  without lib: rc=$r2 out=$(cat "$o2") err=$(cat "$e2.n")"
    fail=1; return
  fi
  if [ -n "${WANT_RC:-}" ] && [ "$r1" != "$WANT_RC" ]; then
    echo "FAIL: $name — expected exit $WANT_RC, got $r1"; fail=1; return
  fi
  if [ -s "$st_without/logs/failopen.log" ]; then
    echo "FAIL: $name — logged without the lib installed"; fail=1; return
  fi
  log="$st_with/logs/failopen.log"
  got="$(jq -r 'select(.event == "HOOK-FAILOPEN") | .hook + " " + .cause' "$log" 2>/dev/null | paste -sd';' -)"
  case "$want" in
    "?") ;;
    "-") [ -z "$got" ] || { echo "FAIL: $name — expected no HOOK-FAILOPEN, got: $got"; fail=1; return; } ;;
    *) [ "$got" = "$want" ] || { echo "FAIL: $name — expected '$want', got '$got'"; fail=1; return; } ;;
  esac
  if [ -f "$log" ] && grep -q "$SECRET" "$log"; then
    echo "FAIL: $name — payload content reached the log"; fail=1; return
  fi
  echo "PASS: $name (rc=$r1)"
}

# run_case <name> <hook path inside the stage dir> <stdin> <expected> [VAR=value ...]
# Env: CASE_PATH (default fake judge on PATH), LOCKS (default 1), WANT_RC,
# NO_HOME=1 (run with HOME and AW_STATE_DIR unset), HOOK_ARGS (adapter argv).
run_case() {
  local name="$1" hook="$2" input="$3" want="$4"; shift 4
  local with without st_with st_without t unset_env=()
  with="$(stage 1 "${LOCKS:-1}")"; without="$(stage 0 "${LOCKS:-1}")"
  st_with="$(mktemp -d)"; st_without="$(mktemp -d)"; t="$(mktemp -d)"
  [ "${NO_HOME:-0}" = 1 ] && unset_env=(-u HOME -u AW_STATE_DIR)
  local env_with=(AW_STATE_DIR="$st_with") env_without=(AW_STATE_DIR="$st_without")
  [ "${NO_HOME:-0}" = 1 ] && { env_with=(); env_without=(); }
  # shellcheck disable=SC2086
  printf '%s' "$input" | env ${unset_env[@]+"${unset_env[@]}"} PATH="${CASE_PATH:-$FAKE_BIN:$BASE_PATH}" ${env_with[@]+"${env_with[@]}"} "$@" \
    bash "$with/$hook" ${HOOK_ARGS:-} > "$t/o1" 2> "$t/e1"; echo $? > "$t/r1"
  # shellcheck disable=SC2086
  printf '%s' "$input" | env ${unset_env[@]+"${unset_env[@]}"} PATH="${CASE_PATH:-$FAKE_BIN:$BASE_PATH}" ${env_without[@]+"${env_without[@]}"} "$@" \
    bash "$without/$hook" ${HOOK_ARGS:-} > "$t/o2" 2> "$t/e2"; echo $? > "$t/r2"
  compare_runs "$name" "$want" "$with" "$without" "$st_with" "$st_without" \
    "$t/o1" "$t/e1" "$(cat "$t/r1")" "$t/o2" "$t/e2" "$(cat "$t/r2")"
}

SEND="{\"session_id\":\"s1\",\"tool_name\":\"SendMessage\",\"tool_input\":{\"to\":\"main\",\"message\":\"$SECRET progress\"}}"

# --- send-gate.sh (PreToolUse SendMessage) ---
CASE_PATH="$NO_JUDGE_BIN:$BASE_PATH" run_case "send-gate: judge missing" send-gate.sh "$SEND" "send-gate judge-exit-127"
run_case "send-gate: judge exits 1" send-gate.sh "$SEND" "send-gate judge-exit-1" JUDGE_MODE=exit1
run_case "send-gate: judge prints garbage" send-gate.sh "$SEND" "send-gate judge-output-unparsable" JUDGE_MODE=garbage
run_case "send-gate: judge gives no decision" send-gate.sh "$SEND" "send-gate judge-decision-missing" JUDGE_MODE=empty
run_case "send-gate: empty stdin" send-gate.sh "" "send-gate empty-input"
run_case "send-gate: unparsable stdin" send-gate.sh "$SECRET {" "send-gate input-unparsable"
run_case "send-gate: SendMessage without a message" send-gate.sh '{"session_id":"s1","tool_name":"SendMessage","tool_input":{"to":"main"}}' "send-gate field-missing"
LOCKS=0 run_case "send-gate: locks lib missing" send-gate.sh "$SEND" "send-gate locks-lib-missing"
run_case "send-gate: control message is not a failure" send-gate.sh '{"session_id":"s1","tool_name":"SendMessage","tool_input":{"type":"shutdown_request"}}' "-"
run_case "send-gate: send decision logs nothing" send-gate.sh "$SEND" "-" JUDGE_MODE=send
run_case "send-gate: drop decision logs nothing" send-gate.sh "$SEND" "-" JUDGE_MODE=drop
run_case "send-gate: unknown decision fails closed (queued), logs nothing" send-gate.sh "$SEND" "-" JUDGE_MODE=maybe

# --- scope-gate.sh (PreToolUse Agent) ---
AGENT="{\"session_id\":\"s1\",\"tool_name\":\"Agent\",\"tool_use_id\":\"tu1\",\"tool_input\":{\"subagent_type\":\"x\",\"description\":\"g\",\"prompt\":\"$SECRET\"}}"
CASE_PATH="$NO_JUDGE_BIN:$BASE_PATH" run_case "scope-gate: judge missing" scope-gate.sh "$AGENT" "scope-gate judge-exit-127"
run_case "scope-gate: judge exits 1" scope-gate.sh "$AGENT" "scope-gate judge-exit-1" JUDGE_MODE=exit1
run_case "scope-gate: judge prints garbage" scope-gate.sh "$AGENT" "scope-gate judge-output-unparsable" JUDGE_MODE=garbage
run_case "scope-gate: judge gives no decision" scope-gate.sh "$AGENT" "scope-gate judge-decision-missing" JUDGE_MODE=empty
run_case "scope-gate: unknown decision" scope-gate.sh "$AGENT" "scope-gate judge-decision-unknown" JUDGE_MODE=maybe
run_case "scope-gate: unparsable stdin" scope-gate.sh "nope" "scope-gate input-unparsable"
run_case "scope-gate: empty stdin" scope-gate.sh "" "scope-gate empty-input"
run_case "scope-gate: Agent call without tool_use_id" scope-gate.sh '{"tool_name":"Agent","tool_input":{}}' "scope-gate field-missing"
run_case "scope-gate: missing decision denies, logs nothing" scope-gate.sh "$AGENT" "-" JUDGE_MODE=missing

# --- external-write-guard.sh (PreToolUse gh pr merge|comment|review, Linear, Slack) ---
SESS="$(mktemp -d)"; printf 'not json' > "$SESS/s9.json"
run_case "external-write-guard: matched write without session id" external-write-guard.sh \
  '{"tool_name":"Bash","tool_input":{"command":"gh pr merge 1"}}' "external-write-guard session-id-missing"
run_case "external-write-guard: session state unreadable" external-write-guard.sh \
  '{"session_id":"s9","tool_name":"Bash","tool_input":{"command":"gh pr merge 1"}}' "external-write-guard session-state-unreadable" AW_JUDGE_SESSIONS_DIR="$SESS"
run_case "external-write-guard: unparsable stdin" external-write-guard.sh "$SECRET" "external-write-guard input-unparsable"
run_case "external-write-guard: empty stdin" external-write-guard.sh "" "external-write-guard empty-input"
run_case "external-write-guard: unmatched tool logs nothing" external-write-guard.sh '{"tool_name":"Bash","tool_input":{"command":"ls"}}' "-"

# --- done-gate.sh (Stop) ---
NOASSIST="$(mktemp)"; echo '{"type":"user","message":{"content":"hi"}}' > "$NOASSIST"
BROKEN="$(mktemp)"; echo '{"type":"assistant", broken' > "$BROKEN"
CLAIM="$(mktemp)"; echo '{"type":"assistant","message":{"content":[{"type":"text","text":"All done."}]}}' > "$CLAIM"
WORKING="$(mktemp)"; echo '{"type":"assistant","message":{"content":[{"type":"text","text":"Still working on the parser."}]}}' > "$WORKING"
run_case "done-gate: transcript unreadable" done-gate.sh '{"session_id":"s1","transcript_path":"/nonexistent/t.jsonl"}' "done-gate transcript-unreadable"
run_case "done-gate: no assistant line" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$NOASSIST\"}" "done-gate no-assistant-line"
run_case "done-gate: assistant line unparsable" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$BROKEN\"}" "done-gate transcript-line-unparsable"
run_case "done-gate: unparsable stdin" done-gate.sh "$SECRET" "done-gate input-unparsable"
run_case "done-gate: empty stdin" done-gate.sh "" "done-gate empty-input"
run_case "done-gate: ask-check judge fails" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$WORKING\"}" "done-gate judge-ask-check-exit-1" JUDGE_MODE=exit1
run_case "done-gate: brief lookup judge fails" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$CLAIM\"}" "done-gate judge-brief-exit-1" JUDGE_MODE=exit1
CASE_PATH="$NO_JUDGE_BIN:$BASE_PATH" run_case "done-gate: judge missing for the brief" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$CLAIM\"}" "done-gate judge-brief-exit-127"
run_case "done-gate: no brief is not a failure" done-gate.sh "{\"session_id\":\"s1\",\"transcript_path\":\"$CLAIM\"}" "-" JUDGE_MODE=nobrief
run_case "done-gate: re-invocation logs nothing" done-gate.sh '{"stop_hook_active":true}' "-"
run_case "done-gate: no claim source logs nothing" done-gate.sh '{"session_id":"s1"}' "-"

# --- outbox-flush.sh (TeammateIdle) ---
OUTBOX="$(mktemp -d)"; printf 'not json\n' > "$OUTBOX/s1.jsonl"
run_case "outbox-flush: outbox unreadable" outbox-flush.sh '{"session_id":"s1","teammate_name":"t"}' "outbox-flush outbox-unreadable" AW_OUTBOX_DIR="$OUTBOX"
run_case "outbox-flush: unparsable stdin" outbox-flush.sh "$SECRET" "outbox-flush input-unparsable"
run_case "outbox-flush: empty stdin" outbox-flush.sh "" "outbox-flush empty-input"

# --- Bash safety hooks (PreToolUse Bash) ---
NO_CAT="$(path_without cat)"
for h in block-destructive block-push-main detect-secrets; do
  run_case "$h: unparsable stdin" "$h.sh" "$SECRET rm -rf /" "$h input-unparsable"
  run_case "$h: empty stdin" "$h.sh" "" "$h empty-input"
  run_case "$h: Bash call without a command string" "$h.sh" '{"tool_name":"Bash","tool_input":{"cmd":"x"}}' "$h field-missing"
  run_case "$h: normal command logs nothing" "$h.sh" '{"tool_name":"Bash","tool_input":{"command":"ls"}}' "-"
  # A crash under Claude Code (set -e abort; here cat is missing) is counted by the EXIT trap.
  CASE_PATH="$NO_CAT" WANT_RC=127 run_case "$h: crash is counted, exit code kept" "$h.sh" '{"tool_input":{"command":"ls"}}' "$h exit-127"
  CASE_PATH="$NO_CAT" WANT_RC=127 run_case "$h: crash under an adapter is left to the adapter" "$h.sh" '{"tool_input":{"command":"ls"}}' "-" AW_PROVIDER=codex
done
run_case "block-destructive: deny logs nothing" block-destructive.sh '{"tool_input":{"command":"rm -rf /tmp/x"}}' "-"

# --- The logger can never change a decision, even when it can't run ---
NO_DATE="$(path_without date judge)"
for h in block-destructive scope-gate send-gate external-write-guard done-gate outbox-flush; do
  NO_HOME=1 run_case "$h: HOME and AW_STATE_DIR unset" "$h.sh" "$SECRET" "?"
  CASE_PATH="$NO_DATE" run_case "$h: date missing" "$h.sh" "$SECRET" "?"
done

# --- Provider adapters ---
EXIT3="$(mktemp -d)/exit3.sh"; printf '#!/usr/bin/env bash\nexit 3\n' > "$EXIT3"
PRE='{"hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"ls"}}'
for p in codex cursor; do
  HOOK_ARGS="/nonexistent/hook.sh" run_case "$p adapter: hook script missing" "adapters/$p.sh" "$PRE" "$p-adapter hook-script-missing"
  HOOK_ARGS="$EXIT3" run_case "$p adapter: hook exits 3" "adapters/$p.sh" "$PRE" "$p-adapter hook-exit-3"
  run_case "$p adapter: no hook script argument" "adapters/$p.sh" "$PRE" "$p-adapter args-invalid"
  HOOK_ARGS="block-destructive.sh" run_case "$p adapter: unparsable host payload" "adapters/$p.sh" "$SECRET {" "$p-adapter input-unparsable"
  HOOK_ARGS="block-destructive.sh" run_case "$p adapter: empty host payload" "adapters/$p.sh" "" "$p-adapter empty-input"
  HOOK_ARGS="block-destructive.sh" run_case "$p adapter: allowing hook logs nothing" "adapters/$p.sh" "$PRE" "-"
done
HOOK_ARGS="block-destructive.sh" CASE_PATH="$JQ_FAIL_BIN:$FAKE_BIN:$BASE_PATH" run_case "codex adapter: payload normalization fails" \
  adapters/codex.sh "$PRE" "codex-adapter payload-unparsable" FAIL_JQ_ON='.prompt_id = (.prompt_id'
HOOK_ARGS="block-destructive.sh" CASE_PATH="$JQ_FAIL_BIN:$FAKE_BIN:$BASE_PATH" run_case "cursor adapter: payload normalization fails" \
  adapters/cursor.sh '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"ls"}}' "cursor-adapter payload-unparsable" FAIL_JQ_ON='def jsonish'
HOOK_ARGS="block-destructive.sh" CASE_PATH="$JQ_FAIL_BIN:$FAKE_BIN:$BASE_PATH" run_case "cursor adapter: result rendering fails" \
  adapters/cursor.sh '{"hook_event_name":"preToolUse","tool_name":"Shell","tool_input":{"command":"ls"}}' "cursor-adapter result-unparsable" FAIL_JQ_ON='--argjson rc'

exit $fail
