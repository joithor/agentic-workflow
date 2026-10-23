# Deploying Sindri

Every `sindri-pod.sh` verb (`connect`, `profile edit`, `repo add`, `approve`, `doctor`) takes `--context C` and `--namespace N`
(kubectl's own defaults when omitted) and names the pod with `--pod P`, or with `--release R` (the pod is `<R>-0`); with neither, it
is `sindri-0`. Exit 0 and 2 mean the same everywhere (0 done; 2 cannot start: usage, a prerequisite, or the pod unreachable); 3
and 4 are per verb, below. Output is plain: no colour or spinner.

## First install, in order

1. Terminal 1: create the namespace (label `pod-security.kubernetes.io/enforce=baseline`), then
   `helm install <release> deploy/helm/sindri --namespace <ns> --timeout 30m -f values.yaml` (no `--wait`).
2. Terminal 1: `scripts/sindri-pod.sh connect --release <release> --namespace <ns>`. It waits for Sindri's container, opens the app and holds the terminal.
3. Terminal 2: `scripts/sindri-pod.sh doctor --release <release> --namespace <ns>` every few minutes until the Onyx rows are ok (skip this and the next step when Onyx is off).
4. Terminal 1: Ctrl-C, then `scripts/sindri-pod.sh connect --onyx --release <release> --namespace <ns>`; sign up in the browser and press Enter. Do it soon after install: Onyx's first account is unclaimed until it runs.
5. Terminal 2: `scripts/sindri-pod.sh profile edit --no-approve ...` (creates the default profile when there is none); set `repo-pull.allowedHosts`.
6. Terminal 2, per repo: `scripts/sindri-pod.sh repo add <name> <url> [--credential env:NAME] ...`. It records the repo and clones nothing.
7. Terminal 2: `scripts/sindri-pod.sh approve ...`, once, and type the word. The first clone and index follow.

## Connect to a pod

`scripts/sindri-pod.sh connect [--onyx [--adopt]] [--port PORT] [--check] [--dry-run]` opens the desktop
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

`--onyx [--adopt]` also forwards Onyx's page. Onyx runs as sibling containers in the pod, started by the kubelet at install; its
port is read from the pod's `AW_SINDRI_ONYX_URL` (8081 when that has none) and forwarded to the same local port, because Onyx's
`WEB_DOMAIN` is that address. `AW_SINDRI_POD_ONYX_PORT` overrides the local port and prints a warning (sign-up redirects and
connector OAuth may land on the pod's port). A taken local port is exit 2: "local port N is in use: stop what uses it, then run
this again". With `AW_SINDRI_ONYX_URL` unset in the pod, Onyx is off: "Onyx is off in this install (onyx.enabled=false); use
connect." and exit 2. It prints "Onyx's page: <address>", then runs `kubectl exec -it <pod> -c sindri -- env
AW_SINDRI_ONYX_FORWARD_URL=<address> sindri onyx attach [--adopt]` at the same terminal; `--adopt` is for an Onyx whose admin
account an earlier Sindri volume made (attach asks for that admin's login). `attach` waits for Onyx's health route for up to 20
minutes (`SND-ONYX-016` after that: run `sindri-pod.sh doctor`, then `connect --onyx` again), checks the pinned release, then
runs the account steps; sign up in the browser at the forwarded address and press Enter. Ctrl-C during `attach` stops that command
only. A failure is printed with its own code, followed by `attach exited <n>` on stderr; the app stays connected and the script's
own exit codes stay 0, 2 and 3. Re-running is safe. With Onyx's containers not ready, `connect` carries on and prints
"Onyx: not answering, see sindri doctor".

Exit codes: 0 a normal end (Ctrl-C), 2 a prerequisite or start failure (or Onyx off with `--onyx`), 3 gave up reconnecting.
The wait for the sindri container takes up to 10 minutes, printing its state every 15 s (`AW_SINDRI_POD_RETRIES` and
`AW_SINDRI_POD_RETRY_WAIT` override its attempts and spacing, as they do for reconnecting); the not-running line names the state and
`kubectl logs <pod> -c sindri`.

`--dry-run` prints the commands and writes nothing.

## Change the profile in a pod

`scripts/sindri-pod.sh profile edit [--no-approve]` is how you change `repo-pull.allowedHosts`, `index.graph.enabled` or any other
profile key in a pod: the profile is a file on the pod's volume and the image has no editor. With no profile in the pod it first
runs `sindri profile init` there (`kubectl exec -it`). It copies `profile.yaml` out of the pod (the path the pod's Sindri
reads: `AW_SINDRI_PROFILE`, else `<AW_SINDRI_HOME>/profile/profile.yaml`), remembers the pod's copy by its sha256, and opens it
in `$EDITOR` (`vi` if unset).

- Nothing changed: `profile unchanged; nothing to validate or approve`, exit 0.
- Changed: the edit goes to a temp path next to the profile in the pod and `sindri profile validate` runs on it there (the temp
  path is passed in `AW_SINDRI_PROFILE` until `sindri profile validate --file` exists). Exit 2 is invalid: the errors are put at
  the top of the file as `# sindri-pod:` comments and the editor reopens. Leaving the file as it was aborts with exit 3 and keeps
  your file.
- Valid: the live file is replaced only if the pod's copy still has the hash it had. If a `repo add`, an `onyx attach` or another
  edit changed it meanwhile, nothing is replaced and it prints "The profile on the pod changed while you were editing (<the
  top-level keys that differ>). Nothing was replaced; your edit is kept at <path>. Run profile edit again and make the change on
  top of the new copy." (exit 4). Otherwise it runs `kubectl exec -it <pod> -c sindri -- sindri profile approve` at your terminal,
  where the preview and the typed word are yours. `--no-approve` stops after the file is in place (the first install, where the one
  approval comes last).

`repo-pull.allowedHosts`, `index.graph.enabled` and every switch are read from the approved profile, so an edit, and an undo, take effect when
you approve, not when the file is saved.

Exit codes: 0 changed and approved, unchanged, or `--no-approve` done; 1 approve refused or declined; 2 usage or the pod
unreachable; 3 an invalid edit aborted; 4 conflict. Your edited copy stays under
`$AW_STATE_DIR/sindri-pod/<context>-<namespace>/profile-edit/` on any failure (the path is printed); it is removed on success.

## repo add, approve, doctor

Thin wrappers, each `kubectl exec -it <pod> -c sindri -- sindri ...`, and each returns the wrapped command's own exit code (2 for
this script's own failures: usage, the pod unreachable):

- `scripts/sindri-pod.sh repo add <name> <url> [--credential env:NAME]` runs `sindri repo add --url <url> --name <name> [--credential ...]`.
  It records the repo in the profile and clones nothing; it refuses an unlisted host (`SND-REPO-005`), so run `profile edit
  --no-approve` first. The clone runs after you approve.
- `scripts/sindri-pod.sh approve` runs `sindri profile approve`. Once, after `connect --onyx` and `repo add`.
- `scripts/sindri-pod.sh doctor` runs `sindri doctor`. The Onyx rows read "not set up" or warn until Onyx's containers answer.
