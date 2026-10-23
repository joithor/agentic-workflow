#!/usr/bin/env bash
# scripts/sindri-pod.sh — the owner's door to a Sindri that runs in a Kubernetes pod (deployable-context spec K8, §C.3).
#
#   sindri-pod.sh connect [--context C] [--namespace N] [--pod P] [--port PORT] [--onyx] [--check] [--dry-run]
#
#   sindri-pod.sh profile edit [--no-approve] [--context C] [--namespace N] [--pod P]
#
# profile edit changes the pod's profile: it copies profile.yaml out of the pod (`kubectl exec ... cat`, at the path the pod's
# Sindri reads: AW_SINDRI_PROFILE, else <AW_SINDRI_HOME>/profile/profile.yaml), opens it in ${VISUAL:-${EDITOR:-vi}} and
# stops with "unchanged" when you change nothing. Otherwise it copies the edit to a temp path next to the profile in the pod
# and runs `sindri profile validate` on it there (AW_SINDRI_PROFILE=<temp path>; exit 0 or 1 is valid, the errors of an invalid
# one are shown and you choose to edit again or abort). A valid edit is moved into place and
# `kubectl exec -it <pod> -c sindri -- sindri profile approve` runs at this terminal: the approval is the protected verb you
# type, and its exit code is this script's. --no-approve stops after the profile is in place and says it is not approved (the
# first install sets several keys, then approves once). Your edited copy stays under $AW_STATE_DIR/sindri-pod/<context>-<namespace>/profile-edit/
# on any failure (its path is printed) and is removed on success.
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
#                   it is published on the pod's 127.0.0.1; default 8081, AW_SINDRI_POD_ONYX_PORT, the same port on your
#                   machine) and run `kubectl exec -it <pod> -c sindri -- sindri onyx attach` at this terminal: it checks
#                   Onyx answers and runs the pinned release, then the account steps; at the sign-up step you sign up in the
#                   browser at the forwarded address and press Enter. Nothing is pulled or started; re-running is safe.
#                   Ctrl-C during attach stops that command only: the app and the forwards stay until connect ends
#   --check         report each prerequisite (kubectl, jq, the app binary, exec and port-forward rights, the sindri container ready, the
#                   local port free) and exit 0 if all hold, 2 if any does not; changes nothing
#   --dry-run       print the kubectl and sindri-ui commands and the mirror home's path; run and write nothing
#
# Environment (all optional; .env.example): AW_STATE_DIR, TMPDIR, AW_SINDRI_UI, AW_SINDRI_POD_RETRIES,
# AW_SINDRI_POD_RETRY_WAIT, AW_SINDRI_POD_POLL, AW_SINDRI_POD_ONYX_PORT.
# Needs kubectl (exec and port-forward rights on the namespace), jq and sindri-ui (`sindri-app --publish-only`).
# Each prerequisite failure is one line and exit 2. Exit: 0 the app was closed (or attach's own exit code when it failed) or the script was interrupted, 3 the pod was
# lost for good (attempts spent), 2 usage or a prerequisite (the sindri container not ready included).
# Bash 3.2 compatible (macOS /bin/bash).
set -uo pipefail

say() { echo "sindri-pod: $*" >&2; }
die() { say "$*"; exit 1; }
give_up() { say "$*"; exit 3; }
usage() { say "usage: sindri-pod.sh connect [--context C] [--namespace N] [--pod P] [--port PORT] [--onyx] [--check] [--dry-run] | profile edit [--no-approve] [--context C] [--namespace N] [--pod P]"; exit 2; }

mode=connect
if [ "${1:-}" = profile ] && [ "${2:-}" = edit ]; then mode=edit; shift 2
elif [ "${1:-}" = connect ]; then shift
else usage; fi
ctx="" ns="" pod=sindri-0 lport="" onyx=0 dry=0 check=0 noapprove=0
while [ $# -gt 0 ]; do
  case "$1" in
    --context) [ $# -ge 2 ] || usage; ctx="$2"; shift 2 ;;
    --namespace) [ $# -ge 2 ] || usage; ns="$2"; shift 2 ;;
    --pod) [ $# -ge 2 ] || usage; pod="$2"; shift 2 ;;
    --port) [ $# -ge 2 ] || usage; lport="$2"; shift 2 ;;
    --onyx) onyx=1; shift ;;
    --check) check=1; shift ;;
    --no-approve) noapprove=1; shift ;;
    --dry-run) dry=1; shift ;;
    *) say "unknown argument '$1'"; usage ;;
  esac
done
[ "$mode" = connect ] || [ "$onyx$dry$check$lport" = 000 ] || usage
[ "$mode" = edit ] || [ "$noapprove" = 0 ] || usage
case "$lport" in ''|*[!0-9]*) [ -z "$lport" ] || { say "--port must be a number"; exit 2; } ;; esac

retries="${AW_SINDRI_POD_RETRIES:-30}"
retry_wait="${AW_SINDRI_POD_RETRY_WAIT:-10}"
poll="${AW_SINDRI_POD_POLL:-5}"
onyx_port="${AW_SINDRI_POD_ONYX_PORT:-8081}"
state="${AW_STATE_DIR:-$HOME/.agentic-workflow}"

kargs=()   # --context and --namespace, once both are resolved
kc() { kubectl ${kargs[@]+"${kargs[@]}"} "$@"; }

# profile edit: see the header.
profile_edit() {
  command -v kubectl >/dev/null 2>&1 || die "kubectl is not on the PATH"
  [ -n "$ctx" ] || ctx="$(kubectl config current-context 2>/dev/null)"
  [ -n "$ctx" ] || die "no kubectl context (pass --context)"
  [ -n "$ns" ] || ns="$(kubectl --context "$ctx" config view --minify -o 'jsonpath={..namespace}' 2>/dev/null)"
  [ -n "$ns" ] || ns=default
  kargs=(--context "$ctx" --namespace "$ns")
  local safe dir ppath tmp_remote orig edit ans rc editor out lines
  safe="$(printf '%s-%s' "$ctx" "$ns" | tr -c 'A-Za-z0-9._-' '_')"
  dir="$state/sindri-pod/$safe/profile-edit"
  umask 077
  mkdir -p "$dir" || die "can't create $dir"
  orig="$dir/profile.orig.yaml" edit="$dir/profile.yaml"
  ppath="$(kc exec "$pod" -c sindri -- sh -c 'h="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"; echo "${AW_SINDRI_PROFILE:-$h/profile/profile.yaml}"' 2>/dev/null)"
  [ -n "$ppath" ] || die "can't read the profile path from $pod in $ctx/$ns"
  kc exec "$pod" -c sindri -- cat "$ppath" > "$orig" 2>/dev/null || die "can't read $ppath from $pod (does the pod have a profile? run 'sindri profile init' in it)"
  cp "$orig" "$edit"
  tmp_remote="$ppath.edit-$$"
  editor="${VISUAL:-${EDITOR:-vi}}"
  while :; do
    sh -c "$editor \"\$1\"" _ "$edit" || { say "the editor failed; your copy is at $edit"; return 1; }
    if cmp -s "$orig" "$edit"; then
      rm -r "$dir"
      say "profile unchanged; nothing to validate or approve"
      return 0
    fi
    kc exec -i "$pod" -c sindri -- sh -c 'cat > "$1"' _ "$tmp_remote" < "$edit" || { say "can't copy the edit into $pod; your copy is at $edit"; return 1; }
    out="$(kc exec "$pod" -c sindri -- sh -c 'AW_SINDRI_PROFILE="$1" sindri profile validate' _ "$tmp_remote" 2>&1)"; rc=$?
    if [ "$rc" -ge 2 ]; then
      printf '%s\n' "$out" >&2
      say "the edited profile is not valid (sindri profile validate exit $rc)"
      printf 'sindri-pod: edit again or abort? [e/a] ' >&2
      read -r ans || ans=a
      case "$ans" in
        e|E) continue ;;
        *) kc exec "$pod" -c sindri -- rm -f "$tmp_remote" >/dev/null 2>&1
           say "aborted; the pod's profile is unchanged; your edited copy is at $edit"
           return 2 ;;
      esac
    fi
    break
  done
  kc exec "$pod" -c sindri -- sh -c 'mv "$1" "$2"' _ "$tmp_remote" "$ppath" || { say "can't move the edit into place; your copy is at $edit"; return 1; }
  lines="$(diff "$orig" "$edit" | grep -c '^[<>]')"
  if [ "$noapprove" = 1 ]; then
    rm -r "$dir"
    say "profile changed ($lines lines differ), valid and in place in $pod, not approved (--no-approve); next: kubectl exec -it $pod -c sindri -- sindri profile approve"
    return 0
  fi
  kc exec -it "$pod" -c sindri -- sindri profile approve; rc=$?
  if [ "$rc" -ne 0 ]; then
    say "the profile is in place in $pod but not approved (sindri profile approve exit $rc); your edited copy is at $edit"
    say "next: kubectl exec -it $pod -c sindri -- sindri profile approve"
    return "$rc"
  fi
  rm -r "$dir"
  say "profile changed ($lines lines differ) and approved in $pod; next: sindri-pod.sh connect, or 'kubectl exec $pod -c sindri -- sindri status' to see it take effect"
}

# The pod's home, resolved in the pod the way the app resolves its own (ControlFilePath.cs).
pod_read='h="${AW_SINDRI_HOME:-${AW_STATE_DIR:-$HOME/.agentic-workflow}/sindri-app}"; cat "$h/control.json" && echo && echo @@aw-token@@ && cat "$h/auth/human.token"'

# The sindri container's ready flag (true, false, or empty when the pod or the container is not there) and the other
# containers that are not ready ("name=false ...", information only).
sindri_ready() { kc get pod "$pod" -o 'jsonpath={.status.containerStatuses[?(@.name=="sindri")].ready}' 2>/dev/null; }
other_containers() { kc get pod "$pod" -o 'jsonpath={range .status.containerStatuses[?(@.name!="sindri")]}{.name}={.ready} {end}' 2>/dev/null | tr ' ' '\n' | grep -v '=true$' | grep . | paste -sd' ' -; }

alive() { [ -n "$1" ] && kill -0 "$1" 2>/dev/null; }

find_ui() {
  if [ -n "${AW_SINDRI_UI:-}" ]; then printf '%s\n' "$AW_SINDRI_UI"; return; fi
  # shellcheck disable=SC2012
  ls -dt "$state"/bin/sindri-app/*/sindri-ui 2>/dev/null | head -1
}

[ "$mode" = edit ] && { profile_edit; exit $?; }

if [ "$dry" = 1 ]; then
  echo "+ checks: kubectl auth can-i create pods/exec and pods/portforward; kubectl get pod $pod (the sindri container ready); the local port is free; the app binary exists"
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
  # Ready means the sindri container, not the pod: an Onyx container that crash-loops keeps the pod not Ready and Sindri works.
  # connect waits for it (the retry settings); --check only reports.
  n=0
  while :; do
    sready="$(sindri_ready)"
    [ "$sready" = true ] || [ "$check" = 1 ] || [ "$n" -ge "$retries" ] && break
    n=$((n + 1))
    say "waiting for the sindri container of pod $pod (attempt $n of $retries)"
    sleep "$retry_wait"
  done
  others="$(other_containers)"
  if [ "$sready" = true ]; then
    step_ok "the sindri container of pod $pod is ready"
    [ -z "$others" ] || say "note: other containers in $pod are not ready ($others); Sindri does not need them"
  else
    step_fail "the sindri container of pod $pod is not ready (ready: ${sready:-unknown})${others:+; other containers, for information: $others}"
  fi
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

reconcile_retrying || give_up "can't reach $pod in $ctx/$ns after $retries attempts (see $logs)"

AW_SINDRI_HOME="$home" nohup "$ui" >> "$logs/ui.log" 2>&1 &
ui_pid=$!
say "app started (pid $ui_pid) on $home"
say "connected: $pod at http://127.0.0.1:$lport; Ctrl-C to stop"

attach_rc=0
if [ "$onyx" = 1 ]; then
  kubectl "${kargs[@]}" port-forward "pod/$pod" "$onyx_port:$onyx_port" >> "$logs/onyx-port-forward.log" 2>&1 &
  onyx_pf_pid=$!
  say "Onyx is at http://127.0.0.1:$onyx_port — sign up there when attach asks"
  trap ':' INT   # Ctrl-C reaches attach (and this shell) but ends only attach
  kc exec -it "$pod" -c sindri -- sindri onyx attach
  rc=$?
  trap 'exit 0' INT
  case "$rc" in
    0) say "Onyx attached" ;;
    130) say "attach stopped; the app stays connected; rerun connect --onyx to continue" ;;
    *) attach_rc=$rc
       say "attach exited $rc"
       say "sindri onyx attach failed (exit $rc): Onyx is not answering at the forwarded port $onyx_port, runs a different version than the pinned release, or the exec was refused; the app stays connected; rerun connect --onyx" ;;
  esac
fi

while alive "$ui_pid"; do
  sleep_for 1
  alive "$ui_pid" || break
  reconcile_retrying || give_up "lost $pod in $ctx/$ns after $retries attempts (see $logs)"
done
say "the app was closed"
exit "$attach_rc"
