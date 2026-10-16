#!/usr/bin/env bash
# aw:meta-lane-gate — the lane hook for a non-Claude Meta lane (Codex, Cursor), installed user-wide by scripts/install-meta.sh
# through the provider adapters (config/hooks/adapters/README.md). Claude lanes use meta-stop-gate.sh and SendMessage instead.
# It acts only in a lane: AW_META_TEAM and AW_META_LANE set by scripts/meta-lane.sh; anywhere else it exits 0 at once.
#   UserPromptSubmit (Codex)              prints `aw-meta lane-inbox` as plain text: the adapter makes it developer context
#   SessionStart, PostToolUse (Cursor)    the same text as hookSpecificOutput.additionalContext (cursor.sh -> additional_context)
#   Stop                                  records the reply, then asks `aw-meta lane-stop-check`: the turn's last assistant
#                                         text after a `REPLY TO META:` line goes to `aw-meta lane-reply --stdin` (the hook runs
#                                         outside the agent's sandbox, so the agent never needs to write the state dir); exit 1
#                                         from lane-stop-check = the lane still owes a reply -> exit 2 with the reason on stderr
#                                         (Codex: the stop is blocked; Cursor: followup_message, capped by loop_limit)
# Only lane-stop-check exit 1 blocks. Exit 0, exit 2 (variables unset, no team config, a tool error) and anything else pass,
# the cause logged by failopen.
set -uo pipefail
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen meta-lane-gate "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# A pass-through reads its stdin first, so the host's write never meets a closed pipe.
pass() { cat > /dev/null 2>&1; exit 0; }
[ -n "${AW_JUDGE_CHILD:-}" ] && pass
[ -n "${AW_META_TEAM:-}" ] && [ -n "${AW_META_LANE:-}" ] || pass

META="${AW_META_BIN:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-meta}"
[ -x "$META" ] || pass   # not installed: nothing to gate

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
command -v jq > /dev/null 2>&1 || { failopen jq-missing; exit 0; }
EVENT="$(printf '%s' "$INPUT" | jq -r '.hook_event_name // empty' 2>/dev/null)" || { failopen input-unparsable; exit 0; }

case "$EVENT" in
  UserPromptSubmit|SessionStart|PostToolUse)
    OUT="$(AW_LOG_LEVEL=none "$META" lane-inbox 2>&1)"; rc=$?
    [ "$rc" = 0 ] || { failopen "lane-inbox-exit-$rc"; exit 0; }
    [ -n "$OUT" ] || exit 0
    if [ "$EVENT" = UserPromptSubmit ]; then
      printf '%s\n' "$OUT"
    else
      jq -nc --arg e "$EVENT" --arg m "$OUT" '{hookSpecificOutput: {hookEventName: $e, additionalContext: $m}}' 2> /dev/null
    fi
    exit 0 ;;
  Stop)
    ACTIVE="$(printf '%s' "$INPUT" | jq -r '.stop_hook_active // false' 2>/dev/null)"
    LAST="$(printf '%s' "$INPUT" | jq -r '.last_assistant_message // empty' 2>/dev/null)"
    # The text after the last line that starts with the marker (the rest of that line and every line after it).
    REPLY="$(printf '%s\n' "$LAST" | awk '
      { if (match($0, /^[[:space:]]*REPLY TO META:/)) { found = 1; sub(/^[[:space:]]*REPLY TO META:[[:space:]]*/, ""); text = $0; next }
        if (found) text = text "\n" $0 }
      END { if (found) print text }')"
    if [ -n "$(printf '%s' "$REPLY" | tr -d '[:space:]')" ]; then
      printf '%s\n' "$REPLY" | AW_LOG_LEVEL=none "$META" lane-reply --stdin > /dev/null 2>&1 || failopen "lane-reply-exit-$?"
    fi
    args=(lane-stop-check)
    [ "$ACTIVE" = true ] && args+=(--stop-hook-active)
    OUT="$(AW_LOG_LEVEL=none "$META" "${args[@]}" 2>&1)"; rc=$?
    case "$rc" in
      0) # the loop guard's "stop allowed ..." warning: shown to the user
         [ -z "$OUT" ] || jq -nc --arg m "$OUT" '{systemMessage: $m}' 2> /dev/null
         exit 0 ;;
      1) printf '%s\n' "$OUT" >&2; exit 2 ;;
      *) failopen "lane-stop-check-exit-$rc"; exit 0 ;;
    esac ;;
  *) exit 0 ;;
esac
