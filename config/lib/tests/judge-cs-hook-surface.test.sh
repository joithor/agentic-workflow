#!/usr/bin/env bash
# config/lib/tests/judge-cs-hook-surface.test.sh — the frozen hook surface (spec §4a step 3, §4c).
# The contract is only what the hooks read (spec §4c); the rest of the C# CLI is its own design.
# Runs the real hook scripts in config/hooks/ with the C# judge (src/Judge) first on PATH, offline
# (AW_JUDGE_PROVIDERS=rules: pre-rules and rule fallbacks only, no model call), and checks each
# hook's stdout, exit code and the files it leaves in a scratch AW_STATE_DIR.
# When the TS judge is built (judge/dist/cli.js), every case also runs against it and the two runs
# must agree on exit codes, stderr and parsed JSON (keys sorted, ids and times masked), so a change on either side that moves the surface fails.
#
# AW_JUDGE_CS_BIN  a built C# judge (the NativeAOT publish or the JIT apphost). Unset: the test builds
#                  src/Judge/Aw.Judge.Host in Release into a temp dir first (needs dotnet 10).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HOOKS="$ROOT/config/hooks"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

if [ -z "${AW_JUDGE_CS_BIN:-}" ]; then
  dotnet build "$ROOT/src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj" -c Release -o "$WORK/cs-build" -warnaserror -nologo -v quiet > "$WORK/build.log" 2>&1 \
    || { cat "$WORK/build.log"; echo "FAIL: could not build the C# judge"; exit 1; }
  AW_JUDGE_CS_BIN="$WORK/cs-build/judge"
fi
[ -x "$AW_JUDGE_CS_BIN" ] || { echo "FAIL: no C# judge at $AW_JUDGE_CS_BIN"; exit 1; }

SIDES="cs"
TS_CLI="$ROOT/judge/dist/cli.js"
if [ -f "$TS_CLI" ] && command -v node > /dev/null 2>&1; then SIDES="cs ts"; else echo "NOTE: judge/dist not built; checking the C# judge against the recorded expectations only"; fi

# One PATH shim per side, so the hooks call `judge` exactly as installed hooks do.
mkdir -p "$WORK/shim-cs" "$WORK/shim-ts"
printf '#!/usr/bin/env bash\nexec "%s" "$@"\n' "$AW_JUDGE_CS_BIN" > "$WORK/shim-cs/judge"
printf '#!/usr/bin/env bash\nexec node "%s" "$@"\n' "$TS_CLI" > "$WORK/shim-ts/judge"
chmod +x "$WORK/shim-cs/judge" "$WORK/shim-ts/judge"

export AW_JUDGE_PROVIDERS=rules AW_LOG_LEVEL=none
unset AW_OUTBOX_DIR AW_TEAMMATE_NAMES_FILE AW_JUDGE_SESSIONS_DIR AW_JUDGE_CHILD 2> /dev/null || true

# fresh <side> <case>: a new scratch state dir for this side and case
fresh() { local d="$WORK/state-$1-$2"; rm -rf "$d"; mkdir -p "$d/judge"; echo "$d"; }

# hook <side> <state> <script> <stdin> -> writes $WORK/<case>.<side>.{out,err,rc}
hook() {
  local side="$1" state="$2" script="$3" input="$4" name="$5" rc=0
  PATH="$WORK/shim-$side:$PATH" AW_STATE_DIR="$state" bash "$HOOKS/$script" <<< "$input" \
    > "$WORK/$name.$side.out" 2> "$WORK/$name.$side.err" || rc=$?
  echo "$rc" > "$WORK/$name.$side.rc"
}

# judge <side> <state> args... — the same side's judge, for setup and for reading state back
judge_on() { local side="$1" state="$2"; shift 2; PATH="$WORK/shim-$side:$PATH" AW_STATE_DIR="$state" judge "$@"; }

# The contract is field names, values and exit codes, not formatting: JSON lines are compared after jq -S
# (sorted keys, canonical numbers and escapes), with ids and timestamps masked.
norm() {
  [ -f "$1" ] || return 0
  while IFS= read -r line || [ -n "$line" ]; do
    printf '%s' "$line" | jq -cS '.' 2> /dev/null || printf '%s\n' "$line"
  done < "$1" | sed -E 's/[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:.]+Z/<TS>/g; s/"id":"[^"]+"/"id":"<ID>"/g'
}

# state_dump <state>: every file the hooks wrote (not the judge's own sqlite), normalized
state_dump() {
  local state="$1" f
  (cd "$state" && find . -type f ! -name 'decisions.sqlite*' ! -name 'config.json' | LC_ALL=C sort) | while read -r f; do
    echo "== $f"; norm "$state/$f"
  done
}

FAILS=0
fail() { echo "FAIL: $*"; FAILS=$((FAILS + 1)); }

# same <case>: the cs and ts runs agree (stdout, stderr, exit code, state files)
same() {
  local name="$1"
  [ "$SIDES" = "cs ts" ] || return 0
  diff <(norm "$WORK/$name.cs.out") <(norm "$WORK/$name.ts.out") > /dev/null || fail "$name: stdout differs from the TS judge: $(diff <(norm "$WORK/$name.cs.out") <(norm "$WORK/$name.ts.out") | head -5)"
  diff <(norm "$WORK/$name.cs.err") <(norm "$WORK/$name.ts.err") > /dev/null || fail "$name: stderr differs from the TS judge"
  diff "$WORK/$name.cs.rc" "$WORK/$name.ts.rc" > /dev/null || fail "$name: exit $(cat "$WORK/$name.cs.rc") vs TS $(cat "$WORK/$name.ts.rc")"
  diff <(state_dump "$WORK/state-cs-$name") <(state_dump "$WORK/state-ts-$name") > /dev/null || fail "$name: hook-written files differ from the TS judge's run"
}

rc_of() { cat "$WORK/$1.cs.rc"; }
out_of() { cat "$WORK/$1.cs.out"; }

# ---- send-gate (wake-gate) ----
for side in $SIDES; do
  s="$(fresh "$side" send-urgent)"; hook "$side" "$s" send-gate.sh '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"blocked: need your review"}}' send-urgent
  s="$(fresh "$side" send-ack)"; hook "$side" "$s" send-gate.sh '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"ok"}}' send-ack
  s="$(fresh "$side" send-undecided)"; hook "$side" "$s" send-gate.sh '{"session_id":"s1","agent_type":"builder-a","tool_input":{"to":"main","message":"still working through the second task now"}}' send-undecided
done
out_of send-urgent | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || fail "send-urgent: expected allow, got $(out_of send-urgent)"
out_of send-ack | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || fail "send-ack: expected deny (pure ack dropped), got $(out_of send-ack)"
# rules-only chain: no model decides, the rule fallback says send -> allow
out_of send-undecided | jq -e '.hookSpecificOutput.permissionDecision == "allow"' > /dev/null || fail "send-undecided: expected allow, got $(out_of send-undecided)"
for c in send-urgent send-ack send-undecided; do [ "$(rc_of $c)" = 0 ] || fail "$c: exit $(rc_of $c)"; same $c; done

# ---- scope-gate (brief-scope + brief save) ----
for side in $SIDES; do
  s="$(fresh "$side" scope-exempt)"
  hook "$side" "$s" scope-gate.sh '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu-x","tool_input":{"subagent_type":"Explore","name":"scout","description":"find the config loader","prompt":"Acceptance: list the files. Proof: rg config"}}' scope-exempt
  judge_on "$side" "$s" brief get tu-x > "$WORK/scope-exempt.$side.brief" 2>&1 || true
  s="$(fresh "$side" scope-empty)"
  hook "$side" "$s" scope-gate.sh '{"session_id":"s1","prompt_id":"p1","tool_use_id":"tu-y","tool_input":{"subagent_type":"general-purpose","description":"","prompt":""}}' scope-empty
done
out_of scope-exempt | jq -e '(.hookSpecificOutput.permissionDecision // "allow") == "allow"' > /dev/null 2>&1 || [ -z "$(out_of scope-exempt)" ] || fail "scope-exempt: expected allow, got $(out_of scope-exempt)"
out_of scope-empty | jq -e '.hookSpecificOutput.permissionDecision == "deny"' > /dev/null || fail "scope-empty: expected deny, got $(out_of scope-empty)"
if [ "$SIDES" = "cs ts" ]; then
  # Only what hooks read back from a brief: its id and acceptance criteria (done-gate, done-gate-annotate).
  diff <(jq -S '{toolUseId, acceptanceCriteria}' "$WORK/scope-exempt.cs.brief" 2> /dev/null) <(jq -S '{toolUseId, acceptanceCriteria}' "$WORK/scope-exempt.ts.brief" 2> /dev/null) > /dev/null \
    || fail "scope-exempt: the saved brief differs: cs=$(cat "$WORK/scope-exempt.cs.brief") ts=$(cat "$WORK/scope-exempt.ts.brief")"
fi
for c in scope-exempt scope-empty; do same $c; done

# ---- subagent-start-map (map-by-name / map-by-dispatch + set-agent-id) ----
for side in $SIDES; do
  s="$(fresh "$side" map-name)"
  printf '%s' '{"toolUseId":"tu-a","sessionId":"s1","promptId":"p1","dispatchName":"builder-a","subagentType":"general-purpose","goal":"g","acceptanceCriteria":"a","proofCommand":"p"}' | judge_on "$side" "$s" brief save
  echo '{"name":"builder-a"}' > "$s/judge/teammate-names.jsonl"
  hook "$side" "$s" subagent-start-map.sh '{"agent_id":"agent-1","agent_type":"builder-a","session_id":"s1","prompt_id":"p1"}' map-name
  judge_on "$side" "$s" brief get tu-a | jq -r '.agentId' > "$WORK/map-name.$side.agent"
  s="$(fresh "$side" map-dispatch)"
  printf '%s' '{"toolUseId":"tu-b","sessionId":"s2","promptId":"p9","dispatchName":null,"subagentType":"lean-coder","goal":"g","acceptanceCriteria":"a","proofCommand":"p"}' | judge_on "$side" "$s" brief save
  hook "$side" "$s" subagent-start-map.sh '{"agent_id":"agent-2","agent_type":"lean-coder","session_id":"s2","prompt_id":"p9"}' map-dispatch
  judge_on "$side" "$s" brief get tu-b | jq -r '.agentId' > "$WORK/map-dispatch.$side.agent"
done
[ "$(cat "$WORK/map-name.cs.agent")" = agent-1 ] || fail "map-name: agentId $(cat "$WORK/map-name.cs.agent"), expected agent-1"
[ "$(cat "$WORK/map-dispatch.cs.agent")" = agent-2 ] || fail "map-dispatch: agentId $(cat "$WORK/map-dispatch.cs.agent"), expected agent-2"
for c in map-name map-dispatch; do [ "$(rc_of $c)" = 0 ] || fail "$c: exit $(rc_of $c)"; same $c; done

# ---- done-gate-annotate (brief get) ----
for side in $SIDES; do
  s="$(fresh "$side" annotate)"
  printf '%s' '{"toolUseId":"tu-c","sessionId":"s1","promptId":"p1","dispatchName":null,"subagentType":"lean-coder","goal":"g","acceptanceCriteria":"all tests pass","proofCommand":"npm test"}' | judge_on "$side" "$s" brief save
  hook "$side" "$s" done-gate-annotate.sh '{"tool_use_id":"tu-c","tool_response":{"result":"Finished: all tests pass (npm test)."}}' annotate
done
out_of annotate | jq -e '.hookSpecificOutput.additionalContext | startswith("Brief acceptance criteria plausibly met")' > /dev/null || fail "annotate: got $(out_of annotate)"
same annotate

# ---- done-gate (ask-check, brief get task:<sid>) ----
transcript() { jq -nc --arg t "$2" '{type:"assistant", message:{content:[{type:"text", text:$t}]}}' > "$1"; }
transcript "$WORK/t-merge.jsonl" "Next I will merge the release branch."
transcript "$WORK/t-claim.jsonl" "Done — all tests pass, ran npm test."
for side in $SIDES; do
  s="$(fresh "$side" done-ask)"; hook "$side" "$s" done-gate.sh "$(jq -nc --arg t "$WORK/t-merge.jsonl" '{transcript_path:$t, session_id:"s1"}')" done-ask
  s="$(fresh "$side" done-brief)"
  printf '%s' '{"toolUseId":"task:s1","sessionId":"s1","promptId":"p1","dispatchName":null,"subagentType":"main","goal":"g","acceptanceCriteria":"all tests pass","proofCommand":"npm test"}' | judge_on "$side" "$s" brief save
  hook "$side" "$s" done-gate.sh "$(jq -nc --arg t "$WORK/t-claim.jsonl" '{transcript_path:$t, session_id:"s1"}')" done-brief
  s="$(fresh "$side" done-nobrief)"; hook "$side" "$s" done-gate.sh "$(jq -nc --arg t "$WORK/t-claim.jsonl" '{transcript_path:$t, session_id:"s1"}')" done-nobrief
done
[ "$(rc_of done-ask)" = 0 ] || fail "done-ask: a merge next step must stop to ask (exit 0), got $(rc_of done-ask)"
[ ! -e "$WORK/state-cs-done-ask/judge/sessions/s1.json" ] || fail "done-ask: must not record auto_continued_at"
[ "$(rc_of done-brief)" = 0 ] || fail "done-brief: a claim meeting the brief must pass, got $(rc_of done-brief)"
[ "$(rc_of done-nobrief)" = 0 ] || fail "done-nobrief: a claim with evidence and no brief must pass, got $(rc_of done-nobrief)"
grep -q 'judge-brief-exit' "$WORK/state-cs-done-nobrief/logs/failopen.log" 2> /dev/null && fail "done-nobrief: 'no brief' must not log a fail-open"
for c in done-ask done-brief done-nobrief; do same $c; done

# ---- judge-health (SessionStart) ----
for side in $SIDES; do s="$(fresh "$side" health)"; hook "$side" "$s" judge-health.sh '' health; done
[ -z "$(out_of health)" ] || fail "health: a fresh judge is ok and the hook prints nothing, got $(out_of health)"
[ "$(rc_of health)" = 0 ] || fail "health: exit $(rc_of health)"
same health

if [ "$FAILS" -gt 0 ]; then echo "judge-cs-hook-surface: $FAILS failure(s) (sides: $SIDES)"; exit 1; fi
echo "All judge-cs-hook-surface tests passed (sides: $SIDES)."
