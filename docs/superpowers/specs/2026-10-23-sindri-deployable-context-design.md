# Sindri as a deployable unit: one image, Onyx's containers beside it in the same pod, the existing MCP port opened to the cluster

**Status:** design, revision 3 (the owner's answers q73 and q74 of 2026-10-23 applied on top of revision 2's; §11 lists what changed),
a draft until the owner confirms the two follow-ups in §1 · **Date:** 2026-10-23 · **Asked by:** the owner, 2026-10-23 (relayed by
sindri-meta): "make Sindri + Onyx a deployable unit for an EKS pod that stands up both. The owner port-forwards to it to set it up
(profile approval, Onyx admin, connectors). Then it's added to an internal MCP server so everyone can query a constantly updating
context retrieval map", refined the same night: "the deliverable is a Dockerfile that wraps Sindri", then (q73) "Sindri and Onyx are one
deployable unit; Onyx's prepackaged images run beside Sindri; nothing privileged, no Docker-in-Docker" and (q74) "no new shared door;
cluster agents use the existing `/sindri/mcp`". The full-map page asked for the same night is Part D of this spec
(`2026-10-23-sindri-context-whole-map-design.md`). · **Builds on:** the API surface spec
(`2026-10-10-sindri-api-surface-design.md` §6 local-only, §8 MCP, §11 deferred auth), the Onyx managed spec
(`2026-10-16-sindri-onyx-managed-design.md`), the context retrieval spec, `docs/csharp/sindri.md` (home layout, lease, serve),
`docs/csharp/forge.md` (credentials), `binaries.lock`, `context/onyx.lock` · **Lanes:** context leads; api reviews and builds the
MCP surface (§B); engine the repo freshness service (§C.2); ui Part D's page; ideas the follow-ups of §9 · **Next:** the
owner confirms the two flagged items of §1 (the work-queue reads and the business-context switch, both for the cluster);
then /specToProvenPR takes §9's phases to PRs.

**Keeping it app-agnostic.** Nothing here names a work app, company, cluster, registry or host. Every one of those is a value the
deployer gives (Helm values, the profile, Kubernetes secrets), and the examples use `example.com` and `registry.example.com`.

## 0. The short version

Today Sindri is a desktop program: it binds to loopback only, trusts whoever holds the token file in its home, runs one copy per
person, and manages Onyx with Docker Compose on the same machine. In a pod, most of that stays. This spec turns it into:

1. **One image** (`Dockerfile` at the repo root, built by CI from the repo): Sindri's control plane (as `sindri-app` publishes it),
   the pinned `temporal` binary, the code-index tools, `git`, and nothing secret. It runs `sindri serve` in the foreground with all
   state on one volume. It has no Docker CLI and no daemon.
2. **Onyx as sibling containers of the same pod, from one chart.** One container per service of Onyx's own Compose file at our pin
   (`context/onyx.lock`), each running Onyx's prepackaged image **by digest**. They talk to each other and to Sindri over the pod's
   loopback. **Nothing is privileged, nothing runs Docker.** The containers' part of the chart is **rendered by a script** from the
   pinned Compose file and `onyx.lock`, with a CI drift check, so an Onyx upgrade is: bump the lock, re-render, `helm upgrade`. Sindri
   treats this Onyx as an **unmanaged Onyx at a loopback URL**; one command, `sindri onyx attach`, does the account steps.
3. **One door, the one that exists**: `/sindri/mcp` on Sindri's existing API port. In the pod that port is also bound on the pod's
   address and a Service exposes it. A caller that does not come from loopback reaches **only** `/sindri/mcp` and a small `GET
   /sindri/ping`; it is a new, narrower kind of caller (`shared`) that sees only the five `context_*` tools. Everything else on the
   port stays loopback-only, and the owner (through `kubectl port-forward`, which arrives as loopback) keeps every right they have today.
4. **Code and docs only by default** for that caller: it is served the code and docs sources of the approved repos. Business context
   (Onyx's chat and tickets) and the work-queue read tools (`sindri_status` and its kin) are served only when the owner turns on a
   profile switch each, because with no authentication anything in the cluster could then read them (both flagged in §1).
5. **Always fresh**: a new Sindri service pulls each repo's default branch; the code index, the docs index and the map follow it
   as they already do; Onyx's connectors (set up once by the owner in Onyx's page) keep tickets and chat current; Sindri's existing
   sync and refresh services read them.
6. **The owner's steps** stay one command or one click each: `scripts/sindri-pod.sh connect` opens the app on the pod; approvals
   are the app's existing one-click-and-type confirmations (or `kubectl exec -it … sindri profile approve` at a terminal).
   `sindri-pod.sh connect --onyx` forwards Onyx's page for the one browser sign-up and runs `sindri onyx attach` in the pod.
7. **No model in v1.** Everything the map and the five tools do works without one; only labelling suggested links and the
   decisions pass need a model, and they stay off in the pod (K12).

The price of one unit: the pod holds all of Onyx plus Sindri (and the Ollama sidecar), so it must fit **one node**. Onyx's floor
alone is `onyx.lock`'s `docker-memory-bytes` (5.5 GiB of memory) and its images are about 11 GB to pull; the kind run
measures the whole pod (K-S0) and deploy.md says what node size that means.

## 1. Decisions

The owner answered K1, K2, K3, K5 and K12 on 2026-10-23 (revision 2) and q73 and q74 the same day (revision 3); those rows are
**decided**. Revision 3 removes revision 2's flagged privileged sidecar: **nothing in this design is privileged**. Two items are
**flagged** for the owner to confirm before this spec goes ready, both about what a caller from the cluster may read. The rest
keep their defaults, which builders follow if the owner says nothing. Until the owner confirms them, the phases that don't depend on
them may start (K-S0, K-A1, A2d, K-O1, K-O2, K-C2, K-C3, Part D, and K-B1 with both switches off, which is their default).

> **Two decisions for you, in plain words.**
>
> 1. **The work queue, for the cluster (new in revision 3).** `/sindri/mcp` also has six read-only tools about your own work
>    (`sindri_status`, `sindri_queue`, `sindri_item`, `sindri_item_timeline`, `sindri_pending_decisions`, `sindri_job`). Callers
>    from the cluster get **none of them** by default, and never `sindri_request` (it files work). Turn on
>    `share.workQueueReads` and every pod in the cluster can read your items, queue and pending decisions for the repo it names.
>    **Default:** off. **Alternative:** on.
> 2. **Chat and tickets for the cluster (K5).** With no sign-in, anything in the cluster can read what the door serves.
>    **Default:** code and docs only. **Alternative:** turn on `share.businessContext`, and every pod in the cluster can then read
>    every chat message and ticket Onyx has pulled in, including the private channels its connector can see. It is one switch for all
>    of Onyx's sources.
>
> **How they fit together:** with both off, the cluster sees exactly the code and docs maps of the repos you approve, and Onyx
> serves only your own queries in the app. **Either way:** approving a repo in the pod's profile publishes its code and docs to
> every pod in the cluster.

Defaults revision 3 took where the owner's answers left a choice (each is reversible without a redesign; the PR body repeats them):

| Default | Where | The other way |
|---|---|---|
| A caller from the cluster sees the 5 `context_*` tools; the 6 `sindri_*` reads are off (flagged); `sindri_request` is never offered | B.2 | `share.workQueueReads: true` adds the six; `sindri_request` stays refused (it files work, and a many-user door should not start work) |
| A non-loopback request to any other route is a bare 404, as an unknown route is | B.2 | a 403 with a code, if a deployer wants the refusal named |
| The shared client is named `shared`; it needs `share.enabled`, not `context.sessions.enabled` | B.2 | none needed |
| nginx listens on a high loopback port; Sindri talks to Onyx through nginx | A.3 | Sindri calling api_server directly (needs the Onyx adapter to drop the `/api` prefix); K-S0 decides |
| Onyx's pinned images are per-platform digests, chosen by `onyx.platform` (default `amd64`) | A.3 | an index digest row in `onyx.lock`, if K-O1 adds one |
| OpenSearch runs with `bootstrap.memory_lock=false` and bound to loopback | A.3 | none if K-S0 shows the bootstrap checks only warn |
| The chart ships an optional NetworkPolicy (`networkPolicy.enabled`, default off) that admits only the API port | C.4 | K3 stands: no policy unless the deployer asks |

| # | Question, in plain words | Answer | Why, and the cost |
|---|---|---|---|
| **K1** | How does Onyx come up beside Sindri? | **Decided (q73): as sibling containers of the same pod, from Onyx's prepackaged images by digest (`context/onyx.lock`), in one chart** (§A.3). Not Onyx's Helm chart, not Docker Compose in the pod. **Nothing is privileged; no Docker-in-Docker, no Docker CLI in the image** | The owner's answer. It removes revision 2's rootless Docker-in-Docker sidecar with its `privileged: true`, its namespace of its own and its Sysbox alternative. The cost: the chart carries a container per Onyx service, so it must stay in step with Onyx's Compose file; that is why the containers are **rendered by a script** from the pinned Compose file and the lock, with a CI drift check (K-O1). Pods also lose Docker's private network: the containers share the pod's loopback (§6) |
| **K2** | Onyx's database: inside the cluster, or a managed database? | **Decided by K1: inside the pod**, Onyx's own Postgres container (`relational_db`) on a volume claim | No operator, no cluster-wide resources, nothing for a cluster admin to install. The snapshot schedule (K7) covers this claim too |
| **K3** | Who can reach the door? | **Decided: any pod in the cluster.** No NetworkPolicy on the namespace by default | The owner's answer. The door is now `/sindri/mcp` and the ping, with only the `context_*` tools for a cluster caller; the rest of the port refuses a caller that isn't loopback, and the owner's rights (approvals, items, workflows) are reachable only through `kubectl port-forward` and `exec`, which arrive as loopback |
| **K4** | What can everyone's agents do through the door? | **Read the context map and nothing else**: `context_query`, `context_lenses`, `context_node`, `context_neighbors`, `context_sources`. The six `sindri_*` reads are off for them (flagged, `share.workQueueReads`); `sindri_request` never | Today's `/sindri/mcp` has 12 tools (`McpToolTable`), one of which files requests; a many-user door should not start work, and the read tools show the owner's queue. The table is chosen per caller (§B.2) |
| **K5** | What can a caller see? | **Decided: no authentication** (no bearer tokens, no token store, no minting). A caller names a repo with `X-Sindri-Repo` and reads any repo in the approved profile. **Code and docs sources only**, unless the profile's `share.businessContext: true` is set. **Flagged:** that switch | The owner's answer. The consequences, stated plainly: with no authentication, anything that can open a connection to the Service (any pod in the cluster, K3) reads every approved repo's map, code snippets and docs, so **approving a repo in the pod's profile publishes it to the whole cluster**; that approval is the only gate. With the switch on a caller also reads every record Onyx's connectors pulled in (chat messages, including the private channels Onyx's connector can see, and tickets), because Sindri reads Onyx as one user and Onyx's own per-source permissions don't reach a caller; the switch is all or nothing across Onyx's sources in v1. Nobody can be told apart or cut off one by one: the owner's kill switch is `share.enabled: false` in the profile (the port then refuses every non-loopback caller but the ping), or the chart's `service.enabled: false` (no Service). Both switches live in the profile, so changing them is a profile change the owner approves like any other, and the `profile approve` preview says in plain words "every pod in the cluster will be served these repos: …" and, with a switch on, "and every chat and ticket record Onyx holds" or "and your items, queue and pending decisions". Doctor lists the served repos and shows each switch as a warn row while it is on |
| **K6** | How does the pod get the code, and how fresh? | **A new Sindri service, `repo-pull`,** fast-forwards each repo's default branch every minute with a read-only token from a Kubernetes secret; the first clone happens once when a repo is added | Sindri never clones today and expects a checkout on disk. The common git-sync sidecar lays a repo out as linked worktrees, which Sindri resolves to a main checkout without a tree; a service of our own keeps the layout Sindri already checks (clean, HEAD in main) |
| **K7** | Where does the state live, and what is the backup? | **Two volume claims**: Sindri's (`/data`) and Onyx's (Postgres, the search index, the model caches; subPaths of one claim). **One copy of the pod** (a StatefulSet of one), **nightly snapshots of both claims together, 7 kept** (a CronJob in the chart with RBAC for `VolumeSnapshot` only, taking the two within seconds; or a `VolumeGroupSnapshot` where the cluster has it) | Sindri's ledger, views, map and Temporal's history are SQLite files that allow one writer; two replicas would fight over the lease. Snapshotting both claims at the same moment keeps the map's Onyx cursor and Onyx's database at the same point; Postgres recovers from a crash-consistent snapshot as from a power cut. Two snapshots seconds apart can leave the map's Onyx cursor a little ahead of Onyx's database; the sync service resyncs, as it does after any restore |
| **K8** | How does the owner see the app on the pod? | **`scripts/sindri-pod.sh connect`**: one command that port-forwards, fetches a fresh human token over `kubectl exec`, and opens the desktop app against the pod | Today the app only reaches a Sindri whose files it can read on the same machine. A browser version of the app is still later (UI spec §12.2) |
| **K9** | Where do secrets go? | **Kubernetes secrets as environment variables** (`env:` pointers): one read-only git token per git host; never in the image, the profile or the chart's values. Onyx's own secrets (its database password, its OpenSearch admin password, its auth secrets) are a second Secret the chart creates on first install (random, kept across upgrades), mounted into Onyx's containers only. In pod mode Sindri's child processes (git, tools) get only an allowlisted environment, not every secret (§A.2 A2d) | Secrets mounted as files are symlinks in Kubernetes, and Sindri's `file:` pointers refuse symlinks on purpose. The Onyx token Sindri makes at attach is written to Sindri's volume (`secrets/`, owner-only), as the managed mode does today when no keychain exists. Onyx's Compose file falls back to well-known passwords (`password`, `StrongPassword123!`) when nothing is set; the render script refuses to emit a container that would use a fallback |
| **K10** | Embeddings for the code index (local models only)? | **An Ollama sidecar** in the Sindri pod on `127.0.0.1:11434`, its model pulled once onto the volume and its digest checked against `context/code-index.lock` by doctor | Sindri allows only a loopback Ollama; a sidecar shares the pod's loopback, so no code change |
| **K11** | Images: built how, pinned how? | **Two locks, one per puller.** Our own image: built by CI from the repo for linux/amd64 and linux/arm64, pushed to a registry from config, referenced **by digest** in the chart. `deploy/images.lock` pins the base images and Ollama by digest, in `onyx.lock`'s style, with a `check` script. **Onyx's images are pulled by the kubelet now, by the digests `context/onyx.lock` already pins**, rendered into the chart (K-O1) | The kubelet's images in `deploy/images.lock` (no Docker CLI source, no Docker-in-Docker image any more), the Onyx stack's in `context/onyx.lock`, one lock per source of truth. `onyx.lock` records one digest per platform; a per-platform manifest digest pulls only on that platform, so the chart picks the row by `onyx.platform` |
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
   too), the three stages' outputs under `/opt/sindri`, a fixed non-root user, `ENTRYPOINT ["tini","--","/opt/sindri/start.sh"]`,
   where `start.sh` copies the temporal binary to where Sindri looks for it
   (`$AW_STATE_DIR/cache/bin/temporal-<ver>/temporal`, with no override) whenever that versioned directory is missing (a new
   volume, or a new temporal pin after an upgrade) and then `exec`s `/opt/sindri/sindri serve`. Every variable a `kubectl exec`
   needs is image `ENV`, not set by `start.sh` (an exec doesn't run it): `AW_STATE_DIR=/data/state`,
   `AW_SINDRI_HOME=/data/state/sindri-app`, `HOME=/data/home`, `AW_SINDRI_DEPLOY=pod`; the chart adds the rest. Read-only root
   filesystem; `/data` and `/tmp` writable; the git and `uv` and graphify caches live under `/data` (a read-only root has no other
   writable home). `/opt/sindri` is world-readable. The image has no Docker CLI, no Compose plugin and no daemon, and the user's
   uid is nothing special any more (revision 2 needed uid 1000 to match the rootless daemon).

`serve` runs in the foreground, ignores SIGHUP and stops cleanly on SIGTERM (it already does: `ServeCommand.cs`), stopping its
Temporal dev server within 10 s. `serve`'s Onyx hooks (`OnyxServeHooks`) act only on a Sindri-**managed** Onyx and "an unmanaged
Onyx is never touched", so in the pod, where Onyx is unmanaged (§B.3), a Sindri restart leaves Onyx's containers running: no code
change. When the whole pod stops, Onyx's containers stop with it, after Sindri's (native sidecars stop last, §A.3). The pod's
grace period is 120 s: Sindri's own stop is at most about 30 s (20 s for launched sessions, 10 s for Temporal); Postgres and
OpenSearch get the rest to flush; K-S0 times a pod delete with Postgres under write load.

### A.2 What the image needs from Sindri (code changes)

| # | Change | Where | Why |
|---|---|---|---|
| A2a | A **pod mode**, `AW_SINDRI_DEPLOY=pod` (unset: today's desktop behaviour, byte for byte; in pod mode telemetry export is on, to the OpenTelemetry endpoint the values give) | `SettingsFromEnvironment`, `Profile` (`deploy:` read-only echo for doctor) | one switch that the changes below key on; a desktop install can't enter it by accident because nothing sets it |
| A2b | **No new host variable for the lease.** The lease records `Environment.MachineName` (`PosixProcessProbe`), which in a StatefulSet pod is already its stable name (`sindri-0`); a restarted pod then has the same host; on the same node `LeaseDecision.Liveness` proves the old holder dead by its process start time (container pids repeat and the boot id is the node's), and on another node by the boot id. A force-deleted pod or a node that comes back with the same boot id can read as provably dead while the old process still holds the volume, so deploy.md says never to force-delete `sindri-0`, and the heavy restart test (§7) covers a normal rollout and a force delete. The test is the proof; only if it shows a 60 s wait does a builder add a pod-mode host override | `Infrastructure/Lease` (test only) | removes a setting the code didn't need |
| A2c | Doctor rows for pod mode: the volume writable, owner-only and under 70 % full, the temporal binary present, `bwrap` usable (user namespaces), Ollama answering, **Onyx's containers ready** (K-O2), **Onyx reachable** (its health route at the attached URL) and **the pin matches Onyx's `/api/version`** (the existing `OnyxSettings.CheckVersion`), each business-context and work-queue switch (a warn row while on, K5), the served repos (K5), the model-less steps listed as off (K12), the map's lag behind each repo's main branch. Sindri's pod has no Kubernetes service account (`automountServiceAccountToken: false`), so "containers ready" can't ask the API server: doctor opens each Onyx container's loopback port from a list the chart passes (`AW_SINDRI_ONYX_PORTS`, `name:port` pairs written by the same render that wrote the containers) and the row names the ones that don't answer. On a fresh install a row whose thing isn't set up yet (Onyx before `onyx attach`, no repos, no map yet) reads "not set up" with the next step, never fail; rows are ordered as the owner sets things up (volume, repos, map, Onyx, door), and each warn or fail names its fix | `Features/Doctor`, `Infrastructure/Onyx` | the owner's first check after install, through `kubectl exec … sindri doctor` |
| A2d | **Pod-mode child environment**: one environment factory that every `ProcessStartInfo` in `src/Sindri` goes through (today about 20 files spawn processes and inherit the whole environment, among them `ProcessToolRunner`, `Workflow/GitProcess`, `DevServerProcesses` for the detached Temporal server, `GitChangeSource` and `ProcessClaudeCli`); in pod mode it passes only an allowlist (`PATH`, `HOME`, `LANG`, the one credential a call needs), and an architecture test fails on any `new ProcessStartInfo` outside it | `Infrastructure/Processes` plus each spawner | the env-pointer secrets of K9 must not reach every child |
| A2e | **Onyx in pod mode is attached, never managed.** (1) `sindri onyx up` and `down` in pod mode refuse with a new `SND-ONYX-` code (K-O2 takes the next free number) whose fix text is "Onyx is part of this pod. Run `sindri onyx attach`." (2) `onyx attach [--url U]` (§B.3). (3) No preflight change: the preflight is managed mode's (Docker, Compose, memory and disk on the machine) and never runs for an unmanaged Onyx, which is why revision 2's memory, disk, stop-hook and fix-text changes are gone. The pod's memory and disk are the chart's requests, limits and claim sizes, measured in K-S0 | `Features/OnyxAttach`, `Host/Commands/OnyxVerbs` | without (1) a stray `onyx up` in the pod would look for Docker and fail with a Docker message |

If the cluster forbids user namespaces, `bwrap` can't start and the code graph can't build (`SND-CODEIDX-007`); the chart then sets
`index.graph.enabled: false` and doctor says so. The text and symbol index still work. Under a container runtime's default seccomp
profile `bwrap` usually cannot create a user namespace, so Sindri's container (which keeps the default profile; **no container in
the pod is privileged**) will run with the graph off; the kind run records which it is, and `docs/sindri/deploy.md` names the one
seccomp option an admin may choose.

### A.3 Onyx's containers in the pod

Onyx ships a Compose file, and its images are prepackaged. The pod runs **those images** as sibling containers, one per Compose
service, each by the digest `context/onyx.lock` pins (the lock has one `image` row per repository and platform). Nothing here runs
Docker, mounts a socket or asks for a privilege.

| Container (Compose service) | Image (`onyx.lock` row) | Listens on (pod loopback) | Role |
|---|---|---|---|
| `api_server` | `onyx-backend` | `127.0.0.1:8080` | Onyx's API; runs the database migration at start |
| `background` | `onyx-backend` | none | connectors and indexing workers |
| `web_server` | `onyx-web-server` | `127.0.0.1:3000` | Onyx's page |
| `inference_model_server` | `onyx-model-server` | `127.0.0.1:9000` | query embeddings |
| `indexing_model_server` | `onyx-model-server` (`INDEXING_ONLY=True`) | `127.0.0.1:9001` (see below) | indexing embeddings |
| `relational_db` | `postgres:15.2-alpine` | `127.0.0.1:5432` | Onyx's database |
| `opensearch` | `opensearch:3.6.0` | `127.0.0.1:9200` | the search index |
| `cache` | `redis:7.4-alpine` | `127.0.0.1:6379` | Onyx's cache (no persistence, as in the Compose file) |
| `nginx` | `nginx:1.25.5-alpine` | `127.0.0.1:<onyx.port>` (see below) | the one front door Sindri and the owner's browser use |

`minio` (the `s3-filestore` profile) and `code-interpreter` are **not** run: Sindri's use of Onyx needs neither, and the
code-interpreter's Compose service is the one that asks for `user: root` and carries a commented-out `privileged: true`. The chart's
`onyx.s3Filestore.enabled` and `onyx.codeInterpreter.enabled` default to false and K-O1 renders them only if asked.

| Aspect | Rule |
|---|---|
| Images | Each container's `image` is `repository@sha256:<digest>` from `onyx.lock`, picked by `onyx.platform` (`amd64` by default; a pod on arm64 nodes sets `arm64`), because the lock records a manifest digest per platform and a per-platform manifest pulls only there. The render script fails if a service of the pinned Compose file has no `image` row for the platform, and `scripts/onyx-lock.sh verify` (maintainer-run) keeps meaning what it means today |
| Rendering (K-O1) | `scripts/onyx-pod.mjs` reads `context/onyx.lock`, a committed copy of the pinned Compose file (`deploy/onyx/docker-compose.yml`, its sha256 checked against the lock's `file docker_compose/docker-compose.yml` row) and a small override file (`deploy/onyx/pod-overrides.yaml`, the table below as data), and writes `deploy/helm/sindri/templates/onyx-containers.yaml` plus the port list for doctor (`AW_SINDRI_ONYX_PORTS`). `--check` re-renders to memory and fails on any difference (CI, and a bash test in AGENTS.md's list); it also fails when an `environment:` line of the Compose file is not either carried over, overridden or listed as dropped, so a new Onyx setting can't slip in unnoticed. **An Onyx upgrade is:** bump `context/onyx.lock` (`onyx-lock.sh verify`), refresh the committed Compose copy, run the script, read the diff, `helm upgrade` |
| Reaching each other | Compose reaches services by name on a private network; in a pod they share one network namespace, so the script sets each host variable the Compose file reads to `127.0.0.1`: `POSTGRES_HOST`, `OPENSEARCH_HOST`, `REDIS_HOST`, `MODEL_SERVER_HOST`, `INDEXING_MODEL_SERVER_HOST`, `API_SERVER_HOST` (all six appear in the pinned Compose file as `${NAME:-service}` defaults), and the web server's `INTERNAL_URL` to `http://127.0.0.1:8080` (its Compose default is `http://api_server:8080`). nginx's own upstream names (`api_server`, `web_server`) live in its templates, not in an environment variable; **K-S0 confirms** how the templates take another host (an `envsubst` value in `run-nginx.sh`, or a `hostAliases` entry that maps `api_server` and `web_server` to `127.0.0.1`, which needs no template change) |
| Port clashes | Both model servers listen on 9000: the Compose file gives them separate containers and its health checks both call `http://localhost:9000/api/health`. In a pod one needs another port: the indexing server gets 9001 (the script rewrites its health probe and the clients' port, `INDEXING_MODEL_SERVER_PORT`; **not confirmed: the variable's spelling, and whether the server's own listen port is configurable by environment (the Compose file sets neither)**; K-S0 reads the image, and if the port is fixed in the image's command, the script overrides the container's `args`). Every other port above is distinct (8080, 3000, 5432, 9200, 6379) |
| nginx | The Compose file publishes nginx's port 80 as `${HOST_PORT:-3000}:80` and `${HOST_PORT_80:-80}:80`, and its health check calls `127.0.0.1:80`. In a pod there is no publishing, nginx listens in the pod, and port 80 needs root. The default is a **high port on loopback** (`onyx.port`, default 8081): **K-S0 decides** among (a) a listen-port override in nginx's template, bound to `127.0.0.1` (the script then also rewrites the health probe); (b) the pod sysctl `net.ipv4.ip_unprivileged_port_start=0` (a safe sysctl, but pod-wide); (c) Sindri calling `api_server` directly on `127.0.0.1:8080`, which drops nginx from Sindri's path but not from the owner's browser, and would need the Onyx adapter to stop adding the `/api` prefix nginx strips (a code change in `Aw.Context.Infrastructure`). The spec's default is (a). Sindri reaches `http://127.0.0.1:<onyx.port>`, and so does the owner through `kubectl port-forward` |
| Nothing on the pod's address | In Compose these services sit on a private network. In a pod they would listen on the pod's address, reachable by any pod (K3). So **every Onyx listener is set to loopback**, and K-S0 proves it with `ss -ltn` in each container and a scan of the pod's address from another namespace, which must find only the API port (§6): Postgres `-c listen_addresses=127.0.0.1` (appended to the Compose command `-c max_connections=250`); Redis `--bind 127.0.0.1` (appended to the Compose command); OpenSearch `network.host=127.0.0.1` (and `http.host`, `transport.host`); the API server's `uvicorn … --host 127.0.0.1` (the Compose command has `--host 0.0.0.0`; the script rewrites it); the web server `HOSTNAME=127.0.0.1` (a Next.js setting; **unverified for this image**); the model servers' `--host`; nginx's listen address. A setting an image does not honour is a K-S0 finding, answered with a `NetworkPolicy` (C.4) or a different image setting, and the spec is amended before K-O1 |
| OpenSearch | The Compose file sets `bootstrap.memory_lock=true` and `memlock` and `nofile` ulimits. A pod's container cannot set ulimits (the runtime's defaults apply), so the script sets `bootstrap.memory_lock=false`, keeps `discovery.type=single-node`, `OPENSEARCH_JAVA_OPTS=-Xms2g -Xmx2g` and the admin password from the Secret. **To be verified in K-S0:** with the node bound to loopback only, OpenSearch is in development mode, so the `vm.max_map_count` and file-descriptor bootstrap checks only **warn**; if the pinned image still refuses to start, the fallback is an init container that raises `vm.max_map_count` (which is privileged, so it would break this spec's rule) or a node setting the deployer makes (a node-wide `vm.max_map_count` of 262144, which deploy.md lists). K-S0 records which, and the warning's effect on the index's speed |
| Security contexts | **No container is privileged**, `allowPrivilegeEscalation: false` everywhere, `automountServiceAccountToken: false` on the pod, all capabilities dropped except where an entrypoint needs a few. The namespace carries Pod Security level **`baseline`** (or none): `baseline` forbids privileged containers, host namespaces and host paths, and allows images that start as root with the runtime's default capabilities. **Which containers can run non-root is unverified and K-S0 records it per container**; the expectation, to be confirmed: `postgres`, `redis`, `nginx` and `opensearch` entrypoints start as root and drop to their own user (Postgres needs `CHOWN`, `SETUID` and `SETGID` for its data directory unless the volume is already owned by its uid and the pod sets `fsGroup`); the Onyx images may or may not already run as a non-root user. `restricted` is the goal wherever an image allows it (non-root, no added capabilities, `RuntimeDefault` seccomp), the chart sets those fields per container from values, and the namespace label is whichever level the weakest container needs. deploy.md says which containers are the reason it is `baseline` |
| Start order | Compose uses `depends_on`. The script turns the dependency order into Kubernetes native sidecars (init containers with `restartPolicy: Always`, Kubernetes 1.29 or later), each with a startup probe from its Compose health check: `relational_db`, `cache`, `opensearch`, `inference_model_server`, `indexing_model_server` start in that order and the pod's main containers (Sindri, `api_server`, `background`, `web_server`, `nginx`, Ollama) start after all of them pass. `api_server` runs `alembic upgrade head` first and restarts under Kubernetes' own restart policy if the database is late; nginx's start script waits for the API server, as the Compose file's comment says. The sidecars stop after the main containers |
| Volumes | Onyx's named volumes become **one claim** (`onyx-data`, default 60 Gi) with a `subPath` per Compose volume: `db_volume` (Postgres; PGDATA is a subdirectory so the filesystem's `lost+found` doesn't break `initdb`), `opensearch-data`, `model_cache_huggingface` and `indexing_huggingface_model_cache` (the model servers' caches; the Compose file says they only save download time). The log volumes (`api_server_logs`, `background_logs`, `*_model_server_logs`) become `emptyDir`s (logs also go to stdout); `minio_data` and `file-system` aren't used; Redis keeps its Compose `tmpfs`. The pod sets `fsGroup` and `fsGroupChangePolicy: OnRootMismatch` (no recursive chown of the claim on every mount). nginx's `../data/nginx` bind mount is a ConfigMap generated by the script from the files whose sha256 `onyx.lock` pins (`file data/nginx/…` rows). The claim is snapshotted with Sindri's (K7) |
| Secrets | A Secret the chart creates on first install (random values, kept across `helm upgrade` by a `lookup`) holds `POSTGRES_PASSWORD`, `OPENSEARCH_ADMIN_PASSWORD` and Onyx's auth secrets, and is mounted into Onyx's containers only, never into Sindri's or Ollama's. Because all the containers share the pod's loopback these are not a boundary against another container of the pod (§6); they stop the well-known Compose defaults from being the real passwords |
| Resources | Requests and limits per container from values. The sum of the memory limits is at least `onyx.lock`'s `docker-memory-bytes` (5.5 GiB) for Onyx, with OpenSearch's limit at least twice its heap (4 GiB). **The pod's footprint is all of Onyx plus Sindri plus Ollama, and must fit one node**; K-S0 and the kind run record the measured CPU, memory and disk, and deploy.md turns them into a node size |
| Egress | The kubelet pulls Onyx's images (about 11 GB across the pod; the registries `onyx.lock` names), so the pod's first start takes minutes. At run time the pod needs the git hosts, Onyx's connectors' services (the chat and ticket systems the owner connects), and, if the model servers fetch their model weights at start (the Compose file's cache volumes suggest they may; **K-S0 records it**), the model hub's host. The deployer's egress rules, if any, must allow these; deploy.md lists the hosts. Onyx's web connector can fetch cluster-internal addresses too, **and now also the pod's own loopback** (§6), so the connector checklist says to give it only public URLs |
| Not run in the pod | `onyx up`, `onyx down`, `docker`, `docker compose`: Sindri's managed mode (preflight, fetch, render, `OnyxServeHooks`) is for a laptop, and an unmanaged Onyx is never touched by `serve` (§A.1) |

## 3. Part B: the network (api lane builds, context reviews)

Today (API spec §6): loopback only, `LoopbackGuard` (it checks the **Host** header and any browser **Origin**, not the connection's
address), one human token file per serve start, no other auth, and a request with no caller headers is a tokenless `session`.
Pod mode opens exactly one thing: the MCP route to callers that are not on loopback.

### B.1 The owner's rights: unchanged

- The API listener's loopback behaviour and its guard rules stay exactly as they are for a loopback caller. `kubectl port-forward`
  reaches the pod's loopback (the container runtime connects from inside the pod's network namespace), so the owner arrives as
  loopback with `Host: localhost`, which the guard allows. `kubectl exec -it` gives the CLI a terminal and none of the agent
  signals, so it calls as the human at a terminal and the protected verbs (`profile approve`, `recipe approve-all`, `onyx attach`)
  work as they do on a laptop; without `-t` it calls as a session and is refused (`SND-AUTH-002`), as it should be.
- The desktop app reaches it through `scripts/sindri-pod.sh connect` (Part C.3).

### B.2 The existing MCP port, opened to the cluster

There is no second listener, no second application and no new route. Today `Pipeline.Configure` builds the one chain
(`UseLoopbackGuard`, `UseApiVersionHeader`, `UseApiProblems`, `UseMcpCaller`, `UseCallerContext`, `UseReadYourWrites`,
`UseCommandOriginRefusal`, `UseValueObjectValidation`, `MapSindriEndpoints`, `MapSindriMcp`) and `MapSindriMcp` maps `/sindri/mcp`
with `McpRouteMetadata`. Because `WebApplication` runs endpoint routing before user middleware (`UseMcpCaller` already relies on
this: it reads `context.GetEndpoint()`), the guard knows which endpoint a request matched, including case and trailing-slash
variants, before it decides.

| Aspect | Rule |
|---|---|
| Listener | In pod mode only. `ServePort.Listen` adds `Listen(IPAddress.Loopback, port)` today (`ServePort.cs:35`); in pod mode it adds one listener on the same port for every address instead (Kestrel can't hold the loopback and the any-address listener on one port; the pod's IPv6-only clusters use the IPv6 any-address in dual mode). `ServeListenerCheck` still checks that `ServePort.Listen` ran. `control.json` still records the loopback URL. A `Service` (`service.enabled`, default true) exposes the API port (`AW_SINDRI_PORT`, default 7410); the owner's `connect` forwards the same port to the pod. The doc line "the only listener is 127.0.0.1" and any test or banned symbol that forbids a non-loopback listen are amended to name this one pod-mode exception. Outside pod mode nothing changes |
| What "loopback" means | The connection's remote address (IPv4-mapped IPv6 unwrapped) is `127.0.0.0/8` or `::1`. Not the Host header. Everything the owner does arrives from loopback (port-forward, `exec`, the app through the forward). So does **every other container in the pod** (§6). A call to the pod's own address from inside the pod, or through the Service, is not loopback |
| `LoopbackGuard` | **Loopback remote: unchanged** (a non-loopback Host is 400, a non-loopback Origin is 403). **Any other remote:** it reaches only `/sindri/mcp` (every method and the variants that route) and `GET /sindri/ping`, decided from the endpoint already chosen: its metadata is `McpRouteMetadata`, or a new `SharedPingRouteMetadata` on the ping. Every other request, including no endpoint at all, gets a bare **404** and goes no further (the same answer an unknown path gets, so the route list isn't disclosed). Then, for the two it admits: Host must be one of `AW_SINDRI_MCP_HOSTS` (the Service names callers use; the port is stripped before the comparison; the chart's default is the Service's short, namespaced and cluster-local names), else 400 `SND-API-016` whose text names the allowed hosts (accepted under K5: they are cluster Service names, no secret); any `Origin` is refused, 403 `SND-API-008` (no browser page may call it; `UseMcpCaller` already refuses every Origin on `/sindri/mcp`, loopback ones included). **Test:** an integration test walks every endpoint in the `EndpointDataSource` (every route pattern with its parameters filled in, every method its `HttpMethodMetadata` lists, plus case, trailing-slash and encoded variants) with a **non-loopback remote** and expects the 404 for all but those two. It also walks them with a loopback remote and expects today's behaviour. A new route therefore can't become reachable from the cluster by accident |
| `GET /sindri/ping` | Loopback remote: the existing `PingView` (pid and lease epoch, which `control.json` checks and `sindri-pod.sh connect` reads). Any other remote: a new small record, `{ epoch, lagSeconds }`, never the pid. The chart's readiness and liveness probes use it with a `Host` header from the list. It stays up while `share.enabled` is false (the probes need it) |
| Caller (`shared`) | A request the guard admitted from a non-loopback remote is the new client **`shared`**: `ClientLabel.Shared` (JSON `shared`), `ContextClient.Shared` (JSON `shared`), the actor `session:shared` that `CallerContext.ActorOf` builds, and the `shared` arm of `ContextScopes.Client(actor)` (which today maps an unknown label to `http`); `FencePolicies.For` gives it the same rules as `mcp` (repo-scoped, labels fenced `<untrusted source="map">`, never a command). `UseMcpCaller` already drops every `X-Sindri-*` header but `X-Sindri-Repo` (so `X-Sindri-Caller`, `-Terminal` and `-Token` are ignored) and sets `X-Sindri-Client: mcp`; for a non-loopback remote it sets `shared` instead, from the connection, never from a header. `CallerHeaderParsing` learns the label, and `CallerContextMiddleware` honours `shared` only on a request the MCP middleware labelled that way and reads it as no label anywhere else (a loopback caller writing `X-Sindri-Client: shared` on another route gets nothing: a test). So a `shared` caller is always a session, can never be a human, and cannot reach a command route (the guard has already refused every route but two) |
| Admission | `ScopeRules.Resolve` today refuses every session unless `context.sessions.enabled` (`SND-CONTEXT-020`) and gives a session only the repo it names. A `shared` caller is admitted by **`share.enabled`** instead, in the same place, with the same repo rule, so opening the door doesn't also open the owner's loopback sessions. The repo must be in the approved profile; one that isn't answers `SND-CONTEXT-091` (new: "no such repo here"; checked before the engine is called). The body lists nothing, so a caller can't list the repos, though it can confirm a name it guesses; with K5 that is no secret worth more. Repo names compare exactly as the profile spells them. An integration test runs the door against the profile exactly as the chart ships it |
| Tools | `McpToolTable` is one static 12-row list and `List` answers every tool "whatever the profile says" (context spec §8.6); `Call` already reads the caller from `ICallerContextAccessor`. Both now choose the rows by caller: for a loopback caller the 12 as today (goldens unchanged); for `shared` the five `context_*` tools (`context_query`, `context_lenses`, `context_node`, `context_neighbors`, `context_sources`; all `ReadOnlyHint`; the same implementations, so answers and limits are the same), plus the six `sindri_*` reads (`sindri_status`, `sindri_queue`, `sindri_item`, `sindri_item_timeline`, `sindri_pending_decisions`, `sindri_job`) **only when the profile's `share.workQueueReads` is true (flagged)**, and **never `sindri_request`**. A tool not offered answers as unknown (`SND-CONTEXT-023`, as an unknown name does today), not as a refusal that confirms it exists; a test calls `sindri_request` as `shared` with the key on. `context_query`'s `item`, `symbol` and `diff` arguments (they reach Sindri's work items and checkouts, not the map) are left out of the `shared` schema and refused as unknown arguments (`SND-CONTEXT-022`). The `shared` goldens differ from the loopback goldens in these ways only: the tool list, the business-context filter, the refused arguments, no model steps (K12), and `context_sources`' reduced fields |
| Business context (K5) | While `share.businessContext` is false (the default) a `shared` caller gets **code and docs only**, through the existing source-allowlist rule: an **allowlist by source** (`SourceId` and source kind; "provenance" already names who produced an edge), not a list of kinds to drop, and the Onyx query isn't made at all. An answer keeps only what came from the named repo's own code and docs sources (its code index, its docs index, and map nodes whose source is one of those). Everything else (every Onyx and Linear hit, every record node from any other source: chat, tickets, and the documents, issues or projects of any other connector) is dropped from all five tools, and a decision that quotes such a record has its quote text withheld. Edges, neighbours and counts that lead to a dropped node are dropped with it, and `context_node` on such a node's key, or on another repo's node, answers as not found rather than being filtered after the read. A golden test holds one node of every `NodeKind` and `RecordKind` from every source kind and shows only the repo's code and docs surviving. An answer that dropped something says so in a note ("business context withheld for this caller"), and answers carry the map's existing state notes (building, stale). `context_sources` shows each source's name, kind and state only: no URL, no paths, no token pointers, no instance build or port, no other repos' names, and only the repo's own code and docs sources unless the switch is on. **This default is the lead's reading of the owner's earlier answer and is flagged again in §1** |
| Limits | Counted only for non-loopback callers (the owner's loopback calls are never queued behind them): 16 calls in flight in total (more wait, up to 32 waiting; beyond that 429 `SND-API-014` with `Retry-After`), a per-call deadline of 30 s, and a 64 KiB request body cap set per request through the body-size feature. The tools' own budgets are unchanged. With no caller identity the limits are global: one noisy caller slows the others, which deploy.md names; the refusal log's source address is the one way to find it. **Not carried over from revision 2:** Kestrel limits of a separate listener (`MaxConcurrentConnections`, a smaller header limit, a body-rate floor): Kestrel's limits are per server, so changing them would also bind the owner's event stream and commands; Kestrel's defaults (30 s to send headers, a minimum request data rate) already apply to all and stay. While K12 is "no model", the model-backed parts of `context_query` (rewrite and adjudication) are skipped for `shared`, so a query never starts a model call in the pod. Queries take priority over index builds for CPU |
| Observability | Calls are counted per repo and per refusal code and exported as metrics (`sindri_shared_calls_total{repo,tool,outcome}`, `sindri_shared_inflight`; `repo` is a served repo's name or `other`, never the caller's text), and each refusal is one JSON log line with the code, the repo (truncated) and the source address, at most one line a second per code; a read writes no ledger row and there is no audit of what was read (no identity to record, K5). Metrics go nowhere unless the deployer sets an OpenTelemetry endpoint in the values |
| No authentication (K5) | There is no `Authorization` check, no token store and no minting. The Host allowlist and Origin refusal are not access control: they keep a browser page (DNS rebinding) and stray hostnames out, nothing more. Who may call is decided by who can reach the Service's port: per K3, any pod in the cluster. deploy.md states this and the consequence (K5) in its first section |
| Repo | `X-Sindri-Repo`, set by the internal MCP server per registered entry, as `sindri mcp register` sets it locally; a call without it is `SND-CONTEXT-021`, as today (`McpToolTable.Call` checks it) |
| Transport and restarts | Plain HTTP inside the cluster; TLS terminates at the internal MCP server or a mesh (deployer's choice, stated in deploy.md). **Onyx down:** the existing `SND-CONTEXT-010` (degraded answer, not an error). **Restarts:** one pod means the door is down while Sindri restarts (an upgrade, a node drain); callers see connection refused, and the internal MCP server reports the tool unavailable. The kind run records the restart-to-first-answer time, with a target under 2 minutes; readiness passes as soon as `serve` answers and is not tied to the map's lag (a stale map still answers, with its stale note) |

The doctor rule that `sindri` is never registered user-wide stays for laptops: the cluster's server is a different name
(`sindri-context`) on the internal MCP server, and the doctor check only reads the local agent configs.

The new codes (`SND-CONTEXT-091`, `SND-API-014`, `SND-API-016`) and their fix texts are in §8.

### B.3 Onyx on the pod's loopback: unmanaged, attached

Nothing changes in the query adapter. Onyx's `nginx` answers on `http://127.0.0.1:<onyx.port>` (§A.3), so `OnyxSettings.CheckUrl`'s
loopback rule (`localhost`, `127.*`, `[::1]`) passes as it is. The profile's section is the unmanaged shape, `context.sources.onyx`
with `url: http://127.0.0.1:<onyx.port>`, `token: <secret pointer>`, `version: v4.9.0` and `managed: false` (a managed one must be
exactly `http://127.0.0.1:<port>` and is started by `serve`; an unmanaged one is never touched, `OnyxServeHooks`). Revision 2
dropped `sindri onyx attach` with its `network: cluster` and `AW_SINDRI_ONYX_HOSTS`; revision 3 needs **the account stage only**,
and so the verb comes back, smaller:

- `sindri onyx attach [--url http://127.0.0.1:<port>]` (default from `AW_SINDRI_ONYX_URL`, which the chart sets). A **protected
  verb** like `onyx up`: a human at a terminal only (an agent session or a non-terminal gets `SND-AUTH-002`, exit 1). It checks that
  Onyx answers its health route and reports the release `context/onyx.lock` pins, then runs the same account stage as `onyx up`
  (the owner's sign-up in the browser, Sindri's dedicated user and token, the profile section above, ready for `profile approve`).
  It starts, pulls and writes nothing in Onyx's workspace, and is safe to re-run.
- It revives from the branch `feat/sindri-onyx-attach` (`cb225bf00`, a wip) **minus its parent commit `87028b190`** (the
  `network: cluster` setting, `OnyxClusterPolicy` and `AW_SINDRI_ONYX_HOSTS`): the service, the `IOnyxAccountStage` split of the
  account steps, the verb and its catalog row stay; the wip's refusal of a loopback address is reversed (attach is for the
  loopback Onyx here), and no network setting is read.
- If Onyx is unreachable, `context_query` answers with its code and docs hits and a note that Onyx is unavailable (the existing
  partial-answer behaviour), doctor goes warn, and nothing else stops.
## 4. Part C: running it (context lane)

### C.1 The chart

`deploy/helm/sindri/` (new, ours), one release: a StatefulSet of one whose pod holds Sindri, the Ollama sidecar (pinned to
`OLLAMA_HOST=127.0.0.1:11434` so it never listens on the pod's address) and **Onyx's containers (§A.3, rendered by
`scripts/onyx-pod.mjs` into one template file, never edited by hand)**; two volume claims (`/data`, default 50 Gi; `onyx-data`,
default 60 Gi); the images by digest; the env from a Secret (`env:` pointers) and Onyx's own Secret (§A.3); one Service for the API
port (`service.enabled`, default true; the owner's `connect` forwards the same port to the pod directly, and Onyx's port has no
Service, because it binds loopback only); a VolumeSnapshot schedule for both claims together (K7); `terminationGracePeriodSeconds:
120` (§A.1); a PodDisruptionBudget; the containers' resource requests and limits from values; and the probes: a startup probe
(generous, for the first volume setup and Onyx's migration), readiness and liveness probes on `GET /sindri/ping` (with a `Host`
header from the allowed list). Logs are JSON lines on stderr (`AW_LOG_LEVEL`), read with `kubectl logs -c <container>`;
`docs/sindri/deploy.md` lists the commonest failure signatures (volume full, Temporal not up, an Onyx container not ready, Onyx
unreachable, clone refused) and the doctor row that names each. Sindri's and Ollama's security contexts are non-root, read-only
root, all capabilities dropped, no privilege escalation, default seccomp; Onyx's containers get the strictest context their image
allows (§A.3, recorded per container by K-S0). **Nothing is privileged.** Values hold every deployer-specific thing: registry,
namespace, the git hosts and one Secret key per host (the repos themselves are added with `sindri repo add --url`, which writes
the profile, so they are defined once; a changed Secret needs a pod restart), the MCP hosts, storage class, resources,
`onyx.platform`, `onyx.port`, `onyx.enabled` (default `true`; `false` leaves every Onyx container, the `onyx-data` claim and its
Secret out, for a pod that serves code and docs only and is smaller; it is not a privilege switch any more) and `share.enabled`
(default `true`). The chart ships a `values.schema.json`, an example values file, and a `helm lint` plus `helm template` test in CI
(rendered against the example, checked for the security contexts of §A.3 and §C.1 and for "no `privileged`, no host path, no
Docker socket" anywhere). The Kubernetes Secret for git is mounted into Sindri's container only.

Onyx needs no values file of its own beyond `onyx.*` above: the script renders its containers from its pinned Compose file. Its
admin sign-up page is on the pod's loopback only, so no other pod in the cluster can reach it; inside the pod, Sindri, Ollama and
Onyx's other containers could, so the owner makes the admin account right after install, with `connect --onyx` (§C.3), before
connecting any source. The kind run records the whole footprint (CPU, memory, disk of every container) in
`docs/sindri/deploy.md` so the owner can size the real cluster's node.

### C.2 The repo service (engine lane)

`repo-pull` (a Sindri service, on by default in pod mode and opt-in elsewhere like `context-docs-refresh`, every 1 minute; at most one refresh per repo in flight, moves of main while one runs are coalesced into one follow-up, and the index, map and docs refresh keep a minimum interval of 5 minutes per repo, so a busy main doesn't queue endless rebuilds on the one writer):
- for each profile repo with a `pull:` block (`{ url, credential: env:NAME }`): the first time, `git clone --no-hardlinks` into
  `/data/repos/<name>` (the profile's `path` must be under `AW_SINDRI_REPOS_DIR` in pod mode); after that `git fetch origin
  <default branch>` and `git merge --ff-only`; a dirty tree or a non-fast-forward is a doctor row and a needs-you row, never a
  reset; the credential is passed through `GIT_ASKPASS` reading the pointer, never in a URL or an argument, and the helper answers only for the host of that repo's URL; the repo `<name>` must match `[a-z0-9][a-z0-9._-]*` and resolve under `AW_SINDRI_REPOS_DIR`; the URL must be https (v1; an ssh key would need a secret and a `known_hosts` file, a later addition) and its host in the profile's allowed git hosts (the chart's values), `--` precedes it on every git call, and git runs with `protocol.file.allow=never`, `http.followRedirects=false`, hooks off (`core.hooksPath=/dev/null`) and submodules off; the credential should be read-only (doctor can't check that; deploy.md says so);
- `code-index-main-move` then sees HEAD move and indexes, and the map and docs refresh follow, as on a laptop. The freshness number is the map's lag behind main, shown in doctor and as `lagSeconds` in the non-loopback `GET /sindri/ping` response, and exported as a metric (telemetry export is on in pod mode, off by default elsewhere); a lag over 15 minutes is a doctor warning.

v1 pulls and indexes each repo's default branch only: an agent working on a feature branch gets main's map, which deploy.md says.

`sindri repo add` in pod mode takes `--url` and the credential pointer instead of an existing path, and writes the `pull:` block.

### C.3 The owner's steps

| Step | How | One click? |
|---|---|---|
| Install | create a namespace (Pod Security level `baseline` or stricter, §A.3; the chart's NOTES name the level the rendered containers need), then `helm install` the one release with the deployer's values. The kubelet pulls about 11 GB of Onyx images, so the pod takes minutes to become Ready | one command each |
| Open the app on the pod | `scripts/sindri-pod.sh connect [--context C] [--namespace N]`: `kubectl port-forward` to the pod's loopback API port, `kubectl exec … sh -c` reading the pod's `control.json` and `auth/human.token` (no new verb; built in K-C3) for a fresh human token **and the pod's current pid and lease epoch** (the app accepts a `control.json` only when its `Pid` and `Epoch` match `/sindri/ping`, which on loopback still carries the pid), a local mirror home (`control.json` with the forwarded loopback URL, pid, epoch, the token file, all 0600) and `sindri-ui` started on it. The app needs no change: it still reads a loopback URL and a token from a home. When the port-forward drops or the pod restarts (a new epoch and a new human token), the script retries and rewrites the mirror home, and the app shows its existing "control plane not running" state meanwhile, while the script prints "pod restarting, reconnecting (attempt n of 30)" and gives up after 5 minutes with exit 1. The mirror home is removed on exit, Ctrl-C included (a trap; a crash leaves it under `$TMPDIR`, 0700, and the next run removes stale ones). Prerequisites: `kubectl` with exec and port-forward rights on the namespace, and `sindri-ui` installed (`sindri-app`); `connect --check` reports each one. Each failure has one line and exit 2: kubectl missing or no rights (names the verb refused), the pod not Ready (names its phase), the local port taken (names it and `--port`), `sindri-ui` not found (names `sindri-app`). It prints "connected: <pod> at <url>; Ctrl-C to stop" on success | one command |
| Approve the profile, recipes | in the app (its existing typed confirmations), or `kubectl exec -it sindri-0 -c sindri -- sindri profile approve` | one click and the typed word, as today |
| Onyx | `sindri-pod.sh connect --onyx` does everything `connect` does (the app opens too), forwards **Onyx's port** from the pod (the same port on the owner's machine), and runs `kubectl exec -it sindri-0 -c sindri -- sindri onyx attach` at the same terminal: it checks Onyx answers and runs the pinned release, then the account steps; at the sign-up step it prints the forwarded address, the owner signs up in the browser and presses Enter. Nothing is pulled or started by this command (the kubelet did that at install), so it takes seconds, and re-running it is safe (a dropped exec, a laptop that slept). Ctrl-C during `attach` stops that command only, and the app stays connected | one command, one browser sign-up, one Enter |
| Connect sources | in Onyx's page (Linear, Slack, GitHub, docs), the connector checklist `onyx attach` prints. These records reach the cluster only with `share.businessContext: true` (K5) | the owner's OAuth, in Onyx |
| Give everyone access | register the existing MCP port in the internal MCP server, one entry per repo, no secret: for example `{ "name": "sindri-context-<repo>", "url": "http://<service>.<ns>.svc:7410/sindri/mcp", "headers": { "X-Sindri-Repo": "<repo>" } }`. `sindri share entries --url <service-url>` (read-only, any caller; the URL is the deployer's, which the pod can't know) prints these entries as JSON for every served repo, exit 0 (with none served: `[]` and one line on stderr, "no repos served: approve repos, and check share.enabled"). One entry per served repo (named `sindri-context-<repo>`, so an agent sees each repo's tools once per repo). Each new repo is three owner steps (`repo add`, approve, paste its entry), since the header is fixed per entry. To check a registration: `curl -H 'Host: <service>' http://<service>:7410/sindri/ping` from any pod answers `{ epoch, lagSeconds }` | one paste |

The scripts' terminal output follows `NO_COLOR` and prints no spinner or colour when stdout is not a terminal; the bash tests
check both.

The first install, in order (times from the kind run go into deploy.md): create the namespace and `helm install` (minutes, mostly
the image pull); `connect` and approve the profile and recipes (one click each); `connect --onyx` for the admin account (seconds);
the first clone and index (minutes per repo; done when doctor's map row for the repo is ok and `/sindri/ping`'s `lagSeconds` is a
number); register the MCP port (one paste per repo); then, when wanted, Onyx's connectors.

### C.4 Network rules

Per K3 there is no NetworkPolicy on the namespace by default: any pod in the cluster may reach the API port's Service, and the
port itself decides what a non-loopback caller may do (§B.2). The owner's routes, Temporal, Ollama and every Onyx container
listen on the pod's loopback only, so they are reachable from inside the pod and through `kubectl port-forward` and `exec`; the
listener settings of §A.3 and the guard of §B.2, not a NetworkPolicy, keep them the owner's. `kubectl exec` and `port-forward`
rights are the owner's power (they reach the human token, the ledger and Onyx's admin page), and so are volume-snapshot read rights
(they hold the ledger, the secrets directory and Onyx's database); deploy.md says to grant them to the owner only. For a CNI that
enforces policies the chart can add an optional NetworkPolicy (`networkPolicy.enabled`, default off) that admits ingress to the API
port only, a second line of defence behind the listener settings if an Onyx image ignores one. A deployer who wants egress rules
lists the git and forge hosts, Onyx's registries and connector services, and the model hub (§A.3).

### C.5 Upgrading and getting back

`docs/sindri/deploy.md` gains: upgrading Sindri (a new image digest in the values, `helm upgrade`, the pod restarts and takes the
lease; Onyx's containers restart with the pod, so Onyx answers degraded for a few minutes; the only downgrade is the previous
digest plus, if state moved, a snapshot restore), upgrading Onyx (**bump the pin in `context/onyx.lock`, `onyx-lock.sh verify`,
refresh `deploy/onyx/docker-compose.yml`, run `node scripts/onyx-pod.mjs`, read the diff, build a new Sindri image that embeds
the new lock, `helm upgrade`, then `sindri onyx attach` again so the profile's pinned version follows**; doctor's `/api/version`
row shows a mismatch until it does; Onyx's database migrations run forward at `api_server` start, so the only way back is a
snapshot restore), and restoring (both claims from the same nightly snapshot, so the map's Onyx cursor and Onyx's database match;
the heavy test runs this restore once).

## 5. Part D: the whole map on open, a query zooms in

As written in `2026-10-23-sindri-context-whole-map-design.md`: the page shows the whole map the moment it opens, a query zooms the
camera into it, `GET /sindri/context/map/whole` with a node and an area level. In the pod it is the owner's page (through the
connected app). It is not served to the cluster in this spec (the guard of §B.2 gives a non-loopback caller 404 for it): a whole
map is too large for an agent's context, and the five tools already zoom.

## 6. Security

- **One port, two kinds of caller.** The owner's rights keep today's local-only model (loopback remote; protected verbs need a
  terminal or the app's typed confirmation). A caller that isn't loopback reaches two endpoints, decided by endpoint metadata and
  proven by a test that walks every endpoint; it is a `shared` session, repo-scoped by header, fenced, limited, and **not
  authenticated** (K5). `/sindri/mcp` for such a caller offers five read-only tools (eleven with the flagged work-queue switch),
  never `sindri_request`, and no command route is reachable.
- **What anyone in the cluster can read** (K3, K5): the map of every approved repo (labels, kinds, edges, the tools' cited code
  and docs snippets, all of which already pass the egress scrub). With `share.businessContext: true`, also every record Onyx's
  connectors pulled in; with `share.workQueueReads: true`, also the repo's items, queue and pending decisions. The owner turns each on
  only for a cluster whose every workload may read them.
- **Nothing is privileged**, there is no Docker daemon or socket, and no pod host path. The cost of dropping Docker's private
  network: **the pod's loopback is shared by every container in it**, so the boundary inside the pod is the pod. Ollama and every
  Onyx container (api_server, background workers, the model servers, the web server, Postgres, OpenSearch, Redis, nginx) can reach
  the owner's routes on `127.0.0.1` with no token, as a session: the unfiltered `/sindri/mcp` (all 12 tools, and `sindri_request`)
  and the session-level API, though a human-only action (approvals, protected verbs) still needs the per-start human token they
  cannot read; the Temporal dev server's unauthenticated frontend; and Onyx's database and search index. The sharpest case is
  Onyx's connectors (`background`) and web connector, which fetch addresses the owner gives and whose results Onyx stores: a
  connector pointed at `http://127.0.0.1:7410/…` would read Sindri's own API as a session. Revision 2 had Onyx behind a rootless
  daemon with the host loopback disabled; revision 3 gives that up with the owner's answer. **Accepted for v1** under the
  honest-mistake model (pinned prebuilt images, connectors chosen by the owner); mitigations: the connector checklist says to give
  the web connector only public URLs, the session reads Onyx's containers could make are the ones any agent session on a laptop can
  make, and a later spec could put Sindri's API behind a Unix socket or a token for sessions (K-I1). The alternative, noted: Onyx
  in its own pod, which `OnyxSettings.CheckUrl`'s loopback rule forbids today (it is the rule `network: cluster` would have bent).
- **What listens on the pod's address** (reachable by any pod, K3): only the API port. Every Onyx container, Temporal
  (`--ip 127.0.0.1`), Ollama (`OLLAMA_HOST`) and Sindri's own admin routes (refused by remote address) are loopback-only.
  Whether each Onyx image honours its loopback setting is **unverified until K-S0**, which runs `ss -ltn` in every container and
  scans the pod's address from another namespace, and must find only the API port.
- **Secrets** live in Kubernetes Secrets and reach Sindri as `env:` pointers; the image and the chart's values hold none; Onyx's
  secrets are a chart-made Secret mounted into Onyx's containers only; the Onyx token Sindri makes stays on the encrypted volume,
  owner-only.
- **Supply chain.** Every kubelet image by digest: our own, the base images and Ollama in `deploy/images.lock`, Onyx's in
  `context/onyx.lock`; our own image built from the repo by CI, both locks checked in CI, and the rendered Onyx template checked
  against them by `onyx-pod.mjs --check`.

## 7. Testing

- **Unit:** pod-mode settings (unset is the desktop behaviour: a golden of the settings, doctor output and `serve`'s start log with the variable unset), the lease's stable host id, `LoopbackGuard`'s decision table (remote loopback or not, endpoint metadata, Host, Origin), the caller label (`shared` honoured only where the MCP middleware set it), the per-caller tool rows, the business-context allowlist on all five tools (one record of every kind and source), the limits, `repo-pull`'s decisions (clone, fast-forward, dirty, diverged), `onyx-pod.mjs` (its override table, the refusal of a fallback password, the per-platform digest choice).
- **Integration:** **the endpoint walk of §B.2** (every endpoint, a non-loopback remote, expects 404 for all but `/sindri/mcp` and the ping; a loopback remote expects today's answers); `/sindri/mcp` as a `shared` caller against a temp home with two repos, every refusal code, the five tools' answers equal to the loopback goldens except the differences §B.2 names, with business context on and filtered with it off; `sindri_request` and (with the key off) the six reads answer as unknown; a `sindri_*` tool call and a command route sent as `shared` are refused; `onyx attach` against a fake Onyx (healthy, wrong version, unreachable, refused for a session); `onyx up` refused in pod mode; `repo-pull` against a local bare repo; `repo-pull` refuses a `file://` URL, a bad name and an unlisted host.
- **Spike, before the chart (K-S0):** on kind with a pinned node image (Kubernetes 1.29 or later), a hand-written pod with Sindri's image and the Onyx containers at the lock's digests, no privilege anywhere. It records, per item: that every Onyx container reaches the others on `127.0.0.1` with the host variables of §A.3 set; how nginx's templates take the upstream hosts and how its listen port is moved (the three options of §A.3); how the indexing model server's port is moved (the variable's real spelling, or `args`); that OpenSearch bound to loopback starts with `bootstrap.memory_lock=false`, its bootstrap checks only warn, and what the warning costs; **which containers run non-root, with which capabilities, and the Pod Security level that results**; the output of `ss -ltn` in every container and a scan of the pod's address from another namespace, which must find only the API port; Onyx's pages and `/api/version` answering from Sindri's container at `http://127.0.0.1:<onyx.port>` and through a bare port-forward; `sindri onyx attach` end to end; `bwrap` under the default seccomp profile (expected: graph off); whether the model servers download weights at start; the pod's measured footprint; a pod delete with Postgres under write load. Its results go into deploy.md and, where one fails, into an amendment of this spec before K-O1 and K-C1.
- **Heavy (nightly, and by hand per PR):** the image built and run (`docker run` with a volume: serve up, Temporal up, doctor ok, a restart takes the lease at once; a restore of a snapshot); the chart on a local kind cluster (`deploy/test/kind-up.sh`): install, every container ready, `onyx-pod.mjs --check` clean, `sindri-pod.sh connect` opens the app, `connect --onyx` runs `attach` to a healthy state, **Onyx's port answers from Sindri's container on `127.0.0.1` and through a bare port-forward**, a call from another namespace's pod answers through the Service on `/sindri/mcp` and every other path is 404, business-context records are absent until the switch is on, the restart-to-first-answer time (target under 2 minutes) and the footprint are recorded, and a small soak (8 parallel callers, a commit to a repo every 30 s) passes with the freshness lag's p95 under 10 minutes and the `context_query` p95 under 5 s (the p50s are recorded beside them).
- **Owner check, once, on the real cluster:** the namespace's Pod Security level and the node's settings (§A.3) on its node image, install, connect, approve, `connect --onyx`, connect one source, register the MCP port, query from an agent through the internal MCP server.

## 8. Errors and settings

| Code | Meaning | Fix text (short) | Phase |
|---|---|---|---|
| `SND-CONTEXT-091` | the door doesn't serve that repo (not in the approved profile, or `share.enabled` is false) | "No such repo here. Ask the owner which repos it serves." | K-B1 |
| `SND-API-014` | too many calls in flight from non-loopback callers (429, `Retry-After`) | "The server is busy. Wait the Retry-After seconds and retry." | K-B1 |
| `SND-API-016` | a Host a non-loopback caller may not use (400) | "This server answers cluster callers only for <hosts>. Use one of them, or ask the deployer to add yours to AW_SINDRI_MCP_HOSTS." | K-B1 |
| `SND-ONYX-<next>` | `onyx up` or `down` in pod mode | "Onyx is part of this pod. Run `sindri onyx attach`." | K-O2 |
| `SND-REPO-001` | the first clone failed | names the git error and the URL's host | K-C2 |
| `SND-REPO-002` | the checkout has local changes | "Sindri never resets a repo; clean it by hand (kubectl exec) or remove and re-add it." | K-C2 |
| `SND-REPO-003` | main moved in a way that isn't a fast-forward | "Upstream was rewritten; re-add the repo to clone it fresh." | K-C2 |
| `SND-REPO-004` | the credential pointer is missing or empty | names the variable | K-C2 |
| `SND-REPO-005` | the URL, its host or the repo name is refused (not https, not an allowed host, a bad name) | names which rule | K-C2 |

Existing codes reused: `SND-CONTEXT-021` (no `X-Sindri-Repo`; its text names the header), `SND-CONTEXT-022` (an argument the
`shared` schema doesn't have), `SND-CONTEXT-023` (an MCP tool not offered to this caller), `SND-CONTEXT-010` (Onyx down, degraded
answer), `SND-API-008` (Origin refused), `SND-AUTH-002` (a protected verb without a terminal). Revision 2's `SND-API-015`
(`AW_SINDRI_SHARED_LISTEN` without pod mode) is gone with that variable. Each new code gets its fix text in `SindriErrors.cs` and
a row in the API spec's §9. Part D owns `SND-CONTEXT-200..209`.

| Variable | Set by | Default | Meaning | Phase |
|---|---|---|---|---|
| `AW_SINDRI_DEPLOY` | the image (`ENV`) | unset (desktop) | `pod` turns pod mode on; every pod-mode behaviour keys on it | K-A1 |
| `AW_SINDRI_MCP_HOSTS` | the chart | none (every non-loopback Host refused) | the Host names a non-loopback caller may use on `/sindri/mcp` and the ping (`SND-API-016`) | K-B1 |
| `AW_SINDRI_REPOS_DIR` | the chart | `/data/repos` in pod mode | where `repo-pull` clones | K-C2 |
| `AW_SINDRI_ONYX_URL` | the chart | unset | the default URL of `sindri onyx attach` (`http://127.0.0.1:<onyx.port>`) | K-O2 |
| `AW_SINDRI_ONYX_PORTS` | the chart (written by `onyx-pod.mjs`) | unset | `name:port` pairs of Onyx's containers, which doctor opens | K-O2 |

Dropped from revision 2: `AW_SINDRI_SHARED_LISTEN`, `AW_SINDRI_SHARED_HOSTS` (renamed above), `AW_SINDRI_DOCKER_MEMORY_BYTES`,
`AW_SINDRI_DOCKER_DISK_BYTES`, `DOCKER_HOST` and `DOCKER_CONFIG`. All the above go in `.env.example`.

| Profile key | Default | Meaning | Phase |
|---|---|---|---|
| `share.enabled` | true in pod mode | a non-loopback caller is served the approved repos (admits the `shared` client) | K-B1 |
| `share.businessContext` | false | such a caller also gets Onyx's records (K5, flagged) | K-B1 |
| `share.workQueueReads` | false | such a caller also gets the six read-only `sindri_*` tools (flagged; `sindri_request` is never offered) | K-B1 |
| `repos[].pull` | none | `{ url, credential: env:NAME }`, written by `repo add --url` | K-C2 |
| `repo-pull.allowedHosts` | none | the git hosts `repo-pull` may clone from | K-C2 |
| `index.graph.enabled` | true | false where `bwrap` can't start (§A.2) | existing |
| `deploy` | read-only echo | pod or desktop, for doctor | K-A1 |
| `context.sources.onyx` | written by `onyx attach` | `{ url: http://127.0.0.1:<port>, token, version, managed: false }`, an existing key | existing |

Each phase adds its keys to `docs/csharp/sindri-profile.md` (a bash test checks every key is named there).

## 9. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Needs |
|---|---|---|---|
| **K-S0 spike** | context | the kind spike of §7: the sibling pod without privilege, loopback wiring, nginx's port, the model server's port, OpenSearch in development mode, non-root per container and the Pod Security level, the address scan finding only the API port, `attach` end to end, the footprint; plus `deploy/test/node-probe.sh`, a one-pod check of the node settings the owner runs on the real cluster right after; results into deploy.md, and an amendment here if one fails | K-A1's image (a local build) |
| **K-A1 image** | context | the Dockerfile (no Docker CLI), the CI workflow that builds it for both platforms and pushes it to the configured registry (the build stage needs clang and zlib for NativeAOT) and prints the digest for the values, the start script, `deploy/images.lock` (the kubelet's own images: base images and Ollama; no Docker or Onyx rows) and its check, the heavy `docker run` proof, pod mode's switch (A2a) and the lease restart check (A2b, a test only) | nothing |
| **K-O1 Onyx siblings** | context | `scripts/onyx-pod.mjs` and its override file, the committed Compose copy with its sha check, the generated `onyx-containers.yaml` of the chart (containers, native-sidecar order, volumes, ConfigMap for nginx's templates, the Secret), `--check` in CI and a bash test (added to AGENTS.md's list), the `helm template` checks that nothing is privileged | K-S0's results (it can start from the override table of §A.3 and be amended by them), K-A1 for the chart skeleton |
| **K-O2 onyx attach** | context | revive `sindri onyx attach` from `feat/sindri-onyx-attach` (`cb225bf00`) without `87028b190`, reverse its loopback refusal, `AW_SINDRI_ONYX_URL`, `onyx up`/`down` refused in pod mode, doctor's Onyx rows (containers ready, reachable, pin matches) and `AW_SINDRI_ONYX_PORTS`, the catalog row and `Aw.Sindri.Ui.Tests` golden | nothing for the code (the doctor rows' port list comes from K-O1) |
| **K-B1 open `/sindri/mcp` to the cluster** | api | pod mode's any-address listener (`ServePort`), `LoopbackGuard` by remote address and endpoint metadata, the endpoint-walk test, the reduced ping, the `shared` member of `ClientLabel` and `ContextClient` (in `Aw.Context.Core`, with `ContextScopes.Client`, `FencePolicies.For` and `CallerContext.ActorOf`), per-caller rows in `McpToolTable`, `ScopeRules` admission by `share.enabled`, the business-context allowlist and the three `share.*` profile keys (with the approve preview's plain-words lines), `sindri share entries`, the limits, metrics and refusal log, the new codes; `lagSeconds` read through a lag interface K-C2 provides (a stub until then); `docs/csharp/sindri-profile.md` for the new keys | nothing (pod-mode switch stubbed until K-A1 merges: the listener reads one env var) |
| **K-C2 repo-pull** | engine | the service, `repo add --url`, its codes and doctor rows, the lag interface and metric, the `pull:` profile keys in `docs/csharp/sindri-profile.md` | A2d (its git calls take a credential, so they go through the allowlisted environment); the service and its tests start against the factory's interface before A2d merges |
| **K-C1 chart** | context | the Sindri chart (Sindri and Ollama containers and their first pull, the Service, probes, claims, `values.schema.json`, the example values, the `helm lint`/`template` test, the snapshot schedule, the optional NetworkPolicy) around K-O1's rendered Onyx template, the kind test | K-S0 (its results), K-A1, K-O1; K-B1, K-O2, K-C2 and K-C3 for the full kind test |
| **K-C3 connect** | context | `scripts/sindri-pod.sh connect` (and `--onyx`, forwarding Onyx's port and running `onyx attach`), its bash test with a fake `kubectl` (added to AGENTS.md's test list) | K-A1 for the heavy proof only; K-O2 for the real `attach` |
| **A2d child environment** | context | the allowlisted child environment factory and its architecture test | nothing (K-C2 needs it) |
| **A2c doctor rows** | context | the pod-mode doctor rows (the Ollama model's digest among them) except two groups: the Onyx rows are K-O2's and the repo rows are K-C2's | K-A1, K-B1 |
| **K-I1 ideas** | ideas | the follow-ups that don't block the first deploy: the freshness dashboard on the Health page, a model provider for the pod (K12's later spec), authentication for the shared caller if the owner later wants it, a token or Unix socket for sessions inside the pod (§6), TLS by mesh notes; each is written as a one-page spec, then taken as its own phase | nothing |
| **Part D W1–W4** | context, ui | as its own §9 | nothing (runs alongside everything) |
| **K-E2E** | context | the kind run of §7 end to end, the docs (`docs/sindri/deploy.md`) | everything above |

Every phase's gate also runs `scripts/check-env-example.sh` and `node scripts/check-deps.mjs` (§8's variables go in
`.env.example`, and a new edge goes in `deps.yaml`). Every phase writes its own part of `docs/sindri/deploy.md` (and its API §9
rows, the chart's NOTES, and the AGENTS.md command lines it adds) in the same PR; K-E2E only joins them up.

Parallel: at the start, five lanes are busy at once: context on K-A1, A2d, K-O2 and Part D W1 (K-S0 as soon as K-A1's image builds, K-O1 as
soon as K-S0 reports), api on K-B1, engine on K-C2, ui on Part D W3 (against W1's goldens). Context takes K-C3 after K-A1, then
K-C1; ui takes W4 after W3. K-E2E is last. The ideas lane starts with its one-page specs at once, and after each of api, engine
and ui finishes its one phase it takes that lane's next item from K-I1, so no lane idles. Every phase is one PR with a scoped
gate (build plus the unit and integration lanes of the touched projects; `Aw.Sindri.Ui.Tests` in any phase that adds an API golden
or a catalog row).

## 10. Left to the builders

The chart's exact resource numbers (measured on the kind run), the start script's details, the nginx listen-port option and the
model server's port setting (K-S0), each Onyx container's security context (K-S0), the snapshot class, and the kind cluster's
version. Each is recorded in the code or `docs/sindri/deploy.md` where it lands.

## 11. What revision 3 changed (the owner's answers q73 and q74, 2026-10-23)

| Answer | Change |
|---|---|
| q73: Sindri and Onyx are one deployable unit, nothing privileged, no Docker-in-Docker | Onyx's prepackaged images (by `onyx.lock` digest) run as sibling containers of the pod from one chart (new §A.3 replaces the Docker sidecar). Gone: the Docker-in-Docker sidecar, `privileged`, Sysbox, the uid-1000 reasoning, `DOCKER_HOST`, `DOCKER_CONFIG`, the Docker CLI and Compose plugin in the image, `AW_SINDRI_DOCKER_MEMORY_BYTES` and `_DISK_BYTES`, A2e's preflight changes, the namespace of its own and `connect --onyx` running `onyx up`. Added: the render script and its drift check (K-O1), loopback wiring and port clashes, native-sidecar start order, one `onyx-data` claim with subPaths, Onyx's Secret, `onyx attach` (K-O2, revived minus `87028b190`), Onyx doctor rows, the shared-loopback consequence (§6). K1 and K2 are decided; K7 snapshots both claims; K9 gains Onyx's Secret; K11 keeps two locks with Onyx pulled by the kubelet; `onyx.enabled: false` now only means a smaller pod |
| q74: no new shared door | The existing `/sindri/mcp` on the existing API port, opened to the cluster (§B.2). Gone: the second `WebApplication`, `AW_SINDRI_SHARED_LISTEN`, `SND-API-015`, the per-call scope bridge, the separate pipeline and Kestrel limits, and the second path `/sindri/context-mcp`. Added: the guard decides by remote address and endpoint metadata, the endpoint-walk test, the `shared` caller picked from the connection, per-caller tool rows, the flagged `share.workQueueReads`, the reduced `GET /sindri/ping`. `SND-API-016` now names `AW_SINDRI_MCP_HOSTS` |
| §9 | K-A2 removed; K-O1 and K-O2 added; K-B1 is "open `/sindri/mcp` to the cluster" (api lane) |

Revision 2's changes, one line each, still hold unless the table above says otherwise:

- K1/K2: Onyx's Compose setup, not Onyx's Helm chart (revision 3 keeps no Helm chart for Onyx either, and no Compose in the pod).
- K3: any pod may reach the door; no NetworkPolicy by default.
- K5: no authentication; no tokens, token store or minting; code and docs only by default with the flagged `share.businessContext`.
- K12: no model in v1.
- Sandbox PRs #352, #355, #363, #366: not needed for this spec.

The open drafts' updates to match are listed in the PR body, not here.
