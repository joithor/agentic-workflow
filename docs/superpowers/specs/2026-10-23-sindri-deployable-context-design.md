# Sindri as a deployable unit: one image, Onyx's containers beside it in the same pod, the existing MCP port opened to the cluster

**Status:** design, revision 3 (the owner's answers q73 and q74 of 2026-10-23 applied on top of revision 2's, then `/autoplan` rounds 1 and 2's fixes; §11 lists what changed),
a draft until the owner answers the two decisions in §1 (its third item is a note) · **Date:** 2026-10-23 · **Asked by:** the owner, 2026-10-23 (relayed by
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
owner answers the two decisions of §1 (the work queue for the cluster; chat and tickets for the cluster; the node size is a note, settled after K-S0);
then /specToProvenPR takes §9's phases to PRs.

**Keeping it app-agnostic.** Nothing here names a work app, company, cluster, registry or host. Every one of those is a value the
deployer gives (Helm values, the profile, Kubernetes secrets), and the examples use `example.com` and `registry.example.com`.

## 0. The short version

### For you

One pod runs Sindri and Onyx (the search over chat and tickets) together, installed from one chart. After that, the agents of everyone in
your cluster can ask it about the code and docs of the repos you approve, and about nothing else unless you switch more on. Your own
rights stay yours: you reach the pod only through `kubectl port-forward` and `kubectl exec`, and nobody else in the cluster gets them.
Setting it up is a handful of commands, each one step: install the chart; `scripts/sindri-pod.sh connect --onyx` (opens the app on the
pod and does the one Onyx sign-up); `scripts/sindri-pod.sh profile edit` (allow your git host, and turn a sharing switch on or off if
you choose); add the repos; approve once; paste one entry per repo into your internal MCP server. Two choices are yours, in the box in
§1; the node size is a note there. If your node is small, Onyx stays off and the cluster gets code and docs only.

### For builders

Today Sindri is a desktop program: it binds to loopback only, trusts whoever holds the token file in its home, runs one copy per
person, and manages Onyx with Docker Compose on the same machine. In a pod, most of that stays. This spec turns it into:

1. **One image** (`Dockerfile` at the repo root, built by CI from the repo): Sindri's control plane (as `sindri-app` publishes it),
   the pinned `temporal` binary, the code-index tools, `git`, and nothing secret. It runs `sindri serve` in the foreground with all
   state on one volume. It has no Docker CLI and no daemon.
2. **Onyx beside it, in the same pod, from one chart.** Onyx's own prepackaged images run next to Sindri as ordinary containers.
   Nothing is privileged and nothing runs Docker. A script renders them from Onyx's pinned Compose file, so an Onyx upgrade is:
   bump the lock, re-render, `helm upgrade`. Sindri uses this Onyx as one it does not manage; one command, `sindri onyx attach`,
   does the account steps.
3. **One door, the one that exists.** `/sindri/mcp` on Sindri's API port is opened to the cluster. A caller from the cluster
   reaches only that and a small ping, and by default sees only the five `context_*` read tools. The owner, through `kubectl port-forward`,
   keeps every right they have today.
4. **Code and docs only by default** for that caller: it is served the code and docs sources of the approved repos. Business context
   (Onyx's chat and tickets) and the work-queue read tools (`sindri_status` and its kin) are served only when the owner turns on a
   profile switch each, because with no authentication anything in the cluster could then read them (both flagged in §1).
5. **Always fresh**: a new Sindri service pulls each repo's default branch; the code index, the docs index and the map follow it
   as they already do; Onyx's connectors (set up once by the owner in Onyx's page) keep tickets and chat current; Sindri's existing
   sync and refresh services read them.
6. **The owner's steps** stay one command or one click each: `scripts/sindri-pod.sh connect` opens the app on the pod;
   `connect --onyx` also forwards Onyx's page for the one browser sign-up and runs `sindri onyx attach` in the pod, before the one
   approval; `profile edit` is the one way to change a profile key in the pod; approvals are the app's existing
   one-click-and-type confirmations (or `kubectl exec -it … sindri profile approve` at a terminal).
7. **No model in v1.** Everything the map and the five tools do works without one; only labelling suggested links and the
   decisions pass need a model, and they stay off in the pod (K12).

The price of one unit: the pod holds all of Onyx plus Sindri (and the Ollama sidecar), so it must fit **one node**. Onyx's floor
alone is about 5.5 GiB of memory and its images are about 11 GB to pull; the spike measures the whole pod (K-S0), deploy.md says
what node size that means, and the owner chooses the node after that (§1, note 3). A node too small for Onyx means Onyx off.

## 1. Decisions

The owner answered K1, K2, K3, K5 and K12 on 2026-10-23 (revision 2) and q73 and q74 the same day (revision 3); those rows are
**decided**. **Nothing in this design is privileged.** Two decisions are **flagged** for the owner, in the box below, both about what a
caller from the cluster may read; a third item is a note about the node. The rest keep their defaults, which builders follow if the owner says nothing.
Until the owner answers, the phases that don't depend on the answers may start (K-S0, K-A1, A2d, K-O2, K-C2, K-C3, K-C1, Part D, and
K-B1 with both switches off, which is their default; K-O1 waits for K-S0's report).

> **Two decisions and a note for you, in plain words.**
>
> 1. **Should agents in the cluster see your work queue?** `/sindri/mcp` also has six read-only tools about your own work: your
>    items, the queue, the decisions waiting on you, and an item's timeline and jobs. **Default: off.** If you turn it on
>    (`share.workQueueReads`), every pod in the cluster can read them for any repo it names. Item titles and notes can quote tickets
>    and chats, so this is not safer than sharing chat. It is off because nobody signs in, so nobody can be told apart or cut off one
>    by one. **To undo:** run `scripts/sindri-pod.sh profile edit` and set it back to false: it opens the profile in your editor, checks it,
>    copies it back and asks you to approve with the typed word; it applies on the next request. (For everything at once,
>    `service.enabled: false` and `helm upgrade` removes the Service.) The tool that files work (`sindri_request`) is never offered
>    to the cluster either way.
> 2. **Should agents in the cluster see chat and tickets?** **Default: off**; they get the code and docs of the repos you approve.
>    If you turn it on (`share.businessContext`), every pod in the cluster can read every chat message and ticket Onyx has pulled
>    in, private channels its connector can see included, and it is one switch for all of Onyx's sources. Off for the same reason as
>    above. **To undo:** the same as 1, for `share.businessContext`.
> 3. **Which node? (a note; nothing to answer yet.)** The pod holds Onyx, Sindri and a small embedding model, so it needs **one node**
>    that fits all of it. I cannot name the size yet: the spike (K-S0, about 2 working days) measures the real memory, disk and CPU,
>    and deploy.md turns them into a node size. Then you pick. Tell me now if you only have a small node: Onyx then stays off
>    (`onyx.enabled: false`), the cluster gets code and docs only, and decision 2 stops mattering, because there is no chat or ticket
>    search to share.
>
> **How they fit together:** with both switches off, the cluster sees exactly the code and docs maps of the repos you approve, and
> Onyx serves only your own queries in the app. **Either way:** approving a repo in the pod's profile publishes its code and docs to
> every pod in the cluster.

Defaults revision 3 took where the owner's answers left a choice (each is reversible without a redesign; the PR body repeats them):

| Default | Where | The other way |
|---|---|---|
| A caller from the cluster sees the 5 `context_*` tools; the 6 `sindri_*` reads are off (flagged); `sindri_request` is never offered | B.2 | `share.workQueueReads: true` adds the six; `sindri_request` stays refused (it files work, and a many-user door should not start work) |
| A non-loopback request to any other route is a bare 404, as an unknown route is | B.2 | a 403 with a code, if a deployer wants the refusal named |
| The shared client is named `shared`; it needs `share.enabled`, not `context.sessions.enabled` | B.2 | none needed |
| nginx listens on a high loopback port; Sindri talks to Onyx through nginx | A.3 | Sindri calling api_server directly (needs the Onyx adapter to drop the `/api` prefix); K-S0 verifies the default |
| Onyx's pinned images are per-platform digests, chosen by `onyx.platform` (default `amd64`) | A.3 | an index digest row in `onyx.lock`, if K-O1 adds one |
| OpenSearch runs with `bootstrap.memory_lock=false` and bound to loopback | A.3 | none if K-S0 shows the bootstrap checks only warn |
| The per-source cap is off (`share.perSourceCap: 0`) | B.2 | a number; behind the internal MCP server it caps everyone together, so set it only when callers reach the Service directly |
| nginx finds Onyx's upstreams through `ONYX_BACKEND_API_HOST` and `ONYX_WEB_SERVER_HOST` set to `127.0.0.1`, and its port is moved by a `sed` in the container command | A.3 | `hostAliases` on the pod, if K-S0 shows the variables don't take effect |
| A profile key (a sharing switch, `repo-pull.allowedHosts`, `share.perSourceCap`) changes only through `sindri-pod.sh profile edit`; no chart value sets one | C.1, C.3 | none needed |
| Backup, a disruption budget and a network policy are left out of the chart in v1 | C.4, C.5, K7 | each comes back without a redesign: snapshots as a CronJob, `maxUnavailable: 1`, a policy admitting the API port only (§11) |

| # | Question, in plain words | Answer | Why, and the cost |
|---|---|---|---|
| **K1** | How does Onyx come up beside Sindri? | **Decided (q73): as sibling containers of the same pod, from Onyx's prepackaged images by digest (`context/onyx.lock`), in one chart** (§A.3). Not Onyx's Helm chart, not Docker Compose in the pod. **Nothing is privileged; no Docker-in-Docker, no Docker CLI in the image** | The owner's answer. The cost: the chart carries a container per Onyx service, so it must stay in step with Onyx's Compose file; that is why the containers are **rendered by a script** from the pinned Compose file and the lock, with a CI drift check (K-O1). Pods also lose Docker's private network: the containers share the pod's loopback (§6) |
| **K2** | Onyx's database: inside the cluster, or a managed database? | **Decided by K1: inside the pod**, Onyx's own Postgres container (`relational_db`) on a volume claim | No operator, no cluster-wide resources, nothing for a cluster admin to install. The manual backup procedure (K7) covers this claim too |
| **K3** | Who can reach the door? | **Decided: any pod in the cluster.** No NetworkPolicy on the namespace by default | The owner's answer. The door is now `/sindri/mcp` and the ping, with only the `context_*` tools for a cluster caller; the rest of the port refuses a caller that isn't loopback, and the owner's rights (approvals, items, workflows) are reachable only through `kubectl port-forward` and `exec`, which arrive as loopback |
| **K4** | What can everyone's agents do through the door? | **Read the context map and nothing else**: `context_query`, `context_lenses`, `context_node`, `context_neighbors`, `context_sources`. The six `sindri_*` reads are off for them (flagged, `share.workQueueReads`); `sindri_request` never | Today's `/sindri/mcp` has 12 tools (`McpToolTable`), one of which files requests; a many-user door should not start work, and the read tools show the owner's queue. The table is chosen per caller (§B.2) |
| **K5** | What can a caller see? | **Decided: no authentication** (no bearer tokens, no token store, no minting). A caller names a repo with `X-Sindri-Repo` and reads any repo in the approved profile. **Code and docs sources only**, unless the profile's `share.businessContext: true` is set. **Flagged:** that switch | The owner's answer. The consequences, stated plainly: with no authentication, anything that can open a connection to the Service (any pod in the cluster, K3) reads every approved repo's map, code snippets and docs, so **approving a repo in the pod's profile publishes it to the whole cluster**; that approval is the only gate. With the switch on a caller also reads every record Onyx's connectors pulled in (chat messages, including the private channels Onyx's connector can see, and tickets), because Sindri reads Onyx as one user and Onyx's own per-source permissions don't reach a caller; the switch is all or nothing across Onyx's sources in v1. Nobody can be told apart or cut off one by one: the owner's kill switch is `share.enabled: false` in the profile (the port then refuses every non-loopback caller but the ping), or the chart's `service.enabled: false` (no Service). Both switches live in the profile only, so changing them is a profile change the owner makes with `sindri-pod.sh profile edit` and approves like any other (§C.3), and the `profile approve` preview says in plain words what is published (its four texts, verbatim, are in §C.3). Doctor lists the served repos and shows each switch as a warn row while it is on |
| **K6** | How does the pod get the code, and how fresh? | **A new Sindri service, `repo-pull`,** fast-forwards each repo's default branch every minute with a read-only token from a Kubernetes secret; the first clone happens once when a repo is added | Sindri never clones today and expects a checkout on disk. The common git-sync sidecar lays a repo out as linked worktrees, which Sindri resolves to a main checkout without a tree; a service of our own keeps the layout Sindri already checks (clean, HEAD in main) |
| **K7** | Where does the state live, and what is the backup? | **Two volume claims**: Sindri's (`/data`) and Onyx's (Postgres and the search index; one claim, subPaths). **One copy of the pod** (a StatefulSet of one). **The backup is a manual procedure in deploy.md for v1**: no CronJob and no RBAC in the chart (a reversible default, §11) | Sindri's ledger, views, map and Temporal's history are SQLite files that allow one writer; two replicas would fight over the lease. The procedure snapshots both claims at the same moment (a `VolumeGroupSnapshot` where the cluster has it, else two `VolumeSnapshot`s seconds apart), so the map's Onyx cursor and Onyx's database stay at the same point; Postgres recovers from a crash-consistent snapshot as from a power cut; two snapshots seconds apart can leave the cursor a little ahead of Onyx's database, and the sync service resyncs, as it does after any restore. The snapshots hold the human token, the ledger, Onyx's database and the connector OAuth credentials (§6), so the namespace is the owner's alone and the StorageClass should be encrypted where the cluster has one |
| **K8** | How does the owner see the app on the pod? | **`scripts/sindri-pod.sh connect`**: one command that port-forwards, fetches a fresh human token over `kubectl exec`, and opens the desktop app against the pod | Today the app only reaches a Sindri whose files it can read on the same machine. A browser version of the app is still later (UI spec §12.2) |
| **K9** | Where do secrets go? | **Kubernetes secrets as environment variables** (`env:` pointers): one read-only git token per git host; never in the image, the profile or the chart's values. Onyx's own secrets (its database password, its OpenSearch admin password, its auth secrets) are a second Secret the chart creates on first install (random, kept across upgrades), mounted into Onyx's containers only. In pod mode Sindri's child processes (git, tools) get only an allowlisted environment, not every secret (§A.2 A2d) | Secrets mounted as files are symlinks in Kubernetes, and Sindri's `file:` pointers refuse symlinks on purpose. The Onyx token Sindri makes at attach is written to Sindri's volume (`secrets/`, owner-only), as the managed mode does today when no keychain exists. Onyx's Compose file falls back to well-known passwords (`password`, `StrongPassword123!`) and keys (`minioadmin`) when nothing is set; the render script refuses to emit a container that would use `minioadmin` or any other well-known fallback (A.3, Rendering) |
| **K10** | Embeddings for the code index (local models only)? | **An Ollama sidecar** in the Sindri pod on `127.0.0.1:11434`, its model pulled once onto the volume **by digest** and checked against `context/code-index.lock` before Sindri serves queries (doctor shows the result) | Sindri allows only a loopback Ollama; a sidecar shares the pod's loopback, so no code change |
| **K11** | Images: built how, pinned how? | **Two locks, one per source.** Our own image is built by CI from the repo for linux/amd64 and linux/arm64, pushed to a registry from config, and referenced **by digest** in the chart. Our own and the base images, and Ollama, are pinned in `deploy/images.lock` (in `onyx.lock`'s style, with a `check` script). Onyx's are pinned in `context/onyx.lock`, pulled by the kubelet by the digests it holds and rendered into the chart (K-O1). A `helm template` test asserts every image reference in the rendered chart is a digest that appears in one of the two locks | One lock per source of truth. `onyx.lock` records one digest per platform; a per-platform manifest digest pulls only on that platform, so the chart picks the row by `onyx.platform`. Model weights are pinned too (K10, §A.3 Egress) |
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
   writable home). `/opt/sindri` is world-readable. The image has no Docker CLI, no Compose plugin and no daemon.

`serve` runs in the foreground, ignores SIGHUP and stops cleanly on SIGTERM (it already does: `ServeCommand.cs`), stopping its
Temporal dev server within 10 s. `serve`'s Onyx hooks (`OnyxServeHooks`) act only on a Sindri-**managed** Onyx and "an unmanaged
Onyx is never touched", so in the pod, where Onyx is unmanaged (§B.3), a Sindri restart leaves Onyx's containers running: no code
change. When the whole pod stops, Onyx's containers stop with it, at the same time (all are ordinary containers, so every container gets the stop signal at once). The pod's
grace period is 120 s: Sindri's own stop is at most about 30 s (20 s for launched sessions, 10 s for Temporal); Postgres and
OpenSearch get the rest to flush; K-S0 times a pod delete with Postgres under write load.

### A.2 What the image needs from Sindri (code changes)

| # | Change | Where | Why |
|---|---|---|---|
| A2a | A **pod mode**, `AW_SINDRI_DEPLOY=pod` (unset: today's desktop behaviour, byte for byte; in pod mode telemetry export is on, to the endpoint in `OTEL_EXPORTER_OTLP_ENDPOINT`, which the chart sets from `telemetry.endpoint`) | `SettingsFromEnvironment`, `Profile` (`deploy:` read-only echo for doctor) | one switch that the changes below key on; a desktop install can't enter it by accident because nothing sets it |
| A2b | **No new host variable for the lease.** The lease records `Environment.MachineName` (`PosixProcessProbe`), which in a StatefulSet pod is already its stable name (`sindri-0`); a restarted pod then has the same host; on the same node `LeaseDecision.Liveness` proves the old holder dead by its process start time (container pids repeat and the boot id is the node's), and on another node by the boot id. A force-deleted pod or a node that comes back with the same boot id can read as provably dead while the old process still holds the volume, so deploy.md says never to force-delete `sindri-0`, and the heavy restart test (§7) covers a normal rollout and a force delete. The test is the proof; only if it shows a 60 s wait does a builder add a pod-mode host override | `Infrastructure/Lease` (test only) | removes a setting the code didn't need |
| A2c | Doctor rows for pod mode: the volume writable, owner-only and under 70 % full, the temporal binary present, `bwrap` usable (user namespaces), Ollama answering, **Onyx's containers ready** (K-O2), **Onyx reachable** (its health route at the attached URL) and **the pin matches Onyx's `/api/version`** (the existing `OnyxSettings.CheckVersion`), each business-context and work-queue switch (a warn row while on, K5), the served repos (K5), the model-less steps listed as off (K12), the map's lag behind each repo's main branch, and a warn row when a request from a loopback remote carried a service-name Host or one of the forwarded headers (a mesh sidecar or a proxy is in front of this pod; B.2). **When Onyx is off** (`AW_SINDRI_ONYX_ENABLED=false`, which the chart sets with `onyx.enabled: false`) the Onyx rows read "Onyx: off in this install (code and docs only)" at ok level, with no "not set up" and no command to run. Sindri's pod has no Kubernetes service account (`automountServiceAccountToken: false`), so "containers ready" can't ask the API server: doctor opens each Onyx container's loopback port from a list the chart passes (`AW_SINDRI_ONYX_PORTS`, `name:port` pairs written by K-O1's render, the same one that wrote the containers) and the row names the ones that don't answer. On a fresh install a row whose thing isn't set up yet (Onyx before `onyx attach`, no repos, no map yet) reads "not set up" with the next step, never fail; rows are ordered as the owner sets things up (volume, repos, map, Onyx, door), and each warn or fail names its fix | `Features/Doctor`, `Infrastructure/Onyx` | the owner's first check after install, through `kubectl exec … sindri doctor` |
| A2d | **Pod-mode child environment**: one environment factory that every `ProcessStartInfo` in `src/Sindri` goes through (today about 20 files spawn processes and inherit the whole environment, among them `ProcessToolRunner`, `Workflow/GitProcess`, `DevServerProcesses` for the detached Temporal server, `GitChangeSource` and `ProcessClaudeCli`); in pod mode it passes only an allowlist (`PATH`, `HOME`, `LANG`, the one credential a call needs), and an architecture test fails on any `new ProcessStartInfo` outside it | `Infrastructure/Processes` plus each spawner | the env-pointer secrets of K9 must not reach every child |
| A2e | **Onyx in pod mode is attached, never managed.** (1) `sindri onyx up` and `down` in pod mode refuse with `SND-ONYX-013` (the highest code on main is `SND-ONYX-012`; K-O2 re-checks at merge) whose fix text is "Onyx is part of this pod. Run `sindri onyx attach`." (2) `onyx attach [--url U]` (§B.3). (3) No preflight change: the preflight is managed mode's (Docker, Compose, memory and disk on the machine) and never runs for an unmanaged Onyx, which is why revision 2's memory, disk, stop-hook and fix-text changes are gone. The pod's memory and disk are the chart's requests, limits and claim sizes, measured in K-S0 | `Features/OnyxAttach`, `Host/Commands/OnyxVerbs` | without (1) a stray `onyx up` in the pod would look for Docker and fail with a Docker message |
| A2f | **Temporal runs headless in pod mode**: `TemporalDevServer` starts the dev server with `--headless` and no `--ui-port` (Sindri never uses the UI); the frontend and HTTP ports stay on `127.0.0.1`. Temporal's ports are not fixed: `TemporalDevServer` picks free loopback ports at random at each start (`PickPorts`), and `DevServerProcesses.Parse` reads them back from `ps`. **K-S0 confirms** the flag's real spelling in the pinned `temporal` (`temporal server start-dev --help`) and that `--headless` with `--ui-port` omitted still takes `--http-port` | `Workflows/TemporalDevServer` | the unauthenticated Temporal UI is one less thing on the shared loopback (§6) |

If the cluster forbids user namespaces, `bwrap` can't start and the code graph can't build (`SND-CODEIDX-007`); doctor says so, and the owner sets the profile key
`index.graph.enabled: false` with `profile edit` (§C.3; it is a profile key only, and no chart value sets it). The text and symbol index still work. Under a container runtime's default seccomp
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
| `inference_model_server` | `onyx-model-server` | `127.0.0.1:9000` | embeddings, for queries and for indexing (the one model server, as in `onyx up`) |
| `relational_db` | `postgres:15.2-alpine` | `127.0.0.1:5432` | Onyx's database |
| `opensearch` | `opensearch:3.6.0` | `127.0.0.1:9200` (HTTPS with a demo certificate once the admin password is set), `9300` (transport), `9600` (performance analyzer) | the search index |
| `cache` | `redis:7.4-alpine` | `127.0.0.1:6379` | Onyx's cache (no persistence, as in the Compose file) |
| `nginx` | `nginx:1.25.5-alpine` | `127.0.0.1:<onyx.port>` (see below) | the one front door Sindri and the owner's browser use |

The pod's whole port map, written as data by `onyx-pod.mjs` and diffed by the kind test against `ss -ltn` in each container:
the rows above, plus Sindri's API port `7410` (on **both** the loopback and the pod's address, and the only port on the pod's
address), Temporal's frontend and HTTP ports and its internal service ports (all loopback; **picked at random by `TemporalDevServer` at each start**, so the map says "Temporal: recorded in `control.json` and `sindri status`", not a number; headless drops only the UI port), and Ollama `11434` (loopback). The kind test's `ss -ltn` diff first reads Sindri's own random loopback ports from `sindri status --json` and excludes them (checking each is on `127.0.0.1`), then compares everything else with the map exactly. The main containers start together (Start order below).

`minio` (the `s3-filestore` profile), `code-interpreter` and `indexing_model_server` are **not** run. Sindri's use of Onyx needs
none of them: the one model server embeds for indexing too, with `INDEXING_MODEL_SERVER_HOST` pointing at it, as `onyx up` does
(which also removes the stock file's clash of both model servers on port 9000), and the code-interpreter's Compose service is the
one that asks for `user: root` and carries a commented-out `privileged: true`. Without minio the render also sets what pointed at
it: `FILE_STORE_BACKEND` goes to Onyx's database-backed file store (`postgres`; **K-S0 confirms** v4.9.0 accepts that value, and if
no store without S3 exists K-S0 stops and tells the owner, §7), and every `S3_*` and `CODE_INTERPRETER_*` line is **dropped** (listed
as dropped, so `--check` is satisfied and no `minioadmin` is ever emitted). The chart has no `onyx.s3Filestore` or
`onyx.codeInterpreter` switch in v1.

| Aspect | Rule |
|---|---|
| Images | Each container's `image` is `repository@sha256:<digest>` from `onyx.lock`, picked by `onyx.platform` (`amd64` by default; a pod on arm64 nodes sets `arm64`), because the lock records a manifest digest per platform and a per-platform manifest pulls only there. The render script fails if a service of the pinned Compose file has no `image` row for the platform, and `scripts/onyx-lock.sh verify` (maintainer-run) keeps meaning what it means today |
| Rendering (K-O1) | `scripts/onyx-pod.mjs` reads these inputs, all committed: `context/onyx.lock`; `deploy/onyx/docker-compose.yml`, `deploy/onyx/env.template` and `deploy/onyx/data/nginx/*` (copies of the pinned upstream files, each sha256 checked against the lock's `file …` row, as the Compose copy is); the shared sizing file (next row); and `deploy/onyx/pod-overrides.yaml` (only what is pod-only: loopback hosts and binds, ports, the nginx `sed`, the file store, secrets, volumes, probes, and the Compose-only constructs, which stay out of the sizing file). It writes `deploy/helm/sindri/templates/onyx-containers.yaml`, the nginx ConfigMap and the port map (`AW_SINDRI_ONYX_PORTS`). `--check` first verifies every input against the lock, then re-renders to memory and fails on any difference, the ConfigMap included (CI, and a bash test in AGENTS.md's list). It also fails when an `environment:` line of the Compose file or a key of `env.template` is not carried over, overridden or listed as dropped, so a new Onyx setting can't slip in unnoticed. A pod has no `.env`, so `env_file: .env` becomes explicit `env` entries from `env.template`: `AUTH_TYPE=basic`, `DISABLE_TELEMETRY=true`, `WEB_DOMAIN=http://127.0.0.1:<onyx.port>` (the address of the port-forwarded sign-up; `connect --onyx` forwards the same local port by default, §C.3) and the rest as the template has them. The render **refuses** `minioadmin`, `password`, `StrongPassword123!` and every other well-known fallback: a credential variable (name has PASSWORD, SECRET, KEY or TOKEN) whose Compose default isn't covered by the Secret or the dropped list. `--check` also has a **required-keys list** for the secrets that have no Compose default and would otherwise be empty or stored in the clear: `USER_AUTH_SECRET` (the API server refuses to start without it) and `ENCRYPTION_KEY_SECRET` (it encrypts connector credentials; unset, they sit unencrypted in Postgres, which snapshots copy), besides `POSTGRES_PASSWORD` and `OPENSEARCH_ADMIN_PASSWORD`; a render that doesn't take each of them from Onyx's Secret fails. **An Onyx upgrade is:** bump `context/onyx.lock` (`onyx-lock.sh verify`), refresh the committed copies, run the script, read the diff, `helm upgrade` |
| One recipe for managed and pod Onyx | `sindri onyx up` already ships tested overrides for one user (`OnyxConfig.cs`, `docs/sindri/onyx.md` §2): one model server (no indexing model server, `INDEXING_MODEL_SERVER_HOST=inference_model_server`), the Slack and Discord bots kept from starting, the celery worker concurrencies at 1, OpenSearch's heap at 1g with a 2g limit. The pod starts from the same values. **Only the values that carry over to a pod are shared**, in one data file, `context/onyx-sizing.json`: the environment values per service, the memory and heap numbers, the concurrencies, which services run, and the bot flags **as environment** (K-S0 confirms v4.9.0 has such flags; if it has only the stock `background` command that seds `supervisord.conf` into `/tmp`, that command stays in `pod-overrides.yaml` and in `OnyxConfig`, the file records only that the bots are off, and the drift test compares that flag; the `background` container then needs a writable `/tmp`). **Compose-only constructs are not in the file**: `ports: !override`, `profiles`, `depends_on … required: false` and shell commands stay in `OnyxConfig` (C#) and in `pod-overrides.yaml`. `OnyxConfig` (in `Aw.Sindri.Core`) takes the file's content as a parameter, embedded from Infrastructure the way the lock is; `onyx-pod.mjs` reads it from disk. **The drift check:** a C# test renders `OnyxConfig`'s override and asserts every value in the file appears in it unchanged, and that the override sets no sizing-class key (an `environment:` entry, `mem_limit`, a heap or concurrency setting) the file lacks; `onyx-pod.mjs --check` asserts the same two things for the pod's containers. So a value changed on one side fails one of the two tests. The lock's `docker-memory-bytes` (5.5 GiB) was measured for the stock set; K-S0 re-measures it for this one |
| Reaching each other | Compose reaches services by name on a private network; in a pod they share one network namespace, so the script sets each host variable the Compose file reads to `127.0.0.1`: `POSTGRES_HOST`, `OPENSEARCH_HOST`, `REDIS_HOST`, `MODEL_SERVER_HOST`, `INDEXING_MODEL_SERVER_HOST`, `API_SERVER_HOST` (all appear in the pinned Compose file as `${NAME:-service}` defaults), and the web server's `INTERNAL_URL` to `http://127.0.0.1:8080` (its Compose default is `http://api_server:8080`). nginx's upstream names come from two variables its start script reads: `run-nginx.sh` exports `ONYX_BACKEND_API_HOST` (default `api_server`) and `ONYX_WEB_SERVER_HOST` (default `web_server`) and `envsubst`s them into the template's `server ${ONYX_BACKEND_API_HOST}:8080` and `server ${ONYX_WEB_SERVER_HOST}:3000` (read in the pinned copies), so the script sets both to `127.0.0.1`: no template edit, so the pinned hashes stay valid. `hostAliases` on the pod (those names to `127.0.0.1`) is the fallback only, if K-S0 shows the variables don't take effect |
| Ports | One model server, so the stock file's clash (both model servers on 9000) is gone. Every port in the map above is distinct, and the kind test diffs the map against `ss -ltn` in each container |
| nginx | The Compose file publishes nginx's port 80 as `${HOST_PORT:-3000}:80` and `${HOST_PORT_80:-80}:80`, and its health check calls `127.0.0.1:80`. In a pod there is no publishing, nginx listens in the pod, and port 80 needs root. The default is a **high port on loopback** (`onyx.port`, default 8081), moved **in the container's command, not in the committed templates**: the template hard-codes `listen 80 default_server;`, so the command (Compose's own `cp -a /nginx-templates/. /etc/nginx/conf.d/`, then `run-nginx.sh`) gets one `sed` between the two that rewrites that line in the copied `app.conf.template` to `listen 127.0.0.1:<onyx.port> default_server;`. The ConfigMap keeps the pinned files byte for byte, so the sha256 rows in `onyx.lock` stay valid; the script writes the liveness probe against the new port (`/nginx-health`). nginx needs a writable `/etc/nginx/conf.d` (the `cp` target), `/tmp`, `/var/cache/nginx` and `/run`: they are `emptyDir`s, so the root filesystem need not be writable. K-S0 verifies the `sed` and the start script. The alternative is Sindri calling `api_server` directly on `127.0.0.1:8080`, which drops nginx from Sindri's path but not from the owner's browser and would need the Onyx adapter to stop adding the `/api` prefix nginx strips (a code change in `Aw.Context.Infrastructure`). The pod sysctl `net.ipv4.ip_unprivileged_port_start=0` is pod-wide and refused or unsafe on some clusters, so it is the last resort. Sindri reaches `http://127.0.0.1:<onyx.port>`, and so does the owner through `kubectl port-forward` |
| Nothing on the pod's address | In Compose these services sit on a private network. In a pod they would listen on the pod's address, reachable by any pod (K3). So **every Onyx listener is set to loopback**, and K-S0 proves it with `ss -ltn` in each container and a scan of the pod's address from another namespace, which must find only the API port (§6): Postgres `-c listen_addresses=127.0.0.1` (appended to the Compose command `-c max_connections=250`); Redis `--bind 127.0.0.1` (appended to the Compose command); OpenSearch `network.host=127.0.0.1` (and `http.host`, `transport.host`); the API server's `uvicorn … --host 127.0.0.1` (the Compose command has `--host 0.0.0.0`; the script rewrites it); the web server `HOSTNAME=127.0.0.1` (a Next.js setting; **unverified for this image**); the model servers' `--host`; nginx's listen address. A setting an image does not honour is a K-S0 finding, answered with a different image setting or, if there is none, by the stop rule of §7, and the spec is amended before K-O1 |
| OpenSearch | The Compose file sets `bootstrap.memory_lock=true` and `memlock` and `nofile` ulimits. A pod's container cannot set ulimits (the runtime's defaults apply), so the script sets `bootstrap.memory_lock=false`, keeps `discovery.type=single-node`, the heap from the sizing file (`-Xms1g -Xmx1g`) and the admin password from the Secret. **To be verified in K-S0:** with the node bound to loopback only, OpenSearch is in development mode, so the `vm.max_map_count` and file-descriptor bootstrap checks only **warn**; if the pinned image still refuses to start, the fallback is an init container that raises `vm.max_map_count` (which is privileged, so it would break this spec's rule) or a node setting the deployer makes (a node-wide `vm.max_map_count` of 262144, which deploy.md lists). K-S0 records which, and the warning's effect on the index's speed |
| Security contexts | **No container is privileged**, `allowPrivilegeEscalation: false` everywhere, `automountServiceAccountToken: false` on the pod. **Every container drops ALL capabilities and adds back only what K-S0 proves its entrypoint needs** (recorded per container; `NET_RAW` never). The namespace carries Pod Security level **`baseline`** (or none): `baseline` forbids privileged containers, host namespaces and host paths, and allows images that start as root. The spike and the kind run label their namespace `pod-security.kubernetes.io/enforce=baseline`, so "baseline is enough" is proved, not asserted. **Which containers can run non-root is unverified and K-S0 records it per container**; the expectation, to be confirmed: `postgres`, `redis`, `nginx` and `opensearch` entrypoints start as root and drop to their own user (Postgres needs `CHOWN`, `SETUID` and `SETGID` for its data directory unless the volume is already owned by its uid and the pod sets `fsGroup`); the Onyx images may or may not already run as a non-root user. `restricted` is the goal wherever an image allows it (non-root, `RuntimeDefault` seccomp), the chart sets those fields per container from values, and the namespace label is whichever level the weakest container needs. deploy.md says which containers are the reason it is `baseline` |
| Start order | **No Onyx container is a native sidecar, and none gates Sindri.** All containers, Sindri's and Onyx's, are ordinary containers that start together; the only init container is the one that prepares the volume directories (Volumes row). **Sindri's container is first in the pod spec, and that init container runs Sindri's own image**: the kubelet pulls and starts in spec order, so Sindri starts as soon as its image is there, while the Onyx images are still pulling (an init container on another image would add a pull in front of everything). K-S0 and the kind run measure **time-to-Sindri-up on a cold node** (`/sindri/ping` answering before the last Onyx image finishes pulling) and record it. Compose's `depends_on` is not turned into pod ordering: Onyx's own images retry (`api_server` runs `alembic upgrade head` first and restarts under Kubernetes' restart policy if the database is late; nginx's start script waits for the API server; the model servers are `service_started`, not `service_healthy`, in Compose), and **K-S0 verifies** that the pod settles with no ordering at all. Why: Sindri must be up in seconds even while ~11 GB of images are still pulling or OpenSearch is crash-looping, so the owner can run `doctor`, approve a profile and serve code and docs. **Probes:** Onyx containers get **liveness probes only** (generous, from their Compose health checks where Compose has one, with long start periods; Compose has **no health check for OpenSearch**, so its liveness probe is one we write: a TCP check on `9200`, because the port answers HTTPS with a demo certificate) and **no readiness probes**; Sindri's readiness is `/sindri/ping`. The Service sets **`publishNotReadyAddresses: true`**, so a crash-looping Onyx container (which makes the pod not Ready) does not take `/sindri/mcp` out of the Service, and the door answers with Onyx's degraded answer (`SND-CONTEXT-010`). Two consequences: the endpoint also stays published during the 120 s termination, so callers see refused connections for up to that long on every rollout; and Sindri's readiness probe now decides only the pod's Ready condition (what `helm --wait` and `kubectl rollout status` read), not routing. The pod-state table (C.1) says what each state means |
| Volumes | Onyx's named volumes become **one claim** (`onyx-data`, default 60 Gi) with a `subPath` for each that must persist: `db_volume` (Postgres; PGDATA is a subdirectory so the filesystem's `lost+found` doesn't break `initdb`) and `opensearch-data`. **The model caches get no volume over the image's baked path.** Docker seeds a new named volume from the image; a Kubernetes volume at that path would hide whatever the image baked in and force a download at start. K-S0 lists where each model server's weights live and whether they download at start: if baked in, the path stays as the image has it (a writable `emptyDir` only if the process writes there); if they download, a volume is allowed and the weights are pinned (Egress row). Postgres gets a memory-backed `emptyDir` at `/dev/shm` (1 Gi, from Compose's `shm_size: 1g`, counted in its memory limit). The log volumes become `emptyDir`s (logs also go to stdout); `minio_data` and `file-system` aren't used; Redis keeps its Compose `tmpfs`. **subPath directories** the kubelet creates are often root-owned and outside `fsGroup`, so one plain, unprivileged init container (Sindri's image, which has a shell, `mkdir` and `chown`) creates each directory and sets its owner to the uid that container runs as (adding `CHOWN` and `FOWNER` only if K-S0 shows the claim's filesystem needs them; K-S0 verifies each directory). The pod sets `fsGroup` and `fsGroupChangePolicy: OnRootMismatch`. nginx's `../data/nginx` bind mount is a ConfigMap rendered from the committed copies of the files whose sha256 `onyx.lock` pins. The claim is part of the backup procedure (K7) |
| Secrets | A Secret the chart creates on first install (random values, kept across `helm upgrade` by a `lookup`) holds `POSTGRES_PASSWORD`, `OPENSEARCH_ADMIN_PASSWORD` (which the Compose file passes to OpenSearch as `OPENSEARCH_INITIAL_ADMIN_PASSWORD`), `USER_AUTH_SECRET` and `ENCRYPTION_KEY_SECRET`, each by name (the last two have no Compose default, so `--check`'s required-keys list covers them, Rendering row), and is mounted into Onyx's containers only, never into Sindri's or Ollama's. Because all the containers share the pod's loopback these are not a boundary against another container of the pod (§6); they stop the well-known Compose defaults from being the real passwords. `lookup` returns nothing under `helm template` and `--dry-run`, so the CI render test sees fresh random values on every run: it compares the Secret's **keys**, not values, and the heavy kind test proves the values survive a `helm upgrade` |
| Resources | Every container sets requests **equal to** limits, so the pod is **Guaranteed QoS** and node pressure doesn't pick it first (an eviction would take the door down with Onyx). The numbers for Sindri and Ollama come from K-A1's `docker run` (K-C1 ships with them), Onyx's from K-S0's measurement (K-O1 adds them), and they become the minimums in `values.schema.json`, not prose. The sum of the memory limits is at least K-S0's re-measured figure for the sizing set (the lock's `docker-memory-bytes`, 5.5 GiB, is for the stock set), with OpenSearch's limit at least twice its heap. **The pod's footprint is all of Onyx plus Sindri plus Ollama, and must fit one node** (the owner's node choice, §1); the kind run records the measured CPU, memory and disk, and deploy.md turns them into a node size |
| Egress | The kubelet pulls Onyx's images (about 11 GB across the pod; the registries `onyx.lock` names), so the pod's first start takes minutes. At run time the pod needs the git hosts, Onyx's connectors' services (the chat and ticket systems the owner connects), and, if the model servers fetch their weights at start (K-S0 records it, Volumes row), the model hub's host. **Weights are pinned:** the Hugging Face revision of each model is set by an environment setting K-S0 names, and Ollama's model is pulled by digest; each is verified before Sindri serves queries (doctor shows the result). The kind run records every host the pod contacts at start and deploy.md allowlists exactly those. Onyx's web connector can fetch cluster-internal addresses too, **and the pod's own loopback** (§6), so the connector checklist says to give it only public URLs |
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
| Listener | In pod mode only. there are two listen sites, both `Listen(IPAddress.Loopback, port)` today: `ServePort.Listen` (`ServePort.cs:35`) and `LoopbackBinding.Build` (`LoopbackBinding.cs:40`, which pins the web host's configuration so that no ASP.NET configuration reaches it, so pod mode is read through its `environment` function, as the port is). In pod mode both add one listener on the same port for every address instead (Kestrel can't hold the loopback and the any-address listener on one port; the call is `ListenAnyIP(port)`, which takes the IPv6 any-address in dual mode and falls back to IPv4 where the kernel has no IPv6, so IPv6-only clusters work). `ServeListenerCheck` still checks that `ServePort.Listen` ran. `control.json` still records the loopback URL. A `Service` (`service.enabled`, default true) exposes the API port (`AW_SINDRI_PORT`, default 7410); the owner's `connect` forwards the same port to the pod. The doc line "the only listener is 127.0.0.1" and any test or banned symbol that forbids a non-loopback listen are amended to name this one pod-mode exception. Outside pod mode nothing changes |
| What "loopback" means | Two tests together. **Remote:** the connection's remote address (IPv4-mapped IPv6 unwrapped) is exactly `127.0.0.1` or `::1`; not the rest of `127.0.0.0/8` (a mesh sidecar connects from `127.0.0.6`). **Host:** the Host header is `127.0.0.1`, `[::1]` or `localhost`, with or without the port. The owner's rights need **both**. Everything the owner does arrives with both (port-forward, `exec`, the app through the forward). So does **every other container in the pod** (§6). A call to the pod's own address from inside the pod, or through the Service, is not loopback |
| `LoopbackGuard` | **Loopback remote and loopback Host: unchanged** (a non-loopback Origin is 403; outside pod mode a non-loopback Host is 400 as today). **Everything else, in pod mode, is the `shared` rule, a loopback remote with any other Host included**: a mesh sidecar or in-pod proxy that hands cluster traffic over from `127.0.0.1` or `127.0.0.6` carries a service-name Host, so it gets `shared`, never the owner's rights (and it is not a 400). **A loopback-remote request that carries any of `X-Forwarded-For`, `Forwarded`, `Via` or a header starting `X-Envoy-` is `shared` too, whatever its Host**: a real `port-forward` or `exec` never sets them and a proxy does, and a proxy that rewrites Host to `localhost` would otherwise pass as the owner. **Under the `shared` rule** a request reaches only `/sindri/mcp` (every method and the variants that route) and `GET /sindri/ping`, decided from the endpoint already chosen: its metadata is `McpRouteMetadata`, or a new `SharedPingRouteMetadata` on the ping. Every other request, including no endpoint at all, gets a **404** and goes no further. For a non-loopback remote it is **bare** (the same answer an unknown path gets, so the route list isn't disclosed). For a **loopback remote whose Host is neither loopback nor listed** (the owner's tunnel by machine name, `kubectl port-forward --address 0.0.0.0`, a mesh) the 404 carries a short problem body, `SND-API-017`, that names the rule: "This request reached Sindri from inside the pod with a Host Sindri does not treat as yours. Use Host localhost, 127.0.0.1 or [::1]." Nothing is disclosed that the sender, who is inside the pod, could not already see, and an owner who typed the wrong name is told why every route is gone. Then, for the two it admits: Host must be one of `AW_SINDRI_MCP_HOSTS` (the Service names callers use; the port is stripped before the comparison; the chart renders the list and its probes' Host from the one value `service.hosts`, whose default is the Service's short, namespaced and cluster-local names), else 400 `SND-API-016` whose text names the allowed hosts (accepted under K5: they are cluster Service names, no secret); any `Origin` is refused, 403 `SND-API-008` (no browser page may call it; `UseMcpCaller` already refuses every Origin on `/sindri/mcp`, loopback ones included). **Test:** an integration test walks every endpoint in the `EndpointDataSource` (every route pattern with its parameters filled in, every method its `HttpMethodMetadata` lists, plus case, trailing-slash and encoded variants) with a **non-loopback remote** and expects the 404 for all but those two. It also walks them with a loopback remote and a loopback Host and expects today's behaviour, and runs the mesh cases: a remote of `127.0.0.6` with a service-name Host is `shared`; `127.0.0.1` with Host `sindri.<ns>.svc` is `shared`; `127.0.0.2` with `Host: localhost` is not the owner; `127.0.0.1` with `Host: localhost` and `X-Forwarded-For` (then `Forwarded`, `Via`, `X-Envoy-Foo`) is `shared`; a loopback remote with Host `laptop.example.com` gets the 404 with `SND-API-017`'s body and a non-loopback remote gets the bare 404. A unit test keeps the `::ffff:127.0.0.1` unwrap case (an IPv4-mapped loopback is loopback; `::ffff:127.0.0.6` is not). A new route therefore can't become reachable from the cluster by accident. The chart sets the Istio and Linkerd opt-out annotations on the pod by default (`sidecar.istio.io/inject: "false"`, `linkerd.io/inject: disabled`) through `podAnnotations`, a chart value the deployer extends for any other mesh (Cilium, Consul and Kuma use other keys); deploy.md says not to mesh this pod and that any proxy in front of it must not rewrite Host. Doctor warns when a loopback-remote request carried a forwarded header or a service-name Host. `service.hosts` must never hold a loopback name (`localhost`, `127.*`, `::1`): the schema pattern and the render reject it (it would not grant owner rights, since the remote test still applies, but it is a misleading setting) |
| `GET /sindri/ping` | Loopback remote: the existing `PingView` (pid and lease epoch, which `control.json` checks and `sindri-pod.sh connect` reads). Any other remote: a new small record, `{ epoch, lagSeconds }`, never the pid. The chart's readiness and liveness probes use it with a `Host` header from the list. It stays up while `share.enabled` is false (the probes need it). It is **outside the 16-call limiter** (below), so a busy door can't fail the liveness probe; it has its own small cap (4 in flight, a 1 s deadline), and a test holds the limiter full and still gets the ping |
| Caller (`shared`) | A request admitted under the `shared` rule is the new client **`shared`**: `ClientLabel.Shared`, `ContextClient.Shared` (JSON `shared`), the actor `session:shared` that `CallerContext.ActorOf` builds, and the `shared` arm of `ContextScopes.Client(actor)` (which today maps an unknown label to `http`); `FencePolicies.For` gives it the same rules as `mcp` (repo-scoped, labels fenced `<untrusted source="map">`, never a command). `UseMcpCaller` already drops every `X-Sindri-*` header but `X-Sindri-Repo` and sets `X-Sindri-Client: mcp`; for a request admitted under the `shared` rule it instead stores the caller on **`HttpContext.Items`**, from the connection, never from a header, and `CallerContextMiddleware` reads it from there. No header can say `shared`; one test asserts that a loopback caller writing `X-Sindri-Client: shared` gets nothing. A `shared` caller is always a session, can never be a human, and cannot reach a command route (the guard has already refused every route but two) |
| Admission | `ScopeRules.Resolve` today refuses every session unless `context.sessions.enabled` (`SND-CONTEXT-020`) and gives a session only the repo it names. A `shared` caller is admitted by **`share.enabled`** instead, in the same place, with the same repo rule, so opening the door doesn't also open the owner's loopback sessions. The repo must be in the approved profile; one that isn't answers `SND-CONTEXT-091` (new: "no such repo here"; checked before the engine is called). The body lists nothing, so a caller can't list the repos, though it can confirm a name it guesses; with K5 that is no secret worth more. Repo names compare exactly as the profile spells them. An integration test runs the door against the profile exactly as the chart ships it |
| Tools | `McpToolTable` is one static 12-row list and `List` answers every tool "whatever the profile says" (context spec §8.6); `Call` already reads the caller from `ICallerContextAccessor`. Both now choose the rows by caller: for a loopback caller the 12 as today (goldens unchanged); for `shared` the five `context_*` tools (`context_query`, `context_lenses`, `context_node`, `context_neighbors`, `context_sources`; all `ReadOnlyHint`; the same implementations, so answers and limits are the same), plus the six `sindri_*` reads (`sindri_status`, `sindri_queue`, `sindri_item`, `sindri_item_timeline`, `sindri_pending_decisions`, `sindri_job`) **only when the profile's `share.workQueueReads` is true (flagged)**, and **never `sindri_request`**. A tool not offered answers as unknown (`SND-CONTEXT-023`, as an unknown name does today, with its `shared` text from §8), not as a refusal that confirms it exists; a test calls `sindri_request` as `shared` with the key on. `context_query`'s `item`, `symbol` and `diff` arguments (they reach Sindri's work items and checkouts, not the map) are left out of the `shared` schema and refused as unknown arguments (`SND-CONTEXT-022`, its `shared` text in §8). The `shared` goldens differ from the loopback goldens in these ways only: the tool list, the business-context filter, the refused arguments, no model steps (K12), and `context_sources`' reduced fields |
| Business context (K5) | **New engine work, not an existing rule**: `ScopeRules.MayRead` today denies only `SourceKind.Intent`, and linked intent is shown to every session by the owner's decision of 2026-10-15. While `share.businessContext` is false (the default) a `shared` caller gets **code and docs only**, applied at **one choke point below the tools**: a default-deny view of `GraphSnapshot` (a filtered `RepoScope`) built for the `shared` caller. It keeps only what came from the named repo's own code and docs sources, by `SourceId` and source kind (its code index, its docs index, and map nodes whose source is one of those), and drops anything of a kind it doesn't know. All five tools read the map only through that view, and the Onyx query isn't made at all. Edges, neighbours and counts that lead to a dropped node are dropped with it; `context_node` on such a node's key, or on another repo's node, answers as not found rather than being filtered after the read; a decision that quotes a dropped record has its quote withheld. The golden test holds one node of every `NodeKind` and `RecordKind` from every source kind **and covers every free-text field of every tool's answer** (labels, notes, error texts, the aggregates of `context_lenses`, the counts of `context_sources`), and shows only the repo's code and docs surviving. An answer that dropped something says so in a note ("business context withheld for this caller", never a count or a source name), and answers carry the map's existing state notes. `context_sources` shows each source's name, kind and state only: no URL, no paths, no token pointers, no instance build or port, no other repos' names, and only the repo's own code and docs sources unless the switch is on. This is `Aw.Context.Core` code, the context lane's package, built in K-B1 with a **context-lane reviewer**. **The default is the lead's reading of the owner's earlier answer and is flagged again in §1** |
| Limits | Counted only for `shared` callers (the owner's calls are never queued behind them, and the ping is outside the count): 16 calls in flight in total (more wait, up to 32 waiting; beyond that 429 `SND-API-014` with `Retry-After`), a per-call deadline of 30 s, and a 64 KiB request body cap set per request through the body-size feature. The tools' own budgets are unchanged. With no caller identity one noisy caller could take all 16, so there is an optional **per-source-address cap** in middleware (profile key `share.perSourceCap`, **default 0, which is off**; when set, it refuses the next in-flight call from one source address with 429 `SND-API-015` and `Retry-After`, ahead of the global limit). It keys on the connection's source address only. **Behind the internal MCP server (the intended path, §C.3) every call comes from that one address, so there the cap limits everyone together, not each caller**: leave it off, and set it only when callers reach the Service directly (each pod its own address); fairness behind a proxy is the proxy's job, and deploy.md says so. A test sends 20 calls from one address with the cap at 8 and expects 8 admitted and 12 refused with `SND-API-015`, and with the cap at 0 expects only the global limit. The refusal log's source address finds the caller. **Not carried over from revision 2:** Kestrel limits of a separate listener (`MaxConcurrentConnections`, a smaller header limit, a body-rate floor): Kestrel's limits are per server, so changing them would also bind the owner's event stream and commands; Kestrel's defaults (30 s to send headers, a minimum request data rate) already apply to all and stay. While K12 is "no model", the model-backed parts of `context_query` (rewrite and adjudication) are skipped for `shared`, so a query never starts a model call in the pod. Queries take priority over index builds for CPU |
| Observability | Calls are counted per repo and per refusal code and exported as metrics (`sindri_shared_calls_total{repo,tool,outcome}`, `sindri_shared_inflight`; `repo` is a served repo's name or `other`, never the caller's text), and each refusal is one JSON log line with the code, the repo (truncated) and the source address, at most one line a second per code; a read writes no ledger row and there is no audit of what was read (no identity to record, K5). Successful calls are also counted by source address at debug level (`AW_LOG_LEVEL=debug`), so after a mistake the owner can answer "which pod read repo X"; no content is stored. Metrics go nowhere unless the deployer sets an OpenTelemetry endpoint in the values |
| No authentication (K5) | There is no `Authorization` check, no token store and no minting. The Host allowlist and Origin refusal are not access control: they keep a browser page (DNS rebinding) and stray hostnames out, nothing more. Who may call is decided by who can reach the Service's port: per K3, any pod in the cluster. deploy.md states this and the consequence (K5) in its first section |
| Repo | `X-Sindri-Repo`, set by the internal MCP server per registered entry, as `sindri mcp register` sets it locally; a call without it is `SND-CONTEXT-021`, as today (`McpToolTable.Call` checks it) |
| Transport and restarts | Plain HTTP inside the cluster is accepted (K3, K5): code snippets cross the cluster network in clear text, and deploy.md's first section says so. A deployer who wants TLS terminates it at the internal MCP server or in front of the Service; this pod is not meshed (above). **Onyx down:** the existing `SND-CONTEXT-010` (degraded answer, not an error), even with an Onyx container crash-looping, because the Service publishes not-ready addresses (§A.3 Start order); a heavy test kills OpenSearch and checks it. **Restarts:** one pod means the door is down while Sindri restarts (an upgrade, a node drain); callers see connection refused, and the internal MCP server reports the tool unavailable. The kind run records the restart-to-first-answer time, with a target under 2 minutes, recorded and not gated (a volume re-attach and Onyx's migration are on the path); readiness passes as soon as `serve` answers and is not tied to the map's lag (a stale map still answers, with its stale note) |

The doctor rule that `sindri` is never registered user-wide stays for laptops: the cluster's server is a different name
(`sindri-context`) on the internal MCP server, and the doctor check only reads the local agent configs.

**What a cluster agent sees when the answer isn't an answer.** These are the `shared` caller's situations; the verbatim texts are
in §8, once. A fix text shown to a `shared` caller never names an owner-only command (`sindri mcp register`, `context map import`,
`profile approve`, `context map build`): it says what is happening and when to retry, or "ask the owner of this Sindri to …". That
is why `SND-CONTEXT-021`, `-022`, `-023` and `-030` get a `shared` variant of their text.

| Situation | What the agent gets |
|---|---|
| repo not served, or sharing off | `SND-CONTEXT-091`; the same text for both, on purpose |
| the call names no repo | `SND-CONTEXT-021`, `shared` text |
| an argument the `shared` schema doesn't have | `SND-CONTEXT-022`, `shared` text |
| tool not offered | `SND-CONTEXT-023`, `shared` text |
| repo approved, **first clone running** | an answer, not an error, with the state note `setting up` (the same note as the first build) |
| repo approved, first map build running | the same `setting up` note |
| repo approved, no map and none being built (the first build did not start or did not finish) | `SND-CONTEXT-030`, `shared` text |
| built, **repo can't be updated** (the clone failed, or main diverged, or the checkout is dirty: `SND-REPO-001..003`) | the answer with the state notes `stale` and `not updated` |
| built, empty result | an empty list and the `nothing found` line |
| built, behind main | the answer with the state note `stale` and `lagSeconds` |
| business context withheld (switch off) | the answer plus the `withheld` note (no count, no source) |
| Onyx down, switch on | the code and docs answer plus the existing note that Onyx is unavailable (`SND-CONTEXT-010`); with the switch off Onyx is never asked, so its being down is invisible to the agent |

The new codes (`SND-CONTEXT-091`, `SND-API-014`, `SND-API-015`, `SND-API-016`, `SND-API-017`) and their fix texts are in §8.

### B.3 Onyx on the pod's loopback: unmanaged, attached

Nothing changes in the query adapter. Onyx's `nginx` answers on `http://127.0.0.1:<onyx.port>` (§A.3), so `OnyxSettings.CheckUrl`'s
loopback rule (`localhost`, `127.*`, `[::1]`) passes as it is. The profile's section is the unmanaged shape, `context.sources.onyx`
with `url: http://127.0.0.1:<onyx.port>`, `token: <secret pointer>`, `version: v4.9.0` and `managed: false` (a managed one must be
exactly `http://127.0.0.1:<port>` and is started by `serve`; an unmanaged one is never touched, `OnyxServeHooks`). The pod needs
**the account stage only**, so the verb is small:

- `sindri onyx attach [--url http://127.0.0.1:<port>] [--adopt]` (default URL from `AW_SINDRI_ONYX_URL`, which the chart sets). A **protected
  verb** like `onyx up`: a human at a terminal only (an agent session or a non-terminal gets `SND-AUTH-002`, exit 1). It checks that
  Onyx answers its health route and reports the release `context/onyx.lock` pins, then runs the same account stage as `onyx up`
  (the owner's sign-up in the browser, Sindri's dedicated user and token, the profile section above). It **proposes** that section
  and never approves it: the install order (§C.3) runs `attach` before the one `profile approve`, so the Onyx source, the repos and
  the switches are approved once. It starts, pulls and writes nothing in Onyx's workspace, and is safe to re-run.
- **The first sign-up is Onyx's admin.** Between the pod starting and `attach`, any container of the pod could register first.
  `attach` therefore refuses with `SND-ONYX-014` when Onyx already holds an account that this setup did not create: it counts
  Onyx's users before the sign-up step; zero is fine, and one is fine only if it is the account `attach` recorded on Sindri's
  volume (`onyx-attach.json`, owner-only) at an earlier, dropped run. The chart's NOTES print `connect --onyx` as the first step
  after install. If v4.9.0 has a setting that closes sign-up after the first account, K-S0 records it and the chart sets it.
- **`attach --adopt`: an Onyx whose account an earlier Sindri volume made** (the volume was lost, or restored from an older
  snapshot than Onyx's, which is a normal mishap). `SND-ONYX-014` is not a reason to wipe anything. With `--adopt`, at a terminal,
  the owner is asked for the existing admin's email and password (not shown, never stored, in no log, file or argument, as
  `onyx up` asks for them), `attach` logs in with them, checks the account is an admin, and then runs the same steps as a first
  attach to make Sindri's own user and a **fresh token**; it records the account in `onyx-attach.json`. It never deletes or
  changes anything else in Onyx. If the owner did not make that account, they must not adopt it: the only fix is an empty Onyx
  claim, which deletes everything Onyx holds (its data, its connectors and their OAuth credentials), and the fix text says so.
- **Already attached** means all three hold: the recorded token still resolves and Onyx takes it as an admin, Onyx answers its
  health route, and `/api/version` equals the release `context/onyx.lock` pins. Then `attach` says so ("Onyx is already attached
  (release v4.9.0). Nothing to do.") and exits 0, without waiting for Enter. After an Onyx upgrade (§C.5) the version differs, so it
  is **not** a no-op: it says "Updating the pinned version to v4.9.x", rewrites the profile section's `version` and proposes it for
  the next approval.
- **How it ends.** Every run ends with a last line and the next step. Attached: "Onyx is attached. Next: allow your git host with
  `sindri-pod.sh profile edit`, add repos with `sindri repo add --url …`, then approve once." Exit codes: 0 attached or already
  attached; 1 refused or failed, with the code printed (`SND-AUTH-002`, `SND-ONYX-014`, …); 2 usage or a tool error.
- **Onyx off.** With `AW_SINDRI_ONYX_ENABLED=false` `attach` refuses with `SND-ONYX-015` ("This install runs without Onyx
  (onyx.enabled=false). Set it to true and run helm upgrade to add Onyx."), and the approve preview says business context is
  unavailable (§C.3).
- **The address printed at sign-up** is the owner's forwarded one: `connect --onyx` passes it in `AW_SINDRI_ONYX_FORWARD_URL`
  through the exec; with that unset (a plain `kubectl exec`) `attach` prints the in-pod address and says to port-forward it.
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
port (`service.enabled`, default true, `publishNotReadyAddresses: true`; the owner's `connect` forwards the same port to the pod
directly, and Onyx's port has no Service, because it binds loopback only); `terminationGracePeriodSeconds: 120` (§A.1); every
container's requests equal to its limits, from values (§A.3); and the probes: for Sindri a startup probe (generous, for the first
volume setup), a readiness and a liveness probe on `GET /sindri/ping`, and for Onyx's containers liveness probes only (§A.3 Start
order). **One value, `service.hosts`, renders both `AW_SINDRI_MCP_HOSTS` and the `Host` header of Sindri's probes**, so they can't
disagree; a `helm template` test asserts the probe Host is in the list, and doctor lists the hosts. The pod carries `podAnnotations`, whose defaults are the mesh
opt-outs `sidecar.istio.io/inject: "false"` and `linkerd.io/inject: disabled` (B.2); a deployer adds the keys of any other mesh. Logs are JSON lines on stderr
(`AW_LOG_LEVEL`), read with `kubectl logs -c <container>`. Telemetry export goes to `telemetry.endpoint` when set. Sindri's and
Ollama's security contexts are non-root, read-only root, **all capabilities dropped**, no privilege escalation, default seccomp;
Onyx's containers get the strictest context their image allows, all capabilities dropped and only the proven ones added back
(§A.3, recorded per container by K-S0). **Nothing is privileged.** Values hold every deployer-specific thing: registry, namespace,
the git hosts' credentials (one Secret key per host; the repos themselves are added with `sindri repo add --url`, which writes the
profile, and the hosts `repo-pull` may use are a profile key, §C.2; a changed Secret needs a pod restart), `service.hosts`,
storage class, resources, `onyx.platform`, `onyx.port`, `telemetry.endpoint`, `podAnnotations`, `git.credentials` (below) and `onyx.enabled` (default `true`; `false` leaves every
Onyx container, the `onyx-data` claim and its Secret out and sets `AW_SINDRI_ONYX_ENABLED=false`, for a pod that serves code and docs only and is smaller).
**The chart's values never set profile keys**: `share.*`, `index.graph.enabled`, `repo-pull.allowedHosts` and the rest live in the
profile only and change with `sindri-pod.sh profile edit` (§C.3); the chart's own kill switch is `service.enabled: false` and `helm upgrade`. The chart ships a `values.schema.json` (Kubernetes **1.29 or later**, the version the chart is
tested on and the kind run is pinned to), an example values file, and a `helm lint` plus `helm template` test in CI (rendered
against the example, checked for the security contexts of §A.3 and §C.1, for "no `privileged`, no host path, no Docker socket"
anywhere, and for "every image is a digest from a lock"). The Kubernetes Secret for git is mounted into Sindri's container only.
There is no PodDisruptionBudget, no snapshot CronJob and no NetworkPolicy in v1 (§11).

**The chart arrives in two steps.** K-C1 ships Sindri and Ollama with `onyx.enabled: false` and does not wait for K-S0: it is the
first thing the kind test runs. It is not the "everyone can query the code and docs map" milestone by itself: **milestone 1 = K-A1 +
K-B1 + K-C2 + K-C3 + K-C1** (no Onyx): the image, the door, the repo freshness, the owner's connect and profile edit, and the chart.
The owner's check can run then, on a small node. K-C1 takes its Sindri and Ollama resource numbers from K-A1's `docker run`. K-O1 adds
Onyx's rendered containers once K-S0 has reported.

Install with `helm install --timeout 30m` and **without `--wait`**: Helm returns at once and the kubelet carries on pulling about
11 GB of images in the background; `scripts/sindri-pod.sh connect` then waits for Sindri's container to be ready, which is the step
that matters. `--wait` is wrong here on purpose: it waits for the pod to be Ready, and an Onyx container that loops keeps the pod
from Ready while Sindri works, so it would fail an install that is fine. Because the Service publishes not-ready addresses
(§A.3), Sindri's readiness probe decides only the pod's Ready condition, which is what `helm --wait` and `kubectl rollout status`
read, and not routing. The chart's NOTES say all of this, print the install command, and print `kubectl get pod -w` and
`kubectl describe pod` to watch it.

**From a Secret key to the credential pointer.** A git credential is a Secret key that becomes one environment variable in
Sindri's container only, and the profile points at the variable with `env:NAME`. The values file maps one to the other:

```yaml
git:
  credentials:
    - host: git.example.com        # must also be in the profile's repo-pull.allowedHosts
      secret: sindri-git           # an existing Secret in the namespace
      key: token                   # its key
      env: GIT_TOKEN_EXAMPLE       # the variable it becomes in Sindri's container
```

and the repo is then added with `sindri repo add --url https://git.example.com/team/repo.git --credential env:GIT_TOKEN_EXAMPLE`.
The chart test checks that every `env:` pointer in the example profile names an `env` of the example values. A changed Secret
needs a pod restart.

**What `kubectl get pod` shows, and what it means** (chart NOTES and deploy.md; doctor says the same where Sindri is up):

| The pod shows | What it means | What to do |
|---|---|---|
| `ContainerCreating` or `Pending` for minutes | the kubelet is pulling ~11 GB of images, or a claim isn't bound | `kubectl describe pod`; wait, or fix the storage class |
| `ImagePullBackOff` | the registry, a digest, or `onyx.platform` doesn't match the nodes | `kubectl describe pod` names the image; check the platform value |
| `CrashLoopBackOff` on `sindri` | Sindri is down, so there is no door | `kubectl logs -c sindri` |
| `CrashLoopBackOff` on an Onyx container (say `opensearch`) | **Onyx is down and Sindri is up**: the door still answers code and docs, and a business-context query gets Onyx's degraded answer. The pod reads not Ready, but the Service still routes | `kubectl logs -c opensearch`; `doctor` names the container whose port doesn't answer |
| `Running` and Ready, but Onyx misbehaves | Onyx containers have no readiness probes, so Ready says nothing about Onyx | `sindri doctor` (its Onyx rows) |
| `OOMKilled` | a container's memory limit is too small | raise that limit (the schema's minimum is the floor) |
| `Evicted` | node pressure | use a bigger node; the pod is Guaranteed QoS, so it is the last to go |

Onyx needs no values file of its own beyond `onyx.*` above: the script renders its containers from its pinned Compose file. Its
admin sign-up page is on the pod's loopback only, so no other pod in the cluster can reach it; inside the pod, Sindri, Ollama and
Onyx's other containers could, so the owner makes the admin account right after install, with `connect --onyx` (§C.3), before
connecting any source, and `attach` refuses an account it did not expect (B.3). The kind run records the whole footprint (CPU,
memory, disk of every container) in `docs/sindri/deploy.md` so the owner can size the real cluster's node.

### C.2 The repo service (engine lane)

`repo-pull` (a Sindri service, on by default in pod mode and opt-in elsewhere like `context-docs-refresh`, every 1 minute; at most one refresh per repo in flight, moves of main while one runs are coalesced into one follow-up, and the index, map and docs refresh keep a minimum interval of 5 minutes per repo, so a busy main doesn't queue endless rebuilds on the one writer):
- for each profile repo with a `pull:` block (`{ url, credential: env:NAME }`): the first time, `git clone --no-hardlinks` into
  `/data/repos/<name>` (the profile's `path` must be under `AW_SINDRI_REPOS_DIR` in pod mode); after that `git fetch origin
  <default branch>` and `git merge --ff-only`; a dirty tree or a non-fast-forward is a doctor row and a needs-you row, never a
  reset; the credential is passed through `GIT_ASKPASS` reading the pointer, never in a URL or an argument, and the helper answers only for the host of that repo's URL; the repo `<name>` must match `[a-z0-9][a-z0-9._-]*` and resolve under `AW_SINDRI_REPOS_DIR`; the URL must be https (v1; an ssh key would need a secret and a `known_hosts` file, a later addition) and its host in the profile's `repo-pull.allowedHosts` (profile-only: changing it is a profile change the owner approves, not a chart value), `--` precedes it on every git call, and git runs with `protocol.file.allow=never`, `http.followRedirects=false`, hooks off (`core.hooksPath=/dev/null`) and submodules off; the credential should be read-only (doctor can't check that; deploy.md says so);
- `code-index-main-move` then sees HEAD move and indexes, and the map and docs refresh follow, as on a laptop. The freshness number is the map's lag behind main, shown in doctor and as `lagSeconds` in the non-loopback `GET /sindri/ping` response, and exported as a metric (telemetry export is on in pod mode, off by default elsewhere); a lag over 15 minutes is a doctor warning.

v1 pulls and indexes each repo's default branch only: an agent working on a feature branch gets main's map, which deploy.md says.

`sindri repo add` in pod mode takes `--url` and the credential pointer instead of an existing path, and writes the `pull:` block.

### C.3 The owner's steps

| Step | How | One click? |
|---|---|---|
| Install | create a namespace labelled `pod-security.kubernetes.io/enforce=baseline` (or stricter, §A.3; the chart's NOTES name the level the rendered containers need), then `helm install --timeout 30m` (no `--wait`, §C.1) the one release with the deployer's values. The kubelet pulls about 11 GB of Onyx images, so the pod takes minutes to become Ready; Sindri is first in the pod, so it starts as soon as its own image is there, and no Onyx container gates it | one command each |
| Open the app on the pod | `scripts/sindri-pod.sh connect [--context C] [--namespace N]`: `kubectl port-forward` to the pod's loopback API port, `kubectl exec … sh -c` reading the pod's `control.json` and `auth/human.token` (no new verb; built in K-C3) for a fresh human token **and the pod's current pid and lease epoch** (the app accepts a `control.json` only when its `Pid` and `Epoch` match `/sindri/ping`, which on loopback still carries the pid), a local mirror home (`control.json` with the forwarded loopback URL, pid, epoch, the token file, all 0600) and `sindri-ui` started on it. The app needs no change: it still reads a loopback URL and a token from a home. At the start it waits (up to 10 minutes, printing the container's state every 15 s) for the `sindri` container to be ready. When the port-forward drops or the pod restarts (a new epoch and a new human token), the script retries and rewrites the mirror home, and the app shows its existing "control plane not running" state meanwhile, while the script prints "pod restarting, reconnecting (attempt n of 30)" and gives up after 5 minutes with exit 3. The mirror home is removed on exit, Ctrl-C included (a trap; a crash leaves it under `$TMPDIR`, 0700, and the next run removes stale ones). Prerequisites: `kubectl` with exec and port-forward rights on the namespace, and `sindri-ui` installed (`sindri-app`); `connect --check` reports each one. Each failure has one line and exit 2: kubectl missing or no rights (names the verb refused), Sindri's container not running (names its state and says `kubectl logs <pod> -c sindri`; the **pod's** Ready state is not checked, because an Onyx container that loops is normal and Sindri still works, so with Onyx not answering `connect` carries on and prints "Onyx: not answering, see sindri doctor"), the local port taken (names it and `--port`), `sindri-ui` not found (names `sindri-app`). It prints "connected: <pod> at <url>; Ctrl-C to stop" on success. Its own exit codes: 0 a normal end (Ctrl-C), 2 a prerequisite or start failure, 3 gave up reconnecting | one command |
| Approve the profile, recipes | in the app (its existing typed confirmations), or `kubectl exec -it sindri-0 -c sindri -- sindri profile approve`. **Once, after `connect --onyx`**, so the one preview shows the Onyx source, the repos and the switches | one click and the typed word, as today |
| Change a profile key (a sharing switch, `repo-pull.allowedHosts`, `share.perSourceCap`, `index.graph.enabled`) | `scripts/sindri-pod.sh profile edit [--no-approve]` (K-C3). The profile is a file on the pod's volume and the image has no editor and a read-only root, so this is the only way to change or undo a key. It finds the profile file in the pod, copies it to a temporary file on your machine (`kubectl exec … cat`), opens `$EDITOR` (`vi` if unset), copies the result back to a temporary path in the pod and checks it there with `sindri profile validate --file <path>` (a small addition K-C3 makes; exit 2 means invalid, and the editor reopens with the error), replaces the live file only if the pod's copy did not change meanwhile, and then runs `kubectl exec -it sindri-0 -c sindri -- sindri profile approve` so the preview and the typed word are yours. `--no-approve` saves the file and stops (the first install, where the one approval comes at the end). A change applies on the next request once approved | an editor and the typed word |
| Onyx | `sindri-pod.sh connect --onyx` does everything `connect` does (the app opens too) and forwards **Onyx's port** from the pod to the owner's machine **on the same local port as the pod's `onyx.port`** (8081 by default, read from the pod's `AW_SINDRI_ONYX_URL`), because Onyx's `WEB_DOMAIN` is `http://127.0.0.1:<onyx.port>`, so its sign-up redirects and connector OAuth callbacks come back to that address. A taken local port is one line, "local port 8081 is in use: stop what uses it, then run this again" (exit 2). `AW_SINDRI_POD_ONYX_PORT` overrides the local port and prints a warning ("sign-up redirects and connector OAuth may land on port 8081, not <chosen>"); K-S0 tests sign-up and one OAuth round trip through a different local port (§7). It **prints the forwarded address before it runs `attach`** ("Onyx's page: http://127.0.0.1:8081"), then runs `kubectl exec -it sindri-0 -c sindri -- sindri onyx attach` at the same terminal with that address in `AW_SINDRI_ONYX_FORWARD_URL`. `attach` checks Onyx answers and is the pinned release, then runs the account steps; at the sign-up step it prints the forwarded address, the owner signs up in the browser and presses Enter. Nothing is pulled or started (the kubelet did that at install), so it takes seconds. `attach`'s failure codes (for example `SND-ONYX-014`, an account already exists that this setup didn't make) are printed unchanged, followed by `attach exited <n>` on stderr (the script's own exit codes are 0, 2 and 3, so the two never mix), and **the app stays connected**; Ctrl-C during `attach` stops that command only. An already-attached, healthy Onyx makes `attach` a no-op that says so, exit 0. A dropped exec mid sign-up (a laptop that slept) is fixed by running the same command again; `attach` picks up where it was. It ends with attach's last line and the next step (§B.3). With Onyx off (`onyx.enabled: false`) it refuses at once: "Onyx is off in this install (onyx.enabled=false); use connect.", exit 2 | one command, one browser sign-up, one Enter |
| Connect sources | in Onyx's page (Linear, Slack, GitHub, docs), the connector checklist `onyx attach` prints. It warns against internal URLs (`localhost`, `127.0.0.0/8`, `::1`, the pod's address, `*.svc`): Onyx's web connector shares this pod's loopback (§6). These records reach the cluster only with `share.businessContext: true` (K5) | the owner's OAuth, in Onyx |
| Give everyone access | register the existing MCP port in the internal MCP server, one entry per repo, no secret: for example `{ "name": "sindri-context-<repo>", "url": "http://<service>.<ns>.svc:7410/sindri/mcp", "headers": { "X-Sindri-Repo": "<repo>" } }`. `sindri share entries --url <service-url>` (read-only, any caller; the URL is the deployer's, which the pod can't know) prints these entries as JSON for every served repo, exit 0 (with none served: `[]` and one line on stderr, "no repos served: approve repos, and check share.enabled"). One entry per served repo (named `sindri-context-<repo>`, so an agent sees each repo's tools once per repo). Each new repo is three owner steps (`repo add`, approve, paste its entry), since the header is fixed per entry. To check a registration: `curl -H 'Host: <service>' http://<service>:7410/sindri/ping` from any pod answers `{ epoch, lagSeconds }`. Behind this one entry every call comes from the internal MCP server's address, so leave `share.perSourceCap` at 0 (off): the per-source cap there would limit the whole company together (§B.2) | one paste |

The scripts' terminal output follows `NO_COLOR` and prints no spinner or colour when stdout is not a terminal; the bash tests
check both for `connect`, `connect --onyx`, `profile edit` and the lines `attach` prints.

The first install, in order (times from the kind run go into deploy.md): create the namespace and `helm install --timeout 30m` (no
`--wait`: it returns at once and the image pull carries on); `connect --onyx` for the admin account (it waits for Sindri's
container, then seconds; plain `connect` when Onyx is off) **before anything is approved**; `profile edit --no-approve` to set
`repo-pull.allowedHosts` (and any sharing switch you want on) **before** `repo add`, which refuses an unlisted host
(`SND-REPO-005`); add the repos (`sindri repo add --url … --credential env:NAME`); approve the profile and recipes **once**; the
first clone and index (minutes per repo; done when doctor's map row for the repo is ok and `/sindri/ping`'s `lagSeconds` is a
number); register the MCP port (one paste per repo); then, when wanted, Onyx's connectors.

**The approve preview's texts, verbatim** (`<repos>` is the served repos; the doctor warn rows use the same words). With the
sharing switch off, the preview says "Sharing is off: nothing is published to the cluster." Otherwise, by the two switches:

| `share.businessContext` | `share.workQueueReads` | The preview says |
|---|---|---|
| off | off | "Approving publishes the code and docs maps of <repos> to every pod in the cluster. Chat and tickets stay private. Your work queue stays private." |
| on | off | "Approving publishes the code and docs maps of <repos> to every pod in the cluster. It also publishes every chat message and ticket record Onyx holds, private channels its connector can see included. Your work queue stays private." |
| off | on | "Approving publishes the code and docs maps of <repos> to every pod in the cluster. It also lets every pod in the cluster read your items, queue and pending decisions for those repos. Chat and tickets stay private." |
| on | on | "Approving publishes the code and docs maps of <repos> to every pod in the cluster. It also publishes every chat message and ticket record Onyx holds, private channels its connector can see included, and lets every pod in the cluster read your items, queue and pending decisions for those repos." |

With Onyx off (`AW_SINDRI_ONYX_ENABLED=false`) the chat-and-tickets sentence is replaced by "Chat and tickets are unavailable: Onyx is off in this install.", and `share.businessContext` is flagged "no effect, Onyx is off" in doctor and the preview.

### C.4 Network rules and who can read what

Per K3 there is no NetworkPolicy on the namespace by default, and the chart ships none in v1 (a reversible default, §11): any pod
in the cluster may reach the API port's Service, and the port itself decides what a non-loopback caller may do (§B.2). The owner's
routes, Temporal, Ollama and every Onyx container listen on the pod's loopback only, so they are reachable from inside the pod and
through `kubectl port-forward` and `exec`; the listener settings of §A.3 and the guard of §B.2 keep them the owner's. deploy.md
says how a deployer adds a policy that admits the API port only. `kubectl exec` and `port-forward` rights are the owner's power
(they reach the human token, the ledger and Onyx's admin page), and so are volume-snapshot read rights and the right to create a
pod or claim in the namespace (they reach the human token, the ledger, Onyx's database with the connector OAuth credentials, and
Sindri's Onyx token). **The namespace is the owner's alone**; deploy.md says so in its first section, next to "approving a repo
publishes it", and says to use an encrypted StorageClass where the cluster has one. A deployer who wants egress rules lists the git
and forge hosts, Onyx's registries and connector services, and the model hub (§A.3).

### C.5 Upgrading and getting back

`docs/sindri/deploy.md` gains: upgrading Sindri (a new image digest in the values, `helm upgrade`, the pod restarts and takes the
lease; Onyx's containers restart with the pod, so Onyx answers degraded for a few minutes; the only downgrade is the previous
digest plus, if state moved, a snapshot restore), upgrading Onyx (**bump the pin in `context/onyx.lock`, `onyx-lock.sh verify`,
refresh `deploy/onyx/docker-compose.yml`, run `node scripts/onyx-pod.mjs`, read the diff, build a new Sindri image that embeds
the new lock, `helm upgrade`, then `sindri onyx attach` again so the profile's pinned version follows**; doctor's `/api/version`
row shows a mismatch until it does; Onyx's database migrations run forward at `api_server` start, so the only way back is a
snapshot restore), and the manual backup and restore of K7 (snapshot both claims at the same moment, restore both from the same pair, so the map's Onyx cursor and Onyx's database match; the heavy test runs this restore once).

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
  connector pointed at `http://127.0.0.1:7410/…` would read Sindri's own API as a session. **Accepted for v1** under the
  honest-mistake model (pinned prebuilt images, connectors chosen by the owner); mitigations: the connector checklist says to give
  the web connector only public URLs (and warns against `localhost`, `127.0.0.0/8`, `::1`, the pod's address and `*.svc`), Temporal runs headless in pod mode (no UI port), K-S0 records whether Onyx's own web-connector guard already blocks loopback, including through a redirect to loopback and through a DNS name that resolves to `127.0.0.1`, the loopback `sindri_request` stays because the owner's rights are unchanged, the session reads Onyx's containers could make are the ones any agent session on a laptop can
  make, and a later spec could put Sindri's API behind a Unix socket or a token for sessions (K-I1). The alternative, noted: Onyx
  in its own pod, which `OnyxSettings.CheckUrl`'s loopback rule forbids today (it is the rule `network: cluster` would have bent).
- **What listens on the pod's address** (reachable by any pod, K3): only the API port. Every Onyx container, Temporal
  (`--ip 127.0.0.1`), Ollama (`OLLAMA_HOST`) and Sindri's own admin routes (refused by endpoint, remote address and Host) are loopback-only.
  Whether each Onyx image honours its loopback setting is **unverified until K-S0**, which runs `ss -ltn` in every container and
  scans the pod's address from another namespace, and must find only the API port.
- **Secrets** live in Kubernetes Secrets and reach Sindri as `env:` pointers; the image and the chart's values hold none; Onyx's
  secrets are a chart-made Secret mounted into Onyx's containers only; the Onyx token Sindri makes stays on the encrypted volume,
  owner-only.
- **Snapshots and claims are the secrets store.** Anyone who can create a pod or a claim in the namespace, or read a volume
  snapshot, reads the human token, the ledger, Onyx's database (the connector OAuth credentials) and Sindri's Onyx token. The
  namespace is the owner's alone, snapshots are a manual procedure the owner runs, and the StorageClass should be encrypted
  where the cluster has one (§C.4).
- **No mesh on this pod.** A mesh sidecar would hand cluster traffic to Sindri from loopback; the guard treats that as `shared`
  by Host or by a forwarded header (B.2), the chart sets the opt-out annotations (`podAnnotations`), and deploy.md says not to mesh the pod.
- **Supply chain.** Every kubelet image by digest: our own, the base images and Ollama in `deploy/images.lock`, Onyx's in
  `context/onyx.lock`; our own image built from the repo by CI, both locks checked in CI, and the rendered Onyx template checked
  against them by `onyx-pod.mjs --check`; a `helm template` test asserts every image in the chart is a digest from a lock. Model
  weights are pinned too (Hugging Face revisions, Ollama's digest) and verified before Sindri serves queries.

## 7. Testing

- **Unit:** pod-mode settings (unset is the desktop behaviour: a golden of the settings, doctor output and `serve`'s start log with the variable unset), the lease's stable host id, `LoopbackGuard`'s decision table (remote loopback or not, Host loopback or not, endpoint metadata, Origin, and the mesh cases of B.2), the `shared` caller (carried on `HttpContext.Items`, never read from a header), the per-source cap (off by default; 20 calls from one address with the cap at 8), the forwarded-header and hint-body cases, the `::ffff:127.0.0.1` unwrap, the ping outside the limiter, the per-caller tool rows, the business-context view on all five tools (one record of every kind and source, every free-text field), the limits, `repo-pull`'s decisions (clone, fast-forward, dirty, diverged), `onyx-pod.mjs` (its override table, the refusal of a fallback password, the per-platform digest choice).
- **Integration:** **the endpoint walk of §B.2** (every endpoint, a non-loopback remote, expects 404 for all but `/sindri/mcp` and the ping; a loopback remote expects today's answers); `/sindri/mcp` as a `shared` caller against a temp home with two repos, every refusal code, the five tools' answers equal to the loopback goldens except the differences §B.2 names, with business context on and filtered with it off; `sindri_request` and (with the key off) the six reads answer as unknown; a `sindri_*` tool call and a command route sent as `shared` are refused; `onyx attach` against a fake Onyx (healthy, wrong version, unreachable, refused for a session, a foreign first account `SND-ONYX-014`, `--adopt` with the existing admin, already attached, a version mismatch that updates, Onyx off `SND-ONYX-015`); the bash tests of `sindri-pod.sh` with a fake `kubectl` (`connect` with the Onyx container looping and the `sindri` container running connects and prints "Onyx: not answering, see sindri doctor"; `profile edit` with a fake `$EDITOR`, an invalid edit, and a changed-meanwhile file); `onyx up` refused in pod mode (`SND-ONYX-013`); the chart's `helm template` test (nothing privileged, every image a digest from a lock, the probe Host in the MCP hosts, the Onyx Secret checked by keys because `lookup` returns nothing under `template`); `repo-pull` against a local bare repo; `repo-pull` refuses a `file://` URL, a bad name and an unlisted host.
- **Spike (K-S0), time-boxed to 2 working days, before K-O1:** on kind with a pinned node image (Kubernetes 1.29 or later) and a namespace labelled `pod-security.kubernetes.io/enforce=baseline`, a hand-written pod with Sindri's image and the Onyx containers at the lock's digests, no privilege anywhere. It fills this fixed checklist into `docs/sindri/deploy-spike.md`, one line per item (result, or "failed" with the evidence):
  1. every Onyx container reaches the others on `127.0.0.1` with the host variables of §A.3, and the pod settles with **no start ordering** (A.3 Start order); **time-to-Sindri-up on a cold node** (`/sindri/ping` answers before the last Onyx image finishes pulling, with Sindri first in the spec and the init container on Sindri's image) is recorded;
  2. nginx reaches its upstreams with `ONYX_BACKEND_API_HOST` and `ONYX_WEB_SERVER_HOST` set to `127.0.0.1` (`hostAliases` only if that fails), the `sed` that moves its listen port works on the copied template with the pinned files unedited, and its writable directories suffice (A.3 nginx);
  3. OpenSearch bound to loopback starts with `bootstrap.memory_lock=false`, its bootstrap checks only warn, and what the warning costs, and the TCP liveness probe on `9200` (HTTPS, demo certificate) works;
  4. **per container: non-root or not, capabilities needed, and the Pod Security level that results**;
  5. `ss -ltn` in every container and a scan of the pod's address from another namespace, which must find only the API port; the port map of A.3 matches;
  6. v4.9.0 starts with `FILE_STORE_BACKEND=postgres` and makes no S3 call at startup, whether `S3_*` and `CODE_INTERPRETER_*` can be left out, and any setting that closes sign-up after the first account;
  7. where each model server's weights live, whether they download at start, the hosts contacted at start, and the Hugging Face revision setting;
  8. each `subPath` directory's owner and what the init container must do; Postgres with a memory `/dev/shm`;
  9. the web server's loopback setting (`HOSTNAME`), and whether Onyx's web connector refuses loopback addresses, **a redirect to loopback (a public host that redirects) and a DNS name that resolves to `127.0.0.1`**;
  10. Onyx's pages and `/api/version` answering from Sindri's container at `http://127.0.0.1:<onyx.port>` and through a bare port-forward at the same local port; **sign-up and one OAuth-style connector round trip through a port-forward at a different local port** (`WEB_DOMAIN` holds `onyx.port`; some providers also refuse a `127.0.0.1` redirect, and if so the checklist prefers token-based connectors). The attach end to end is K-O2's proof and the kind run's, not this item;
  11. `bwrap` under the default seccomp profile (expected: graph off); the pinned `temporal`'s headless flag from `temporal server start-dev --help` (A2f: `--headless` with `--ui-port` omitted still takes `--http-port`; this needs nothing from K-O2);
  12. the pod's measured footprint (requests = limits per container, and the re-measured Onyx memory for the sizing set), and a pod delete with Postgres under write load.

  **Overrun rule.** At the end of the second working day K-S0 reports what is done and what is not, and the owner decides whether to extend the box. If not, each open item becomes a named acceptance check of K-O1 instead of a delay.

  **Stop rule.** If an Onyx image cannot run without a privileged container or cannot be kept on loopback, or no file store without S3 exists, K-S0 **stops**, K-O1 does not start, and the owner is told that Onyx stays off for v1 (`onyx.enabled: false`, code and docs only), which the cluster door survives. No workaround that contradicts q73. The report is `docs/sindri/deploy-spike.md`; its results are summarized in `docs/sindri/deploy.md`; where an item fails but isn't a stop, an amendment of this spec comes before K-O1.
- **Heavy (nightly, and by hand per PR):** the image built and run (`docker run` with a volume: serve up, Temporal up, doctor ok, a restart takes the lease at once; a restore of a snapshot); the chart on a local kind cluster (`deploy/test/kind-up.sh`): `helm install --timeout 30m` (no `--wait`) into a namespace labelled `pod-security.kubernetes.io/enforce=baseline`, `connect` waiting for Sindri's container, Sindri's readiness and Onyx's liveness probes passing (Onyx has no readiness probes, so "ready" says nothing about it; doctor's Onyx rows are asserted instead), time-to-Sindri-up on a cold node recorded, `onyx-pod.mjs --check` clean, `sindri-pod.sh connect` opens the app, `connect --onyx` runs `attach` to a healthy state, **Onyx's port answers from Sindri's container on `127.0.0.1` and through a bare port-forward**, a call from another namespace's pod answers through the Service on `/sindri/mcp` and every other path is 404, business-context records are absent until the switch is on, the restart-to-first-answer time (target under 2 minutes) and the footprint are recorded, **killing `opensearch` leaves `/sindri/mcp` answering `context_query` with the Onyx-down degraded answer**, a request from remote `127.0.0.6` with a service-name Host is `shared`, and a small soak (8 parallel callers, a commit to a repo every 30 s) runs and records the freshness lag's and `context_query`'s p50 and p95 (recorded, not gated, until a first real load sets the numbers). K-C1's kind test runs with `onyx.enabled: false` first; K-O1 adds the run with Onyx.
- **Owner check, once, on the real cluster:** the namespace's Pod Security level and the node's settings (§A.3) on its node image, install, connect, approve, `connect --onyx`, connect one source, register the MCP port, query from an agent through the internal MCP server.

## 8. Errors and settings

| Code | Meaning | Fix text (short) | Phase |
|---|---|---|---|
| `SND-CONTEXT-091` | the door doesn't serve that repo (not in the approved profile, or `share.enabled` is false) | "No such repo here. Ask the owner of this Sindri which repos it serves." | K-B1 |
| `SND-API-014` | too many calls in flight from `shared` callers (429, `Retry-After`) | "The server is busy. Wait the Retry-After seconds and retry." | K-B1 |
| `SND-API-015` | one source address already has its share of calls in flight, only when `share.perSourceCap` is set (429, `Retry-After`) | "Too many calls from this address at once. Wait the Retry-After seconds and retry." | K-B1 |
| `SND-API-016` | a Host a non-loopback caller may not use (400) | "This server answers cluster callers only for <hosts>. Use one of them, or ask the deployer to add yours to AW_SINDRI_MCP_HOSTS." | K-B1 |
| `SND-API-017` | a loopback remote whose Host is neither loopback nor listed reached a route (404 with a body; non-loopback remotes get a bare 404) | "This request reached Sindri from inside the pod with a Host Sindri does not treat as yours. Use Host localhost, 127.0.0.1 or [::1]." | K-B1 |
| `SND-ONYX-013` | `onyx up` or `down` in pod mode (the highest code on main is `SND-ONYX-012`; K-O2 re-checks at merge and takes the next free number if another lane has taken 013) | "Onyx is part of this pod. Run `sindri onyx attach`." | K-O2 |
| `SND-ONYX-014` | `onyx attach` finds an Onyx account that this Sindri did not create (the first account is Onyx's admin) | "This Onyx has an admin account that this Sindri did not create. If you made it, for example before this volume was lost or restored, run `sindri onyx attach --adopt` and sign in with it. If you did not make it, someone else got there first: the only fix is to start from an empty Onyx claim, which deletes everything Onyx holds (its data, its connectors and their OAuth credentials)." | K-O2 |
| `SND-ONYX-015` | `onyx attach` where Onyx is off (`AW_SINDRI_ONYX_ENABLED=false`) | "This install runs without Onyx (onyx.enabled=false). Set it to true and run helm upgrade to add Onyx." | K-O2 |
| `SND-REPO-001` | the first clone failed | names the git error and the URL's host | K-C2 |
| `SND-REPO-002` | the checkout has local changes | "Sindri never resets a repo; clean it by hand (kubectl exec) or remove and re-add it." | K-C2 |
| `SND-REPO-003` | main moved in a way that isn't a fast-forward | "Upstream was rewritten; re-add the repo to clone it fresh." | K-C2 |
| `SND-REPO-004` | the credential pointer is missing or empty | names the variable | K-C2 |
| `SND-REPO-005` | the URL, its host or the repo name is refused (not https, not an allowed host, a bad name) | names which rule | K-C2 |

Existing codes reused: `SND-CONTEXT-021` (no `X-Sindri-Repo`; its text names the header), `SND-CONTEXT-022` (an argument the
`shared` schema doesn't have), `SND-CONTEXT-023` (an MCP tool not offered to this caller), `SND-CONTEXT-010` (Onyx down, degraded
answer), `SND-API-008` (Origin refused), `SND-AUTH-002` (a protected verb without a terminal). Fix texts shown to `shared` callers never name an owner-only command (B.2). Each new code gets its fix text in `SindriErrors.cs` and
a row in the API spec's §9. Part D owns `SND-CONTEXT-200..209`.

**Texts shown to a `shared` caller, once** (their situations are the table in §B.2; `SND-CONTEXT-091`'s text is in the table above).
None names an owner-only command.

| Where | Text |
|---|---|
| `SND-CONTEXT-021` | "This call names no repo. Set the X-Sindri-Repo header in this MCP server's entry, or ask the owner of this Sindri for the entry." |
| `SND-CONTEXT-022` | "This tool does not take <argument> for you. See tools/list for its inputs." |
| `SND-CONTEXT-023` | "<name> is not offered to this caller; tools/list names the tools you can call." |
| `SND-CONTEXT-030` | "No map for <repo> yet: its first build has not started or did not finish. Retry in a few minutes; if this keeps answering, ask the owner of this Sindri to run the first map build." |
| note `setting up` (first clone or first build running) | "<repo> is being set up: its code is being fetched and its map built for the first time. Retry in a few minutes." |
| note `stale` | "The map for <repo> is <lagSeconds> seconds behind the main branch." |
| note `not updated` | "<repo> is not being updated, so its latest changes are missing from this answer. Ask the owner of this Sindri." |
| note `nothing found` | "Nothing found in code and docs." |
| note `withheld` | "business context withheld for this caller" |

| Variable | Set by | Default | Meaning | Phase |
|---|---|---|---|---|
| `AW_SINDRI_DEPLOY` | the image (`ENV`) | unset (desktop) | `pod` turns pod mode on; every pod-mode behaviour keys on it | K-A1 |
| `AW_SINDRI_MCP_HOSTS` | the chart, from `service.hosts` | none (every non-loopback Host refused) | the Host names a non-loopback caller may use on `/sindri/mcp` and the ping (`SND-API-016`) | K-B1 |
| `AW_SINDRI_REPOS_DIR` | the chart | `/data/repos` in pod mode | where `repo-pull` clones | K-C2 |
| `AW_SINDRI_ONYX_URL` | the chart | unset | the default URL of `sindri onyx attach` (`http://127.0.0.1:<onyx.port>`) | K-O2 |
| `AW_SINDRI_ONYX_PORTS` | the chart (**K-O1** writes it, in `onyx-pod.mjs`'s render) | unset | `name:port` pairs of Onyx's containers, which doctor opens (**K-O2** reads it) | K-O1 writes, K-O2 reads |
| `AW_SINDRI_ONYX_ENABLED` | the chart, from `onyx.enabled` | unset (read as true) | `false` makes doctor, `attach` and the approve preview say Onyx is off in this install (`SND-ONYX-015`) | K-O2 |
| `AW_STATE_DIR`, `AW_SINDRI_HOME`, `HOME` | the image (`ENV`) | `/data/state`, `/data/state/sindri-app`, `/data/home` | where state lives (a `kubectl exec` doesn't run `start.sh`) | K-A1 |
| `AW_SINDRI_PORT` | the chart | 7410 | the API port (exists) | existing |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | the chart, from `telemetry.endpoint` | unset | where pod mode exports telemetry | K-A1 |
| `AW_SINDRI_POD_ONYX_PORT` | the owner's shell, read by `sindri-pod.sh` | `onyx.port` (8081) | overrides the local port `connect --onyx` forwards Onyx's page to (by default the same as `onyx.port`); using it prints a warning that sign-up redirects and connector OAuth may land on `onyx.port` | K-C3 |
| `AW_SINDRI_ONYX_FORWARD_URL` | `sindri-pod.sh`, through the exec | unset | the forwarded address `onyx attach` prints at sign-up | K-O2 |

All the above go in `.env.example`.

| Profile key | Default | Meaning | Phase |
|---|---|---|---|
| `share.enabled` | true in pod mode | a non-loopback caller is served the approved repos (admits the `shared` client); a profile key only, no chart value sets it | K-B1 |
| `share.businessContext` | false | such a caller also gets Onyx's records (K5, flagged) | K-B1 |
| `share.workQueueReads` | false | such a caller also gets the six read-only `sindri_*` tools (flagged; `sindri_request` is never offered) | K-B1 |
| `share.perSourceCap` | 0 (off) | how many of the 16 in-flight calls one source address may hold (`SND-API-015`); behind the internal MCP server it caps everyone together | K-B1 |
| `repos[].pull` | none | `{ url, credential: env:NAME }`, written by `repo add --url` | K-C2 |
| `repo-pull.allowedHosts` | none | the git hosts `repo-pull` may clone from; **profile-only** (changing it needs approval, it is not a chart value) | K-C2 |
| `index.graph.enabled` | true | false where `bwrap` can't start (§A.2); a profile key only | existing |
| `deploy` | read-only echo | pod or desktop, for doctor | K-A1 |
| `context.sources.onyx` | written by `onyx attach` | `{ url: http://127.0.0.1:<port>, token, version, managed: false }`, an existing key | existing |

Profile keys change only through `sindri-pod.sh profile edit` (§C.3); the chart's values never set one. Each phase adds its keys to `docs/csharp/sindri-profile.md` (a bash test checks every key is named there).

## 9. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Needs |
|---|---|---|---|
| **K-S0 spike** | context | the kind spike of §7, **time-boxed to 2 working days** with a stop rule, run as a fixed checklist; it writes `docs/sindri/deploy-spike.md` (one line per checklist item; its results are summarized in deploy.md), reports at the end of day 2 if not done (§7 overrun rule); plus `deploy/test/node-probe.sh`, a one-pod check of the node settings the owner runs on the real cluster right after; an amendment here if an item fails | K-A1's image (a local build) |
| **K-A1 image** | context | the Dockerfile (no Docker CLI), the CI workflow that builds it for both platforms and pushes it to the configured registry (the build stage needs clang and zlib for NativeAOT) and prints the digest for the values, the start script, `deploy/images.lock` (the kubelet's own images: base images and Ollama; no Docker or Onyx rows) and its check, the heavy `docker run` proof, pod mode's switch (A2a) and the lease restart check (A2b, a test only) | nothing |
| **K-O1 Onyx siblings** | context | `scripts/onyx-pod.mjs` and its inputs under `deploy/onyx/` (Compose copy, `env.template`, nginx templates, `pod-overrides.yaml`, each sha-checked against the lock), `context/onyx-sizing.json` (only the values that carry over to a pod, §A.3) shared with `OnyxConfig` (and the C# drift test), the nginx `sed`, **`AW_SINDRI_ONYX_PORTS` (K-O1 writes it)**, the generated `onyx-containers.yaml` of the chart (containers, the init container for volume directories, volumes, the nginx ConfigMap, the Secret), `--check` in CI and a bash test (added to AGENTS.md's list), the `helm template` checks that nothing is privileged and every image is a lock digest, and the `deps.yaml` file edges for `scripts/onyx-pod.mjs` (to `context/onyx.lock`, the Compose copy and the sizing file) | **K-S0's report** (the script skeleton, the sha checks and `--check` can start earlier; the override table waits for the report), K-C1 for the chart skeleton |
| **K-O2 onyx attach** | context | revive `sindri onyx attach` from `feat/sindri-onyx-attach` (`cb225bf00`) without `87028b190`, reverse its loopback refusal, `AW_SINDRI_ONYX_URL` and `AW_SINDRI_ONYX_FORWARD_URL`, the foreign-account refusal (`SND-ONYX-014`), the no-op when already attached, `attach --adopt`, the version-mismatch update, `onyx up`/`down` refused in pod mode (`SND-ONYX-013`), Onyx off (`SND-ONYX-015`, `AW_SINDRI_ONYX_ENABLED`, the doctor and preview texts), Temporal headless in pod mode (A2f), doctor's Onyx rows (containers answering, reachable, pin matches; it reads `AW_SINDRI_ONYX_PORTS`, which K-O1 writes) and the attach end-to-end proof (on the kind run), the catalog row and `Aw.Sindri.Ui.Tests` golden | nothing for the code (the doctor rows' port list comes from K-O1) |
| **K-B1 open `/sindri/mcp` to the cluster** | api | pod mode's any-address listener (both listen sites, `ServePort` and `LoopbackBinding`), `LoopbackGuard` by remote address, Host and endpoint metadata, the endpoint-walk test with the mesh cases, the reduced ping outside the limiter, the optional per-source cap (`SND-API-015`, off by default), the `shared` member of `ClientLabel` and `ContextClient` (in `Aw.Context.Core`, with `ContextScopes.Client`, `FencePolicies.For` and `CallerContext.ActorOf`) carried on `HttpContext.Items`, per-caller rows in `McpToolTable`, `ScopeRules` admission by `share.enabled`, the business-context view (new engine code, one choke point) and the `share.*` profile keys (with the approve preview's plain-words lines), `sindri share entries`, the limits, metrics and refusal log, the new codes (`SND-API-017` among them), the forwarded-header rule and the `shared` texts of §8; `lagSeconds` read through a lag interface K-C2 provides (a stub until then); `docs/csharp/sindri-profile.md` for the new keys | nothing (pod-mode switch stubbed until K-A1 merges: the listener reads one env var); **a context-lane reviewer** for the `Aw.Context.Core` changes |
| **K-C2 repo-pull** | engine | the service, `repo add --url`, its codes and doctor rows, the lag interface (it also reports each repo's update state: cloning, updating, cannot be updated) and metric, the `pull:` profile keys in `docs/csharp/sindri-profile.md` | A2d (its git calls take a credential, so they go through the allowlisted environment); the service and its tests start against the factory's interface before A2d merges |
| **K-C1 chart** | context | the Sindri chart with **Sindri and Ollama and `onyx.enabled: false`** (the containers and their first pull, the Service, probes, the claim, `service.hosts`, the mesh opt-outs, `values.schema.json`, the example values, the `helm lint`/`template` test, the pod-state table in the NOTES, `podAnnotations`, the `git.credentials` mapping, `service.hosts` rejecting loopback names, Sindri's container first and the volume init container on Sindri's image), and its kind test; K-O1 adds the Onyx containers to it. With K-A1, K-B1, K-C2 and K-C3 it is **milestone 1** (§C.1) | K-A1; K-B1, K-C2 and K-C3 for the full kind test (**not** K-S0 or K-O1) |
| **K-C3 connect** | context | `scripts/sindri-pod.sh connect` (waiting for the `sindri` container, not the pod's Ready; and `--onyx`, forwarding Onyx's port on the same local port, printing the address, running `onyx attach`) and `profile edit` with `sindri profile validate --file`, its bash test with a fake `kubectl` (added to AGENTS.md's test list), including a stale-epoch case, an Onyx container looping with Sindri running, a taken Onyx port, `profile edit` cases, attach failures passing through with the app still connected, and the `NO_COLOR` and non-tty output | K-A1 for the heavy proof only; K-O2 for the real `attach` |
| **A2d child environment** | context | the allowlisted child environment factory and its architecture test | nothing (K-C2 needs it) |
| **A2c doctor rows** | context | the pod-mode doctor rows (the Ollama model's digest among them) except two groups: the Onyx rows are K-O2's and the repo rows are K-C2's | K-A1, K-B1 |
| **K-I1 ideas** | ideas | the follow-ups that don't block the first deploy: the freshness dashboard on the Health page, a model provider for the pod (K12's later spec), authentication for the shared caller if the owner later wants it, a token or Unix socket for sessions inside the pod (§6), TLS by mesh notes; each is written as a one-page spec, then taken as its own phase | nothing |
| **Part D W1–W4** | context, ui | as its own §9 | nothing (runs alongside everything) |
| **K-E2E** | context | the kind run of §7 end to end, the docs (`docs/sindri/deploy.md`) | everything above |

Every phase's gate also runs `scripts/check-env-example.sh` and `node scripts/check-deps.mjs` (§8's variables go in
`.env.example`, and a new edge goes in `deps.yaml`). Every phase writes its own part of `docs/sindri/deploy.md` (and its API §9
rows, the chart's NOTES, and the AGENTS.md command lines it adds) in the same PR; K-E2E only joins them up.

Parallel: at the start, five lanes are busy at once: context on K-A1, A2d, K-O2 and Part D W1 (K-S0 as soon as K-A1's image builds, K-O1 as
soon as K-S0 reports), api on K-B1, engine on K-C2, ui on Part D W3 (against W1's goldens). Context takes K-C3 and K-C1 (Sindri and Ollama, Onyx off) after K-A1, without waiting for K-S0; ui takes W4 after W3. K-E2E is last. The ideas lane starts with its one-page specs at once, and after each of api, engine
and ui finishes its one phase it takes that lane's next item from K-I1, so no lane idles. Every phase is one PR with a scoped
gate (build plus the unit and integration lanes of the touched projects; `Aw.Sindri.Ui.Tests` in any phase that adds an API golden
or a catalog row).

## 10. Left to the builders

The chart's exact resource numbers (measured on the kind run), the start script's details, the nginx `sed` (K-S0 verifies it), each Onyx container's security context (K-S0), the pod-state table's wording, and the kind cluster's
version. Each is recorded in the code or `docs/sindri/deploy.md` where it lands.

## 11. What revision 3 changed (the owner's answers q73 and q74, 2026-10-23)

| Answer | Change |
|---|---|
| q73: Sindri and Onyx are one deployable unit, nothing privileged, no Docker-in-Docker | Onyx's prepackaged images (by `onyx.lock` digest) run as sibling containers of the pod from one chart (new §A.3 replaces the Docker sidecar). Gone: the Docker-in-Docker sidecar, `privileged`, Sysbox, the uid-1000 reasoning, `DOCKER_HOST`, `DOCKER_CONFIG`, the Docker CLI and Compose plugin in the image, `AW_SINDRI_DOCKER_MEMORY_BYTES` and `_DISK_BYTES`, A2e's preflight changes, the namespace of its own and `connect --onyx` running `onyx up`. Added: the render script and its drift check (K-O1), loopback wiring and port clashes, native-sidecar start order, one `onyx-data` claim with subPaths, Onyx's Secret, `onyx attach` (K-O2, revived minus `87028b190`), Onyx doctor rows, the shared-loopback consequence (§6). K1 and K2 are decided; K7 snapshots both claims; K9 gains Onyx's Secret; K11 keeps two locks with Onyx pulled by the kubelet; `onyx.enabled: false` now only means a smaller pod |
| q74: no new shared door | The existing `/sindri/mcp` on the existing API port, opened to the cluster (§B.2). Gone: the second `WebApplication`, `AW_SINDRI_SHARED_LISTEN`, `SND-API-015`, the per-call scope bridge, the separate pipeline and Kestrel limits, and the second path `/sindri/context-mcp`. Added: the guard decides by remote address and endpoint metadata, the endpoint-walk test, the `shared` caller picked from the connection, per-caller tool rows, the flagged `share.workQueueReads`, the reduced `GET /sindri/ping`. `SND-API-016` now names `AW_SINDRI_MCP_HOSTS` |
| §9 | K-A2 removed; K-O1 and K-O2 added; K-B1 is "open `/sindri/mcp` to the cluster" (api lane) |

**Moved out of the body (revision 2's history).** Revision 2 had Onyx in a rootless Docker-in-Docker sidecar with the host
loopback disabled, needed uid 1000 to match that daemon, and dropped `sindri onyx attach` with its `network: cluster` setting and
`AW_SINDRI_ONYX_HOSTS` (revision 3 brings the verb back, smaller). Dropped variables: `AW_SINDRI_SHARED_LISTEN`,
`AW_SINDRI_SHARED_HOSTS` (renamed `AW_SINDRI_MCP_HOSTS`), `AW_SINDRI_DOCKER_MEMORY_BYTES`, `AW_SINDRI_DOCKER_DISK_BYTES`,
`DOCKER_HOST` and `DOCKER_CONFIG`. Revision 2's `SND-API-015` (`AW_SINDRI_SHARED_LISTEN` without pod mode) is gone; the number now
means the per-source cap. The first revision-3 draft ran Onyx's containers as native sidecars that gated Sindri; round 1 removed that.

**Round 1 (`/autoplan`, 2026-10-23).** The fixes, by where they landed:

| Finding | Change |
|---|---|
| Start order and readiness (arch A1, design D1, devex) | no Onyx container is a native sidecar or gates Sindri; Onyx gets liveness probes only; the Service publishes not-ready addresses; pod-state table; a kill-opensearch heavy check (A.3 Start order, C.1, B.2) |
| Mesh and loopback (A2, C1, devex) | owner rights need a loopback remote (`127.0.0.1`, `::1` only) and a loopback Host; a loopback remote with another Host is `shared`; mesh opt-out annotations; tests; `LoopbackBinding.cs:40` named (B.2) |
| One Onyx recipe (A3) | `context/onyx-sizing.json` shared by `OnyxConfig` and the render; one model server; the port clash is gone (A.3) |
| File store and defaults (A4, C4) | S3-free file store, `S3_*` and `CODE_INTERPRETER_*` dropped, `env.template` handled, `WEB_DOMAIN`, well-known fallbacks refused, `attach` refuses a foreign account (`SND-ONYX-014`) (A.3, B.3) |
| Ping and limits (A5, C5) | the ping is outside the limiter; per-source cap, `SND-API-015` (B.2) |
| Render inputs (A6) | `env.template` and the nginx templates committed and sha-checked; `--check` covers the ConfigMap (A.3) |
| Pod mechanics (A7-A10, C10) | no volume over baked caches, `/dev/shm`, requests = limits, init container for subPath ownership, pinned weights, digest test (A.3, §6) |
| Cut for v1 (P3, A11) | PodDisruptionBudget, NetworkPolicy, snapshot CronJob and RBAC. **Reversible defaults**: each comes back without a redesign (a CronJob, `maxUnavailable: 1`, a policy admitting the API port only). Snapshots are a manual procedure (K7, C.4) |
| Ports and sidecar sysctl (A12) | full port map; the sysctl option marked risky; the restart target recorded, not gated |
| Business context (C2, S2, S3) | new work, one choke point, free-text golden, a context-lane reviewer; the caller on `HttpContext.Items` (B.2, K-B1) |
| Other security (C3, C6, C7, C9, C11, C12) | Temporal headless, connector warnings, one host value, drop ALL capabilities, plain HTTP stated, source-address debug counts, `allowedHosts` profile-only (A2f, A.3, C.1, B.2, C.2) |
| Cluster agent texts (D2) | the table of what a `shared` agent sees; fix texts never name owner-only commands (B.2) |
| `connect --onyx` and install order (D3, D4) | forwarded address, port variable, failure and no-op behaviour; one approval after `attach` (B.3, C.3) |
| Plan shape (P1, P2, P4-P6) | K-C1 split (Sindri and Ollama first); K-S0 time-boxed with a stop rule and a report; the §1 box in plain words with a third item; revision-2 history moved here (§1, §7, §9) |
| Devex | `helm install --timeout 30m`, variable table, stale-epoch bash test, `lookup` under `template`, baseline label in the spike and kind runs, `deps.yaml` file edge, `SND-ONYX-013`, `hostAliases`, Kubernetes 1.29 (C.1, §7, §8, §9) |

**Round 2 (`/autoplan`, 2026-10-23).** Two reversible defaults changed; the rest are fixes.

| Default | Was | Now |
|---|---|---|
| `share.perSourceCap` | 8 of 16 | 0 (off): behind the internal MCP server one address is everyone, so it capped the whole company |
| nginx's upstream names | `hostAliases` | `ONYX_BACKEND_API_HOST` and `ONYX_WEB_SERVER_HOST` set to `127.0.0.1` (`run-nginx.sh` reads them); the listen port moves by a `sed` in the command; `hostAliases` is the fallback |

Fixes by where they landed: `sindri-pod.sh profile edit` and one home per profile key (C.1, C.3, §8); `connect` checks Sindri's container, not the pod (C.3); the sizing file holds only what carries to a pod (A.3); Temporal's ports are random (A.3, A2f); `SND-API-017` for a loopback remote with a foreign Host, and forwarded headers make a request `shared` (B.2); OpenSearch is HTTPS with a hand-written liveness probe; `ListenAnyIP`; install without `--wait` (A.3, C.1); `WEB_DOMAIN` and the forwarded port (C.3); `USER_AUTH_SECRET`, `ENCRYPTION_KEY_SECRET` and the required-keys list (A.3); `attach --adopt` and a safer `SND-ONYX-014` (B.3); every `shared` text in §8; Onyx-off texts and `SND-ONYX-015`; the approve preview's four texts (C.3); the spike's overrun rule, item moves and report name (§7); Sindri's container first and the init container on its image (A.3); the Secret-to-pointer mapping (C.1); the plain-words §0 and the node note (§0, §1).

Revision 2's changes, one line each, still hold unless the table above says otherwise:

- K1/K2: Onyx's Compose setup, not Onyx's Helm chart (revision 3 keeps no Helm chart for Onyx either, and no Compose in the pod).
- K3: any pod may reach the door; no NetworkPolicy by default.
- K5: no authentication; no tokens, token store or minting; code and docs only by default with the flagged `share.businessContext`.
- K12: no model in v1.
- Sandbox PRs #352, #355, #363, #366: not needed for this spec.

The open drafts' updates to match are listed in the PR body, not here.
