#!/usr/bin/env bash
# meta-lane.sh <team> <lane>: run one lane's Claude session inside its tmux session (scripts/meta-launch.sh starts it).
# The session is named <team>-<lane> (`claude -n`), so its name in the agent list never changes across restarts. It
# resumes the lane's recorded session id when that session has a transcript; otherwise it starts a fresh session with
# a new id, records the id, and sends the lane's kickoff prompt (<prompts>/<lane>.md) when there is one. For the meta
# lane it also registers the session with aw-meta (role-set), which arms the meta hooks for it. Drops to a shell when
# claude exits, so the pane survives.
#   AW_META_CONFIG       team config (default $AW_STATE_DIR/meta/team.json): repo, prompts, metaLane
#   AW_META_CLAUDE       the claude command (default claude; tests)
#   AW_META_CLAUDE_ARGS  extra claude arguments (default none: setup.sh sets permissions.defaultMode to
#                        bypassPermissions, so every lane runs in the same mode and messages are never held)
#   AW_META_NO_SHELL     1: exit when claude exits instead of dropping to a shell (tests)
set -u
team="${1:?usage: meta-lane.sh <team> <lane>}"
lane="${2:?usage: meta-lane.sh <team> <lane>}"
for n in "$team" "$lane"; do
  case "$n" in *[!a-z0-9-]*|"") echo "meta-lane: bad name '$n' (lowercase letters, digits, dashes)" >&2; exit 2 ;; esac
done
state_dir="${AW_STATE_DIR:-$HOME/.agentic-workflow}"
config="${AW_META_CONFIG:-$state_dir/meta/team.json}"
cfg() { [ -f "$config" ] && jq -r "$1 // empty" "$config" 2> /dev/null; }
repo="$(cfg .repo)"; repo="${repo:-$PWD}"
prompts="$(cfg .prompts)"
meta_lane="$(cfg .metaLane)"; meta_lane="${meta_lane:-meta}"
claude_cmd="${AW_META_CLAUDE:-claude}"
read -r -a claude_args <<< "${AW_META_CLAUDE_ARGS:-}"
meta_bin="${AW_META_BIN:-$state_dir/bin/aw-meta}"
ids="$state_dir/meta/lane-sessions"
mkdir -p "$ids"
cd "$repo" || exit 1

has_transcript() { compgen -G "$HOME/.claude/projects/*/$1.jsonl" > /dev/null; }
id=""
[ -f "$ids/$lane" ] && id="$(tr -d '[:space:]' < "$ids/$lane")"
if [ -n "$id" ] && has_transcript "$id"; then
  mode=resume
else
  id="$(cat /proc/sys/kernel/random/uuid 2> /dev/null || uuidgen)"
  mode=fresh
fi
printf '%s\n' "$id" > "$ids/$lane.tmp" && mv "$ids/$lane.tmp" "$ids/$lane"
if [ "$lane" = "$meta_lane" ] && [ -x "$meta_bin" ]; then
  AW_LOG_LEVEL=none "$meta_bin" role-set --session "$id" --name "$team-$lane" > /dev/null 2>&1 \
    || echo "meta-lane: aw-meta role-set failed; run it by hand in the meta session" >&2
fi
name="$team-$lane"
# A launcher run from inside a Claude session (the meta recreating a dead lane) must not hand the lane that session's
# identity: its id, pid, messaging socket and token. Only these are cleared; user settings such as model or
# provider variables pass through.
unset CLAUDECODE CLAUDE_PID CLAUDE_CODE_SESSION_ID CLAUDE_CODE_CHILD_SESSION CLAUDE_CODE_MESSAGING_SOCKET \
  CLAUDE_CODE_MESSAGING_TOKEN CLAUDE_CODE_BRIDGE_SESSION_ID CLAUDE_CODE_ENTRYPOINT CLAUDE_CODE_SESSION_ATTENDED \
  CLAUDE_CODE_EXECPATH
if [ "$mode" = resume ]; then
  "$claude_cmd" ${claude_args[@]+"${claude_args[@]}"} --resume "$id" -n "$name"
elif [ -n "$prompts" ] && [ -f "$prompts/$lane.md" ]; then
  "$claude_cmd" ${claude_args[@]+"${claude_args[@]}"} --session-id "$id" -n "$name" "$(cat "$prompts/$lane.md")"
else
  "$claude_cmd" ${claude_args[@]+"${claude_args[@]}"} --session-id "$id" -n "$name"
fi
[ "${AW_META_NO_SHELL:-0}" = 1 ] || exec bash -i
