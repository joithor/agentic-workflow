#!/usr/bin/env bash
# scripts/sindri-pod.sh — the owner's door to a Sindri that runs in a Kubernetes pod (deployable-context spec K8, §C.3).
#
#   sindri-pod.sh connect [--context C] [--namespace N] [--pod P] [--port PORT] [--onyx] [--check] [--dry-run]
#
# connect opens the desktop app on the pod. The app only follows a control.json in a home on this machine, so the script
# builds one: it port-forwards the pod's loopback owner port (the AW_SINDRI_PORT in the pod's control.json), reads the
# pod's control.json and fresh human token over `kubectl exec`, and writes a mirror home in a fresh
# $TMPDIR/aw-sindri-pod.XXXXXX (dirs 0700, files 0600; removed on exit, Ctrl-C included; a crash leaves it, and the next run
# removes the ones whose owner process is gone) holding
#   control.json     {"url":"http://127.0.0.1:<local port>","pid":<pod pid>,"epoch":<lease epoch>}
#                    (the fields src/Sindri/Aw.Sindri.Ui/Api/ControlRecord.cs reads; the app checks pid and epoch against
#                    GET /sindri/ping and never checks the pid is alive locally, so the pod's own pid is right)
#   auth/human.token the pod's per-start human token (src/Sindri/Aw.Sindri.Ui.Desktop/ControlFilePath.cs)
# and starts sindri-ui on it (AW_SINDRI_HOME=<mirror>). The script then watches: when the port-forward drops or the pod's pid,
# epoch or token changes (a restart), it re-reads the pod, restarts the forward and rewrites the mirror home; the app shows
# its "control plane not running" state meanwhile and the script prints "pod restarting, reconnecting (attempt n of 30)".
# The script ends when the app is closed, after 30 attempts 10 s apart (5 minutes) without the pod, or on Ctrl-C (it kills
# only the port-forwards it started; the app window is left alone).
#
#   --context C     kubectl context (default: the current one)
#   --namespace N   namespace (default: the context's, else "default")
#   --pod P         the pod (default: sindri-0)
#   --port PORT     the local port the owner port is forwarded to (default: the pod's own port)
#   --onyx          also forward the pod's Onyx web port (Onyx runs as sibling containers in the pod, started by the kubelet;
#                   it is published on the pod's 127.0.0.1; default 3000, AW_SINDRI_POD_ONYX_PORT, the same port on your
#                   machine) and run `kubectl exec -it <pod> -c sindri -- sindri onyx attach` at this terminal: it checks
#                   Onyx answers and runs the pinned release, then the account steps; at the sign-up step you sign up in the
#                   browser at the forwarded address and press Enter. Nothing is pulled or started; re-running is safe.
#                   Ctrl-C during attach stops that command only: the app and the forwards stay until connect ends
#   --check         report each prerequisite (kubectl, jq, the app binary, exec and port-forward rights, the pod Ready, the
#                   local port free) and exit 0 if all hold, 2 if any does not; changes nothing
#   --dry-run       print the kubectl and sindri-ui commands and the mirror home's path; run and write nothing
#
# Environment (all optional; .env.example): AW_STATE_DIR, TMPDIR, AW_SINDRI_UI, AW_SINDRI_POD_RETRIES,
# AW_SINDRI_POD_RETRY_WAIT, AW_SINDRI_POD_POLL, AW_SINDRI_POD_ONYX_PORT.
# Needs kubectl (exec and port-forward rights on the namespace), jq and sindri-ui (`sindri-app --publish-only`).
# Each prerequisite failure is one line and exit 2. Exit: 0 the app was closed (or attach's own exit code when it failed) or the script was interrupted, 1 the pod was
# lost for good (attempts spent), 2 usage or a prerequisite.
# Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

say() { echo "sindri-pod: $*" >&2; }
die() { say "$*"; exit 1; }
usage() { say "usage: sindri-pod.sh connect [--context C] [--namespace N] [--pod P] [--port PORT] [--onyx] [--check] [--dry-run]"; exit 2; }

[ "${1:-}" = connect ] || usage
shift
ctx="" ns="" pod=sindri-0 lport="" onyx=0 dry=0 check=0
while [ $# -gt 0 ]; do
  case "$1" in
    --context) [ $# -ge 2 ] || usage; ctx="$2"; shift 2 ;;
    --namespace) [ $# -ge 2 ] || usage; ns="$2"; shift 2 ;;
    --pod) [ $# -ge 2 ] || usage; pod="$2"; shift 2 ;;
    --port) [ $# -ge 2 ] || usage; lport="$2"; shift 2 ;;
    --onyx) onyx=1; shift ;;
    --check) check=1; shift ;;
    --dry-run) dry=1; shift ;;
    *) say "unknown argument '$1'"; usage ;;
  esac
done
case "$lport" in ''|*[!0-9]*) [ -z "$lport" ] || { say "--port must be a number"; exit 2; } ;; esac

retries="${AW_SINDRI_POD_RETRIES:-30}"
retry_wait="${AW_SINDRI_POD_RETRY_WAIT:-10}"
poll="${AW_SINDRI_POD_POLL:-5}"
onyx_port="${AW_SINDRI_POD_ONYX_PORT:-3000}"
state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"

kargs=()   # --context and --namespace, once both are resolved
kc() { kubectl ${kargs[@]+"${kargs[@]}"} "$@"; }

# The pod's home, resolved in the pod the way the app resolves its own (ControlFilePath.cs).
pod_read='h="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"; cat "$h/control.json" && echo && echo @@aw-token@@ && cat "$h/auth/human.token"'

alive() { [ -n "$1" ] && kill -0 "$1" 2>/dev/null; }

find_ui() {
  if [ -n "${AW_SINDRI_UI:-}" ]; then printf '%s\n' "$AW_SINDRI_UI"; return; fi
  # shellcheck disable=SC2012
  ls -dt "$state"/bin/sindri-app/*/sindri-ui 2>/dev/null | head -1
}

if [ "$dry" = 1 ]; then
  echo "+ checks: kubectl auth can-i create pods/exec and pods/portforward; kubectl get pod $pod (Ready); the local port is free; the app binary exists"
  echo "mirror home: ${TMPDIR:-/tmp}/aw-sindri-pod.XXXXXX (0700; control.json and auth/human.token 0600; removed on exit)"
  echo "+ kubectl [--context ${ctx:-<current>}] [--namespace ${ns:-<current>}] exec $pod -c sindri -- sh -c '<cat control.json and auth/human.token>'"
  echo "+ kubectl port-forward pod/$pod ${lport:-<pod port>}:<pod port from control.json>"
  echo "+ AW_SINDRI_HOME=<mirror home> <sindri-ui: \$AW_SINDRI_UI, else the newest $state/bin/sindri-app/*/sindri-ui>"
  echo "  (every ${poll}s: re-read pid, epoch and token; on a change or a dropped forward, retry up to $retries attempts ${retry_wait}s apart and rewrite the mirror home)"
  if [ "$onyx" = 1 ]; then
    echo "+ kubectl port-forward pod/$pod $onyx_port:$onyx_port; print http://127.0.0.1:$onyx_port"
    echo "+ kubectl exec -it $pod -c sindri -- sindri onyx attach   (Ctrl-C stops attach only; an attach failure is this script's exit code)"
  fi
  exit 0
fi

fails=0
step_ok() { [ "$check" = 1 ] && say "ok: $*"; return 0; }
step_fail() { say "$*"; fails=1; [ "$check" = 1 ] || exit 2; }
port_taken() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }

have_k=1
if command -v kubectl >/dev/null 2>&1; then step_ok "kubectl is on the PATH"
else step_fail "kubectl is not on the PATH (it needs exec and port-forward rights on the namespace)"; have_k=0; fi
if command -v jq >/dev/null 2>&1; then step_ok "jq is on the PATH"; else step_fail "jq is not on the PATH"; fi
ui="$(find_ui)"
if [ -n "$ui" ] && [ -x "$ui" ]; then step_ok "sindri-ui found ($ui)"
else step_fail "sindri-ui not found; install it with sindri-app (scripts/sindri-app.sh --publish-only) or set AW_SINDRI_UI"; fi

if [ "$have_k" = 1 ]; then
  [ -n "$ctx" ] || ctx="$(kubectl config current-context 2>/dev/null)"
  if [ -z "$ctx" ]; then step_fail "no kubectl context (pass --context)"; have_k=0; fi
fi
if [ "$have_k" = 1 ]; then
  [ -n "$ns" ] || ns="$(kubectl --context "$ctx" config view --minify -o 'jsonpath={..namespace}' 2>/dev/null)"
  [ -n "$ns" ] || ns=default
  kargs=(--context "$ctx" --namespace "$ns")
  for verb in pods/exec pods/portforward; do
    ans="$(kc auth can-i create "$verb" 2>/dev/null)"
    if [ "$ans" = yes ]; then step_ok "kubectl may create $verb in $ns"
    else step_fail "kubectl is not allowed to 'create $verb' in $ns on $ctx (answer: ${ans:-none})"; fi
  done
  st="$(kc get pod "$pod" -o 'jsonpath={.status.phase} {.status.conditions[?(@.type=="Ready")].status}' 2>/dev/null)"
  phase="${st%% *}"
  if [ "$phase" = Running ] && [ "${st#* }" = True ]; then step_ok "pod $pod is Ready"
  else step_fail "pod $pod is not Ready (phase: ${phase:-unknown})"; fi
  want="$lport"
  if [ -z "$want" ]; then   # the pod's own port, when its control.json is readable already
    want="$(kc exec "$pod" -c sindri -- sh -c "$pod_read" 2>/dev/null | sed '/^@@aw-token@@$/,$d' | jq -r '.url // empty' 2>/dev/null \
      | sed -n 's#^.*:\([0-9][0-9]*\)/*$#\1#p')"
  fi
  if [ -z "$want" ]; then step_ok "local port: skipped (the pod's control.json is not readable yet)"
  elif port_taken "$want"; then step_fail "local port $want is taken; pick another with --port"
  else step_ok "local port $want is free"; fi
  if [ "$onyx" = 1 ]; then
    if port_taken "$onyx_port"; then step_fail "local port $onyx_port (Onyx) is taken; set AW_SINDRI_POD_ONYX_PORT"
    else step_ok "local port $onyx_port (Onyx) is free"; fi
  fi
fi
if [ "$check" = 1 ]; then
  [ "$fails" = 0 ] && exit 0
  exit 2
fi
[ "$fails" = 0 ] || exit 2

umask 077
tmp="${TMPDIR:-/tmp}"; tmp="${tmp%/}"
# A crashed run leaves its mirror home; the owner.pid file says whose it was.
for d in "$tmp"/aw-sindri-pod.*; do
  [ -d "$d" ] && [ -O "$d" ] && [ -f "$d/owner.pid" ] || continue
  alive "$(cat "$d/owner.pid" 2>/dev/null)" || rm -rf "$d"
done
home="$(mktemp -d "$tmp/aw-sindri-pod.XXXXXX")" || die "can't create a mirror home under $tmp"
echo $$ > "$home/owner.pid"
safe="$(printf '%s-%s' "$ctx" "$ns" | tr -c 'A-Za-z0-9._-' '_')"
logs="$state/sindri-pod/$safe/logs"   # logs hold no secrets and outlive the mirror home
mkdir -p "$home/auth" "$logs" || die "can't create $home"

pf_pid="" onyx_pf_pid="" ui_pid=""
cur_pid="" cur_epoch="" cur_token="" remote_port=""

stop_own() {   # only the port-forwards this run recorded
  [ -z "$pf_pid" ] || kill "$pf_pid" 2>/dev/null
  [ -z "$onyx_pf_pid" ] || kill "$onyx_pf_pid" 2>/dev/null
  pf_pid="" onyx_pf_pid=""
}
cleanup() { stop_own; case "$home" in "$tmp"/aw-sindri-pod.*) rm -rf "$home" ;; esac; }
trap 'cleanup' EXIT
trap 'exit 0' INT TERM

sleep_for() { sleep "$(awk -v p="$poll" -v n="$1" 'BEGIN { print p * n }')"; }

write_home() {   # atomic, owner-only
  jq -n --arg url "http://127.0.0.1:$lport" --argjson pid "$cur_pid" --argjson epoch "$cur_epoch" \
    '{url: $url, pid: $pid, epoch: $epoch}' > "$home/control.json.tmp" && mv "$home/control.json.tmp" "$home/control.json" || return 1
  printf '%s\n' "$cur_token" > "$home/auth/human.token.tmp" && mv "$home/auth/human.token.tmp" "$home/auth/human.token" || return 1
  chmod 700 "$home" "$home/auth"; chmod 600 "$home/control.json" "$home/auth/human.token"
}

start_forward() {
  [ -z "$pf_pid" ] || kill "$pf_pid" 2>/dev/null
  kubectl "${kargs[@]}" port-forward "pod/$pod" "$lport:$remote_port" >> "$logs/port-forward.log" 2>&1 &
  pf_pid=$!
  sleep_for 1   # settle: a forward that can't bind or reach the pod exits within moments
  alive "$pf_pid"
}

# One reconcile: read the pod, keep the forward on the pod's port, rewrite the mirror home when pid, epoch or token moved.
reconcile() {
  local out ctl tok pid epoch port
  out="$(kc exec "$pod" -c sindri -- sh -c "$pod_read" 2>>"$logs/exec.log")" || return 1
  ctl="$(printf '%s\n' "$out" | sed '/^@@aw-token@@$/,$d')"
  tok="$(printf '%s\n' "$out" | sed '1,/^@@aw-token@@$/d' | head -1 | tr -d '[:space:]')"
  pid="$(printf '%s' "$ctl" | jq -r '.pid // empty' 2>/dev/null)"
  epoch="$(printf '%s' "$ctl" | jq -r '.epoch // empty' 2>/dev/null)"
  port="$(printf '%s' "$ctl" | jq -r '.url // empty' 2>/dev/null | sed -n 's#^.*:\([0-9][0-9]*\)/*$#\1#p')"
  [ -n "$pid" ] && [ -n "$epoch" ] && [ -n "$port" ] && [ -n "$tok" ] || return 1
  if [ "$port" != "$remote_port" ] || ! alive "$pf_pid"; then
    remote_port="$port"
    [ -n "$lport" ] || lport="$port"
    start_forward || return 1
  fi
  if [ "$pid" != "$cur_pid" ] || [ "$epoch" != "$cur_epoch" ] || [ "$tok" != "$cur_token" ]; then
    cur_pid="$pid" cur_epoch="$epoch" cur_token="$tok"
    write_home || return 1
    say "mirror home written: pid $cur_pid, epoch $cur_epoch ($home)"
  fi
}

# reconcile, up to $retries attempts $retry_wait seconds apart (30 x 10 s = 5 minutes by default)
reconcile_retrying() {
  local n=0
  until reconcile; do
    n=$((n + 1))
    [ "$n" -le "$retries" ] || return 1
    say "pod restarting, reconnecting (attempt $n of $retries)"
    sleep "$retry_wait"
  done
  [ "$n" -eq 0 ] || say "reconnected to $pod"
}

reconcile_retrying || die "can't reach $pod in $ctx/$ns after $retries attempts (see $logs)"

AW_SINDRI_HOME="$home" nohup "$ui" >> "$logs/ui.log" 2>&1 &
ui_pid=$!
say "app started (pid $ui_pid) on $home"
say "connected: $pod at http://127.0.0.1:$lport; Ctrl-C to stop"

attach_rc=0
if [ "$onyx" = 1 ]; then
  kubectl "${kargs[@]}" port-forward "pod/$pod" "$onyx_port:$onyx_port" >> "$logs/onyx-port-forward.log" 2>&1 &
  onyx_pf_pid=$!
  say "Onyx: forwarded http://127.0.0.1:$onyx_port; 'sindri onyx attach' now runs in the pod and asks you to sign up there"
  trap ':' INT   # Ctrl-C reaches attach (and this shell) but ends only attach
  kc exec -it "$pod" -c sindri -- sindri onyx attach
  rc=$?
  trap 'exit 0' INT
  case "$rc" in
    0) say "Onyx attached" ;;
    130) say "attach stopped; the app stays connected; rerun connect --onyx to continue" ;;
    *) attach_rc=$rc
       say "sindri onyx attach failed (exit $rc): Onyx is not answering at the forwarded port $onyx_port, runs a different version than the pinned release, or the exec was refused; the app stays connected; rerun connect --onyx" ;;
  esac
fi

while alive "$ui_pid"; do
  sleep_for 1
  alive "$ui_pid" || break
  reconcile_retrying || die "lost $pod in $ctx/$ns after $retries attempts (see $logs)"
done
say "the app was closed"
exit "$attach_rc"
