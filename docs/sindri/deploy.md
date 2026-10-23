# Deploying Sindri

## Connect to a pod

`scripts/sindri-pod.sh connect [--context C] [--namespace N] [--pod P] [--port PORT] [--onyx] [--check] [--dry-run]` opens the desktop
app on a Sindri that runs in a cluster (spec `2026-10-23-sindri-deployable-context-design.md` K8, §C.3). You need `kubectl`
with exec and port-forward rights on the namespace, `jq`, and `sindri-ui` (`scripts/sindri-app.sh --publish-only`, or
`AW_SINDRI_UI`).

The app only follows a `control.json` in a home on your machine, so `connect` builds one in a temporary directory
(directories 0700, files 0600):

- `control.json` holds the forwarded loopback URL, the pod's pid and its lease epoch. The app compares pid and epoch with
  `GET /sindri/ping` and never checks the pid locally, so the pod's own pid is right.
- `auth/human.token` holds the pod's current human token, read over `kubectl exec`.

Before anything else `connect` checks its prerequisites; each failure is one line and exit 2: `kubectl` missing or refused a
verb (`create pods/exec` or `create pods/portforward`, named in the line), the pod not Ready (its phase is named), the local port
taken (named, with `--port`), or `sindri-ui` not found (the line names `sindri-app`). `connect --check` reports every
prerequisite and exits 0 or 2 without starting anything.

The mirror home is a fresh `$TMPDIR/aw-sindri-pod.XXXXXX` (0700), removed when the script exits, Ctrl-C included. A crash leaves
it; the next run removes the ones whose owner process is gone. The logs stay under `$AW_STATE_DIR/sindri-pod/`.

`connect` port-forwards the pod's loopback owner port (the port in the pod's `control.json`; `--port` only changes the local
side), starts `sindri-ui` on the mirror home and prints `connected: <pod> at <url>; Ctrl-C to stop`. Every `AW_SINDRI_POD_POLL`
seconds it re-reads the pod. When the forward drops, or the pod restarts (a new pid, epoch and token), it prints
`pod restarting, reconnecting (attempt n of 30)`, restarts the forward and rewrites the home, one attempt every
`AW_SINDRI_POD_RETRY_WAIT` seconds (10), up to `AW_SINDRI_POD_RETRIES` attempts (30, so 5 minutes) before it exits 1; the app
shows "control plane not running" meanwhile. It ends when you close the app, on Ctrl-C, or when the attempts are spent; it
stops only the port-forwards it started.

`--onyx` also forwards the pod's Onyx web port (Onyx runs as sibling containers in the pod, started by the kubelet at install;
the same port on your machine, default 8081, `AW_SINDRI_POD_ONYX_PORT`) and runs
`kubectl exec -it <pod> -c sindri -- sindri onyx attach` at the same terminal: it checks Onyx answers and runs the pinned
release, then the account steps. At the sign-up step, sign up in the browser at the forwarded address and press Enter.
Nothing is pulled or started, so it takes seconds, and re-running it is safe. Ctrl-C during `attach` stops that command only; the
app and the forwards stay until `connect` ends. If `attach` fails (Onyx not answering at the forwarded port, a different
version than the pinned one, the exec refused), `connect` says so, keeps the app connected and ends with `attach`'s exit code.

`--dry-run` prints the commands and writes nothing.
