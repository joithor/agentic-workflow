#!/usr/bin/env bash
# Tests for scripts/sindri-pod.sh connect with a fake `kubectl` and a fake `sindri-ui` on a temp AW_STATE_DIR: the dry run
# prints the plan and writes nothing; connect writes the mirror home (control.json with the forwarded loopback URL, the pod's
# pid and epoch, auth/human.token; files 0600, dirs 0700), forwards the pod's own port and starts the app on it; a new
# epoch or token in the pod rewrites the home; a port-forward that drops is retried; refusals (no kubectl, no sindri-ui, no
# pod); --onyx forwards Onyx, waits for Enter and then runs `sindri onyx attach` in the pod, in that order.
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
unset AW_SINDRI_HOME AW_SINDRI_UI AW_SINDRI_POD_ONYX_ADDRESS
export HOME="$WORK/home" AW_STATE_DIR="$WORK/state" AW_SINDRI_POD_POLL=0.2 AW_SINDRI_POD_RETRIES=3
mkdir -p "$HOME" "$WORK/bin" "$WORK/pod" "$WORK/runs"
export CALLS="$WORK/calls" POD="$WORK/pod" STOP="$WORK/ui-stop"
: > "$CALLS"

# The pod: what `kubectl exec` reads (the pod's control.json and human token).
pod_state() {   # pid epoch token port
  printf '{"url":"http://127.0.0.1:%s","pid":%s,"epoch":%s}\n' "$4" "$1" "$2" > "$POD/control.json"
  printf '%s\n' "$3" > "$POD/token"
}
pod_state 41 7 tok-one 7480

cat > "$WORK/bin/kubectl" <<'EOS'
#!/usr/bin/env bash
echo "kubectl $*" >> "$CALLS"
case " $* " in
  *" config current-context "*) echo kind-test; exit 0 ;;
  *" config view "*) echo sindri; exit 0 ;;
  *" port-forward "*)
    if [ -e "$POD/pf-fail-once" ] && [[ " $* " == *" pod/"* ]]; then rm -f "$POD/pf-fail-once"; echo "pf failed" >&2; exit 1; fi
    exec sleep 300 ;;
  *" exec -it "*) exit 0 ;;
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
MIRROR="$AW_STATE_DIR/sindri-pod/kind-test-sindri"

mode() { stat -c %a "$1" 2>/dev/null || stat -f %Lp "$1"; }
waitfor() {   # <seconds> <command...>: poll until it succeeds
  local n=$(( $1 * 10 )); shift
  while [ "$n" -gt 0 ]; do "$@" 2>/dev/null && return 0; sleep 0.1; n=$((n - 1)); done
  return 1
}
count() { grep -c -- "$1" "$CALLS" || true; }
has_epoch() { [ "$(jq -r .epoch "$MIRROR/control.json")" = "$1" ]; }
has_ui() { grep -q '^ui home=' "$CALLS"; }
has_line() { grep -q -- "$1" "$CALLS"; }
reset() { : > "$CALLS"; rm -rf "$AW_STATE_DIR/sindri-pod"; }
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
  "$(printf '%s' "$OUT" | grep -c -e 'exec sindri-0' -e 'port-forward pod/sindri-0' -e 'sindri-ui' -e 'onyx attach')" "4"

# --- connect: mirror home, forward, app
run_connect a
waitfor 10 has_ui; check "connect: the app starts" "$?" "0"
check "connect: control.json holds the forwarded loopback URL, the pod's pid and epoch" \
  "$(jq -c . "$MIRROR/control.json")" '{"url":"http://127.0.0.1:7480","pid":41,"epoch":7}'
check "connect: the token file holds the pod's token" "$(cat "$MIRROR/auth/human.token")" "tok-one"
check "connect: files are 0600, directories 0700" \
  "$(mode "$MIRROR/control.json"):$(mode "$MIRROR/auth/human.token"):$(mode "$MIRROR"):$(mode "$MIRROR/auth")" "600:600:700:700"
check "connect: the app runs on the mirror home" "$(grep '^ui home=' "$CALLS" | head -1)" "ui home=$MIRROR"
check "connect: forwards the pod's own port on the chosen context and namespace" \
  "$(count 'kubectl --context kind-test --namespace sindri port-forward pod/sindri-0 7480:7480')" "1"

# --- the pod restarts: new pid, epoch and token
pod_state 52 8 tok-two 7480
waitfor 10 has_epoch 8; check "restart: the mirror home is rewritten with the new epoch" "$?" "0"
check "restart: pid and token follow" "$(jq -r .pid "$MIRROR/control.json"):$(cat "$MIRROR/auth/human.token")" "52:tok-two"
check "restart: files stay 0600" "$(mode "$MIRROR/control.json"):$(mode "$MIRROR/auth/human.token")" "600:600"

# --- the pod's port changes: the forward is restarted on the new port
pod_state 52 9 tok-two 7481
waitfor 10 has_epoch 9; check "new port: the home is rewritten" "$?" "0"
check "new port: the forward is restarted onto the pod's new port, the local one stays" \
  "$(count 'port-forward pod/sindri-0 7480:7481'):$(jq -r .url "$MIRROR/control.json")" "1:http://127.0.0.1:7480"

# --- pod unreachable for a while: the script retries and recovers
touch "$POD/down"; sleep 0.6; rm -f "$POD/down"; pod_state 53 10 tok-two 7481
waitfor 10 has_epoch 10; check "pod down then up: recovered without exiting" "$?" "0"
check "exit: closing the app ends the script cleanly" "$(finish a)" "0"
check "exit: the script said the app was closed" "$(said a 'the app was closed')" "1"

# --- a port-forward that fails once is retried
reset; pod_state 41 7 tok-one 7480; touch "$POD/pf-fail-once"
run_connect b
waitfor 10 has_ui; check "forward retry: the app still starts" "$?" "0"
check "forward retry: port-forward ran twice" "$(count 'port-forward pod/sindri-0')" "2"
check "forward retry: exit 0" "$(finish b)" "0"

# --- --port picks the local port, --pod the pod
reset
run_connect c --port 18480 --pod sindri-9
waitfor 10 has_ui
check "--port: the forward maps the chosen local port to the pod's, for the chosen pod" "$(count 'port-forward pod/sindri-9 18480:7480')" "1"
check "--port: control.json carries the local port" "$(jq -r .url "$MIRROR/control.json")" "http://127.0.0.1:18480"
finish c >/dev/null

# --- --onyx: forward Onyx, wait for Enter, then attach in the pod
reset
run_connect d --onyx
waitfor 10 has_line 'port-forward svc/'; waitfor 10 has_ui
check "--onyx: before Enter the app is up and Onyx forwarded, nothing attached" "$(count 'exec -it')" "0"
touch "$WORK/enter"
waitfor 10 has_line 'sindri onyx attach'; check "--onyx: attach runs after Enter" "$?" "0"
SEQ="$(awk '
  /exec sindri-0 -- sh/ && !a {print "read"; a=1} /port-forward pod\// && !b {print "forward"; b=1}
  /port-forward svc\// && !d {print "onyx-forward"; d=1} /exec -it/ {print "attach"}' "$CALLS" | paste -sd, -)"
check "--onyx: pod read, forward, Onyx forward, then attach" "$SEQ" "read,forward,onyx-forward,attach"
check "--onyx: the attach call and its cluster address" \
  "$(count 'exec -it sindri-0 -- sindri onyx attach http://onyx-webserver.sindri.svc.cluster.local:3000')" "1"
check "--onyx: the sign-up URL is printed" "$(said d 'http://127.0.0.1:3000/auth/signup')" "1"
finish d >/dev/null

# --- refusals
reset
OUT="$(PATH="/usr/bin:/bin" bash "$DIR/sindri-pod.sh" connect 2>&1)"; RC=$?
check "no kubectl: exit 1 with the sentence" "$RC:$(printf '%s' "$OUT" | grep -c 'kubectl is not on the PATH')" "1:1"
OUT="$(PATH="$WORK/bin:$PATH" AW_SINDRI_UI="$WORK/nope" bash "$DIR/sindri-pod.sh" connect 2>&1)"; RC=$?
check "no sindri-ui: exit 1 with the sentence, nothing forwarded" "$RC:$(printf '%s' "$OUT" | grep -c 'no sindri-ui found'):$(count port-forward)" "1:1:0"
touch "$POD/down"
OUT="$(PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect 2>&1)"; RC=$?
check "pod never reachable: exit 1 after the retry budget" "$RC:$(printf '%s' "$OUT" | grep -c "can't reach sindri-0")" "1:1"
rm -f "$POD/down"
PATH="$WORK/bin:$PATH" bash "$DIR/sindri-pod.sh" connect --bogus >/dev/null 2>&1; check "unknown argument: exit 2" "$?" "2"
bash "$DIR/sindri-pod.sh" >/dev/null 2>&1; check "no verb: exit 2" "$?" "2"

exit "$fail"
