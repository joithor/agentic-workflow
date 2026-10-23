# Sindri as a deployable unit: one image, Onyx beside it, a shared context map everyone can query

**Status:** design, revision 2 (the owner's answers of 2026-10-23 on K1, K2, K3, K5 and K12 applied; §11 lists what changed), a
draft until the owner confirms the follow-ups in §1 · **Date:** 2026-10-23 · **Asked by:** the owner, 2026-10-23 (relayed by
sindri-meta): "make Sindri + Onyx a deployable unit for an EKS pod that stands up both. The owner port-forwards to it to set it up
(profile approval, Onyx admin, connectors). Then it's added to an internal MCP server so everyone can query a constantly updating
context retrieval map", refined the same night: "the deliverable is a Dockerfile that wraps Sindri". The full-map page asked for
the same night is Part D of this spec (`2026-10-23-sindri-context-whole-map-design.md`). · **Builds on:** the API surface spec
(`2026-10-10-sindri-api-surface-design.md` §6 local-only, §8 MCP, §11 deferred auth), the Onyx managed spec
(`2026-10-16-sindri-onyx-managed-design.md`), the context retrieval spec, `docs/csharp/sindri.md` (home layout, lease, serve),
`docs/csharp/forge.md` (credentials), `binaries.lock`, `context/onyx.lock` · **Lanes:** context leads; api reviews and builds the
server and MCP surface (§B); engine the repo freshness service (§C.2); ui the connect script and Part D's page; ideas the follow-ups of §9 · **Next:** the
owner confirms the two flagged items of §1 (the business-context switch, K5, and the privileged Docker sidecar, K1); then
/specToProvenPR takes §9's phases to PRs.

**Keeping it app-agnostic.** Nothing here names a work app, company, cluster, registry or host. Every one of those is a value the
deployer gives (Helm values, the profile, Kubernetes secrets), and the examples use `example.com` and `registry.example.com`.

## 0. The short version

Today Sindri is a desktop program: it binds to loopback only, trusts whoever holds the token file in its home, runs one copy per
person, and manages Onyx with Docker Compose on the same machine. In a pod, most of that stays. This spec turns it into:

1. **One image** (`Dockerfile` at the repo root, built by CI from the repo): Sindri's control plane (framework-dependent .NET 10, as
   `sindri-app` publishes it), the pinned `temporal` binary, the code-index tools (graphify, `bwrap`), `git`, the `docker` CLI with
   its Compose plugin, and nothing secret. It runs `sindri serve` in the foreground with all state on one volume.
2. **Onyx the same way as on the desktop**: Sindri's managed mode (`sindri onyx up`) runs Onyx's own Docker Compose setup at our
   pin (`context/onyx.lock`), against a **Docker daemon in a sidecar container** of the same pod (rootless Docker-in-Docker). The
   sidecar shares the pod's network, so Onyx answers on the pod's `127.0.0.1` exactly as it does on a laptop, and Sindri's
   loopback-only rule for Onyx holds unchanged. The one cost: that sidecar needs a privileged container setting (§2 A.3, flagged in K1).
3. **Two doors into Sindri**: the owner's door (today's loopback API and UI, reached with `kubectl port-forward` and
   `kubectl exec -it`, unchanged rules) and a new **shared door**: a read-only MCP endpoint with only the five `context_*` tools and
   **no authentication**, which any pod in the cluster may call; the internal MCP server everyone's agents use points at it.
4. **Code and docs only by default**: the shared door serves the code and docs sources of the approved repos. Business context
   (Onyx's chat and tickets) is served only when the owner turns on one profile switch, because with no authentication anything
   in the cluster could then read it (flagged in K5).
5. **Always fresh**: a new Sindri service pulls each repo's default branch; the code index, the docs index and the map follow it
   as they already do; Onyx's connectors (set up once by the owner in Onyx's page) keep tickets and chat current; Sindri's existing
   sync and refresh services read them.
6. **The owner's steps** stay one command or one click each: `scripts/sindri-pod.sh connect` opens the app on the pod; approvals
   are the app's existing one-click-and-type confirmations (or `kubectl exec -it … sindri profile approve` at a terminal).
   `sindri-pod.sh connect --onyx` runs `sindri onyx up` in the pod and forwards Onyx's page for the one browser sign-up.
7. **No model in v1.** Everything the map and the five tools do works without one; only labelling suggested links and the
   decisions pass need a model, and they stay off in the pod (K12).

## 1. Decisions

The owner answered K1, K2, K3, K5 and K12 on 2026-10-23; those rows are **decided**. Two consequences of those answers are
**flagged** for the owner to confirm before this spec goes ready: K1's privileged sidecar and K5's business-context switch. The rest
keep their defaults, which builders follow if the owner says nothing.

| # | Question, in plain words | Answer | Why, and the cost |
|---|---|---|---|
| **K1** | How does Onyx come up beside Sindri? | **Decided: Onyx's own Docker Compose setup, the one local managed mode drives**, through `sindri onyx up` in the pod, against a rootless Docker-in-Docker sidecar (§A.3). Not Onyx's Helm chart. **Flagged:** the sidecar runs with `privileged: true`, in a namespace of its own | The owner's answer. The one cost: Docker in a pod needs either a Docker-in-Docker sidecar or the node's Docker socket, and both are privileged. The node's socket is out: EKS nodes run containerd and have no Docker socket, and a socket would be root on the node. The least-privileged way that works on stock EKS is the **rootless** Docker-in-Docker image: the flag is still needed (to turn off the seccomp, AppArmor and `/proc` masks the daemon's own sandbox needs), but the daemon and every Onyx container run as uid 1000 with no root in the pod, no host path is mounted and the socket is shared only with Sindri's container. What the flag still gives that container, stated plainly: the node's devices and an unmasked `/proc` and `/sys`, so a process that escaped Onyx's containers and the daemon's user namespace would see more of the node than an ordinary pod does. Stricter still, with no privileged flag at all: the Sysbox container runtime (`runtimeClassName: sysbox-runc`), which a cluster admin installs on the nodes; the chart takes it as a values switch, but v1 tests only the rootless path. Either way the namespace must allow privileged pods (Pod Security level `privileged`), so the chart is installed in a **namespace of its own** (the label then admits no other workload's privileged pods), and EKS Fargate can't run it. **The way out with no privileged container:** `onyx.enabled: false` in the values leaves the sidecar out entirely; the pod then serves code and docs only, which is all the shared door serves anyway while K5's switch is off (Onyx then serves only the owner's own queries through the app) |
| **K2** | Onyx's database: inside the cluster, or a managed database? | **Decided by K1: inside the pod**, in Onyx's own Compose Postgres, on the Docker sidecar's volume | No operator, no cluster-wide resources, nothing for a cluster admin to install beyond the namespace's Pod Security label. The snapshot schedule (K7) covers this volume too |
| **K3** | Who can reach the shared door? | **Decided: any pod in the cluster.** No NetworkPolicy on the namespace | The owner's answer. The shared door serves read-only context tools only; the owner's door (approvals, items, workflows) stays on the pod's loopback, reachable only through `kubectl port-forward` and `exec` |
| **K4** | What can everyone's agents do through the shared door? | **Read the context map and nothing else**: `context_query`, `context_lenses`, `context_node`, `context_neighbors`, `context_sources`; no `sindri_*` tools (items, queue, requests stay the owner's) | Today's per-repo MCP has 12 tools, one of which files requests; a many-user door should not start work |
| **K5** | What can a caller see? | **Decided: no authentication** (no bearer tokens, no token store, no minting). A caller names a repo with `X-Sindri-Repo` and reads any repo in the approved profile. **Code and docs sources only**, unless the profile's `share.businessContext: true` is set. **Flagged:** that switch | The owner's answer. The consequences, stated plainly: with no authentication, anything that can open a connection to the pod (any pod in the cluster, K3) reads every approved repo's map, code snippets and docs, so **approving a repo in the pod's profile publishes it to the whole cluster**; that approval is the only gate. With the switch on a caller also reads every record Onyx's connectors pulled in (chat messages, including the private channels Onyx's connector can see, and tickets), because Sindri reads Onyx as one user and Onyx's own per-source permissions don't reach a caller; the switch is all or nothing across Onyx's sources in v1. Nobody can be told apart or cut off one by one: the owner's kill switch is the door itself (`share.enabled: false` in the profile, or the chart's `share.enabled` value, which removes the Service). Both switches live in the profile, so changing them is a profile change the owner approves like any other, and the `profile approve` preview says in plain words "the shared door will serve these repos to every pod in the cluster: …" and, with the switch on, "and every chat and ticket record Onyx holds". Doctor lists the served repos and shows the business-context switch as a warn row while it is on |
| **K6** | How does the pod get the code, and how fresh? | **A new Sindri service, `repo-pull`,** fast-forwards each repo's default branch every minute with a read-only token from a Kubernetes secret; the first clone happens once when a repo is added | Sindri never clones today and expects a checkout on disk. The common git-sync sidecar lays a repo out as linked worktrees, which Sindri resolves to a main checkout without a tree; a service of our own keeps the layout Sindri already checks (clean, HEAD in main) |
| **K7** | Where does the state live, and what is the backup? | **Two volumes**: Sindri's (`/data`) and the Docker sidecar's (Onyx's images, containers and its Postgres, Redis, search index and file store), both on the cluster's default block storage, encrypted. **One copy of the pod** (a StatefulSet of one), **nightly snapshots of both volumes together, 7 kept** | Sindri's ledger, views, map and Temporal's history are SQLite files that allow one writer; two replicas would fight over the lease. Snapshotting both volumes at the same moment keeps the map's Onyx cursor and Onyx's database at the same point; Postgres recovers from a crash-consistent snapshot as from a power cut |
| **K8** | How does the owner see the app on the pod? | **`scripts/sindri-pod.sh connect`**: one command that port-forwards, fetches a fresh human token over `kubectl exec`, and opens the desktop app against the pod | Today the app only reaches a Sindri whose files it can read on the same machine. A browser version of the app is still later (UI spec §12.2) |
| **K9** | Where do secrets go? | **Kubernetes secrets as environment variables** (`env:` pointers): the repo token and the forge token; never in the image, the profile or the chart's values. Onyx's own secrets (its database and auth secrets) are made by managed mode on the Docker sidecar's volume, as on the desktop. In pod mode Sindri's child processes (git, tools, docker) get only an allowlisted environment, not every secret (§A.2 A2d) | Secrets mounted as files are symlinks in Kubernetes, and Sindri's `file:` pointers refuse symlinks on purpose. The Onyx token Sindri makes at setup is written to the volume (`secrets/`, owner-only), as the managed mode does today when no keychain exists |
| **K10** | Embeddings for the code index (local models only)? | **An Ollama sidecar** in the Sindri pod on `127.0.0.1:11434`, its model pulled once onto the volume at the pinned digest | Sindri allows only a loopback Ollama; a sidecar shares the pod's loopback, so no code change |
| **K11** | Images: built how, pinned how? | **Built by CI from the repo** for linux/amd64 and linux/arm64, pushed to a registry from config, referenced **by digest** in the chart; `deploy/images.lock` pins every image the kubelet pulls (base images, the Docker CLI source, the rootless Docker-in-Docker image, Ollama) by digest, in `onyx.lock`'s style, with a `check` script. Onyx's own images are not in it: the Docker sidecar pulls them, by the digests `context/onyx.lock` already pins, exactly as on the desktop | One lock per puller: the kubelet's images in `deploy/images.lock`, the managed stack's in `context/onyx.lock` |
| **K12** | Does the pod get a language model (for labelling the map and the decisions pass)? | **Decided: no model in v1.** The map, the code index, the docs index and every query work; only labelling suggested links and the decisions pass (with its adjudication) need a model, and they stay off; doctor says so | The owner's answer. Sindri's model port reaches only the owner's own agent CLIs on the same machine (`JudgeModelPort`), and the image ships none and holds no login. The pod's map therefore carries records and links but no model-made labels. A model provider for the pod is its own later spec (K-I1) |

## 2. Part A: the image and the pod

### A.1 The Dockerfile

At the repo root, multi-stage, every `FROM` by digest (from `deploy/images.lock`):

1. **build** (`mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:…`, the SDK `global.json` pins): restore with the locked
   restore, `dotnet publish src/Sindri/Aw.Sindri.Host -c Release --no-self-contained -p:AwRecipesDir=<recipes/sindri>`, the same
   flags `scripts/sindri-app.sh` uses; `judge` and `aw-packets` NativeAOT for the target platform (`AW_SINDRI_JUDGE`,
   `AW_PACKETS_BIN` point at them); the shipped recipes' evals run (`recipe eval --shipped`) and fail the build as they fail
   `sindri-app`. `sindri-ui` is not built: the image has no desktop.
2. **tools**: `temporal` from `binaries.lock` (download, sha256 check, as `scripts/binaries-lock.sh fetch temporal` does), and
   graphify at the code index's pin in a venv (`uv`, as `scripts/install-deps.sh` does).
3. **runtime** (`mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:…`): `git`, `bubblewrap`, `tini` as PID 1 (it reaps the
   detached Temporal process), `procps` (Sindri's `DevServerProcesses` runs `ps` and `kill` for the Temporal dev server on Linux
   too), the `docker` CLI and its Compose plugin (at least 2.24, `OnyxPreflight.ComposeFloor`; copied from a pinned image in
   `deploy/images.lock`, no daemon), the three stages' outputs under `/opt/sindri`, a non-root user (uid 10001),
   `ENTRYPOINT ["tini","--","/opt/sindri/start.sh"]`, where `start.sh` copies the temporal binary to where Sindri looks for it
   (`$AW_STATE_DIR/cache/bin/temporal-<ver>/temporal`, with no override) whenever that versioned directory is missing (a new
   volume, or a new temporal pin after an upgrade) and then `exec`s `/opt/sindri/sindri serve`. Every variable a `kubectl exec`
   needs is image `ENV`, not set by `start.sh` (an exec doesn't run it): `AW_STATE_DIR=/data/state`,
   `AW_SINDRI_HOME=/data/state/sindri-app`, `HOME=/data/home`, `DOCKER_CONFIG=/data/home/.docker`, `AW_SINDRI_DEPLOY=pod`; the
   chart adds `DOCKER_HOST` and the rest. Read-only root filesystem; `/data` and `/tmp` writable; the git, `uv`, graphify and
   docker CLI caches live under `/data` (a read-only root has no other writable home). The image's user is uid 10001; the chart
   runs it as uid 1000 (§A.3 says why).

`serve` runs in the foreground, ignores SIGHUP and stops cleanly on SIGTERM (it already does: `ServeCommand.cs`), stopping its
Temporal dev server within 10 s. On the desktop `serve`'s stop also runs `compose stop` on Onyx (`OnyxServeHooks.Stop`, a 60 s
budget); in pod mode it skips that hook, so a Sindri restart leaves Onyx running in the Docker sidecar, and when the whole pod
stops the sidecar stops last (a native sidecar) and its daemon stops Onyx's containers. The pod's grace period is 120 s, enough
for Sindri's 10 s and the daemon's stop of Onyx (Postgres among it).

### A.2 What the image needs from Sindri (code changes)

| # | Change | Where | Why |
|---|---|---|---|
| A2a | A **pod mode**, `AW_SINDRI_DEPLOY=pod` (unset: today's desktop behaviour, byte for byte) | `SettingsFromEnvironment`, `Profile` (`deploy:` read-only echo for doctor) | one switch that the changes below key on; a desktop install can't enter it by accident because nothing sets it |
| A2b | **No new host variable for the lease.** The lease records `Environment.MachineName` (`PosixProcessProbe`), which in a StatefulSet pod is already its stable name (`sindri-0`); a restarted pod then has the same host; on the same node `LeaseDecision.Liveness` proves the old holder dead by its process start time (container pids repeat and the boot id is the node's), and on another node by the boot id. A force-deleted pod or a node that comes back with the same boot id can read as provably dead while the old process still holds the volume, so deploy.md says never to force-delete `sindri-0`, and the heavy restart test (§7) covers a normal rollout and a force delete. The test is the proof; only if it shows a 60 s wait does a builder add a pod-mode host override | `Infrastructure/Lease` (test only) | removes a setting the code didn't need |
| A2c | Doctor rows for pod mode: the volume writable, owner-only and under 70 % full, the temporal binary present, `bwrap` usable (user namespaces), Ollama answering, the Docker sidecar answering on its socket (rootless, its volume under 70 % full), Onyx reachable, the business-context switch (a warn row while it is on, K5), the served repos (K5), the model-less steps listed as off (K12), the map's lag behind each repo's main branch. On a fresh install a row whose thing isn't set up yet (Onyx before `onyx up`, no repos, no map yet) reads "not set up" with the next step, never fail; rows are ordered as the owner sets things up (volume, Docker, repos, map, Onyx, door), and each warn or fail names its fix | `Features/Doctor` | the owner's first check after install, through `kubectl exec … sindri doctor` |
| A2d | **Pod-mode child environment**: one environment factory that every `ProcessStartInfo` in `src/Sindri` goes through (today about 20 files spawn processes and inherit the whole environment, among them `ProcessToolRunner`, `Workflow/GitProcess`, `DevServerProcesses` for the detached Temporal server, `GitChangeSource` and `ProcessClaudeCli`); in pod mode it passes only an allowlist (`PATH`, `HOME`, `LANG`, `DOCKER_HOST` for the docker CLI's calls, the one credential a call needs), and an architecture test fails on any `new ProcessStartInfo` outside it | `Infrastructure/Processes` plus each spawner | the env-pointer secrets of K9 must not reach every child; `DOCKER_HOST` must reach the docker CLI or managed mode can't find the sidecar |
| A2e | **Pod-mode managed Onyx**, four small changes. (1) **Memory:** the memory row compares Onyx's need (`onyx.lock`'s `docker-memory-bytes`) with the Docker sidecar's memory limit, which the chart passes as `AW_SINDRI_DOCKER_MEMORY_BYTES` (the downward API's `resourceFieldRef` with the sidecar's `containerName`), taking the smaller of that and `docker info`'s `MemTotal` (which inside the sidecar reports the node's memory, not the container's limit). (2) **Disk:** `DockerRootDir` is a path inside the sidecar, so today's check falls back to measuring Sindri's `/data`; in pod mode the disk row compares `onyx.lock`'s `disk-bytes` with the sidecar volume's size, passed as `AW_SINDRI_DOCKER_DISK_BYTES` (the chart writes the claim's size), and doctor's 70 % row sums `docker system df`. (3) **Stop:** `serve`'s stop skips `OnyxServeHooks.Stop` (§A.1). (4) **Fix texts:** the preflight's pod-mode fixes name the chart's values (`docker.resources.limits.memory`, `docker.storage`), not Docker Desktop. Outside pod mode the variables are ignored and nothing changes. The rest of managed mode works as it is (checked in code): the endpoint is a `unix://` socket, which preflight's local-endpoint rule passes (`OnyxPreflight.IsLocalEndpoint`); the port probe runs on the pod's loopback, which the sidecar's published ports share; the browser opener finds no `xdg-open` and prints the address; and `onyx up` under `kubectl exec -it` has a terminal on all three streams, so its prompts and protected-verb check work | `Core/Onyx/OnyxPreflight`, `Infrastructure/Onyx`, `Host/Commands/OnyxServeHooks` | without (1) the preflight passes on a node with 64 GiB while the sidecar's limit kills Onyx's search index; without (2) it measures the wrong volume; without (3) every Sindri restart stops Onyx |

If the cluster forbids user namespaces, `bwrap` can't start and the code graph can't build (`SND-CODEIDX-007`); the chart then sets
`index.graph.enabled: false` and doctor says so. The text and symbol index still work. Under a container runtime's default seccomp
profile `bwrap` usually cannot create a user namespace, so Sindri's container (which keeps the default profile: only the Docker
sidecar is privileged) will run with the graph off; the kind run records which it is, and `docs/sindri/deploy.md` names the one
seccomp option an admin may choose.

### A.3 The Docker sidecar (Onyx's runtime)

| Aspect | Rule |
|---|---|
| Image | The rootless Docker-in-Docker image (`docker:<ver>-dind-rootless`), by digest from `deploy/images.lock`. Its daemon runs as uid 1000 inside a user namespace: a process that escapes an Onyx container is uid 1000 in the sidecar, not root. The chart sets `runAsUser: 1000` and `runAsNonRoot: true` explicitly rather than trusting the image's `USER`, and K-S0 lists the image's setuid binaries (any setuid-root binary other than the `newuidmap`/`newgidmap` pair rootless mode needs is a finding) |
| Command | Pinned daemon arguments, not the image's default entrypoint flags: `dockerd-entrypoint.sh dockerd --host=unix:///run/docker/docker.sock` with `DOCKER_TLS_CERTDIR=""`, so the daemon opens **no TCP listener** (the image's default adds 2375 or 2376 on every address, which with no NetworkPolicy any pod could reach). rootlesskit's `--disable-host-loopback` stays on (`DOCKERD_ROOTLESS_ROOTLESSKIT_DISABLE_HOST_LOOPBACK=true`, the default, set explicitly), which is what keeps Onyx's containers off the pod's loopback |
| Start order | A Kubernetes native sidecar (an init container with `restartPolicy: Always`, Kubernetes 1.29 or later) with a `startupProbe` of `docker info` on its socket: Kubernetes starts Sindri's container only after that probe passes, so `serve`'s one Onyx ensure at start finds the daemon answering; the sidecar stops after Sindri's container |
| Privilege | `privileged: true` and nothing else: no host paths, no host network or PID namespace, no node socket, `automountServiceAccountToken: false` on the pod. The rootless daemon needs the flag only to turn off the seccomp, AppArmor and `/proc` masks its own user-namespace sandbox requires; what the flag exposes anyway is in K1. With the values switch `docker.runtimeClassName: sysbox-runc` (a cluster with Sysbox installed) the flag is off; v1's kind run covers only the rootless path. The namespace carries the Pod Security label `privileged` and holds nothing else (K1; the chart's NOTES say so, and `helm install` into a namespace without the label fails with a Kubernetes admission message, so the chart ships a pre-install check that names the missing label); Fargate can't schedule it. Managed mode's existing check of the rendered Compose (`ComposeHostAccess`: `privileged`, host network, host PID) still refuses any Onyx service that asks for the host, inside the sidecar as on the desktop |
| Socket | The daemon listens only on a unix socket in an `emptyDir` volume mounted into the sidecar and Sindri's container, and not into Ollama's; Sindri's container sets `DOCKER_HOST=unix:///run/docker/docker.sock`. Sindri's container runs as uid 1000, the rootless daemon's own user, so it opens the owner-only socket without a shared group (the image's default uid 10001 is overridden by the chart). No TCP listener (above): on TCP, the pod's shared loopback would let the Ollama sidecar drive Docker, and preflight refuses a `tcp://` endpoint as another machine (`SND-ONYX-010`) |
| Deploy files | Compose resolves a bind mount's source on the **daemon's** filesystem, and the pinned Compose bind-mounts `../data/nginx` (nginx's start script and templates) from the tree managed mode renders under Sindri's home (`OnyxPaths`: `$AW_SINDRI_HOME/onyx/deploy/…`). The sidecar therefore mounts that one subtree of Sindri's volume (`subPath: state/sindri-app/onyx`), **read-only, at the identical path**, and nothing else of `/data`. Without it nginx, Onyx's only published service, can't start. Same uid (1000) on both sides, so the owner-only modes managed mode writes stay readable to the daemon |
| Network | The sidecar shares the pod's network namespace. Onyx's Compose publishes its web port on `127.0.0.1:<port>` (managed mode's override, `OnyxBindings`, as on the desktop); the rootless daemon's port driver binds that port in the pod's namespace, so Sindri's container reaches Onyx at `http://127.0.0.1:<port>`, `OnyxSettings.CheckUrl`'s loopback rule passes unchanged, and `kubectl port-forward` reaches the same port for the owner's browser. Onyx's own containers live in the daemon's network namespace and, with host loopback disabled (above), can't reach the pod's loopback (the owner's door, Temporal, Ollama). They do reach the pod network for egress, so they can reach other cluster Services and, unless the node blocks it, the cloud metadata endpoint; deploy.md tells the deployer to set the instance metadata hop limit to 1 (IMDSv2). **Proven first, in K-S0 (§9):** the published port answers from Sindri's container and through a bare port-forward, and an Onyx container can't reach the pod's loopback; if the rootless port driver can't bind the pod's loopback, the fallback is publishing on the pod's address with the host check widened in pod mode only, and the spec is amended before K-C1 |
| Volume | Its own volume claim at the daemon's data root (images, containers, and Onyx's Compose volumes: Postgres, Redis, the search index, the file store, the model cache), default 60 Gi (`onyx.lock`'s `disk-bytes` is 11 GB plus room for documents and their index), owned by uid 1000, its size passed to Sindri as `AW_SINDRI_DOCKER_DISK_BYTES` (A2e). Snapshotted with Sindri's volume (K7) |
| Resources | Requests and limits from values; the memory limit at least `onyx.lock`'s `docker-memory-bytes` (6 GiB) and passed to Sindri as `AW_SINDRI_DOCKER_MEMORY_BYTES` (A2e) |
| Node prerequisites | Rootless Docker in a pod needs, from the node: user namespaces allowed, cgroup v2 with delegation (Onyx's Compose sets a memory limit on its search index, which a rootless daemon can enforce only with delegated cgroups), and a `vm.max_map_count` the search index accepts (a node-wide setting the rootless daemon can't change). K-S0 proves them on kind; the owner check proves them on the real cluster's node image; deploy.md lists them with the node settings that give them |
| Egress | The daemon pulls Onyx's images by digest from their registries (Docker Hub and the others `onyx.lock` names), and Onyx's connectors reach their services (the chat and ticket systems the owner connects). The deployer's egress rules, if any, must allow these; deploy.md lists the registry hosts |

## 3. Part B: the network (api lane builds, context reviews)

Today (API spec §6): loopback only, `LoopbackGuard` (Host and Origin), one human token file per serve start, no other auth. The
owner's door keeps all of that. Pod mode adds one door.

### B.1 The owner's door: unchanged

- The loopback listener and its guard stay exactly as they are. `kubectl port-forward` reaches it as `localhost`, which the guard
  allows. `kubectl exec -it` gives the CLI a terminal and none of the agent signals, so it calls as the human at a terminal and the
  protected verbs (`profile approve`, `recipe approve-all`, `onyx up`) work as they do on a laptop; without `-t` it calls as a
  session and is refused (`SND-AUTH-002`), as it should be.
- The desktop app reaches it through `scripts/sindri-pod.sh connect` (Part C.3).

### B.2 The shared door (new)

| Aspect | Rule |
|---|---|
| Listener | In pod mode only. Today `Pipeline.Configure` builds one chain (`UseLoopbackGuard`, `UseMcpCaller`, `UseCallerContext`, `MapSindriEndpoints`, `MapSindriMcp`) for every listener, `LoopbackGuard` returns 400 for any non-loopback Host (it would fail the pod's own probes), a request with no caller headers is a tokenless `session`, and `CallerContext` trusts a client's `X-Sindri-Caller`; a second listener on the same application would therefore serve every owner route and `/sindri/mcp`. The shared door is **a second Kestrel endpoint of the same application** (one DI container: one lease holder, one set of SQLite writers, one projector; a second `WebApplication` would build a second container and duplicate them), listening on `AW_SINDRI_SHARED_LISTEN` (for example `0.0.0.0:7411`). Requests on that port are branched off **first**, before the loopback guard, into a terminal pipeline of their own (a `MapWhen` on the local port with its own routing and a 404 at its end; the owner's endpoints also require the loopback port, so they can't match on the shared one): Host allowlist, Origin refusal, the caller-header stripping `UseMcpCaller` already does, and an endpoint table of only `/sindri/context-mcp` and `GET /sindri/ping`; no `MapSindriEndpoints`, no client-asserted caller headers (the caller is always `session`/`shared`, built by the door itself). `GET /sindri/ping` returns a new small record, `{ epoch, lagSeconds }`, never the existing `PingView` with its pid; the chart's probes set a `Host` header from the allowed list. Every other path is 404, and the loopback application never serves `/sindri/context-mcp`. Tests: an architecture test lists every route mapped on the shared application and fails on any other, and an integration test shows every owner route and `/sindri/mcp` answering 404 on the shared port. `AW_SINDRI_SHARED_LISTEN` set without pod mode is refused at start (`SND-API-015`), so a desktop can't open the door by accident |
| Check order | Host (one of `AW_SINDRI_SHARED_HOSTS`, the Service names callers use), then Origin (refused, `SND-API-008`: no browser page may call it), then `X-Sindri-Repo` present (`SND-CONTEXT-021`, as today), then the repo is served (in the approved profile and `share.enabled`; `SND-CONTEXT-091`, new: "no such repo here"). The body lists nothing, so a caller can't list the repos, though it can confirm a name it guesses (091 against a served repo's answer); with K5 that is no secret worth more. A Host outside the list is 400 `SND-API-016` (new), whose text names the allowed hosts, since the commonest registration mistake is a Service name missing from `AW_SINDRI_SHARED_HOSTS`. Repo names compare exactly as the profile spells them |
| No authentication (K5) | There is no `Authorization` check, no token store and no minting. The Host allowlist and Origin refusal are not access control: they keep a browser page (DNS rebinding) and stray hostnames out, nothing more. Who may call is decided by who can reach the Service's port: per K3, any pod in the cluster. `deploy.md` states this and the consequence (K5) in its first section |
| Tools | `context_query`, `context_lenses`, `context_node`, `context_neighbors`, `context_sources` (all `ReadOnlyHint`); the same implementations as `/sindri/mcp`, so their answers, limits and goldens are the same, **through a separate five-row `SharedToolTable`**; the shared door has its own goldens, which differ from `/sindri/mcp`'s only in the three ways named here (the business-context filter, the refused arguments, and no model steps). `McpToolTable` is one static list of 12 tools including `sindri_request`, and `ReadOnlyHint` is only a hint, so `tools/list` and `tools/call` on the shared door use only the five-row table and reject any other name as unknown (a test: calling `sindri_request` fails). `context_query`'s `item`, `symbol` and `diff` arguments are left out of the shared `tools/list` schema and refused on this route as unknown arguments (the existing `SND-CONTEXT-022`; they reach Sindri's work items and checkouts, not the map). **Business context (K5):** while the profile's `share.businessContext` is false (the default), the filter is an **allowlist by provenance**, not a list of kinds to drop: an answer keeps only what came from the named repo's own code and docs sources (its code index, its docs index, and map nodes whose source is one of those). Everything else, every Onyx and Linear hit and every record node from any other source (chat, tickets, and the documents, issues or projects of any other connector), is dropped from all five tools, and a decision that quotes such a record has its quote text withheld (the map's record titles carry `source: linear` and the like, so filtering only `context_query`'s Onyx hits would leak them). A golden test holds one record of every `RecordKind` and every source kind and shows only the repo's code and docs surviving. An answer that dropped something says so in a note ("business context withheld on this door"), and answers carry the map's existing state notes (building, stale), so a caller can tell withheld, empty, still indexing and stale apart. `context_sources` shows each source's name, kind and state only: no URL, no paths, no token pointers, no instance build or port, no other repos' names, and lists only the repo's own code and docs sources unless business context is on |
| Caller | Every call is a `session` caller with a new `ContextClient` member `shared` (JSON `shared`; `FencePolicies.For` gives it the same rules as `mcp`: repo-scoped, labels fenced `<untrusted source="map">`, never a command). Calls are counted per repo and per refusal code and exported as metrics (`sindri_shared_calls_total{repo,tool,outcome}`, `sindri_shared_inflight`), and each refusal is one JSON log line with the code, the repo and the source address; a read writes no ledger row and there is no audit of what was read (no identity to record, K5). Metrics go nowhere unless the deployer sets an OpenTelemetry endpoint in the values |
| Repo | `X-Sindri-Repo`, set by the internal MCP server per registered entry, as `sindri mcp register` sets it locally; a call without it is `SND-CONTEXT-021`, as today |
| Limits | 16 calls in flight in total (more wait, up to 32 waiting; beyond that 429 `SND-API-014` with `Retry-After`), a per-call deadline of 30 s, and Kestrel request-size and header limits on this listener; the tools' own budgets unchanged. With no caller identity the limits are global: one noisy caller slows the others, which `deploy.md` names; the refusal log's source address is the one way to find it. While K12 is "no model", the model-backed parts of `context_query` (its rewrite and adjudication steps) are skipped on this route, so a query never starts a model call in the pod. Queries take priority over index builds for CPU. **Transport:** plain HTTP inside the cluster; TLS terminates at the internal MCP server or a mesh (deployer's choice, stated in deploy.md). **Onyx down:** the existing `SND-CONTEXT-010` (degraded answer, not an error); `/sindri/ping` reports only `{ epoch, lagSeconds }`. **Restarts:** one pod means the door is down while Sindri restarts (an upgrade, a node drain); callers see connection refused, and the internal MCP server reports the tool unavailable. The kind run records the restart-to-first-answer time, with a target under 2 minutes; readiness passes as soon as `serve` answers and is not tied to the map's lag (a stale map still answers, with its stale note) |

The doctor rule that `sindri` is never registered user-wide stays for laptops: the shared door is a different server name
(`sindri-context`) on the internal MCP server, and the doctor check only reads the local agent configs.

The new codes (`SND-CONTEXT-091`, `SND-API-014`, `SND-API-015`, `SND-API-016`) and their fix texts are in §8. Revision 1's `SND-AUTH-010` and `SND-AUTH-011` are dropped with the tokens.

### B.3 Onyx on the pod's loopback

Nothing changes in the query adapter. Onyx answers on `http://127.0.0.1:<port>` through the Docker sidecar (§A.3), so
`OnyxSettings.CheckUrl`'s loopback rule, the managed profile section `onyx up` writes, its five requests, its pinned version
check and its token pointer all work as on the desktop. Revision 1's `network: cluster` setting, `AW_SINDRI_ONYX_HOSTS` and the
`sindri onyx attach` verb are not built. If Onyx is unreachable, `context_query` answers with its code and docs hits and a note
that Onyx is unavailable (the existing partial-answer behaviour), doctor goes warn, and nothing else stops.

## 4. Part C: running it (context lane)

### C.1 The chart

`deploy/helm/sindri/` (new, ours), one release: a StatefulSet of one with three containers (Sindri, the Docker sidecar of §A.3,
the Ollama sidecar, which the chart pins to `OLLAMA_HOST=127.0.0.1:11434` so it never listens on the pod's address), two volume claims (`/data`, default 50 Gi; the Docker sidecar's, default 60 Gi), the images by digest, the
env from a Secret (`env:` pointers), one Service (the shared door's port; the owner's port, pinned to `AW_SINDRI_PORT` in the
chart, which `sindri-pod.sh connect` forwards, has no Service, because it binds loopback only and `kubectl port-forward` goes to
the pod directly; the same holds for Onyx's web port), a VolumeSnapshot schedule for both claims together (K7), `terminationGracePeriodSeconds: 120` (§A.1), a
PodDisruptionBudget, the containers' resource requests and limits from values, and the probes: a startup probe (generous, for the
first volume setup), readiness and liveness probes on the shared door's `GET /sindri/ping` (with a `Host` header from the allowed
list). Logs are JSON lines on stderr (`AW_LOG_LEVEL`), read with `kubectl logs`; `docs/sindri/deploy.md` lists the commonest
failure signatures (volume full, Temporal not up, Docker sidecar not up, Onyx unreachable, clone refused) and the doctor row that
names each. Sindri's and Ollama's security contexts are non-root, read-only root, all capabilities dropped, no privilege
escalation, default seccomp; only the Docker sidecar differs (§A.3). Values hold every deployer-specific thing: registry,
namespace, repo list (name, clone URL, token secret key), the shared hosts, storage class, resources, the Sysbox switch,
`onyx.enabled` (K1's way out) and `share.enabled`. The chart ships a `values.schema.json`, an example values file, and a
`helm lint` plus `helm template` test in CI (rendered against the example, checked for the security contexts of §A.3 and §C.1).
The Kubernetes Secret is mounted into Sindri's container only.

Onyx needs no chart and no values file: `sindri onyx up` fetches the Compose files at the pin, renders them with managed mode's
override and starts them in the sidecar, as on the desktop. Its admin sign-up page is published on the pod's loopback only, so
no other pod in the cluster can reach it; inside the pod, Sindri and the Ollama sidecar could, which is why Onyx is set up by the
owner's own `onyx up` (it doesn't exist before that command), right after install. The kind run records the
whole footprint (CPU, memory, disk of the pod's three containers and Onyx's stack) in `docs/sindri/deploy.md` so the owner can
size the real cluster.

### C.2 The repo service (engine lane)

`repo-pull` (a Sindri service, on by default in pod mode and opt-in elsewhere like `context-docs-refresh`, every 1 minute; at most one refresh per repo in flight, moves of main while one runs are coalesced into one follow-up, and the index, map and docs refresh keep a minimum interval of 5 minutes per repo, so a busy main doesn't queue endless rebuilds on the one writer):
- for each profile repo with a `pull:` block (`{ url, credential: env:NAME }`): the first time, `git clone --no-hardlinks` into
  `/data/repos/<name>` (the profile's `path` must be under `AW_SINDRI_REPOS_DIR` in pod mode); after that `git fetch origin
  <default branch>` and `git merge --ff-only`; a dirty tree or a non-fast-forward is a doctor row and a needs-you row, never a
  reset; the credential is passed through `GIT_ASKPASS` reading the pointer, never in a URL or an argument, and the helper answers only for the host of that repo's URL; the repo `<name>` must match `[a-z0-9][a-z0-9._-]*` and resolve under `AW_SINDRI_REPOS_DIR`; the URL must be https (v1; an ssh key would need a secret and a `known_hosts` file, a later addition) and its host in the profile's allowed git hosts (the chart's values), `--` precedes it on every git call, and git runs with `protocol.file.allow=never`, `http.followRedirects=false`, hooks off (`core.hooksPath=/dev/null`) and submodules off; the credential should be read-only (doctor can't check that; deploy.md says so);
- `code-index-main-move` then sees HEAD move and indexes, and the map and docs refresh follow, as on a laptop. The freshness number is the map's lag behind main, shown in doctor and as `lagSeconds` in the shared `GET /sindri/ping` response, and exported as a metric (telemetry export is on in pod mode, off by default elsewhere); a lag over 15 minutes is a doctor warning.

`sindri repo add` in pod mode takes `--url` and the credential pointer instead of an existing path, and writes the `pull:` block.

### C.3 The owner's steps

| Step | How | One click? |
|---|---|---|
| Install | create a namespace of its own labelled with Pod Security level `privileged` (the Docker sidecar, K1; skip the label with `onyx.enabled: false`), then `helm install` the one release with the deployer's values; the pre-install check names a missing label | one command each |
| Open the app on the pod | `scripts/sindri-pod.sh connect [--context C] [--namespace N]`: `kubectl port-forward` to the pod's loopback port, `kubectl exec` to read a fresh human token **and the pod's current pid and lease epoch** (the app accepts a `control.json` only when its `Pid` and `Epoch` match `/sindri/ping`), a local mirror home (`control.json` with the forwarded loopback URL, pid, epoch, the token file, all 0600) and `sindri-ui` started on it. The app needs no change: it still reads a loopback URL and a token from a home. When the port-forward drops or the pod restarts (a new epoch and a new human token), the script retries and rewrites the mirror home, and the app shows its existing "control plane not running" state meanwhile, while the script prints "pod restarting, reconnecting (attempt n of 30)" and gives up after 5 minutes with exit 1. The mirror home is removed on exit, Ctrl-C included (a trap; a crash leaves it under `$TMPDIR`, 0700, and the next run removes stale ones). Prerequisites: `kubectl` with exec and port-forward rights on the namespace, and `sindri-ui` installed (`sindri-app`); `connect --check` reports each one. Each failure has one line and exit 2: kubectl missing or no rights (names the verb refused), the pod not Ready (names its phase), the local port taken (names it and `--port`), `sindri-ui` not found (names `sindri-app`). It prints "connected: <pod> at <url>; Ctrl-C to stop" on success | one command |
| Approve the profile, recipes | in the app (its existing typed confirmations), or `kubectl exec -it sindri-0 -c sindri -- sindri profile approve` | one click and the typed word, as today |
| Onyx | `sindri-pod.sh connect --onyx` forwards Onyx's web port from the pod (the same port on the owner's machine) and runs `kubectl exec -it sindri-0 -c sindri -- sindri onyx up` at the same terminal: managed mode's preflight, fetch, pull, start and account steps, exactly as on the desktop; at the sign-up step it prints the forwarded address, the owner signs up in the browser and presses Enter. If `onyx up` takes a fallback port, the script re-forwards to it. The image pull is about 11 GB, so this step takes minutes; if the exec drops (a laptop sleeps), re-running `connect --onyx` runs `onyx up` again, which is safe to re-run as on the desktop and keeps the layers already pulled (K-S0 checks a resume after a dropped pull) | one command, one browser sign-up, one Enter |
| Connect sources | in Onyx's page (Linear, Slack, GitHub, docs), the connector checklist `onyx up` prints. These records reach the shared door only with `share.businessContext: true` (K5) | the owner's OAuth, in Onyx |
| Give everyone access | register the shared door in the internal MCP server, one entry per repo, no secret: for example `{ "name": "sindri-context", "url": "http://<service>.<ns>.svc:7411/sindri/context-mcp", "headers": { "X-Sindri-Repo": "<repo>" } }`. `sindri share entries --url <service-url>` (read-only, any caller; the URL is the deployer's, which the pod can't know) prints these entries as JSON for every served repo, exit 0. To check a registration: `curl -H 'Host: <service>' http://<service>:7411/sindri/ping` from any pod answers `{ epoch, lagSeconds }` | one paste |

### C.4 Network rules

Per K3 there is no NetworkPolicy on the namespace: any pod in the cluster may reach the shared door's Service. The owner's door,
Temporal, Ollama and Onyx's web port bind the pod's loopback, so they are reachable only from inside the pod and through
`kubectl port-forward` and `exec`; that, not a NetworkPolicy, is what keeps them the owner's. `kubectl exec` and `port-forward`
rights are the owner's power (they reach the human token, the ledger and Onyx's admin page), and so are volume-snapshot read rights
(they hold the ledger, the secrets directory and Onyx's database); deploy.md says to grant them to the owner only. A deployer who
wants egress rules lists the git and forge hosts, the registries of §A.3 and Onyx's connector services.

### C.5 Upgrading and getting back

`docs/sindri/deploy.md` gains: upgrading Sindri (a new image digest in the values, `helm upgrade`, the pod restarts and takes the
lease; Onyx's containers restart with the pod), upgrading Onyx (bump the pin in `context/onyx.lock`, `onyx-lock.sh verify`, a new
Sindri image that embeds it, then `sindri onyx up` in the pod, as on the desktop; the `/api/version` check in doctor), and
restoring (both volumes from the same nightly snapshot, so the map's Onyx cursor and Onyx's database match; the heavy test runs
this restore once).

## 5. Part D: the whole map on open, a query zooms in

As written in `2026-10-23-sindri-context-whole-map-design.md`: the page shows the whole map the moment it opens, a query zooms the
camera into it, `GET /sindri/context/map/whole` with a node and an area level. In the pod it is the owner's page (through the
connected app). It is not on the shared door in this spec: a whole map is too large for an agent's context, and the five tools
already zoom.

## 6. Security

- **Two doors, two rules.** The owner's door keeps today's local-only model; its protected verbs need a terminal or the app's
  typed confirmation. The shared door is read-only by construction (its own pipeline, one route, five allowlisted read-only
  tools, no command route mapped on that listener, proven by a route-list test), repo-scoped by header, fenced and limited, and
  **not authenticated** (K5).
- **What anyone in the cluster can read** (K3, K5): the map of every approved repo (labels, kinds, edges, the tools' cited code
  and docs snippets, all of which already pass the egress scrub). With `share.businessContext: true`, also every record Onyx's
  connectors pulled in. The owner turns that on only for a cluster whose every workload may read those records.
- **One privileged container.** The Docker sidecar (K1, §A.3) runs with `privileged: true` in a namespace of its own; its daemon
  and Onyx's containers run as uid 1000 in a user namespace, it mounts no host path (the flag still exposes the node's devices and
  an unmasked `/proc` and `/sys`, K1), it has no TCP listener, and its socket is shared only with Sindri's container.
  `onyx.enabled: false` removes it. Sysbox
  removes the flag where the cluster has it. Every other container is non-root, read-only, with all capabilities dropped.
- **Secrets** live in Kubernetes Secrets and reach Sindri as `env:` pointers; the image and the chart's values hold none; Onyx's
  own secrets and the Onyx token Sindri makes stay on the encrypted volumes, owner-only.
- **Supply chain.** Every kubelet image by digest in `deploy/images.lock`, every Onyx image by digest in `context/onyx.lock`, our
  own image built from the repo by CI, both locks checked in CI.
- **What listens on the pod's address** (reachable by any pod, K3): only the shared door. Temporal (`--ip 127.0.0.1`), Onyx's
  web port (`OnyxBindings`), the owner's door and Ollama (`OLLAMA_HOST`) bind loopback, and the Docker daemon has no TCP listener
  (§A.3); K-S0 and the kind run scan the pod's address from another namespace and find only the shared door's port.
- **The pod's loopback is shared by its three containers**, so the boundary inside the pod is the pod: the Ollama sidecar can
  reach the owner's door's tokenless session API (including the unfiltered `/sindri/mcp`), the Temporal dev server's
  unauthenticated frontend, and Onyx's web port, though not the Docker socket (a unix socket it doesn't mount). Accepted for v1:
  the sidecar is a pinned image that only serves embeddings. Alternative, noted: Ollama in its own pod, which Sindri's
  loopback-only Ollama rule forbids today.

## 7. Testing

- **Unit:** pod-mode settings (unset is the desktop behaviour: a golden of the settings and doctor output with the variable unset), the lease's stable host id, the shared listener's route, host,
  Origin and repo rules, the business-context allowlist on all five tools (one record of every kind and source), the global limits, the pod-mode memory, disk and stop changes (A2e),
  `repo-pull`'s decisions (clone, fast-forward, dirty, diverged).
- **Integration:** the shared door against a temp home with two repos, every refusal code, the five tools' answers equal to
  `/sindri/mcp`'s goldens with business context on and filtered with it off; a command route and a `sindri_*` tool call sent to
  the shared port are refused; `repo-pull` against a local bare repo; `repo-pull` refuses a `file://` URL, a bad name and an
  unlisted host.
- **Spike, before the chart (K-S0):** on kind with a pinned node image (Kubernetes 1.29 or later), a hand-written pod with the
  rootless sidecar (privileged, pinned arguments) and the Sindri image: the socket opens from Sindri's container as uid 1000;
  `onyx up` reaches a healthy stack, nginx included (the deploy-files mount); the published port answers on Sindri's
  `127.0.0.1` and through a bare port-forward; an Onyx container can't reach the pod's loopback; a scan of the pod's address from
  another namespace finds only the shared door's port; a dropped `onyx up` resumes; `bwrap` under the default seccomp profile
  (expected: graph off); the node prerequisites of §A.3; the image's setuid binaries. Its results go into deploy.md and, where
  one fails, into an amendment of this spec before K-C1.
- **Heavy (nightly, and by hand per PR):** the image built and run (`docker run` with a volume: serve up, Temporal up, doctor ok,
  a restart takes the lease at once; a restore of a snapshot); the chart on a local kind cluster (`deploy/test/kind-up.sh`):
  install, the Docker sidecar answers on its socket and not to the Ollama container, `sindri-pod.sh connect` opens the app,
  `connect --onyx` runs `onyx up` to a healthy stack, **Onyx's published port answers from Sindri's container on `127.0.0.1`
  and through a bare port-forward** (§A.3's open question), a call from another namespace's pod answers through the shared
  Service, business-context records are absent until the switch is on, the restart-to-first-answer time (target under 2 minutes)
  and the footprint are recorded, and a small soak (8 parallel callers, a commit to a repo every 30 s) passes with the freshness
  lag's p95 under 10 minutes and the `context_query` p95 under 5 s.
- **Owner check, once, on the real cluster:** the node prerequisites of §A.3 on its node image, install, connect, approve, `connect --onyx`, connect one source, register the
  shared door, query from an agent through the internal MCP server.

## 8. Errors and settings

| Code | Meaning | Fix text (short) | Phase |
|---|---|---|---|
| `SND-CONTEXT-091` | the shared door doesn't serve that repo (not in the approved profile, or the door is off) | "No such repo on this door. Ask the owner which repos it serves." | K-B1 |
| `SND-API-014` | too many calls in flight on the shared door (429, `Retry-After`) | "The door is busy. Wait the Retry-After seconds and retry." | K-B1 |
| `SND-API-015` | `AW_SINDRI_SHARED_LISTEN` set without pod mode (refused at start) | "The shared door runs only in pod mode: unset AW_SINDRI_SHARED_LISTEN or set AW_SINDRI_DEPLOY=pod." | K-B1 |
| `SND-API-016` | a Host the shared door doesn't serve (400) | "This door answers only for <hosts>. Use one of them, or ask the deployer to add yours to AW_SINDRI_SHARED_HOSTS." | K-B1 |
| `SND-REPO-001` | the first clone failed | names the git error and the URL's host | K-C2 |
| `SND-REPO-002` | the checkout has local changes | "Sindri never resets a repo; clean it by hand (kubectl exec) or remove and re-add it." | K-C2 |
| `SND-REPO-003` | main moved in a way that isn't a fast-forward | "Upstream was rewritten; re-add the repo to clone it fresh." | K-C2 |
| `SND-REPO-004` | the credential pointer is missing or empty | names the variable | K-C2 |
| `SND-REPO-005` | the URL, its host or the repo name is refused (not https, not an allowed host, a bad name) | names which rule | K-C2 |

Existing codes reused: `SND-CONTEXT-021` (no `X-Sindri-Repo`; its text names the header), `SND-CONTEXT-022` (an argument the
shared schema doesn't have), `SND-CONTEXT-010` (Onyx down, degraded answer), `SND-API-008` (Origin refused). Each new code gets
its fix text in `SindriErrors.cs` and a row in the API spec's §9. Part D owns `SND-CONTEXT-200..209`.

| Variable | Set by | Default | Meaning | Phase |
|---|---|---|---|---|
| `AW_SINDRI_DEPLOY` | the image (`ENV`) | unset (desktop) | `pod` turns pod mode on; every pod-mode behaviour keys on it | K-A1 |
| `AW_SINDRI_SHARED_LISTEN` | the chart | unset (no shared door) | the shared door's address; requires pod mode (`SND-API-015`) | K-B1 |
| `AW_SINDRI_SHARED_HOSTS` | the chart | none (every Host refused) | the Host names the shared door answers for | K-B1 |
| `AW_SINDRI_REPOS_DIR` | the chart | `/data/repos` in pod mode | where `repo-pull` clones | K-C2 |
| `AW_SINDRI_DOCKER_MEMORY_BYTES` | the chart (downward API) | unset (use `MemTotal`) | the Docker sidecar's memory limit | K-A2 |
| `AW_SINDRI_DOCKER_DISK_BYTES` | the chart (the claim's size) | unset (desktop disk rule) | the Docker sidecar volume's size | K-A2 |
| `DOCKER_HOST`, `DOCKER_CONFIG` | the chart, the image | unset | the sidecar's socket; the CLI's config under `/data` | K-A1, A2d |

All go in `.env.example`.

## 9. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Needs |
|---|---|---|---|
| **K-S0 spike** | context | the kind spike of §7: rootless Docker-in-Docker beside the Sindri image, the loopback publish, the deploy-files mount, the address scan, the node prerequisites; results into deploy.md, and an amendment here if one fails | K-A1's image (a local build) |
| **K-A1 image** | context | the Dockerfile (with the docker CLI and Compose plugin), the start script, `deploy/images.lock` (the kubelet's images, including the rootless Docker-in-Docker image and the CLI source; no Onyx rows) and its check, the heavy `docker run` proof, pod mode's switch (A2a) and the lease restart check (A2b, a test only) | nothing |
| **K-A2 pod-mode Onyx** | context | A2e (memory, disk, the skipped stop hook, the fix texts) and the Docker sidecar doctor row | K-A1 |
| **K-B1 shared door** | api | the second `WebApplication` and its pipeline, the five-row `SharedToolTable` route, the business-context allowlist and the `share.enabled` and `share.businessContext` profile keys (with the approve preview's plain-words lines), `sindri share entries`, the metrics and refusal log, the new codes | nothing (pod-mode switch stubbed until K-A1 merges: the listener reads one env var) |
| **K-C2 repo-pull** | engine | the service, `repo add --url`, its codes and doctor rows | A2d (its git calls take a credential, so they go through the allowlisted environment); the service and its tests start against the factory's interface before A2d merges |
| **K-C1 chart** | context | the Sindri chart with the Docker and Ollama sidecars, the deploy-files mount, the pre-install check, `values.schema.json`, the example values, the `helm lint`/`template` test, the snapshot schedule, the kind test | K-S0 (its results), K-A1; K-B1 and K-A2 for the full kind test |
| **K-C3 connect** | context | `scripts/sindri-pod.sh connect` (and `--onyx` running `onyx up`), its bash test with a fake `kubectl` | K-A1 for the heavy proof only |
| **A2d child environment** | context | the allowlisted child environment factory (with `DOCKER_HOST`) and its architecture test | nothing (K-C2 needs it) |
| **A2c doctor rows** | context | the pod-mode doctor rows | K-A1, K-B1 |
| **K-I1 ideas** | ideas | the follow-ups that don't block the first deploy: the freshness dashboard on the Health page, a model provider for the pod (K12's later spec), authentication for the shared door if the owner later wants it, TLS by mesh notes; each is written as a one-page spec, then taken as its own phase | nothing |
| **Part D W1–W4** | context, ui | as its own §9 | nothing (runs alongside everything) |
| **K-E2E** | context | the kind run of §7 end to end, the docs (`docs/sindri/deploy.md`) | everything above |

Every phase's gate also runs `scripts/check-env-example.sh` and `node scripts/check-deps.mjs` (§8's variables go in
`.env.example`, and a new edge goes in `deps.yaml`). Every phase writes its own part of `docs/sindri/deploy.md` (and its API §9
rows, the chart's NOTES, and the AGENTS.md command lines it adds) in the same PR; K-E2E only joins them up.

Parallel: at the start, five lanes are busy at once: context on K-A1, A2d and Part D W1 (K-S0 as soon as K-A1's image builds), api on K-B1, engine on K-C2, ui on Part D
W3 (against W1's goldens). Context takes K-A2 and K-C3 after K-A1, then K-C1; ui takes W4 after W3. K-E2E is last. The ideas lane
starts with its one-page specs at once, and after each of api, engine and ui finishes its one phase it takes that lane's next item
from K-I1, so no lane idles. Every phase is one PR with a scoped gate (build plus the unit and integration lanes of the touched
projects; `Aw.Sindri.Ui.Tests` in any phase that adds an API golden or a catalog row).

## 10. Left to the builders

The chart's exact resource numbers (measured on the kind run), the start script's details, the Docker-in-Docker image's version,
the docker CLI's source image, the snapshot class, and the kind cluster's version. Each is recorded in the code or
`docs/sindri/deploy.md` where it lands.

## 11. What revision 2 changed (the owner's answers, 2026-10-23)

| Answer | Change |
|---|---|
| K1/K2: Onyx's Compose setup, not the Helm chart | Onyx runs through managed mode in a rootless Docker-in-Docker sidecar (new §A.3, A2e); no Onyx chart, values file or Postgres operator; `network: cluster`, `AW_SINDRI_ONYX_HOSTS` and `sindri onyx attach` are dropped (B.3); two volumes snapshotted together (K7); the image gains the docker CLI and Compose |
| K3: any pod may reach the door | No NetworkPolicy (C.4); the loopback binding is what keeps the owner's ports private |
| K5: no auth on the shared door | No bearer tokens, token store or minting; `SND-AUTH-010/011`, `share-tokens.db` and `ErrorKind.Unauthenticated` dropped; limits are global; code and docs only by default, with the flagged `share.businessContext` switch (B.2, §6) |
| K12: no model in v1 | Decided; the steps that need one are named (K12) |
| Sandbox PRs #352, #355, #363, #366 | Not needed for this spec |

The open drafts' updates to match are listed in the PR body, not here.
