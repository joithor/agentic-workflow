#!/usr/bin/env bash
# aw:scope-gate — PreToolUse hook on Agent (lever 3). Gates a dispatch on a
# real brief (goal + acceptance criteria + proof command) via `judge
# brief-scope`, saving an approved brief so the done gate (Task 2) and the
# SubagentStart mapping (subagent-start-map.sh) can find it later.
set -uo pipefail
# HOOK-FAILOPEN: record why this hook is failing open (config/lib/failopen.sh,
# installed at <hooks>/lib/failopen.sh). Logging only: it runs in a subshell
# with its output discarded, so it can never change this hook's exit code,
# stdout or stderr, whatever set -e/-u or the environment do to it.
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen scope-gate "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# A crash (exit other than 0 or 2) lets the call through under Claude Code; count
# it. Under an adapter (AW_PROVIDER set) the adapter logs hook-exit-<N> instead.
# The trap doesn't call exit, so the hook's exit status is unchanged.
trap 'aw_rc=$?; [ -n "${AW_PROVIDER:-}" ] || case "$aw_rc" in 0|2) ;; *) failopen "exit-$aw_rc" ;; esac' EXIT

if [ -n "${AW_JUDGE_CHILD:-}" ]; then
  echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}'
  exit 0
fi

allow() {
  echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}'
  exit 0
}
deny() {
  jq -nc --arg r "$1" '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":$r}}'
  exit 0
}

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; allow; }

SESSION_ID="$(printf '%s' "$INPUT" | jq -r '.session_id // "unknown"')"
PROMPT_ID="$(printf '%s' "$INPUT" | jq -r '.prompt_id // "unknown"')"
TOOL_USE_ID="$(printf '%s' "$INPUT" | jq -r '.tool_use_id // empty')"
AGENT_TYPE="$(printf '%s' "$INPUT" | jq -r '.tool_input.subagent_type // empty')"
DISPATCH_NAME="$(printf '%s' "$INPUT" | jq -r '.tool_input.name // empty')"
GOAL="$(printf '%s' "$INPUT" | jq -r '.tool_input.description // empty')"
PROMPT_TEXT="$(printf '%s' "$INPUT" | jq -r '.tool_input.prompt // empty')"
# Spec F13: a skill's own internal orchestration dispatch sets this true by
# convention on its own Agent call's tool_input — exempt the same way a
# read-only agent type is (brief-scope.ts's own pre-rule).
SKILL_INTERNAL="$(printf '%s' "$INPUT" | jq -r '.tool_input.skill_internal // false')"
[ -n "$TOOL_USE_ID" ] || { failopen input "$INPUT" '.tool_name == "Agent"'; allow; }

# The brief lives inside the dispatch's own prompt text, per convention: a
# goal and acceptance criteria the dispatcher wrote there. This hook doesn't
# invent a new authoring surface — it reads what's already in tool_input.
ACCEPTANCE="$PROMPT_TEXT"
PROOF_COMMAND="$PROMPT_TEXT"

JUDGE_INPUT="$(jq -nc --arg t "$AGENT_TYPE" --arg g "$GOAL" --arg a "$ACCEPTANCE" --arg p "$PROOF_COMMAND" --argjson si "$SKILL_INTERNAL" \
  '{agentType: $t, goal: $g, acceptanceCriteria: $a, proofCommand: $p, skillInternal: $si}')"
JUDGE_OUT="$(printf '%s' "$JUDGE_INPUT" | AW_SESSION_ID="$SESSION_ID" judge brief-scope 2>/dev/null)" || { failopen "judge-exit-$?"; allow; }
DECISION="$(printf '%s' "$JUDGE_OUT" | jq -r '.decision // empty' 2>/dev/null)" || { failopen judge-output-unparsable; allow; }
[ -n "$DECISION" ] || { failopen judge-decision-missing; allow; }

case "$DECISION" in
  ready)
    SAVE_INPUT="$(jq -nc \
      --arg tu "$TOOL_USE_ID" --arg s "$SESSION_ID" --arg p "$PROMPT_ID" \
      --arg dn "$DISPATCH_NAME" --arg st "$AGENT_TYPE" \
      --arg g "$GOAL" --arg a "$ACCEPTANCE" --arg pc "$PROOF_COMMAND" \
      '{toolUseId: $tu, sessionId: $s, promptId: $p, dispatchName: (if $dn == "" then null else $dn end), subagentType: $st, goal: $g, acceptanceCriteria: $a, proofCommand: $pc}')"
    printf '%s' "$SAVE_INPUT" | judge brief save > /dev/null 2>&1 || true
    allow
    ;;
  missing)
    deny "Dispatch brief is missing a goal or acceptance criteria — add both to the prompt before dispatching."
    ;;
  needs_design)
    deny "This dispatch needs a design decision first — send it to the user instead of dispatching."
    ;;
  *)
    failopen judge-decision-unknown
    allow
    ;;
esac
