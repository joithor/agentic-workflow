#!/usr/bin/env bash
# Tests for scripts/meta-launch.sh and scripts/meta-lane.sh: the Warp launch configuration, tmux sessions recreated
# on a private tmux server, resume-by-id vs a fresh start, and the meta lane's role-set. A stub stands in for claude.
# Run: bash scripts/tests/meta-launch.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
command -v tmux > /dev/null || { echo "ok - skipped: tmux not installed"; exit 0; }
# golden_transcript <scripts-dir>: what scripts/meta-launch.sh and scripts/meta-lane.sh do with NO launcher.json, as one
# normalized transcript: the dry-run, the Warp YAML, every tmux command the launcher issues (a recording shim in front of
# tmux), and the claude command lines meta-lane.sh runs (fresh with a multi-line prompt, resumed, no prompt, with
# AW_META_CLAUDE_ARGS, the meta lane's role-set). The committed golden was made by the scripts as they were before
# SL-1 (`bash meta-launch.test.sh --print-golden <dir of the old scripts>`); the test compares the current scripts to it.
golden_transcript() {
  local S="$1" G; G="$(mktemp -d)"
  (
    export TMUX_TMPDIR="$G/tmux" HOME="$G/home" AW_STATE_DIR="$G/state" AW_WARP_LAUNCH_DIR="$G/warp" CALLS="$G/calls"
    unset TMUX AW_LAUNCHER_CONFIG
    for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done
    mkdir -p "$TMUX_TMPDIR" "$HOME/.claude/projects/p" "$G/repo" "$G/prompts" "$AW_STATE_DIR/meta" "$G/shim"
    printf 'API kickoff\nline two $HOME "quoted" '"'"'single'"'"'\n' > "$G/prompts/api.md"
    local real; real="$(command -v tmux)"
    printf '#!/usr/bin/env bash\nprintf "tmux" >> "%s"; printf " %%q" "$@" >> "%s"; echo >> "%s"\nexec "%s" "$@"\n' "$G/tmux-calls" "$G/tmux-calls" "$G/tmux-calls" "$real" > "$G/shim/tmux"
    printf '#!/usr/bin/env bash\nprintf "%%s|" "$@" >> "$CALLS"; echo >> "$CALLS"\n' > "$G/claude"
    printf '#!/usr/bin/env bash\nexec sleep 600\n' > "$G/claude-noop"
    printf '#!/usr/bin/env bash\necho "$*" >> "$CALLS.meta"\n' > "$G/aw-meta"
    chmod +x "$G/shim/tmux" "$G/claude" "$G/claude-noop" "$G/aw-meta"
    export PATH="$G/shim:$PATH" AW_META_BIN="$G/aw-meta" AW_META_NO_SHELL=1 AW_META_CLAUDE="$G/claude-noop"
    local cfg="$AW_STATE_DIR/meta/team.json"
    jq -n --arg r "$G/repo" --arg p "$G/prompts" '{team:"demo", repo:$r, prompts:$p, metaLane:"meta", lanes:["api","ui"]}' > "$cfg"
    echo "== dry-run, no sessions"; bash "$S/meta-launch.sh" --dry-run; echo "rc=$?"
    tmux new-session -d -s keepalive "sleep 600"
    echo "== launch"; bash "$S/meta-launch.sh" --no-open; echo "rc=$?"
    echo "== yaml"; cat "$AW_WARP_LAUNCH_DIR/demo-team.yaml"
    for _ in $(seq 100); do [ "$(ls "$AW_STATE_DIR/meta/lane-sessions" 2> /dev/null | grep -vc exited)" -ge 3 ] && break; sleep 0.1; done
    echo "== rerun with --lanes"; bash "$S/meta-launch.sh" --no-open --lanes api; echo "rc=$?"
    tmux kill-session -t demo-api 2> /dev/null; tmux new-session -d -s demo-api bash; : > "$AW_STATE_DIR/meta/lane-sessions/api.exited"
    echo "== dry-run, exited lane"; bash "$S/meta-launch.sh" --dry-run --lanes api; echo "rc=$?"
    echo "== tmux calls"; cat "$G/tmux-calls"
    tmux kill-server 2> /dev/null; sleep 0.3
    find "$AW_STATE_DIR/meta/lane-sessions" -type f -delete
    rm -f "$CALLS.meta"; export AW_META_CLAUDE="$G/claude"
    echo "== lane api, fresh"; bash "$S/meta-lane.sh" demo api; cat "$CALLS"; : > "$CALLS"
    touch "$HOME/.claude/projects/p/$(cat "$AW_STATE_DIR/meta/lane-sessions/api").jsonl"
    echo "== lane api, resumed"; bash "$S/meta-lane.sh" demo api; cat "$CALLS"; : > "$CALLS"
    echo "== lane api, resumed, AW_META_CLAUDE_ARGS"; AW_META_CLAUDE_ARGS="--model x --verbose" bash "$S/meta-lane.sh" demo api; cat "$CALLS"; : > "$CALLS"
    echo "== lane ui, no prompt"; bash "$S/meta-lane.sh" demo ui; cat "$CALLS"; : > "$CALLS"
    echo "== lane ui, fresh, AW_META_CLAUDE_ARGS"; rm "$AW_STATE_DIR/meta/lane-sessions/ui"; AW_META_CLAUDE_ARGS="--model x" bash "$S/meta-lane.sh" demo ui; cat "$CALLS"; : > "$CALLS"
    echo "== lane meta"; bash "$S/meta-lane.sh" demo meta; cat "$CALLS.meta"
    echo "== bad lane"; bash "$S/meta-lane.sh" demo 'Bad Lane' 2>&1; echo "rc=$?"
  ) 2>&1 | sed -E -e "s#$G#@WORK@#g" -e "s#$S#@SCRIPTS@#g" -e 's/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/@ID@/g'
  rm -r "$G"
}
if [ "${1:-}" = "--print-golden" ]; then golden_transcript "${2:?--print-golden <scripts dir>}"; exit 0; fi

# The golden: with no launcher.json the Warp layout, every tmux command and every claude command line are unchanged.
GOLDEN="$DIR/tests/fixtures/meta-launch/no-launcher.golden.txt"
golden_transcript "$DIR" > "${TMPDIR:-/tmp}/meta-launch-golden.$$"
if diff -u "$GOLDEN" "${TMPDIR:-/tmp}/meta-launch-golden.$$" > "${TMPDIR:-/tmp}/meta-launch-golden.diff.$$"; then
  echo "ok - no launcher.json: dry-run, Warp YAML, tmux commands and claude command lines match the pre-SL-1 golden"
else
  echo "not ok - no launcher.json: output differs from the pre-SL-1 golden"; sed 's/^/  /' "${TMPDIR:-/tmp}/meta-launch-golden.diff.$$"; fail=1
fi
unlink "${TMPDIR:-/tmp}/meta-launch-golden.$$"; unlink "${TMPDIR:-/tmp}/meta-launch-golden.diff.$$"

WORK="$(mktemp -d)"
export TMUX_TMPDIR="$WORK/tmux"; mkdir -p "$TMUX_TMPDIR"; unset TMUX
trap 'tmux kill-server 2> /dev/null; rm -rf "$WORK"' EXIT
for v in $(compgen -e | grep '^AW_META_'); do unset "$v"; done   # the caller may be a meta session: its team config, bin and claude must not reach the stubs
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_WARP_LAUNCH_DIR="$WORK/warp"
mkdir -p "$HOME/.claude/projects/p" "$WORK/repo" "$WORK/prompts" "$AW_STATE_DIR/meta"
echo "API kickoff" > "$WORK/prompts/api.md"
export CALLS="$WORK/claude-calls"
cat > "$WORK/claude" <<'EOF'
#!/usr/bin/env bash
printf '%s|' "$@" >> "$CALLS"; echo >> "$CALLS"
EOF
cat > "$WORK/aw-meta" <<'EOF'
#!/usr/bin/env bash
echo "$*" >> "$CALLS.meta"
EOF
chmod +x "$WORK/claude" "$WORK/aw-meta"
export AW_META_CLAUDE="$WORK/claude" AW_META_BIN="$WORK/aw-meta" AW_META_NO_SHELL=1
cfg="$AW_STATE_DIR/meta/team.json"
jq -n --arg r "$WORK/repo" --arg p "$WORK/prompts" '{team:"demo", repo:$r, prompts:$p, metaLane:"meta", lanes:["api","ui"]}' > "$cfg"

# meta-lane.sh: fresh start with a prompt, then resume once a transcript exists.
cat > "$WORK/claude-env" <<'EOF2'
#!/usr/bin/env bash
env | grep -E '^(CLAUDECODE|CLAUDE_CODE_SESSION_ID|CLAUDE_CODE_MESSAGING_SOCKET|CLAUDE_CODE_USE_BEDROCK)=' | sort > "$CALLS.env"
EOF2
chmod +x "$WORK/claude-env"
CLAUDECODE=1 CLAUDE_CODE_SESSION_ID=parent CLAUDE_CODE_MESSAGING_SOCKET=/s CLAUDE_CODE_USE_BEDROCK=1 \
  AW_META_CLAUDE="$WORK/claude-env" bash "$DIR/meta-lane.sh" demo envcheck
check "claude exited, no shell (tests): no exited marker" "$(test -e "$AW_STATE_DIR/meta/lane-sessions/envcheck.exited" && echo yes)" ""
check "launched from a Claude session: its identity cleared, settings kept" "$(cat "$CALLS.env")" "CLAUDE_CODE_USE_BEDROCK=1"
bash "$DIR/meta-lane.sh" demo api
id="$(cat "$AW_STATE_DIR/meta/lane-sessions/api")"
check "fresh: id recorded" "$(printf '%s' "$id" | grep -cE '^[0-9a-f-]{36}$')" "1"
check "fresh: session id, stable name, kickoff prompt" "$(cat "$CALLS")" "--session-id|$id|-n|demo-api|API kickoff|"
: > "$CALLS"; touch "$HOME/.claude/projects/p/$id.jsonl"
bash "$DIR/meta-lane.sh" demo api
check "transcript exists: resumes the same id" "$(cat "$CALLS")" "--resume|$id|-n|demo-api|"
: > "$CALLS"
bash "$DIR/meta-lane.sh" demo ui
check "no prompt file: starts without a prompt" "$(cut -d'|' -f3,4 "$CALLS")" "-n|demo-ui"
: > "$CALLS"
bash "$DIR/meta-lane.sh" demo meta
mid="$(cat "$AW_STATE_DIR/meta/lane-sessions/meta")"
check "meta lane: role-set with its id and name" "$(cat "$CALLS.meta")" "role-set --session $mid --name demo-meta"
bash "$DIR/meta-lane.sh" demo 'Bad Lane' 2> /dev/null; RC=$?
check "bad lane name refused" "$RC" "2"

# meta-launch.sh: sessions created on a private server, then kept on a rerun; Warp YAML written. The stub lanes exit at
# once, so a keepalive session stops the server from shutting down between the launcher's new-session calls.
tmux new-session -d -s keepalive "sleep 600"
OUT="$(bash "$DIR/meta-launch.sh" --no-open)"; RC=$?
check "launch: exit 0" "$RC" "0"
check "launch: three sessions started" "$(printf '%s\n' "$OUT" | grep -c ': started')" "3"
yaml="$AW_WARP_LAUNCH_DIR/demo-team.yaml"
check "launch: warp yaml written" "$(test -s "$yaml" && echo yes)" "yes"
check "launch: one attach per lane" "$(grep -c 'exec: tmux new -A -s demo-' "$yaml")" "3"
check "launch: a pane that creates its session runs the lane, not a shell" "$(grep -c "exec: tmux new -A -s demo-api -c $WORK/repo -e AW_META_CONFIG=$cfg '$DIR/meta-lane.sh demo api'" "$yaml")" "1"
check "launch: absolute cwd everywhere" "$(grep -c "cwd: $WORK/repo" "$yaml")" "3"
check "launch: meta pane first" "$(grep -m1 -o 'demo-[a-z]*' "$yaml")" "demo-meta"
# A live lane: a script running a child, as meta-lane.sh runs claude (tmux reports the pane's command as bash).
tmux new-session -d -s demo-api "bash -c 'sleep 60; true'" 2> /dev/null
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes api)"
check "rerun: live session kept" "$OUT" "$(printf 'tmux demo-api: running\nwarp: %s' "$yaml")"
check "rerun with --lanes: the layout still has every lane" "$(grep -c 'exec: tmux new -A -s demo-' "$yaml")" "3"
mkdir -p "$WORK/path"; printf '#!/usr/bin/env bash\necho "$1" > "%s"\n' "$WORK/opened" > "$WORK/path/xdg-open"; chmod +x "$WORK/path/xdg-open"
OUT="$(PATH="$WORK/path:$PATH" bash "$DIR/meta-launch.sh" --lanes api)"
for _ in $(seq 50); do [ -s "$WORK/opened" ] && break; sleep 0.1; done   # xdg-open runs in the background
check "open: by the config's name, URI-encoded" "$(tail -1 <<< "$OUT"):$(cat "$WORK/opened" 2> /dev/null)" "warp: opening warp://launch/demo%20team:warp://launch/demo%20team"
# claude restarted by hand in the fallback shell: the marker is still there, but a claude runs in the pane.
mkdir -p "$WORK/fakebin"; cp "$(command -v sleep)" "$WORK/fakebin/claude"
: > "$AW_STATE_DIR/meta/lane-sessions/api.exited"
tmux kill-session -t demo-api; tmux new-session -d -s demo-api "bash -c '$WORK/fakebin/claude 60; true'"
for _ in $(seq 50); do pgrep -f "$WORK/fakebin/claude 60" > /dev/null && break; sleep 0.1; done
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes api)"
check "rerun: a claude restarted by hand under a stale marker is left alone" "$(head -1 <<< "$OUT")" "tmux demo-api: running"
tmux kill-session -t demo-api; tmux new-session -d -s demo-api bash   # claude exited; the pane is back at its shell
: > "$AW_STATE_DIR/meta/lane-sessions/api.exited"                       # as meta-lane.sh leaves it
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes api)"
check "rerun: a lane whose claude exited is restarted in place" "$(head -1 <<< "$OUT")" "tmux demo-api: restarted (claude had exited)"
tmux kill-server 2> /dev/null
OUT="$(bash "$DIR/meta-launch.sh" --no-open --lanes api)"
check "after tmux died: recreated" "$(printf '%s\n' "$OUT" | head -1)" "tmux demo-api: started"
OUT="$(AW_META_CONFIG="$WORK/none.json" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "no team config: exit 1 with the fix" "$RC:$(printf '%s' "$OUT" | grep -c 'aw-meta team-init')" "1:1"
jq '.repo = "relative/path"' "$cfg" > "$WORK/rel.json"
OUT="$(AW_META_CONFIG="$WORK/rel.json" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "relative repo refused (Warp needs absolute cwd)" "$RC" "1"
mkdir -p "$WORK/bin"; ln -s "$DIR/meta-launch.sh" "$WORK/bin/aw-meta-launch"   # as install-meta.sh links it
OUT="$("$WORK/bin/aw-meta-launch" --no-open --dry-run --lanes api)"
check "run through a symlink: finds meta-lane.sh beside the real script" "$(printf '%s\n' "$OUT" | grep -c "^\[dry-run\] tmux new-session .* $DIR/meta-lane.sh demo api")" "1"
jq '.lanes = ["meta", "api"]' "$cfg" > "$WORK/dup.json"   # team-init lists the meta lane in lanes too
OUT="$(AW_META_CONFIG="$WORK/dup.json" bash "$DIR/meta-launch.sh" --no-open --dry-run)"
check "meta lane listed in lanes: one pane, not two" "$(printf '%s\n' "$OUT" | grep -c 'exec: tmux new -A -s demo-meta')" "1"
exit $fail
