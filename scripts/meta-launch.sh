#!/usr/bin/env bash
# meta-launch.sh — stand up a meta-orchestrated team in one Warp window: a pane per lane (the meta lane first, then
# the team config's lanes), each attached to tmux session <team>-<lane> running that lane's Claude session
# (scripts/meta-lane.sh: resumed by id when one exists, else started from its kickoff prompt). Missing tmux sessions
# are recreated first (tmux may have died with the box), and a lane whose claude exited is restarted in place, so
# the panes only attach. Writes the Warp launch configuration and opens it. One command restores the whole team.
#   meta-launch.sh [--lanes a,b,...] [--no-open] [--dry-run]
#   AW_META_CONFIG      team config (default $AW_STATE_DIR/meta/team.json; `aw-meta team-init` writes it)
#   AW_WARP_LAUNCH_DIR  default ${XDG_DATA_HOME:-~/.local/share}/warp-terminal/launch_configurations (Linux),
#                       ~/.warp/launch_configurations (macOS)
set -euo pipefail
here="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"   # through the ~/.local/bin/aw-meta-launch symlink too
only=""; open=1; dry=0
while [ $# -gt 0 ]; do
  case "$1" in
    --lanes) only="${2:?--lanes needs a list}"; shift 2 ;;
    --no-open) open=0; shift ;;
    --dry-run) dry=1; shift ;;
    *) echo "usage: meta-launch.sh [--lanes a,b,...] [--no-open] [--dry-run]" >&2; exit 2 ;;
  esac
done
command -v jq > /dev/null || { echo "meta-launch: jq is not installed" >&2; exit 1; }
command -v tmux > /dev/null || { echo "meta-launch: tmux is not installed" >&2; exit 1; }
config="${AW_META_CONFIG:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/meta/team.json}"
[ -f "$config" ] || { echo "meta-launch: no team config at $config (run: aw-meta team-init --team <t> --repo <dir> --lanes a,b)" >&2; exit 1; }
team="$(jq -r '.team // empty' "$config")"
repo="$(jq -r '.repo // empty' "$config")"
all="$(jq -r '[(.metaLane // "meta")] + (.lanes // []) | reduce .[] as $l ([]; if index([$l]) then . else . + [$l] end) | join(",")' "$config")"
IFS=, read -r -a A <<< "$all"        # every lane: the Warp layout always has a pane for each
IFS=, read -r -a L <<< "${only:-$all}"   # the lanes whose tmux sessions this run checks (--lanes)
for n in "$team" "${A[@]}" "${L[@]}"; do
  case "$n" in *[!a-z0-9-]*|"") echo "meta-launch: bad team or lane name '$n'" >&2; exit 2 ;; esac
done
case "$repo" in /*) [ -d "$repo" ] || { echo "meta-launch: repo $repo not found" >&2; exit 1; } ;;
  *) echo "meta-launch: repo must be an absolute path (Warp requires it): '$repo'" >&2; exit 1 ;; esac
if [ "$(uname -s)" = Darwin ]; then default_dir="$HOME/.warp/launch_configurations"
else default_dir="${XDG_DATA_HOME:-$HOME/.local/share}/warp-terminal/launch_configurations"; fi
dir="${AW_WARP_LAUNCH_DIR:-$default_dir}"
yaml="$dir/$team-team.yaml"

# 1. tmux sessions: create the missing ones (detached), keep the live ones.
for l in "${L[@]}"; do
  s="$team-$l"
  if tmux has-session -t "=$s" 2> /dev/null; then
    # Claude exited or crashed and the pane fell back to its shell: start the lane again in place (resumed by id).
    case "$(tmux display-message -p -t "=$s:" '#{pane_current_command}' 2> /dev/null)" in
      bash|zsh|sh|fish|dash)
        if [ "$dry" = 1 ]; then echo "[dry-run] tmux respawn-pane -k -t $s $here/meta-lane.sh $team $l"
        else
          tmux respawn-pane -k -t "=$s:" -c "$repo" -e "AW_META_CONFIG=$config" "$here/meta-lane.sh $team $l"
          echo "tmux $s: restarted (claude had exited)"
        fi ;;
      *) echo "tmux $s: running" ;;
    esac
  elif [ "$dry" = 1 ]; then echo "[dry-run] tmux new-session -d -s $s -c $repo $here/meta-lane.sh $team $l"
  else
    tmux new-session -d -s "$s" -c "$repo" -e "AW_META_CONFIG=$config" "$here/meta-lane.sh $team $l"
    echo "tmux $s: started"
  fi
done

# 2. Warp launch configuration: the first lane on the left, the others stacked on the right.
# The pane attaches, or creates the session itself with the same command as step 1, so a session that is missing when
# Warp opens (a --lanes run, a lane that exited) still runs the lane instead of a plain shell under its name.
attach() { printf 'tmux new -A -s %s -c %s -e AW_META_CONFIG=%s %s' "$team-$1" "$repo" "$config" "'$here/meta-lane.sh $team $1'"; }
pane() { # indent lane
  printf '%s- cwd: %s\n%s  commands:\n%s    - exec: %s\n' "$1" "$repo" "$1" "$1" "$(attach "$2")"
}
render() {
  echo "# Written by scripts/meta-launch.sh from $config; rerun it rather than editing."
  echo "name: $team team"
  echo "windows:"
  echo "  - tabs:"
  echo "      - title: $team"
  echo "        layout:"
  if [ "${#A[@]}" = 1 ]; then
    printf '          cwd: %s\n          commands:\n            - exec: %s\n' "$repo" "$(attach "${A[0]}")"
    return
  fi
  echo "          split_direction: vertical"
  echo "          panes:"
  pane "            " "${A[0]}"
  echo "            - split_direction: horizontal"
  echo "              panes:"
  for l in "${A[@]:1}"; do pane "                " "$l"; done
}
if [ "$dry" = 1 ]; then echo "[dry-run] would write $yaml:"; render; exit 0; fi
mkdir -p "$dir"
render > "$yaml.tmp"
mv "$yaml.tmp" "$yaml"
echo "warp: $yaml"

# 3. Open it in Warp.
if [ "$open" = 1 ]; then
  # Warp opens a launch configuration by its name (the YAML's name:, URI-encoded), not by its file path.
  uri="warp://launch/$(jq -rn --arg n "$team team" '$n|@uri')"
  echo "warp: opening $uri"
  if [ "$(uname -s)" = Darwin ]; then open "$uri"
  elif command -v xdg-open > /dev/null; then xdg-open "$uri" > /dev/null 2>&1 &
  elif command -v warp-terminal > /dev/null; then warp-terminal "$uri" > /dev/null 2>&1 &
  else echo "open $uri in Warp (Command Palette → Launch Configuration → $team team)"; fi
fi
