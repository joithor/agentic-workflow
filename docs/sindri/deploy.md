# Deploying Sindri

## Connect to a pod

`scripts/sindri-pod.sh connect [--context C] [--namespace N] [--pod P] [--port PORT] [--onyx] [--dry-run]` opens the desktop
app on a Sindri that runs in a cluster (spec `2026-10-23-sindri-deployable-context-design.md` K8, §C.3). You need `kubectl`
with exec and port-forward rights on the namespace, `jq`, and `sindri-ui` (`scripts/sindri-app.sh --publish-only`, or
`AW_SINDRI_UI`).

The app only follows a `control.json` in a home on your machine, so `connect` builds one under
`$AW_STATE_DIR/sindri-pod/<context>-<namespace>/` (directories 0700, files 0600):

- `control.json` holds the forwarded loopback URL, the pod's pid and its lease epoch. The app compares pid and epoch with
  `GET /sindri/ping` and never checks the pid locally, so the pod's own pid is right.
- `auth/human.token` holds the pod's current human token, read over `kubectl exec`.

It then port-forwards the pod's loopback owner port (the port in the pod's `control.json`; `--port` only changes the local
side) and starts `sindri-ui` on the mirror home. Every `AW_SINDRI_POD_POLL` seconds it re-reads the pod. When the forward
drops, or the pod restarts (a new pid, epoch and token), it restarts the forward and rewrites the home, retrying up to
`AW_SINDRI_POD_RETRIES` times with a growing wait; the app shows "control plane not running" meanwhile. It ends when you close
the app, on Ctrl-C, or when the retries are spent; it stops only the port-forwards it started.

`--onyx` also forwards Onyx's web port, prints its sign-up URL (`http://127.0.0.1:3000/auth/signup`), waits for Enter at the
terminal, then runs `kubectl exec -it <pod> -- sindri onyx attach <cluster address>`. The Service, port and address default to
`onyx-webserver`, `3000` and `http://onyx-webserver.<namespace>.svc.cluster.local:3000`; set `AW_SINDRI_POD_ONYX_SERVICE`,
`AW_SINDRI_POD_ONYX_PORT` or `AW_SINDRI_POD_ONYX_ADDRESS` if the chart names them differently.

`--dry-run` prints the commands and writes nothing.
