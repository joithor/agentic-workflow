#!/usr/bin/env bash
# aw:session-id-report — SessionStart hook for Codex and Cursor (installed user-wide by scripts/install-meta.sh, through the
# provider adapters). Reports the session's own id to the host that launched it, so lanes started together each record their
# own id instead of guessing from file timing (launcher spec section 4):
#   AW_META_TEAM + AW_META_LANE  ->  $AW_STATE_DIR/meta/lane-sessions/<lane>          (scripts/meta-lane.sh waits for it)
#   AW_SINDRI_LAUNCH             ->  $AW_SINDRI_HOME/launches/<launchId>.session-id    (Sindri's launcher)
# The key is a variable the host set on that one session, so two sessions started together can't swap ids. Outside a lane or a
# Sindri launch it exits 0 at once, writing nothing. The id is written atomically and 0600. Reads the id from `session_id`
# (cursor.sh maps Cursor's conversation_id to it). Prints nothing; any problem fails open.
set -uo pipefail
failopen() {
  (
    set +eu
    d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || exit 0
    for f in "$d/lib/failopen.sh" "$d/../lib/failopen.sh"; do
      if [ -f "$f" ]; then . "$f" && aw_failopen session-id-report "$@"; exit 0; fi
    done
  ) > /dev/null 2>&1 || true
}
# A pass-through reads its stdin first, so the host's write never meets a closed pipe.
pass() { cat > /dev/null 2>&1; exit 0; }
[ -n "${AW_JUDGE_CHILD:-}" ] && pass
lane_mode=0; [ -n "${AW_META_TEAM:-}" ] && [ -n "${AW_META_LANE:-}" ] && lane_mode=1
[ "$lane_mode" = 1 ] || [ -n "${AW_SINDRI_LAUNCH:-}" ] || pass

INPUT="$(cat 2>/dev/null || true)"
[ -n "$INPUT" ] || { failopen empty-input; exit 0; }
command -v jq > /dev/null 2>&1 || { failopen jq-missing; exit 0; }
ID="$(printf '%s' "$INPUT" | jq -r '.session_id // empty' 2>/dev/null)" || { failopen input-unparsable; exit 0; }
[ -n "$ID" ] || { failopen field-missing; exit 0; }
# The generic bound on an id: it becomes file content on a line of its own, never a path or a command.
[[ "$ID" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || { failopen id-invalid; exit 0; }

# A name that becomes a path component: letters, digits, '.', '_' and '-', and not only dots.
safe_name() { [[ "$1" =~ ^[A-Za-z0-9._-]+$ ]] && [[ "$1" =~ [^.] ]]; }
# write_id <dir> <file>: atomically, 0600.
write_id() {
  local tmp
  mkdir -p "$1" 2> /dev/null || return 1
  tmp="$(umask 077 && mktemp "$1/.$2.XXXXXX")" || return 1
  if printf '%s\n' "$ID" > "$tmp" && chmod 600 "$tmp" && mv -f "$tmp" "$1/$2"; then return 0; fi
  unlink "$tmp" 2> /dev/null
  return 1
}

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

# A nested agent instance inside the lane (codex exec, cursor-agent -p) must not take the lane's session id.
if [ "$lane_mode" = 1 ] && ! own_agent; then lane_mode=0; fi
STATE="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
if [ "$lane_mode" = 1 ]; then
  safe_name "$AW_META_LANE" || { failopen lane-name-invalid; exit 0; }
  write_id "$STATE/meta/lane-sessions" "$AW_META_LANE" || failopen write-failed
fi
if [ -n "${AW_SINDRI_LAUNCH:-}" ]; then
  if safe_name "$AW_SINDRI_LAUNCH"; then
    write_id "${AW_SINDRI_HOME:-$STATE/sindri-app}/launches" "$AW_SINDRI_LAUNCH.session-id" || failopen write-failed
  else
    failopen launch-id-invalid
  fi
fi
exit 0
