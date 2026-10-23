#!/usr/bin/env bash
# scripts/sindri-pod.sh — the owner's door to a Sindri that runs in a Kubernetes pod (deployable-context spec K8, §C.3).
#
#   sindri-pod.sh connect [--context C] [--namespace N] [--pod P] [--port PORT] [--onyx] [--dry-run]
#
# connect opens the desktop app on the pod. The app only follows a control.json in a home on this machine, so the script
# builds one: it port-forwards the pod's loopback owner port (the AW_SINDRI_PORT in the pod's control.json), reads the
# pod's control.json and fresh human token over `kubectl exec`, and writes a mirror home under
# $AW_STATE_DIR/sindri-pod/<context>-<namespace>/ (dirs 0700, files 0600) holding
#   control.json     {"url":"http://127.0.0.1:<local port>","pid":<pod pid>,"epoch":<lease epoch>}
#                    (the fields src/Sindri/Aw.Sindri.Ui/Api/ControlRecord.cs reads; the app checks pid and epoch against
#                    GET /sindri/ping and never checks the pid is alive locally, so the pod's own pid is right)
#   auth/human.token the pod's per-start human token (src/Sindri/Aw.Sindri.Ui.Desktop/ControlFilePath.cs)
# and starts sindri-ui on it (AW_SINDRI_HOME=<mirror>). The script then watches: when the port-forward drops or the pod's pid,
# epoch or token changes (a restart), it re-reads the pod, restarts the forward and rewrites the mirror home; the app shows
# its "control plane not running" state meanwhile. The script ends when the app is closed, when a retry budget is spent,
# or on Ctrl-C (it kills only the port-forwards it started; the app window is left alone).
#
#   --context C     kubectl context (default: the current one)
#   --namespace N   namespace (default: the context's, else "default")
#   --pod P         the pod (default: sindri-0)
#   --port PORT     the local port the owner port is forwarded to (default: the pod's own port)
#   --onyx          also forward the pod's Onyx web port (Onyx runs in the pod's Docker sidecar and publishes it on the pod's
#                   127.0.0.1; default 3000) and run `kubectl exec -it <pod> -c sindri -- sindri onyx up` at this terminal:
#                   its preflight, fetch, pull, start and account steps; at the sign-up step you sign up in the browser at
#                   the forwarded address and press Enter inside `onyx up`. If Onyx took a fallback port, the script reads
#                   it from `sindri onyx status --json` afterwards and forwards that one too
#   --dry-run       print the kubectl and sindri-ui commands and the mirror home's path; run and write nothing
#
# Environment (all optional; .env.example): AW_STATE_DIR, AW_SINDRI_UI, AW_SINDRI_POD_RETRIES, AW_SINDRI_POD_POLL,
# AW_SINDRI_POD_ONYX_PORT.
# Needs kubectl (exec and port-forward rights on the namespace), jq and sindri-ui (`sindri-app --publish-only`).
# Exit: 0 the app was closed or the script was interrupted, 1 a failure (missing tool, retries spent), 2 usage.
# Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

say() { echo "sindri-pod: $*" >&2; }
die() { say "$*"; exit 1; }
usage() { say "usage: sindri-pod.sh connect [--context C] [--namespace N] [--pod P] [--port PORT] [--onyx] [--dry-run]"; exit 2; }

[ "${1:-}" = connect ] || usage
shift
ctx="" ns="" pod=sindri-0 lport="" onyx=0 dry=0
while [ $# -gt 0 ]; do
  case "$1" in
    --context) [ $# -ge 2 ] || usage; ctx="$2"; shift 2 ;;
    --namespace) [ $# -ge 2 ] || usage; ns="$2"; shift 2 ;;
    --pod) [ $# -ge 2 ] || usage; pod="$2"; shift 2 ;;
    --port) [ $# -ge 2 ] || usage; lport="$2"; shift 2 ;;
    --onyx) onyx=1; shift ;;
    --dry-run) dry=1; shift ;;
    *) say "unknown argument '$1'"; usage ;;
  esac
done
case "$lport" in ''|*[!0-9]*) [ -z "$lport" ] || { say "--port must be a number"; exit 2; } ;; esac

retries="${AW_SINDRI_POD_RETRIES:-5}"
poll="${AW_SINDRI_POD_POLL:-5}"
onyx_port="${AW_SINDRI_POD_ONYX_PORT:-3000}"
state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"

kargs=()   # --context and --namespace, once both are resolved
kc() { kubectl ${kargs[@]+"${kargs[@]}"} "$@"; }

# The pod's home, resolved in the pod the way the app resolves its own (ControlFilePath.cs).
pod_read='h="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"; cat "$h/control.json" && echo && echo @@aw-token@@ && cat "$h/auth/human.token"'

find_ui() {
  if [ -n "${AW_SINDRI_UI:-}" ]; then printf '%s\n' "$AW_SINDRI_UI"; return; fi
  # shellcheck disable=SC2012
  ls -dt "$state"/bin/sindri-app/*/sindri-ui 2>/dev/null | head -1
}

if [ "$dry" = 1 ]; then
  echo "mirror home: $state/sindri-pod/<context>-<namespace>/ (0700; control.json and auth/human.token 0600)"
  echo "+ kubectl [--context ${ctx:-<current>}] [--namespace ${ns:-<current>}] exec $pod -c sindri -- sh -c '<cat control.json and auth/human.token>'"
  echo "+ kubectl port-forward pod/$pod ${lport:-<pod port>}:<pod port from control.json>"
  echo "+ AW_SINDRI_HOME=<mirror home> <sindri-ui: \$AW_SINDRI_UI, else the newest $state/bin/sindri-app/*/sindri-ui>"
  echo "  (every ${poll}s: re-read pid, epoch and token; on a change or a dropped forward, retry up to $retries times and rewrite the mirror home)"
  if [ "$onyx" = 1 ]; then
    echo "+ kubectl port-forward pod/$pod $onyx_port:$onyx_port; print http://127.0.0.1:$onyx_port"
    echo "+ kubectl exec -it $pod -c sindri -- sindri onyx up"
    echo "+ kubectl exec $pod -c sindri -- sindri onyx status --json; if its port differs from $onyx_port, forward that one too"
  fi
  exit 0
fi

command -v kubectl >/dev/null 2>&1 || die "kubectl is not on the PATH (it needs exec and port-forward rights on the namespace)"
command -v jq >/dev/null 2>&1 || die "jq is not on the PATH"
ui="$(find_ui)"
[ -n "$ui" ] && [ -x "$ui" ] || die "no sindri-ui found; build it with scripts/sindri-app.sh --publish-only, or set AW_SINDRI_UI"

[ -n "$ctx" ] || ctx="$(kubectl config current-context 2>/dev/null)" || die "no kubectl context (pass --context)"
[ -n "$ns" ] || ns="$(kubectl --context "$ctx" config view --minify -o 'jsonpath={..namespace}' 2>/dev/null)"
[ -n "$ns" ] || ns=default

kargs=(--context "$ctx" --namespace "$ns")

safe="$(printf '%s-%s' "$ctx" "$ns" | tr -c 'A-Za-z0-9._-' '_')"
home="$state/sindri-pod/$safe"
umask 077
mkdir -p "$home/auth" "$home/logs" || die "can't create $home"

pf_pid="" onyx_pf_pid="" ui_pid=""
cur_pid="" cur_epoch="" cur_token="" remote_port=""

stop_onyx_forward() { [ -z "$onyx_pf_pid" ] || kill "$onyx_pf_pid" 2>/dev/null; onyx_pf_pid=""; }

stop_own() {   # only the port-forwards this run recorded
  [ -z "$pf_pid" ] || kill "$pf_pid" 2>/dev/null
  [ -z "$onyx_pf_pid" ] || kill "$onyx_pf_pid" 2>/dev/null
  pf_pid="" onyx_pf_pid=""
}
trap 'stop_own' EXIT
trap 'exit 0' INT TERM

alive() { [ -n "$1" ] && kill -0 "$1" 2>/dev/null; }
sleep_for() { sleep "$(awk -v p="$poll" -v n="$1" 'BEGIN { print p * n }')"; }

write_home() {   # atomic, owner-only
  jq -n --arg url "http://127.0.0.1:$lport" --argjson pid "$cur_pid" --argjson epoch "$cur_epoch" \
    '{url: $url, pid: $pid, epoch: $epoch}' > "$home/control.json.tmp" && mv "$home/control.json.tmp" "$home/control.json" || return 1
  printf '%s\n' "$cur_token" > "$home/auth/human.token.tmp" && mv "$home/auth/human.token.tmp" "$home/auth/human.token" || return 1
  chmod 700 "$home" "$home/auth"; chmod 600 "$home/control.json" "$home/auth/human.token"
}

start_forward() {
  [ -z "$pf_pid" ] || kill "$pf_pid" 2>/dev/null
  kubectl "${kargs[@]}" port-forward "pod/$pod" "$lport:$remote_port" >> "$home/logs/port-forward.log" 2>&1 &
  pf_pid=$!
  sleep_for 1   # settle: a forward that can't bind or reach the pod exits within moments
  alive "$pf_pid"
}

# One reconcile: read the pod, keep the forward on the pod's port, rewrite the mirror home when pid, epoch or token moved.
reconcile() {
  local out ctl tok pid epoch port
  out="$(kc exec "$pod" -c sindri -- sh -c "$pod_read" 2>>"$home/logs/exec.log")" || return 1
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

# reconcile with a bounded, growing wait between tries
reconcile_retrying() {
  local n=0
  until reconcile; do
    n=$((n + 1))
    [ "$n" -le "$retries" ] || return 1
    say "the pod isn't reachable yet (try $n of $retries)"
    sleep_for "$n"
  done
}

reconcile_retrying || die "can't reach $pod in $ctx/$ns after $retries retries (see $home/logs)"

AW_SINDRI_HOME="$home" nohup "$ui" >> "$home/logs/ui.log" 2>&1 &
ui_pid=$!
say "app started (pid $ui_pid) on $home"

if [ "$onyx" = 1 ]; then
  kubectl "${kargs[@]}" port-forward "pod/$pod" "$onyx_port:$onyx_port" >> "$home/logs/onyx-port-forward.log" 2>&1 &
  onyx_pf_pid=$!
  say "Onyx: forwarded http://127.0.0.1:$onyx_port; 'sindri onyx up' now runs in the pod and asks you to sign up there"
  if kc exec -it "$pod" -c sindri -- sindri onyx up; then
    # Onyx may have taken a fallback port; status knows the one it is on.
    real="$(kc exec "$pod" -c sindri -- sindri onyx status --json 2>>"$home/logs/exec.log" | jq -r '.port // empty' 2>/dev/null)"
    if [ -n "$real" ] && [ "$real" != "$onyx_port" ]; then
      stop_onyx_forward
      kubectl "${kargs[@]}" port-forward "pod/$pod" "$real:$real" >> "$home/logs/onyx-port-forward.log" 2>&1 &
      onyx_pf_pid=$!
      say "Onyx is on port $real: open http://127.0.0.1:$real"
    fi
  else
    say "sindri onyx up failed; rerun connect --onyx"
  fi
fi

while alive "$ui_pid"; do
  sleep_for 1
  alive "$ui_pid" || break
  reconcile_retrying || die "lost $pod in $ctx/$ns after $retries retries (see $home/logs)"
done
say "the app was closed"
