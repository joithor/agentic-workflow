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
verb (`create pods/exec` or `create pods/portforward`, named in the line), the `sindri` container of the pod not ready (named; `connect` waits for it up to `AW_SINDRI_POD_RETRIES` attempts first, `--check` only reports; other containers, such as a crash-looping Onyx one, are mentioned only as information and never fail `connect`), the local port
taken (named, with `--port`), or `sindri-ui` not found (the line names `sindri-app`). `connect --check` reports every
prerequisite and exits 0 or 2 without starting anything.

The mirror home is a fresh `$TMPDIR/aw-sindri-pod.XXXXXX` (0700), removed when the script exits, Ctrl-C included. A crash leaves
it; the next run removes the ones whose owner process is gone. The logs stay under `$AW_STATE_DIR/sindri-pod/`.

`connect` port-forwards the pod's loopback owner port (the port in the pod's `control.json`; `--port` only changes the local
side), starts `sindri-ui` on the mirror home and prints `connected: <pod> at <url>; Ctrl-C to stop`. Every `AW_SINDRI_POD_POLL`
seconds it re-reads the pod. When the forward drops, or the pod restarts (a new pid, epoch and token), it prints
`pod restarting, reconnecting (attempt n of 30)`, restarts the forward and rewrites the home, one attempt every
`AW_SINDRI_POD_RETRY_WAIT` seconds (10), up to `AW_SINDRI_POD_RETRIES` attempts (30, so 5 minutes) before it exits 3; the app
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

## Change the profile in a pod

`scripts/sindri-pod.sh profile edit [--no-approve] [--context C] [--namespace N] [--pod P]` is how you change a share switch, `repo-pull.allowedHosts`
or any other profile key in a pod. It copies `profile.yaml` out of the pod (the path the pod's Sindri reads: `AW_SINDRI_PROFILE`, else
`<AW_SINDRI_HOME>/profile/profile.yaml`) and opens it in `${VISUAL:-${EDITOR:-vi}}`.

- Nothing changed: it says `profile unchanged; nothing to validate or approve` and stops.
- Changed: it copies your edit to a temp path next to the profile in the pod and runs `sindri profile validate` on it there. An
  invalid edit shows the errors and asks whether to edit again or abort; abort leaves the pod's profile as it was.
- Valid: it moves the edit into place and runs `kubectl exec -it <pod> -c sindri -- sindri profile approve` at your terminal. The
  approval is the protected verb you type; its exit code is the script's. The profile takes effect only once approved. With `--no-approve` it stops after putting the valid profile in place and says it is not
  approved (the first install: set several keys, approve once at the end).

Your edited copy stays under `$AW_STATE_DIR/sindri-pod/<context>-<namespace>/profile-edit/` on any failure (the path is printed,
and after a refused approval the line names the `approve` command to run by hand); it is removed on success. The last line says what
changed and the next step.
