#!/usr/bin/env bash
# Tests for scripts/sindri-pod.sh connect with a fake `kubectl` and a fake `sindri-ui` on a temp AW_STATE_DIR: the dry run
# prints the plan and writes nothing; connect writes the mirror home (control.json with the forwarded loopback URL, the pod's
# pid and epoch, auth/human.token; files 0600, dirs 0700), forwards the pod's own port and starts the app on it; a new
# epoch or token in the pod rewrites the home; a port-forward that drops is retried; refusals (no kubectl, no sindri-ui, no
# pod, a pod that is not Ready, a local port that is taken, no rights; --check reports each and exits 0 or 2); the mirror
# home lives in $TMPDIR and goes on exit (a TERM too), stale ones are removed; the retry loop prints its attempts and is
# bounded; profile edit (a fake EDITOR): unchanged stops, a valid edit is validated, put back and approved in that order,
# an invalid one is never approved and its copy kept, a refused approval's exit code propagates; --onyx forwards the pod's Onyx port and runs `sindri onyx attach` in the pod with -it; a failed attach is the exit code.
# Run: bash scripts/tests/sindri-pod.test.sh
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fail=0
check() {
  if [ "$2" == "$3" ]; then echo "ok - $1"; else
    echo "not ok - $1"; echo "  expected: $3"; echo "  actual:   $2"; fail=1
  fi
}
WORK="$(cd "$(mktemp -d)" && pwd -P)"
cleanup() {
  local p
  for p in "$WORK"/runs/*.pid; do [ -f "$p" ] && kill "$(cat "$p")" 2>/dev/null; done
  rm -rf "$WORK"
}
trap cleanup EXIT
unset AW_SINDRI_HOME AW_SINDRI_UI
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_SINDRI_POD_POLL=0.2 AW_SINDRI_POD_RETRIES=5 AW_SINDRI_POD_RETRY_WAIT=0.3
export AW_SINDRI_POD_ONYX_PORT=43000 TMPDIR="$WORK/tmp"
mkdir -p "$HOME" "$WORK/bin" "$WORK/pod" "$WORK/runs" "$TMPDIR"
export CALLS="$WORK/calls" POD="$WORK/pod" STOP="$WORK/ui-stop"
: > "$CALLS"

# The pod: what `kubectl exec` reads (the pod's control.json and human token).
pod_state() {   # pid epoch token port
  printf '{"url":"http://127.0.0.1:%s","pid":%s,"epoch":%s}\n' "$4" "$1" "$2" > "$POD/control.json"
  printf '%s\n' "$3" > "$POD/token"
}
pod_state 41 7 tok-one 47480

cat > "$WORK/bin/kubectl" <<'EOS'
#!/usr/bin/env bash
echo "kubectl $*" >> "$CALLS"
case " $* " in
  *" auth can-i "*) r="${!#}"; if [ -e "$POD/deny-${r//\//_}" ]; then echo no; else echo yes; fi; exit 0 ;;
  *" get pod "*) [ -e "$POD/no-pod" ] && exit 1
    case "$*" in
      *'@.name!='*) cat "$POD/others" 2>/dev/null; exit 0 ;;
      *) cat "$POD/sindri-ready" 2>/dev/null || echo true; exit 0 ;;
    esac ;;
  *" config current-context "*) echo kind-test; exit 0 ;;
  *" config view "*) echo sindri; exit 0 ;;
  *" port-forward "*)
    if [ -e "$POD/pf-fail-once" ] && [[ " $* " == *" pod/"* ]]; then rm -f "$POD/pf-fail-once"; echo "pf failed" >&2; exit 1; fi
    exec sleep 300 ;;
  *" exec "*" -- cat "*) cat "$POD/profile.yaml"; exit 0 ;;
  *" exec -i "*) cat > "$POD/uploaded"; exit 0 ;;
  *"sindri profile validate"*) cp "$POD/uploaded" "$POD/validated"; rc="$(cat "$POD/validate-rc" 2>/dev/null || echo 1)"
    [ "$rc" -ge 2 ] && echo "SND-PROFILE-001 bad key" >&2; exit "$rc" ;;
  *" mv "*) cp "$POD/uploaded" "$POD/profile.yaml"; exit 0 ;;
  *'echo "${AW_SINDRI_PROFILE'*) echo /data/profile/profile.yaml; exit 0 ;;
  *"profile approve"*) exit "$(cat "$POD/approve-rc" 2>/dev/null || echo 0)" ;;
  *" exec -it "*) [ -e "$POD/attach-fail" ] && exit "$(cat "$POD/attach-fail")"; exit 0 ;;
  *" exec "*) [ -e "$POD/down" ] && exit 1
    cat "$POD/control.json"; echo "@@aw-token@@"; cat "$POD/token"; exit 0 ;;
esac
exit 0
EOS
cat > "$WORK/bin/sindri-ui" <<'EOS'
#!/usr/bin/env bash
echo "ui home=$AW_SINDRI_HOME" >> "$CALLS"
while [ ! -e "$STOP" ]; do sleep 0.1; done
EOS
chmod +x "$WORK/bin/kubectl" "$WORK/bin/sindri-ui"
export AW_SINDRI_UI="$WORK/bin/sindri-ui"
mirror_dir() { ls -d "$TMPDIR"/aw-sindri-pod.* 2>/dev/null | head -1; }
MIRROR=""

mode() { stat -c %a "$1" 2>/dev/null || stat -f %Lp "$1"; }
waitfor() {   # <seconds> <command...>: poll until it succeeds
  local n=$(( $1 * 10 )); shift
  while [ "$n" -gt 0 ]; do "$@" 2>/dev/null && return 0; sleep 0.1; n=$((n - 1)); done
  return 1
}
count() { grep -c -- "$1" "$CALLS" || true; }
has_epoch() { [ "$(jq -r .epoch "$(mirror_dir)/control.json")" = "$1" ]; }
has_ui() { grep -q '^ui home=' "$CALLS" && [ -n "$(mirror_dir)" ]; }
has_line() { grep -q -- "$1" "$CALLS"; }
reset() { : > "$CALLS"; rm -rf "$AW_STATE_DIR/sindri-pod" "$TMPDIR"/aw-sindri-pod.* "$POD"/deny-* "$POD/no-pod" "$POD/sindri-ready" "$POD/others"; }
run_connect() {   # <tag> args...: connect in the background with the fakes on the PATH; stdin is one Enter once $WORK/enter exists
  local tag="$1"; shift
  rm -f "$STOP" "$WORK/enter" "$WORK/runs/$tag.rc"
  ( PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect "$@" < <(while [ ! -e "$WORK/enter" ]; do sleep 0.1; done; echo) \
      > "$WORK/runs/$tag.out" 2> "$WORK/runs/$tag.err"; echo $? > "$WORK/runs/$tag.rc" ) &
  echo $! > "$WORK/runs/$tag.pid"
}
finish() {   # <tag>: close the fake app, wait for the script to end, echo its exit code
  touch "$STOP"
  waitfor 10 test -s "$WORK/runs/$1.rc" || { echo hung; return; }
  cat "$WORK/runs/$1.rc"
}
said() { cat "$WORK/runs/$1.out" "$WORK/runs/$1.err" | grep -c -- "$2" || true; }

# --- the dry run: prints the plan, calls nothing, writes nothing
OUT="$(PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect --dry-run --context c1 --namespace n1 --onyx 2>&1)"; RC=$?
check "dry run: exit 0, nothing written, no kubectl needed" "$RC:$([ -e "$AW_STATE_DIR/sindri-pod" ] && echo wrote || echo clean)" "0:clean"
check "dry run: names the exec, the forward, the app and Onyx's attach" \
  "$(printf '%s' "$OUT" | grep -c -e 'exec sindri-0 -c sindri' -e 'port-forward pod/sindri-0' -e 'sindri-ui' -e 'onyx attach')" "5"
OUT="$(env -u AW_SINDRI_POD_RETRIES -u AW_SINDRI_POD_RETRY_WAIT PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect --dry-run 2>&1)"
check "dry run: the default is 30 attempts 10 s apart (5 minutes)" "$(printf '%s' "$OUT" | grep -c '30 attempts 10s apart')" "1"

# --- connect: mirror home, forward, app
run_connect a
waitfor 10 has_ui; check "connect: the app starts" "$?" "0"
MIRROR="$(mirror_dir)"
check "connect: control.json holds the forwarded loopback URL, the pod's pid and epoch" \
  "$(jq -c . "$MIRROR/control.json")" '{"url":"http://127.0.0.1:47480","pid":41,"epoch":7}'
check "connect: the token file holds the pod's token" "$(cat "$MIRROR/auth/human.token")" "tok-one"
check "connect: files are 0600, directories 0700" \
  "$(mode "$MIRROR/control.json"):$(mode "$MIRROR/auth/human.token"):$(mode "$MIRROR"):$(mode "$MIRROR/auth")" "600:600:700:700"
check "connect: the app runs on the mirror home" "$(grep '^ui home=' "$CALLS" | head -1)" "ui home=$MIRROR"
check "connect: forwards the pod's own port on the chosen context and namespace" \
  "$(count 'kubectl --context kind-test --namespace sindri port-forward pod/sindri-0 47480:47480')" "1"

# --- the pod restarts: new pid, epoch and token
pod_state 52 8 tok-two 47480
waitfor 10 has_epoch 8; check "restart: the mirror home is rewritten with the new epoch" "$?" "0"
check "restart: pid and token follow" "$(jq -r .pid "$MIRROR/control.json"):$(cat "$MIRROR/auth/human.token")" "52:tok-two"
check "restart: files stay 0600" "$(mode "$MIRROR/control.json"):$(mode "$MIRROR/auth/human.token")" "600:600"

# --- the pod's port changes: the forward is restarted on the new port
pod_state 52 9 tok-two 47481
waitfor 10 has_epoch 9; check "new port: the home is rewritten" "$?" "0"
check "new port: the forward is restarted onto the pod's new port, the local one stays" \
  "$(count 'port-forward pod/sindri-0 47480:47481'):$(jq -r .url "$MIRROR/control.json")" "1:http://127.0.0.1:47480"

# --- pod unreachable for a while: the script retries and recovers
touch "$POD/down"; sleep 0.6; rm -f "$POD/down"; pod_state 53 10 tok-two 47481
waitfor 10 has_epoch 10; check "pod down then up: recovered without exiting" "$?" "0"
check "exit: closing the app ends the script cleanly" "$(finish a)" "0"
check "exit: the script said the app was closed" "$(said a 'the app was closed')" "1"
check "connect: prints the connected line" "$(said a 'connected: sindri-0 at http://127.0.0.1:47480; Ctrl-C to stop')" "1"
check "pod down then up: the attempts were announced and the reconnect said" \
  "$([ "$(said a 'pod restarting, reconnecting (attempt 1 of 5)')" -ge 1 ] && [ "$(said a 'reconnected to sindri-0')" = 1 ] && echo yes)" "yes"
check "exit: the mirror home is removed" "$(mirror_dir)" ""

# --- a port-forward that fails once is retried
reset; pod_state 41 7 tok-one 47480; touch "$POD/pf-fail-once"
run_connect b
waitfor 10 has_ui; check "forward retry: the app still starts" "$?" "0"
check "forward retry: port-forward ran twice" "$(count 'port-forward pod/sindri-0')" "2"
check "forward retry: exit 0" "$(finish b)" "0"

# --- --port picks the local port, --pod the pod
reset
run_connect c --port 18480 --pod sindri-9
waitfor 10 has_ui
check "--port: the forward maps the chosen local port to the pod's, for the chosen pod" "$(count 'port-forward pod/sindri-9 18480:47480')" "1"
check "--port: control.json carries the local port" "$(jq -r .url "$(mirror_dir)/control.json")" "http://127.0.0.1:18480"
finish c >/dev/null

# --- --onyx: forward the pod's Onyx port, run onyx attach in the pod
reset
run_connect d --onyx
waitfor 10 has_line 'exec -it'; waitfor 10 has_ui
check "--onyx: attach runs in the sindri container with -it" "$(count 'exec -it sindri-0 -c sindri -- sindri onyx attach')" "1"
check "--onyx: onyx up is never run" "$(count 'onyx up')" "0"
SEQ="$(awk '
  /exec sindri-0 -c sindri -- sh/ && !a {print "read"; a=1} /port-forward pod\/sindri-0 47480/ && !b {print "forward"; b=1}
  /exec -it/ {print "attach"}' "$CALLS" | paste -sd, -)"
check "--onyx: pod read, owner forward, then attach" "$SEQ" "read,forward,attach"
waitfor 10 has_line 'port-forward pod/sindri-0 43000:43000'; check "--onyx: the Onyx port is forwarded from the pod" "$?" "0"
check "--onyx: no Service forward" "$(count 'port-forward svc/')" "0"
check "--onyx: the forwarded address is printed" "$(said d 'Onyx is at http://127.0.0.1:43000 — sign up there when attach asks')" "1"
check "--onyx: the app is still connected after attach" "$(jq -r .epoch "$(mirror_dir)/control.json")" "7"
check "--onyx: success ends with exit 0" "$(finish d)" "0"

# --- --onyx: attach fails; its exit code propagates with a clear text, the app stays up meanwhile
reset; echo 3 > "$POD/attach-fail"
run_connect e --onyx
waitfor 10 has_ui; waitfor 10 has_line 'exec -it'
sleep 0.5
check "--onyx failure: names the failure and the exit code" "$(said e 'onyx attach failed (exit 3)')" "1"
check "--onyx failure: says what it may be" "$(said e 'not answering at the forwarded port 43000')" "1"
check "--onyx failure: the app was still started" "$(count '^ui home=')" "1"
check "--onyx failure: exit code propagates" "$(finish e)" "3"
rm -f "$POD/attach-fail"

# --- --onyx: Ctrl-C in attach (130) stops attach only
reset; echo 130 > "$POD/attach-fail"
run_connect f --onyx
waitfor 10 has_ui; sleep 0.5
check "--onyx Ctrl-C: attach stopped, app stays" "$(said f 'the app stays connected')" "1"
check "--onyx Ctrl-C: exit 0" "$(finish f)" "0"
rm -f "$POD/attach-fail"

# --- refusals: one line each, exit 2
conn() { PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect "$@" 2>&1; }
lines() { printf '%s\n' "$1" | grep -c .; }
reset
OUT="$(PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect 2>&1)"; RC=$?
check "no kubectl: exit 2 with one line" "$RC:$(lines "$OUT"):$(printf '%s' "$OUT" | grep -c 'kubectl is not on the PATH')" "2:1:1"
OUT="$(AW_SINDRI_UI="$WORK/nope" conn)"; RC=$?
check "no sindri-ui: exit 2, one line naming sindri-app, nothing forwarded" \
  "$RC:$(lines "$OUT"):$(printf '%s' "$OUT" | grep -c 'sindri-app'):$(count port-forward)" "2:1:1:0"
touch "$POD/deny-pods_exec"
OUT="$(conn)"; RC=$?
check "no exec right: exit 2, one line naming the verb" "$RC:$(lines "$OUT"):$(printf '%s' "$OUT" | grep -c "create pods/exec")" "2:1:1"
rm -f "$POD/deny-pods_exec"; touch "$POD/deny-pods_portforward"
OUT="$(conn)"; RC=$?
check "no port-forward right: exit 2 naming pods/portforward" "$RC:$(printf '%s' "$OUT" | grep -c "create pods/portforward")" "2:1"
rm -f "$POD/deny-pods_portforward"
echo false > "$POD/sindri-ready"; echo "onyx-api=false " > "$POD/others"
OUT="$(AW_SINDRI_POD_RETRIES=2 conn)"; RC=$?
check "sindri container not ready: waits, then exit 2 naming the container, the other containers as information" \
  "$RC:$(printf '%s' "$OUT" | grep -c 'attempt 2 of 2'):$(printf '%s' "$OUT" | grep -c 'the sindri container of pod sindri-0 is not ready (ready: false); other containers, for information: onyx-api=false')" "2:1:1"
rm -f "$POD/sindri-ready"
reset; echo "onyx-api=false " > "$POD/others"
run_connect h
waitfor 10 has_ui
check "pod not Ready but the sindri container is: connects, the other container only a note" \
  "$(said h 'note: other containers in sindri-0 are not ready (onyx-api=false)'):$(count '^ui home=')" "1:1"
finish h >/dev/null
reset
rm -f "$POD/others"
touch "$POD/no-pod"
OUT="$(AW_SINDRI_POD_RETRIES=1 conn)"; RC=$?
check "pod missing: exit 2, ready unknown" "$RC:$(printf '%s' "$OUT" | grep -c 'ready: unknown')" "2:1"
rm -f "$POD/no-pod"
if command -v python3 >/dev/null 2>&1; then
  python3 -I -c 'import socket,sys,time
s=socket.socket(); s.bind(("127.0.0.1",48999)); s.listen(1); open(sys.argv[1],"w").write("up"); time.sleep(120)' "$WORK/listening" &
  echo $! > "$WORK/runs/listener.pid"
  waitfor 10 test -e "$WORK/listening"
  OUT="$(conn --port 48999)"; RC=$?
  check "local port taken: exit 2, one line naming the port and --port" \
    "$RC:$(lines "$OUT"):$(printf '%s' "$OUT" | grep -c 'local port 48999 is taken; pick another with --port'):$(count port-forward)" "2:1:1:0"
  kill "$(cat "$WORK/runs/listener.pid")" 2>/dev/null
  python3 -I -c 'import socket,sys,time
s=socket.socket(); s.bind(("127.0.0.1",48998)); s.listen(1); open(sys.argv[1],"w").write("up"); time.sleep(120)' "$WORK/listening2" &
  echo $! > "$WORK/runs/listener2.pid"
  waitfor 10 test -e "$WORK/listening2"
  reset
  OUT="$(AW_SINDRI_POD_ONYX_PORT=48998 conn --onyx)"; RC=$?
  check "Onyx port taken: exit 2, names the port and AW_SINDRI_POD_ONYX_PORT, attach never runs" \
    "$RC:$(printf '%s' "$OUT" | grep -c 'local port 48998 (Onyx) is taken; set AW_SINDRI_POD_ONYX_PORT'):$(count 'exec -it'):$(count '^ui home=')" "2:1:0:0"
  kill "$(cat "$WORK/runs/listener2.pid")" 2>/dev/null
fi

# --- the default Onyx port is 8081
OUT="$(env -u AW_SINDRI_POD_ONYX_PORT PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect --dry-run --onyx 2>&1)"
check "--onyx: the default Onyx port is 8081" "$(printf '%s' "$OUT" | grep -c 'pod/sindri-0 8081:8081')" "1"

# --- --check: every prerequisite, exit 0 or 2, nothing started
reset
OUT="$(conn --check)"; RC=$?
check "--check: all hold, exit 0, an ok line for each" "$RC:$(printf '%s' "$OUT" | grep -c '^sindri-pod: ok:')" "0:7"
check "--check: forwards nothing, starts nothing, writes no mirror home" \
  "$(count port-forward):$(count '^ui home='):$(mirror_dir)" "0:0:"
touch "$POD/deny-pods_exec"; echo false > "$POD/sindri-ready"
OUT="$(AW_SINDRI_UI="$WORK/nope" conn --check)"; RC=$?
check "--check: reports every failure, not just the first, exit 2" \
  "$RC:$(printf '%s' "$OUT" | grep -c -e 'sindri-app' -e 'create pods/exec' -e 'sindri container of pod sindri-0 is not ready')" "2:3"
rm -f "$POD/deny-pods_exec" "$POD/sindri-ready"
reset
OUT="$(conn --check --onyx)"; RC=$?
check "--check --onyx: also checks the Onyx port, runs no attach" "$RC:$(printf '%s' "$OUT" | grep -c 'Onyx) is free'):$(count 'exec -it')" "0:1:0"

# --- cleanup: TERM (what Ctrl-C does) removes the mirror home and the port-forward
reset
run_connect f
waitfor 10 has_ui; M="$(mirror_dir)"
check "cleanup: the mirror home is 0700 and holds the owner pid" "$(mode "$M"):$(test -s "$M/owner.pid" && echo pid)" "700:pid"
kill -TERM "$(cat "$M/owner.pid")"
waitfor 10 test -s "$WORK/runs/f.rc"; check "cleanup: TERM ends the script with exit 0" "$(cat "$WORK/runs/f.rc")" "0"
check "cleanup: the mirror home is gone" "$(mirror_dir)" ""
touch "$STOP"

# --- stale mirror homes of dead runs are removed on the next run; live and unmarked ones are left
reset
mkdir -p "$TMPDIR/aw-sindri-pod.stale" "$TMPDIR/aw-sindri-pod.live" "$TMPDIR/aw-sindri-pod.plain"
echo 999999 > "$TMPDIR/aw-sindri-pod.stale/owner.pid"; echo $$ > "$TMPDIR/aw-sindri-pod.live/owner.pid"
run_connect g
waitfor 10 has_ui
check "stale: a dead run's home is removed" "$([ -d "$TMPDIR/aw-sindri-pod.stale" ] && echo kept || echo removed)" "removed"
check "stale: a live run's and an unmarked directory are left" \
  "$([ -d "$TMPDIR/aw-sindri-pod.live" ] && [ -d "$TMPDIR/aw-sindri-pod.plain" ] && echo kept)" "kept"
finish g >/dev/null

# --- the retry loop is bounded: the pod never answers
reset; touch "$POD/down"
OUT="$(AW_SINDRI_POD_RETRIES=2 conn)"; RC=$?
check "pod never reachable: exit 1 after the attempts" "$RC:$(printf '%s' "$OUT" | grep -c "can't reach sindri-0")" "1:1"
check "pod never reachable: attempts 1 and 2 announced, no third" \
  "$(printf '%s' "$OUT" | grep -c -e 'reconnecting (attempt 1 of 2)' -e 'reconnecting (attempt 2 of 2)'):$(printf '%s' "$OUT" | grep -c 'attempt 3')" "2:0"
check "pod never reachable: the mirror home is removed too" "$(mirror_dir)" ""
rm -f "$POD/down"
conn --bogus >/dev/null; check "unknown argument: exit 2" "$?" "2"
bash "$DIR/sindri-pod.sh" >/dev/null 2>&1; check "no verb: exit 2" "$?" "2"

# --- profile edit: copy out, edit, validate, put back, approve
cat > "$WORK/bin/ed" <<'EOS'
#!/usr/bin/env bash
echo "editor $1" >> "$CALLS"
[ -e "$POD/ed-change" ] && echo "share: true" >> "$1"
exit 0
EOS
chmod +x "$WORK/bin/ed"
printf 'schemaVersion: 1\nuser: me\n' > "$POD/profile.yaml"
pedit() { printf '%s' "${PEDIT_IN:-}" | env -u VISUAL EDITOR="$WORK/bin/ed" PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" profile edit "$@" 2>&1; }
pedit_reset() { reset; rm -f "$POD/ed-change" "$POD/validate-rc" "$POD/approve-rc" "$POD/uploaded" "$POD/validated"
  printf 'schemaVersion: 1\nuser: me\n' > "$POD/profile.yaml"; }
seq_of() { awk '/sindri profile validate/ {print "validate"} / mv / {print "move"} /profile approve/ {print "approve"}' "$CALLS" | paste -sd, -; }

pedit_reset
OUT="$(pedit)"; RC=$?
check "profile edit unchanged: exit 0, says so, no validate or approve" \
  "$RC:$(printf '%s' "$OUT" | grep -c 'profile unchanged; nothing to validate or approve'):$(seq_of)" "0:1:"
check "profile edit: the profile is read with exec cat at the pod's profile path" "$(count 'exec sindri-0 -c sindri -- cat /data/profile/profile.yaml')" "1"

pedit_reset; touch "$POD/ed-change"
OUT="$(pedit)"; RC=$?
check "profile edit changed and valid: validate, then put back, then approve" "$RC:$(seq_of)" "0:validate,move,approve"
check "profile edit: approve runs with -it in the sindri container" "$(count 'exec -it sindri-0 -c sindri -- sindri profile approve')" "1"
check "profile edit: the pod's profile is the edited one" "$(tail -1 "$POD/profile.yaml")" "share: true"
check "profile edit: validate ran on the temp copy, not the live path" \
  "$(count 'AW_SINDRI_PROFILE="$1" sindri profile validate _ /data/profile/profile.yaml.edit-')" "1"
check "profile edit: the final line says what changed and what is next" \
  "$(printf '%s' "$OUT" | tail -1 | grep -c 'profile changed (1 lines differ) and approved in sindri-0; next:')" "1"
check "profile edit: the local copy is removed on success" "$(ls "$AW_STATE_DIR"/sindri-pod/*/profile-edit 2>/dev/null | head -1)" ""

pedit_reset; touch "$POD/ed-change"; echo 2 > "$POD/validate-rc"
PEDIT_IN=$'a\n'; OUT="$(pedit)"; RC=$?; unset PEDIT_IN
check "profile edit invalid: exit 2, errors shown, no move, no approve" \
  "$RC:$(printf '%s' "$OUT" | grep -c 'SND-PROFILE-001 bad key'):$(seq_of)" "2:1:validate"
KEPT="$(ls "$AW_STATE_DIR"/sindri-pod/*/profile-edit/profile.yaml 2>/dev/null | head -1)"
check "profile edit invalid: the local copy is kept and its path printed" \
  "$([ -n "$KEPT" ] && tail -1 "$KEPT"):$(printf '%s' "$OUT" | grep -c -F "your edited copy is at $KEPT")" "share: true:1"
check "profile edit invalid: the pod's profile is unchanged" "$(wc -l < "$POD/profile.yaml" | tr -d ' ')" "2"

pedit_reset; touch "$POD/ed-change"; echo 2 > "$POD/validate-rc"
PEDIT_IN=$'e\na\n'; OUT="$(pedit)"; RC=$?; unset PEDIT_IN
check "profile edit invalid, edit again: the editor opens twice and validate runs twice" "$RC:$(count '^editor '):$(count 'sindri profile validate')" "2:2:2"

pedit_reset; touch "$POD/ed-change"; echo 5 > "$POD/approve-rc"
OUT="$(pedit)"; RC=$?
check "profile edit approve refused: its exit code propagates, order held" "$RC:$(seq_of)" "5:validate,move,approve"
KEPT="$(ls "$AW_STATE_DIR"/sindri-pod/*/profile-edit/profile.yaml 2>/dev/null | head -1)"
check "profile edit approve refused: the copy is kept and the next step named" \
  "$([ -n "$KEPT" ] && echo kept):$(printf '%s' "$OUT" | grep -c 'in place in sindri-0 but not approved')" "kept:1"
pedit_reset
OUT="$(pedit --port 1)"; RC=$?; check "profile edit: connect-only flags are a usage error" "$RC" "2"


exit "$fail"
