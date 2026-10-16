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

# own_agent: false when this hook runs for another agent instance started inside the lane (a `codex exec` or `cursor-agent -p` that
# the lane's agent ran; it inherits the lane variables). scripts/meta-lane.sh exports AW_META_LANE_PID (its own pid, the parent of the
# lane's agent) and AW_META_LANE_PROC (the agent's process name). Walking up from this hook, the lane's agent is the ancestor whose
# parent is the lane script; another ancestor named like the agent means a nested instance. A hook with no lane script among its
# ancestors (a human's codex in the pane after the lane's agent exited) is not the lane's either. Without the variables (a lane
# started by an older meta-lane.sh) every session in the lane counts as the lane's; if ps can't tell, the hook acts.
named_like_agent() { # <pid>: its command name or its argv[0] is the agent's process name
  local c a
  c="$(ps -o comm= -p "$1" 2>/dev/null)"; a="$(ps -o args= -p "$1" 2>/dev/null)"; AGENT_ARGS="$a"; a="${a%% *}"; a="${a##*/}"
  [ "$c" = "$AW_META_LANE_PROC" ] || [ "$a" = "$AW_META_LANE_PROC" ]
}
own_agent() {
  [ -n "${AW_META_LANE_PID:-}" ] && [ -n "${AW_META_LANE_PROC:-}" ] || return 0
  # runs counts separate stretches of agent-named ancestors: forks of one instance (a subshell, a worker) share their parent's
  # command line and are one stretch; a nested instance sits behind a tool shell or has its own command line, so it is another.
  local cur=$$ par runs=0 root_named=0 i=0 prev_named=0 prev_args=""
  while [ "$i" -lt 50 ]; do
    i=$((i + 1))
    par="$(ps -o ppid= -p "$cur" 2>/dev/null | tr -d '[:space:]')"
    case "$par" in ''|*[!0-9]*) return 0 ;; esac
    if [ "$par" = "$AW_META_LANE_PID" ]; then
      [ "$cur" != "$$" ] || return 0
      [ "$prev_named" = 1 ] && root_named=1   # the last ancestor visited is the agent's root
      [ $((runs - root_named)) -le 0 ]
      return
    fi
    [ "$par" -gt 1 ] || return 1
    cur="$par"
    if named_like_agent "$cur"; then
      if [ "$prev_named" = 0 ] || [ "$AGENT_ARGS" != "$prev_args" ]; then runs=$((runs + 1)); fi
      prev_named=1; prev_args="$AGENT_ARGS"
    else
      prev_named=0
    fi
  done
  return 1
}

META="${AW_META_BIN:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/bin/aw-meta}"
[ -x "$META" ] || pass   # not installed: nothing to gate
own_agent || pass        # a nested agent instance inside the lane is not the lane
# A lane name becomes a path component (the reply marker): letters, digits, '.', '_' and '-', and not only dots.
{ [[ "$AW_META_LANE" =~ ^[A-Za-z0-9._-]+$ ]] && [[ "$AW_META_LANE" =~ [^.] ]]; } || { failopen lane-name-invalid; pass; }
REPLIED="${AW_STATE_DIR:-$HOME/.agentic-workflow}/meta/lane-sessions/$AW_META_LANE.replied"   # the checksum of the reply last recorded
# code_of <output>: "-AW-META-NNN" when aw-meta's first line is a coded error, appended to a failopen cause (the code, never the text)
code_of() { if [[ "$1" =~ ^(AW-META-[0-9][0-9][0-9]): ]]; then printf -- '-%s' "${BASH_REMATCH[1]}"; fi; }

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
command -v jq > /dev/null 2>&1 || { failopen jq-missing; exit 0; }
EVENT="$(printf '%s' "$INPUT" | jq -r '.hook_event_name // empty' 2>/dev/null)" || { failopen input-unparsable; exit 0; }

case "$EVENT" in
  UserPromptSubmit|SessionStart|PostToolUse)
    OUT="$(AW_LOG_LEVEL=none "$META" lane-inbox 2>&1)"; rc=$?
    [ "$rc" = 0 ] || { failopen "lane-inbox-exit-$rc$(code_of "$OUT")"; exit 0; }
    [ -n "$OUT" ] || exit 0
    unlink "$REPLIED" 2> /dev/null   # new mail: the next reply is a new one, even if its text repeats
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
      # Record a reply once: a later Stop whose last assistant text is the same (a continuation, a turn with no new text, Cursor's
      # transcript tail) must not open the meta's reply obligation again. Mail delivered since (above) clears the marker.
      SUM="$(printf '%s' "$REPLY" | cksum | tr ' ' ':')"
      if [ "$(cat "$REPLIED" 2> /dev/null)" != "$SUM" ]; then
        RO="$(printf '%s\n' "$REPLY" | AW_LOG_LEVEL=none "$META" lane-reply --stdin 2>&1)"; rrc=$?
        if [ "$rrc" = 0 ]; then
          mkdir -p "$(dirname "$REPLIED")" 2> /dev/null && printf '%s\n' "$SUM" > "$REPLIED" 2> /dev/null
        else
          failopen "lane-reply-exit-$rrc$(code_of "$RO")"
        fi
      fi
    fi
    args=(lane-stop-check)
    [ "$ACTIVE" = true ] && args+=(--stop-hook-active)
    OUT="$(AW_LOG_LEVEL=none "$META" "${args[@]}" 2>&1)"; rc=$?
    case "$rc" in
      0) # the loop guard's "stop allowed ..." warning: shown to the user (Codex; the Cursor adapter drops it, and
         # `aw-meta health` shows the stop cap either way)
         [ -z "$OUT" ] || jq -nc --arg m "$OUT" '{systemMessage: $m}' 2> /dev/null
         exit 0 ;;
      1) printf '%s\n' "$OUT" >&2; exit 2 ;;
      *) failopen "lane-stop-check-exit-$rc$(code_of "$OUT")"; exit 0 ;;
    esac ;;
  *) exit 0 ;;
esac
