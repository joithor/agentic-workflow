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
tmux kill-session -t demo-api 2> /dev/null
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
tmux kill-server 2> /dev/null   # the stub lane may still be winding down: no session may exist for this dry run
mkdir -p "$WORK/bin"; ln -s "$DIR/meta-launch.sh" "$WORK/bin/aw-meta-launch"   # as install-meta.sh links it
OUT="$("$WORK/bin/aw-meta-launch" --no-open --dry-run --lanes api)"
check "run through a symlink: finds meta-lane.sh beside the real script" "$(printf '%s\n' "$OUT" | grep -c "^\[dry-run\] tmux new-session .* $DIR/meta-lane.sh demo api")" "1"
jq '.lanes = ["meta", "api"]' "$cfg" > "$WORK/dup.json"   # team-init lists the meta lane in lanes too
OUT="$(AW_META_CONFIG="$WORK/dup.json" bash "$DIR/meta-launch.sh" --no-open --dry-run)"
check "meta lane listed in lanes: one pane, not two" "$(printf '%s\n' "$OUT" | grep -c 'exec: tmux new -A -s demo-meta')" "1"

# ---- launcher.json (SL-1; docs/csharp/sindri-launcher.md) ----
FIX="$DIR/../tests/fixtures/launcher"
LCFG="$WORK/launcher.json"
reset() { # a fresh tmux server and lane state
  tmux kill-server 2> /dev/null; sleep 0.3
  find "$AW_STATE_DIR/meta/lane-sessions" -type f -delete 2> /dev/null
  tmux new-session -d -s keepalive "sleep 600"
}
waitfor() { for _ in $(seq 100); do eval "$1" && return 0; sleep 0.1; done; return 1; }

# Every shared fixture against its bash: tag (the reader accepts the file or refuses it with SND-LAUNCH-011).
( . "$DIR/launcher-lib.sh"
  bad=""; n=0
  while IFS=' ' read -r f tag; do
    n=$((n + 1))
    if out="$(launcher_validate "$FIX/$f")"; then got=accept; else got=refuse; fi
    [ "$got" = "$tag" ] || bad="$bad $f(want $tag)"
    [ "$got" = accept ] || case "$out" in SND-LAUNCH-011:*) ;; *) bad="$bad $f(wording)" ;; esac
  done < <(jq -r '.fixtures[] | "\(.file) \(.bash)"' "$FIX/manifest.json")
  echo "$n:$bad" > "$WORK/fixture-verdicts" )
check "every shared fixture honours its bash: tag" "$(cut -d: -f2 "$WORK/fixture-verdicts")" ""
check "the whole fixture set ran" "$([ "$(cut -d: -f1 "$WORK/fixture-verdicts")" -ge 40 ] && echo yes)" "yes"

# A file that is refused stops the launch before any session exists.
reset
cp "$FIX/unknown-top-key.json" "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "invalid launcher.json: exit 1, SND-LAUNCH-011 with the file and the key" "$RC:$(grep -c "SND-LAUNCH-011: unknown key \"terminal\".*" <<< "$OUT"):$(grep -c "$LCFG" <<< "$OUT")" "1:1:1"
check "invalid launcher.json: no tmux session was created" "$(tmux list-sessions -F '#S' | grep -c '^demo-')" "0"
OUT="$(AW_LAUNCHER_CONFIG="$WORK/missing.json" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "AW_LAUNCHER_CONFIG naming a missing file: exit 1, the file named, no session created" "$RC:$(grep -c "$WORK/missing.json" <<< "$OUT"):$(tmux list-sessions -F '#S' | grep -c '^demo-')" "1:1:0"
OUT="$(bash "$DIR/meta-launch.sh" --force 2>&1)"; RC=$?
check "--force, --viewer and --stdout without --init-launcher: usage error" "$RC:$(grep -c -- '--init-launcher' <<< "$OUT")" "2:1"
echo '{"schema":1,"meta":{"lanesAgent":"nonesuch"}}' > "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "a lane agent the catalog lacks: exit 1 with SND-LAUNCH-003" "$RC:$(grep -c 'SND-LAUNCH-003' <<< "$OUT")" "1:1"
echo '{"schema":1,"agents":{"codex":{"bin":"x"}},"meta":{"lanesAgent":"codex"}}' > "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "a built-in id without override: true is named" "$RC:$(grep -c 'codex is built in: add "override": true' <<< "$OUT")" "1:1"

# viewer: warp written explicitly equals the no-file layout.
reset
OUT0="$(bash "$DIR/meta-launch.sh" --no-open --dry-run | sed "s#$WORK#W#g")"
cp "$FIX/viewer-warp.json" "$LCFG"
OUT1="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --dry-run | sed "s#$WORK#W#g")"
OUT2="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --dry-run | sed "s#$WORK#W#g" | grep -c 'AW_LAUNCHER_CONFIG')"
check "viewer warp in the file: today's layout, plus the file's path for each pane" "$(diff <(printf '%s\n' "$OUT0") <(printf '%s\n' "$OUT1" | sed 's# -e AW_LAUNCHER_CONFIG=[^ ]*##') > /dev/null && echo same):$OUT2" "same:3"
# The file at the default path ($AW_STATE_DIR/launcher.json) needs no variable and adds nothing to the panes.
cp "$FIX/viewer-warp.json" "$AW_STATE_DIR/launcher.json"
OUT1="$(bash "$DIR/meta-launch.sh" --no-open --dry-run | sed "s#$WORK#W#g")"
check "launcher.json at the default path with viewer warp: the same layout, byte for byte" "$OUT1" "$OUT0"
unlink "$AW_STATE_DIR/launcher.json"

# viewer: print — one attach line per lane, no Warp YAML.
reset; unlink "$yaml" 2> /dev/null; cp "$FIX/viewer-print.json" "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" 2>&1)"; RC=$?
check "viewer print: exit 0, three sessions started" "$RC:$(grep -c ': started' <<< "$OUT")" "0:3"
check "viewer print: one tmux attach line per lane, meta first" "$(grep '^tmux attach' <<< "$OUT" | tr '\n' ';')" "tmux attach -t =demo-meta;tmux attach -t =demo-api;tmux attach -t =demo-ui;"
check "viewer print: no Warp YAML written" "$(test -e "$yaml" && echo yes || echo no)" "no"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --lanes api 2>&1)"
check "viewer print with --lanes: only that lane" "$(grep -c '^tmux attach' <<< "$OUT"):$(grep '^tmux attach' <<< "$OUT")" "1:tmux attach -t =demo-api"

# viewer: exec-template — one window per lane, the placeholders expanded into separate arguments.
cat > "$WORK/viewer" <<'EOF'
#!/usr/bin/env bash
printf '%s|' "$@" >> "$VIEWER_CALLS"; echo >> "$VIEWER_CALLS"
EOF
chmod +x "$WORK/viewer"; export VIEWER_CALLS="$WORK/viewer-calls"
jq -n --arg v "$WORK/viewer" '{schema:1, viewer:{kind:"exec-template", name:"rec", argv:[$v,"--title","{title}","--directory","{cwd}","--","{cmd...}"]}}' > "$LCFG"
reset; : > "$VIEWER_CALLS"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" 2>&1)"; RC=$?
waitfor '[ "$(wc -l < "$VIEWER_CALLS")" -ge 3 ]'
check "exec-template: exit 0, one window per lane" "$RC:$(wc -l < "$VIEWER_CALLS")" "0:3"
check "exec-template: title, cwd and the attach argv as separate arguments" "$(grep -- '-demo-api\|demo-api' "$VIEWER_CALLS")" "--title|demo-api|--directory|$WORK/repo|--|tmux|attach|-t|=demo-api|"
check "exec-template: no Warp YAML written" "$(test -e "$yaml" && echo yes || echo no)" "no"
: > "$VIEWER_CALLS"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; sleep 0.3
check "exec-template with --no-open: no window is opened" "$(wc -c < "$VIEWER_CALLS" | tr -d ' ')" "0"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --dry-run 2>&1)"; sleep 0.3
check "exec-template --dry-run: one argv line per lane, nothing run" "$(grep -c '^\[dry-run\] viewer rec: ' <<< "$OUT"):$(wc -c < "$VIEWER_CALLS" | tr -d ' ')" "3:0"
jq -n '{schema:1, viewer:{kind:"exec-template", name:"ghost", argv:["no-such-terminal-xyz","{title}"]}}' > "$LCFG"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" 2>&1)"; RC=$?
check "a viewer that is not installed never stops the sessions (exit 0, the attach line is shown)" "$RC:$(grep -c 'ghost: no-such-terminal-xyz not found' <<< "$OUT"):$(grep -c '^tmux attach -t =demo-api' <<< "$OUT")" "0:3:1"
jq -n --arg v "$WORK/viewer" '{schema:1, viewer:{kind:"exec-template", name:"rec", argv:[$v,"{title}"]}}' > "$LCFG"
jq '.viewer.argv[1] = "{cmd...}x"' "$LCFG" > "$WORK/l2.json"
OUT2="$(AW_LAUNCHER_CONFIG="$WORK/l2.json" bash "$DIR/meta-launch.sh" --dry-run 2>&1)"; RC=$?
check "a {cmd...} inside an argument is refused" "$RC:$(grep -c 'SND-LAUNCH-011' <<< "$OUT2")" "1:1"

# Lane agents from the catalog: a non-Claude lane, and ids reported one lane at a time.
cat > "$WORK/codex" <<'EOF'
#!/usr/bin/env bash
echo "start $AW_META_LANE" >> "$CALLS.events"
sleep 0.4
id="$(cat /proc/sys/kernel/random/uuid 2> /dev/null || uuidgen | tr '[:upper:]' '[:lower:]')"
mkdir -p "$HOME/.codex/sessions/2026/10/15"
: > "$HOME/.codex/sessions/2026/10/15/rollout-2026-10-15T00-00-00-$id.jsonl"
echo "wrote $AW_META_LANE $id" >> "$CALLS.events"
printf '%s|' "$@" >> "$CALLS.codex"; echo "$AW_SINDRI_CHILD|$AW_META_TEAM|$AW_META_LANE" >> "$CALLS.codex"
sleep 600
EOF
chmod +x "$WORK/codex"
jq -n --arg c "$WORK/codex" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$LCFG"
reset; : > "$CALLS.events"; : > "$CALLS.codex"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open 2>&1)"; RC=$?
check "codex lanes: the launch notes that they are launch-only" "$RC:$(grep -x 'lane api: codex, launch-only for now: no obligations' <<< "$OUT" | wc -l):$(grep -c '^lane meta' <<< "$OUT")" "0:1:0"
ids="$AW_STATE_DIR/meta/lane-sessions"
waitfor '[ -s "$ids/api" ] && [ -s "$ids/ui" ] && [ "$(wc -l < "$CALLS.codex")" -ge 2 ]'
check "codex lanes: started one at a time (each id is reported before the next lane starts)" "$(awk '{print $1" "$2}' "$CALLS.events" | tr '\n' ';')" "start api;wrote api;start ui;wrote ui;"
a="$(tr -d '[:space:]' < "$ids/api")"; u="$(tr -d '[:space:]' < "$ids/ui")"
check "codex lanes: each lane recorded the id its own rollout file carries" "$(grep -c "wrote api $a" "$CALLS.events"):$(grep -c "wrote ui $u" "$CALLS.events")" "1:1"
check "codex lanes: two distinct ids" "$([ "$a" != "$u" ] && [ -n "$a" ] && echo yes)" "yes"
check "codex lanes: the built-in open argv, the prompt as the last argument, none without one" "$(grep -h -- '-C' "$CALLS.codex" | sort | tr '\n' ';')" "-C|$WORK/repo|1|demo|ui;-C|$WORK/repo|API kickoff|1|demo|api;"
check "codex lanes: two codex sessions, not three (the meta lane is Claude)" "$(wc -l < "$CALLS.codex" | tr -d ' ')" "2"
check "the lane variables are in the lane's environment and not in tmux's global one" "$(tmux show-environment -g 2>&1 | grep -c 'AW_SINDRI_CHILD\|AW_META_TEAM\|AW_META_LANE')" "0"
# The meta lane stays Claude Code even when lanesAgent says otherwise; its claude is the stub.
check "the meta lane runs the claude stub, not codex" "$(grep -c 'meta' "$CALLS.events")" "0"
# Resume: a lane whose rollout exists resumes by the reported id.
tmux kill-server 2> /dev/null; : > "$CALLS.codex"
AW_LAUNCHER_CONFIG="$LCFG" AW_META_NO_SHELL=1 bash "$DIR/meta-lane.sh" demo api > /dev/null 2>&1 &
rp=$!; waitfor '[ -s "$CALLS.codex" ]'; kill "$rp" 2> /dev/null; pkill -f "$WORK/codex" 2> /dev/null; wait "$rp" 2> /dev/null
check "codex lane with a recorded id and its rollout: resumes it" "$(head -1 "$CALLS.codex" | cut -d'|' -f1-2)" "resume|$a"
# no processName/transcripts surprises: the pane check uses the catalog's processName.
mkdir -p "$WORK/fakebin2"; cp "$(command -v sleep)" "$WORK/fakebin2/codex"
: > "$ids/api.exited"
tmux kill-server 2> /dev/null; tmux new-session -d -s keepalive "sleep 600"
tmux new-session -d -s demo-api "bash -c '$WORK/fakebin2/codex 60; true'"
waitfor 'pgrep -f "$WORK/fakebin2/codex 60" > /dev/null'
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-launch.sh" --no-open --lanes api 2>&1 | head -2)"
check "a codex lane whose codex runs under a stale marker is left alone (processName from the catalog)" "$(grep -c 'tmux demo-api: running' <<< "$OUT")" "1"
jq '.agents.codex.processName = "other"' "$LCFG" > "$WORK/l3.json"
OUT="$(AW_LAUNCHER_CONFIG="$WORK/l3.json" bash "$DIR/meta-launch.sh" --no-open --dry-run --lanes api 2>&1)"
check "with another processName that pane counts as exited and is restarted" "$(grep -c 'respawn-pane' <<< "$OUT")" "1"
pkill -f "$WORK/fakebin2/codex 60" 2> /dev/null

# A refused prompt ({seed} starting with "-", spec section 4) starts no agent and records no id, whatever the agent.
killstubs() { pkill -f "$WORK/codex" 2> /dev/null; sleep 0.1; }
jq -n '{schema:1, viewer:{kind:"print"}}' > "$WORK/print.json"
for first in '- First: read the spec' '---'; do
  printf '%s\nbody\n' "$first" > "$WORK/prompts/refuse.md"; : > "$CALLS"; rm -f "$ids/refuse"
  OUT="$(AW_LAUNCHER_CONFIG="$WORK/print.json" bash "$DIR/meta-lane.sh" demo refuse 2>&1)"; RC=$?
  check "a prompt starting with '$first' and a launcher.json: refused, no agent start, no recorded id" "$RC:$(grep -c 'SND-LAUNCH-011' <<< "$OUT"):$(wc -c < "$CALLS" | tr -d ' '):$(test -e "$ids/refuse" && echo id || echo none)" "1:1:0:none"
done
: > "$CALLS"; rm -f "$ids/refuse"; : > "$CALLS.codex"
OUT="$(AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-lane.sh" demo refuse 2>&1)"; RC=$?
check "the same prompt with a codex lane: refused, no agent start" "$RC:$(wc -c < "$CALLS.codex" | tr -d ' ' | grep -c '^0$'):$(test -e "$ids/refuse" && echo id || echo none)" "1:1:none"
rm -f "$WORK/prompts/refuse.md"
# An existing but empty prompt file still passes "" as the last argument, as it did before launcher.json.
: > "$WORK/prompts/empty.md"; : > "$CALLS"
bash "$DIR/meta-lane.sh" demo empty
check "an empty <lane>.md passes an empty prompt argument, as before" "$(sed -E 's/[0-9a-f]{8}-[0-9a-f-]{27}/ID/' "$CALLS")" "--session-id|ID|-n|demo-empty||"
rm -f "$WORK/prompts/empty.md"
# The three lane variables reach a Claude Code lane too.
cat > "$WORK/claude-aw" <<'EOF2'
#!/usr/bin/env bash
echo "$AW_SINDRI_CHILD|$AW_META_TEAM|$AW_META_LANE" > "$CALLS.aw"
EOF2
chmod +x "$WORK/claude-aw"
AW_META_CLAUDE="$WORK/claude-aw" bash "$DIR/meta-lane.sh" demo awenv
check "a Claude Code lane sees AW_SINDRI_CHILD, AW_META_TEAM and AW_META_LANE" "$(cat "$CALLS.aw")" "1|demo|awenv"

# Reported ids: only session files created after the start count (an old rollout appended to meanwhile is not chosen).
codex_dir="$HOME/.codex/sessions/2026/10/14"; mkdir -p "$codex_dir"
old="$codex_dir/rollout-2026-10-14T00-00-00-11111111-1111-1111-1111-111111111111.jsonl"; : > "$old"
cat > "$WORK/codex-race" <<'EOF2'
#!/usr/bin/env bash
( sleep 0.4; echo more >> "$OLD_ROLLOUT" ) &
sleep 1
mkdir -p "$HOME/.codex/sessions/2026/10/15"
: > "$HOME/.codex/sessions/2026/10/15/rollout-2026-10-15T00-00-00-22222222-2222-2222-2222-222222222222.jsonl"
sleep 600
EOF2
chmod +x "$WORK/codex-race"; export OLD_ROLLOUT="$old"
jq -n --arg c "$WORK/codex-race" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$WORK/race.json"
rm -f "$ids/race" "$ids/race.settled"
AW_LAUNCHER_CONFIG="$WORK/race.json" AW_META_ID_WAIT=8 bash "$DIR/meta-lane.sh" demo race > /dev/null 2>&1 &
rp=$!; waitfor '[ -e "$ids/race.settled" ]'
check "an existing rollout appended to during the wait is not chosen; the lane's own new one is" "$(tr -d '[:space:]' < "$ids/race" 2> /dev/null)" "22222222-2222-2222-2222-222222222222"
kill "$rp" 2> /dev/null; killstubs; wait "$rp" 2> /dev/null

# Two new session files at once are refused: no id, a .ambiguous marker, and the launcher says so.
cat > "$WORK/codex-two" <<'EOF2'
#!/usr/bin/env bash
mkdir -p "$HOME/.codex/sessions/2026/10/16"
: > "$HOME/.codex/sessions/2026/10/16/rollout-2026-10-16T00-00-00-33333333-3333-3333-3333-333333333333.jsonl"
: > "$HOME/.codex/sessions/2026/10/16/rollout-2026-10-16T00-00-01-44444444-4444-4444-4444-444444444444.jsonl"
sleep 600
EOF2
chmod +x "$WORK/codex-two"
jq -n --arg c "$WORK/codex-two" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$WORK/two.json"
jq -n --arg r "$WORK/repo" '{team:"demo", repo:$r, metaLane:"meta", lanes:["amb","idle"]}' > "$WORK/team2.json"
tmux kill-server 2> /dev/null; sleep 0.3; tmux new-session -d -s keepalive "sleep 600"; rm -f "$ids/amb" "$ids/amb.ambiguous" "$ids/amb.settled"
OUT="$(AW_META_CONFIG="$WORK/team2.json" AW_LAUNCHER_CONFIG="$WORK/two.json" AW_META_ID_WAIT=5 bash "$DIR/meta-launch.sh" --no-open --lanes amb 2>&1)"; RC=$?
check "two unclaimed new session files: no id recorded, .ambiguous left" "$(test -s "$ids/amb" && echo id || echo none):$(test -e "$ids/amb.ambiguous" && echo marker || echo none)" "none:marker"
check "the launcher reports the ambiguous id and still exits 0" "$RC:$(grep -c 'lane amb: ambiguous session id' <<< "$OUT")" "0:1"
killstubs

# An agent that never reports: the launcher times out with a message and the next lane still starts.
cat > "$WORK/codex-idle" <<'EOF2'
#!/usr/bin/env bash
sleep 600
EOF2
chmod +x "$WORK/codex-idle"
jq -n --arg c "$WORK/codex-idle" '{schema:1, agents:{codex:{override:true, bin:$c}}, meta:{lanesAgent:"codex"}}' > "$WORK/idle.json"
rm -f "$ids/idle" "$ids/idle.settled" "$ids/idle.ambiguous"
OUT="$(AW_META_CONFIG="$WORK/team2.json" AW_LAUNCHER_CONFIG="$WORK/idle.json" AW_META_ID_WAIT=1 bash "$DIR/meta-launch.sh" --no-open --lanes idle,amb 2>&1)"; RC=$?
check "an id that never arrives: the timeout message, exit 0, the next lane started" "$RC:$(grep -c 'lane idle has not reported its session id after 1s' <<< "$OUT"):$(grep -c 'tmux demo-amb: started' <<< "$OUT")" "0:1:1"
check "an id that never arrives: nothing recorded" "$(test -s "$ids/idle" && echo id || echo none)" "none"
tmux kill-server 2> /dev/null; killstubs

# Two reported lanes started at the same moment (as Warp's panes do) still start one at a time.
reset; : > "$CALLS.events"; : > "$CALLS.codex"; rm -f "$ids/p1" "$ids/p2" "$ids/p1.settled" "$ids/p2.settled"
export -n OLD_ROLLOUT
AW_LAUNCHER_CONFIG="$LCFG" AW_META_ID_WAIT=10 bash "$DIR/meta-lane.sh" demo p1 > /dev/null 2>&1 & p1=$!
AW_LAUNCHER_CONFIG="$LCFG" AW_META_ID_WAIT=10 bash "$DIR/meta-lane.sh" demo p2 > /dev/null 2>&1 & p2=$!
waitfor '[ -s "$ids/p1" ] && [ -s "$ids/p2" ]'
a1="$(tr -d '[:space:]' < "$ids/p1")"; a2="$(tr -d '[:space:]' < "$ids/p2")"
check "two lanes starting at once: one at a time (each id recorded before the other agent starts)" "$(awk '{print $1}' "$CALLS.events" | tr '\n' ';')" "start;wrote;start;wrote;"
check "two lanes starting at once: each lane recorded its own rollout's id" "$(grep -c "wrote p1 $a1" "$CALLS.events"):$(grep -c "wrote p2 $a2" "$CALLS.events"):$([ "$a1" != "$a2" ] && echo distinct)" "1:1:distinct"
kill "$p1" "$p2" 2> /dev/null; killstubs; wait 2> /dev/null
check "the start lock is released afterwards" "$(test -d "$ids/.reported.lock" && echo held || echo free)" "free"

# Precedence: AW_META_CLAUDE ranks above the file's claude-code bin; without it the file's bin is used.
jq -n '{schema:1, agents:{"claude-code":{override:true, bin:"/nonexistent/claude"}}}' > "$LCFG"
: > "$CALLS"; rm -f "$ids/api" "$ids/ui"
AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-lane.sh" demo ui
check "AW_META_CLAUDE wins over the file's claude-code bin" "$(cut -d'|' -f3 "$CALLS")" "-n"
jq -n --arg c "$WORK/claude" '{schema:1, agents:{"claude-code":{override:true, bin:$c}}}' > "$LCFG"
: > "$CALLS"; rm -f "$ids/ui"
( unset AW_META_CLAUDE; AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-lane.sh" demo ui )
check "without AW_META_CLAUDE the file's bin is the command" "$(cut -d'|' -f3,4 "$CALLS")" "-n|demo-ui"
jq -n --arg c "$WORK/claude" '{schema:1, agents:{"claude-code":{override:true, open:["{bin}","--sid","{sessionId}","--label={name}","{seed}"]}}}' > "$LCFG"
: > "$CALLS"; rm -f "$ids/api"
AW_META_CLAUDE_ARGS="--model x" AW_LAUNCHER_CONFIG="$LCFG" bash "$DIR/meta-lane.sh" demo api
check "an override's open template is used; AW_META_CLAUDE_ARGS go right after {bin}" "$(sed -E 's/[0-9a-f]{8}-[0-9a-f-]{27}/ID/' "$CALLS" | head -1)" "--model|x|--sid|ID|--label=demo-api|API kickoff|"

# --init-launcher: the detection under a PATH holding only the named terminals.
tools="jq mkdir mv chmod cat dirname readlink uname tr head tail grep sed awk date rm printf env bash sh mktemp cp ls basename wc sort"
kd="$WORK/kitty-only"; mkdir -p "$kd"; for t in $tools; do p="$(command -v "$t" 2> /dev/null)" && ln -sf "$p" "$kd/$t"; done
unlink "$kd/uname"; printf '#!/bin/sh\necho Linux\n' > "$kd/uname"; chmod +x "$kd/uname"   # the detection is the same on every host
printf '#!/bin/sh\nexit 0\n' > "$kd/kitty"; chmod +x "$kd/kitty"
INIT="$WORK/init.json"
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher 2>&1)"; RC=$?
check "init-launcher with only kitty on PATH and a display: writes the kitty template" "$RC:$(jq -c '[.viewer.kind, .viewer.name, .viewer.argv[0]]' "$INIT")" '0:["exec-template","kitty","kitty"]'
check "init-launcher: the file is 0600 and passes the reader" "$(stat -c %a "$INIT" 2> /dev/null || stat -f %Lp "$INIT"):$( ( . "$DIR/launcher-lib.sh"; launcher_validate "$INIT" > /dev/null && echo ok ) )" "600:ok"
check "init-launcher says what it chose and why" "$(grep -c 'Wrote .*init.json: kitty (first on PATH)' <<< "$OUT")" "1"
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher 2>&1)"; RC=$?
check "init-launcher refuses to replace a file without --force" "$RC" "1"
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --viewer print --force 2>&1)"; RC=$?
check "init-launcher --viewer print --force replaces it" "$RC:$(jq -c .viewer "$INIT")" '0:{"kind":"print"}'
unlink "$INIT"
OUT="$(env -i HOME="$HOME" PATH="$kd" AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --stdout 2>&1)"
check "init-launcher with no GUI session picks print; --stdout writes no file" "$(jq -c .viewer <<< "$OUT"):$(test -e "$INIT" && echo file || echo none)" '{"kind":"print"}:none'
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 TERM_PROGRAM=kitty AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher 2>&1)"
check "init-launcher names \$TERM_PROGRAM when it picks by it" "$(grep -c 'kitty (from \$TERM_PROGRAM)' <<< "$OUT")" "1"
unlink "$INIT"
OUT="$(env -i HOME="$HOME" PATH="$kd" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --viewer nonesuch 2>&1)"; RC=$?
check "init-launcher with an unknown --viewer: exit 2" "$RC" "2"
mkdir -p "$WORK/warpbin"; for t in $tools; do p="$(command -v "$t" 2> /dev/null)" && ln -sf "$p" "$WORK/warpbin/$t"; done
unlink "$WORK/warpbin/uname"; printf '#!/bin/sh\necho Linux\n' > "$WORK/warpbin/uname"; chmod +x "$WORK/warpbin/uname"
printf '#!/bin/sh\nexit 0\n' > "$WORK/warpbin/warp-terminal"; chmod +x "$WORK/warpbin/warp-terminal"
OUT="$(env -i HOME="$HOME" PATH="$WORK/warpbin" DISPLAY=:0 AW_LAUNCHER_CONFIG="$INIT" bash "$DIR/meta-launch.sh" --init-launcher --stdout 2>&1)"
check "init-launcher picks Warp when it is installed" "$(jq -c .viewer <<< "$OUT")" '{"kind":"warp"}'
tmux kill-server 2> /dev/null
exit $fail
