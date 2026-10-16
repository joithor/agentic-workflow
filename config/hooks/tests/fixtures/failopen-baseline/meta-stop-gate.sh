#!/usr/bin/env bash
# aw:meta-stop-gate — Stop hook (Claude Code only). Keeps the meta orchestrator from going idle while it owes
# something: an inbound peer message with no reply, a lane map or a "main moved" announcement not yet sent, an owner
# answer not relayed, a lapsed watch. `aw-meta stop-check` syncs the inbox from the transcript and decides; this
# script only maps its exit code. Any session that isn't the registered meta session passes straight through
# (aw-meta answers 0 at once). Exit 1 from aw-meta = obligations open → exit 2 with the list on stderr (fed back
# to the model). Everything else fails open.
set -uo pipefail
failopen() { :; }   # baseline: the hook without its fail-open logging
[ -n "${AW_JUDGE_CHILD:-}" ] && exit 0

# The team: AW_META_TEAM when the lane launcher set it; else the tmux socket the session runs on (the launcher names it after the
# team), taken only when that team exists on disk (a named team, or the legacy config's own team). Anything else: no team selected.
if [ -z "${AW_META_TEAM:-}" ] && [ -n "${TMUX:-}" ]; then
  sock="${TMUX%%,*}"; sock="${sock##*/}"
  mroot="${AW_STATE_DIR:-$HOME/.agentic-workflow}/meta"
  if [[ "$sock" =~ ^[a-z0-9][a-z0-9-]*$ ]] && { [ -d "$mroot/teams/$sock" ] || [ "$(jq -r '.team // empty' "$mroot/team.json" 2> /dev/null)" = "$sock" ]; }; then
    export AW_META_TEAM="$sock"
  fi
fi
META="${AW_META_BIN:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-meta}"
[ -x "$META" ] || exit 0   # not installed: nothing to gate

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
command -v jq > /dev/null 2>&1 || { failopen jq-missing; exit 0; }
SESSION="$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2>/dev/null)" || { failopen input-unparsable; exit 0; }
TRANSCRIPT="$(printf '%s' "$INPUT" | jq -r '.transcript_path // empty' 2>/dev/null)"
ACTIVE="$(printf '%s' "$INPUT" | jq -r '.stop_hook_active // false' 2>/dev/null)"
[ -n "$SESSION" ] && [ -n "$TRANSCRIPT" ] || { failopen field-missing; exit 0; }

args=(stop-check --session "$SESSION" --transcript "$TRANSCRIPT")
[ "$ACTIVE" = "true" ] && args+=(--hook-active)
OUT="$(AW_LOG_LEVEL=none "$META" "${args[@]}" 2>&1)"
rc=$?
case "$rc" in
  0) # the loop guard's "stop allowed …" warning: shown to the user, since exit 0 output otherwise goes nowhere
     [ -z "$OUT" ] || jq -nc --arg m "$OUT" '{systemMessage: $m}' 2> /dev/null
     exit 0 ;;
  1) printf '%s\n' "$OUT" >&2; exit 2 ;;
  *) failopen "meta-exit-$rc"; exit 0 ;;
esac
