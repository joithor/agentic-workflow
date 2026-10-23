# Sindri as a deployable unit: one image, Onyx beside it, a shared context map everyone can query

**Status:** design, revision 1, for the owner's morning read · **Date:** 2026-10-23 · **Asked by:** the owner, 2026-10-23 (relayed by
sindri-meta): "make Sindri + Onyx a deployable unit for an EKS pod that stands up both. The owner port-forwards to it to set it up
(profile approval, Onyx admin, connectors). Then it's added to an internal MCP server so everyone can query a constantly updating
context retrieval map", refined the same night: "the deliverable is a Dockerfile that wraps Sindri". The full-map page asked for
the same night is Part D of this spec (`2026-10-23-sindri-context-whole-map-design.md`). · **Builds on:** the API surface spec
(`2026-10-10-sindri-api-surface-design.md` §6 local-only, §8 MCP, §11 deferred auth), the Onyx managed spec
(`2026-10-16-sindri-onyx-managed-design.md`), the context retrieval spec, `docs/csharp/sindri.md` (home layout, lease, serve),
`docs/csharp/forge.md` (credentials), `binaries.lock`, `context/onyx.lock` · **Lanes:** context leads; api reviews and builds the
server and MCP surface (§B); engine the repo freshness service (§C.2); ui the remote connection and Part D's page · **Next:** the
owner reads §0 and the decisions table (§1); then /specToProvenPR takes §9's phases to PRs.

**Keeping it app-agnostic.** Nothing here names a work app, company, cluster, registry or host. Every one of those is a value the
deployer gives (Helm values, the profile, Kubernetes secrets), and the examples use `example.com` and `registry.example.com`.

## 0. The short version (for the morning read)

Today Sindri is a desktop program: it binds to loopback only, trusts whoever holds the token file in its home, runs one copy per
person, and manages Onyx with Docker Compose on the same machine. Every line of that changes in a pod. This spec turns it into:

1. **One image** (`Dockerfile` at the repo root, built by CI from the repo): Sindri's control plane (framework-dependent .NET 10, as
   `sindri-app` publishes it), the pinned `temporal` binary, the code-index tools (graphify, `bwrap`), `git`, and nothing secret. It
   runs `sindri serve` in the foreground with all state on one volume.
2. **Onyx beside it, not inside it**: Onyx's own Helm chart at our pinned commit, as a second release in the same namespace,
   reached by its in-cluster address. Sindri no longer starts Onyx in this mode; it only connects to it.
3. **Two doors into Sindri**: the owner's door (today's loopback API and UI, reached with `kubectl port-forward` and
   `kubectl exec -it`, unchanged rules) and a new **shared door**: a read-only MCP endpoint with only the `context_*` tools, behind
   a bearer token the owner mints, for the internal MCP server that everyone's agents use.
4. **Always fresh**: a new Sindri service pulls each repo's default branch; the code index, the docs index and the map follow it
   as they already do; Onyx's connectors (set up once by the owner in Onyx's page) keep tickets and chat current; Sindri's existing
   sync and refresh services read them.
5. **The owner's steps** stay one command or one click each: `scripts/sindri-pod.sh connect` opens the app on the pod; approvals
   are the app's existing one-click-and-type confirmations (or `kubectl exec -it … sindri profile approve` at a terminal).

## 1. Decisions for the owner (each has a default we follow if you say nothing)

| # | Question, in plain words | Recommended default | Why, and the alternative |
|---|---|---|---|
| **K1** | How does Onyx come up beside Sindri? | **Onyx's own Helm chart** (`deployment/helm/charts/onyx`, chart 0.8.40 at our pin f1738ed), installed as its own release in the same namespace, ClusterIP only, its bundled public load balancer turned off; Sindri connects to it by its in-cluster address | Sindri's managed mode drives Docker Compose, which in a pod needs Docker-in-Docker or the node's Docker socket: privileged, and refused by most EKS pod policies. Onyx ships no single image and no raw manifests at the pin, only the chart (and a Terraform module for AWS). Its "lite" values turn connectors and search off, which defeats the purpose |
| **K2** | Onyx's database: inside the cluster, or a managed database? | **Inside the cluster**, with the chart's own Postgres (the CloudNativePG operator), Redis and object store, on the cluster's default storage class | The chart's Postgres needs the CloudNativePG operator and its cluster-wide resource definitions, which a cluster admin installs once; that is the one cluster-level, hard-to-undo step in this spec. If the cluster won't take operators, Onyx points at a managed Postgres instead (the chart supports it) |
| **K3** | Who can reach the shared door? | **Only the internal MCP server**, by a NetworkPolicy that admits its namespace, plus a bearer token per caller the owner mints and can revoke; nothing public | The shared door serves read-only context tools; the owner's door (approvals, items, workflows) is never on the network, only through port-forward |
| **K4** | What can everyone's agents do through the shared door? | **Read the context map and nothing else**: `context_query`, `context_lenses`, `context_node`, `context_neighbors`, `context_sources`; no `sindri_*` tools (items, queue, requests stay the owner's) | Today's per-repo MCP has 12 tools, one of which files requests; a many-user door should not start work |
| **K5** | Which repos does a caller see? | **The repos the caller's token names** (the owner chooses per token); the internal MCP server registers one entry per repo with its header, as `sindri mcp register` does locally | Keeps today's per-repo scoping (`X-Sindri-Repo`) and adds a check that the token allows that repo |
| **K6** | How does the pod get the code, and how fresh? | **A new Sindri service, `repo-pull`,** fast-forwards each repo's default branch every minute with a read-only token from a Kubernetes secret; the first clone happens once when a repo is added | Sindri never clones today and expects a checkout on disk. The common git-sync sidecar lays a repo out as linked worktrees, which Sindri resolves to a main checkout without a tree; a service of our own keeps the layout Sindri already checks (clean, HEAD in main) |
| **K7** | Where does the state live, and what is the backup? | **One volume** (`/data`, the cluster's default block storage, encrypted), **one replica** (a StatefulSet), **nightly volume snapshots, 7 kept** | Sindri's ledger, views, map and Temporal's history are SQLite files that allow one writer; two replicas would fight over the lease. A snapshot restores everything at once; the views and the code index can be rebuilt, the ledger and map history can't |
| **K8** | How does the owner see the app on the pod? | **`scripts/sindri-pod.sh connect`**: one command that port-forwards, fetches a fresh human token over `kubectl exec`, and opens the desktop app against the pod | Today the app only reaches a Sindri whose files it can read on the same machine. A browser version of the app is still later (UI spec §12.2) |
| **K9** | Where do secrets go? | **Kubernetes secrets as environment variables** (`env:` pointers): the repo token, the forge token, Onyx's admin and Sindri token; never in the image, the profile or the chart's values | Secrets mounted as files are symlinks in Kubernetes, and Sindri's `file:` pointers refuse symlinks on purpose. The Onyx token Sindri makes at setup is written to the volume (`secrets/`, owner-only), as the managed mode does today when no keychain exists |
| **K10** | Embeddings for the code index (local models only)? | **An Ollama sidecar** in the Sindri pod on `127.0.0.1:11434`, its model pulled once onto the volume at the pinned digest | Sindri allows only a loopback Ollama; a sidecar shares the pod's loopback, so no code change |
| **K11** | Images: built how, pinned how? | **Built by CI from the repo** for linux/amd64 and linux/arm64, pushed to a registry from config, referenced **by digest** in the chart; a new `deploy/images.lock` pins every third-party image the deployment pulls (base images, Ollama, Onyx's chart images) by digest, in `onyx.lock`'s style, with a `check` script | Onyx's chart images differ from our Compose pins (its Postgres, Redis and proxy are other images), so the Compose lock can't be reused as is |

Two choices are left to the morning because they are product or cluster-level: **K2** (an operator in the cluster) and **K3** (who
may reach the door). Everything else is reversible and the builders follow the defaults.

## 2. Part A: the image

### A.1 The Dockerfile

At the repo root, multi-stage, every `FROM` by digest (from `deploy/images.lock`):

1. **build** (`mcr.microsoft.com/dotnet/sdk:10.0.401-noble@sha256:…`, the SDK `global.json` pins): restore with the locked
   restore, `dotnet publish src/Sindri/Aw.Sindri.Host -c Release --no-self-contained -p:AwRecipesDir=<recipes/sindri>`, the same
   flags `scripts/sindri-app.sh` uses; `judge` and `aw-packets` NativeAOT for the target platform (`AW_SINDRI_JUDGE`,
   `AW_PACKETS_BIN` point at them); the shipped recipes' evals run (`recipe eval --shipped`) and fail the build as they fail
   `sindri-app`. `sindri-ui` is not built: the image has no desktop.
2. **tools**: `temporal` from `binaries.lock` (download, sha256 check, as `scripts/binaries-lock.sh fetch temporal` does), and
   graphify at the code index's pin in a venv (`uv`, as `scripts/install-deps.sh` does).
3. **runtime** (`mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:…`): `git`, `procps` (Sindri finds its Temporal dev server
   with `ps` and stops it with `kill`), `bubblewrap`, `tini` as PID 1 (it reaps the detached Temporal process), the three stages'
   outputs under `/opt/sindri`, a non-root user (uid 10001), `ENTRYPOINT ["tini","--","/opt/sindri/sindri","serve"]`.
   `AW_STATE_DIR=/data/state`, `AW_SINDRI_HOME=/data/state/sindri-app`, the temporal binary copied to where Sindri looks for it
   (`$AW_STATE_DIR/cache/bin/temporal-<ver>/`) by a start script when the volume is new. Read-only root filesystem; `/data` and
   `/tmp` writable.

`serve` runs in the foreground, ignores SIGHUP and stops cleanly on SIGTERM (it already does: `ServeCommand.cs`), stopping its
Temporal dev server within 10 s; the pod's grace period is 60 s.

### A.2 What the image needs from Sindri (code changes)

| # | Change | Where | Why |
|---|---|---|---|
| A2a | A **pod mode**, `AW_SINDRI_DEPLOY=pod` (unset: today's desktop behaviour, byte for byte) | `SettingsFromEnvironment`, `Profile` (`deploy:` read-only echo for doctor) | one switch that the changes below key on; a desktop install can't enter it by accident because nothing sets it |
| A2b | **Stable host identity for the lease**: in pod mode the lease records `AW_SINDRI_HOST_ID` (the StatefulSet pod name, `sindri-0`) instead of the machine name | `Infrastructure/Lease` | today a restarted pod gets a new hostname and waits up to the 60 s TTL; with a stable name and a new boot id the dead holder is provably dead and the new pod takes the lease at once |
| A2c | Doctor rows for pod mode: the volume writable and owner-only, the temporal binary present, `bwrap` usable (user namespaces), Ollama answering, the shared door's tokens (count, none expired), Onyx reachable | `Features/Doctor` | the owner's first check after install, through `kubectl exec … sindri doctor` |

If the cluster forbids user namespaces, `bwrap` can't start and the code graph can't build (`SND-CODEIDX-007`); the chart then sets
`index.graph.enabled: false` and doctor says so. The text and symbol index still work.

## 3. Part B: the network (api lane builds, context reviews)

Today (API spec §6): loopback only, `LoopbackGuard` (Host and Origin), one human token file per serve start, no other auth. The
owner's door keeps all of that. Pod mode adds one door and changes one rule.

### B.1 The owner's door: unchanged

- The loopback listener and its guard stay exactly as they are. `kubectl port-forward` reaches it as `localhost`, which the guard
  allows. `kubectl exec -it` gives the CLI a terminal and none of the agent signals, so it calls as the human at a terminal and the
  protected verbs (`profile approve`, `recipe approve-all`) work as they do on a laptop; without `-t` it calls as a session and is
  refused (`SND-AUTH-002`), as it should be.
- The desktop app reaches it through `scripts/sindri-pod.sh connect` (Part C.3).

### B.2 The shared door (new)

| Aspect | Rule |
|---|---|
| Listener | In pod mode only: a second Kestrel listener on `AW_SINDRI_SHARED_LISTEN` (for example `0.0.0.0:7411`). It serves exactly one route, `/sindri/context-mcp`, and `GET /sindri/ping`; any other path is 404 on that listener. The loopback listener never serves `/sindri/context-mcp` |
| Host check | The listener's own guard: the `Host` header must be one of `AW_SINDRI_SHARED_HOSTS` (the Service names the internal MCP server uses); `Origin` refused as on `/sindri/mcp` (`SND-API-008`) |
| Auth | `Authorization: Bearer <token>`. Tokens are minted by the owner: `sindri share token add <name> --repos a,b [--expires 90d]` (protected: the human at a terminal or the app's typed confirmation), printed once, stored as a SHA-256 hash in the ledger with its name, repos and expiry; `sindri share token list` and `revoke <name>` (protected). A missing, unknown, expired or revoked token is 401 `SND-AUTH-010`; a repo the token doesn't name is 403 `SND-AUTH-011` |
| Tools | `context_query`, `context_lenses`, `context_node`, `context_neighbors`, `context_sources` (all `ReadOnlyHint`); the same implementations as `/sindri/mcp`, so their answers, limits and goldens are the same |
| Caller | Every call is a `session` caller with client `shared`: repo-scoped, labels fenced `<untrusted source="map">`, never a command. The token's name is recorded on the call's ledger row (`share:<name>`), never the token |
| Repo | `X-Sindri-Repo`, set by the internal MCP server per registered entry, as `sindri mcp register` sets it locally; a call without it is `SND-CONTEXT-021`, as today |
| Limits | 4 calls in flight per token (more waits, then 429 `SND-API-014` after 10 s); the tools' own budgets unchanged |

The doctor rule that `sindri` is never registered user-wide stays for laptops: the shared door is a different server name
(`sindri-context`) on the internal MCP server, and the doctor check only reads the local agent configs.

The new codes: `SND-AUTH-010`, `SND-AUTH-011`, `SND-API-014`, each added to the API spec's error table with its fix.

### B.3 Onyx by its cluster address

`OnyxSettings.CheckUrl` refuses any Onyx URL whose host isn't loopback (`SND-CONTEXT-055`), even unmanaged. In pod mode, and only
then, the profile may set `context.sources.onyx.network: cluster` with a URL whose host is one of `AW_SINDRI_ONYX_HOSTS` (the
Onyx release's Service name, for example `onyx-api-service.<ns>.svc`), `http` allowed because the traffic stays inside the
namespace (the NetworkPolicy of §C.4 limits it to the two releases). `managed` must be `false`. Everything else about the query
adapter is unchanged: its five requests, its pinned version check, its token pointer.

## 4. Part C: running it (context lane)

### C.1 The charts

`deploy/helm/sindri/` (new, ours): a StatefulSet of one, the volume claim (`/data`, size from values, default 50 Gi), the image by
digest, the Ollama sidecar, the env from a Secret (`env:` pointers), two Services (the owner's port, ClusterIP and only for
port-forward; the shared door's port), the NetworkPolicy, and a VolumeSnapshot schedule (K7). Values hold every deployer-specific
thing: registry, namespace, repo list (name, clone URL, token secret key), the shared hosts, storage class, resources.

Onyx: `deploy/helm/onyx-values.yaml` for the upstream chart at the pin: `global.version: v4.9.0`, each Onyx image's `tag` set to
`v4.9.0@sha256:<digest>` (the chart builds `repo:tag`, so a tag with a digest pins it; the chart has no separate digest field),
`nginx.enabled: false`, `configMap.DISABLE_TELEMETRY: "true"`, `auth.userauth.enabled: true` with its secret from an existing
Secret, the default Postgres and Redis passwords replaced by Secrets (the chart warns when they aren't), the "small" sizing of the
chart's `SIZING.md` with the Slack and Discord bots off, and the storage class from values. The chart and its pinned version are
recorded in `deploy/images.lock` beside the image digests.

### C.2 The repo service (engine lane)

`repo-pull` (a Sindri service, opt-in in the profile like `context-docs-refresh`, default every 1 minute):
- for each profile repo with a `pull:` block (`{ url, credential: env:NAME }`): the first time, `git clone --no-hardlinks` into
  `/data/repos/<name>` (the profile's `path` must be under `AW_SINDRI_REPOS_DIR` in pod mode); after that `git fetch origin
  <default branch>` and `git merge --ff-only`; a dirty tree or a non-fast-forward is a doctor row and a needs-you row, never a
  reset; the credential is passed through `GIT_ASKPASS` reading the pointer, never in a URL or an argument;
- `code-index-main-move` then sees HEAD move and indexes, and the map and docs refresh follow, as on a laptop.

`sindri repo add` in pod mode takes `--url` and the credential pointer instead of an existing path, and writes the `pull:` block.

### C.3 The owner's steps

| Step | How | One click? |
|---|---|---|
| Install | `helm install` the two releases with the deployer's values (a cluster admin installs the Postgres operator first, K2) | one command each |
| Open the app on the pod | `scripts/sindri-pod.sh connect [--context C] [--namespace N]`: `kubectl port-forward` to the owner's port, `kubectl exec` to read a fresh human token, a local mirror home (`control.json` with the forwarded loopback URL, the token, 0600) and `sindri-ui` started on it. The app needs no change: it still reads a loopback URL and a token from a home | one command |
| Approve the profile, recipes | in the app (its existing typed confirmations), or `kubectl exec -it sindri-0 -- sindri profile approve` | one click and the typed word, as today |
| Onyx's admin account and Sindri's Onyx token | `sindri-pod.sh connect --onyx` also forwards Onyx's web port and opens its sign-up page; then `kubectl exec -it sindri-0 -- sindri onyx connect` runs the managed mode's steps 6–9 (the owner's account, Sindri's dedicated user and token, the profile section) against the cluster address, with no Docker | one command, then the browser sign-up |
| Connect sources | in Onyx's page (Linear, Slack, GitHub, docs), the connector checklist `onyx up` prints | the owner's OAuth, in Onyx |
| Give everyone access | `sindri share token add <name> --repos …` in the app or at the terminal; the token goes into the internal MCP server's config for the `sindri-context` entries | one command per caller group |

`sindri onyx connect` is the one new Onyx verb: `onyx up`'s account stage (P3 of the Onyx spec) with the stack stage replaced by
"the URL answers `/api/health` and `/api/version` is the pin". It is a protected verb, like `onyx up`.

### C.4 Network policy

Ingress to the Sindri pod: the owner's port from nowhere (port-forward goes through the API server, not the pod network), the shared
door's port from the internal MCP server's namespace only. Egress: the Onyx release's Services, the git and forge hosts from values,
DNS. Onyx's pods: ingress from the Sindri pod and from port-forward only.

## 5. Part D: the whole map on open, a query zooms in

As written in `2026-10-23-sindri-context-whole-map-design.md`: the page shows the whole map the moment it opens, a query zooms the
camera into it, `GET /sindri/context/map/whole` with a node and an area level. In the pod it is the owner's page (through the
connected app). It is not on the shared door in this spec: a whole map is too large for an agent's context, and the five tools
already zoom.

## 6. Security

- **Two doors, two rules.** The owner's door keeps today's local-only model; its protected verbs need a terminal or the app's
  typed confirmation. The shared door is read-only by construction (one route, five read-only tools, no command route mapped on
  that listener), authenticated per caller, repo-scoped per token, fenced, and limited.
- **Secrets** live in Kubernetes Secrets and reach Sindri as `env:` pointers; the image and the chart's values hold none; the
  shared door's tokens are stored hashed; the Onyx token Sindri makes stays on the encrypted volume, owner-only.
- **No privileged containers.** No Docker socket, no host paths; `bwrap` needs user namespaces and nothing else, and without them
  the graph is off.
- **Supply chain.** Every image by digest, our own image built from the repo by CI, the Onyx chart at the pinned commit and
  version, `deploy/images.lock` checked in CI.
- **What everyone can read** is the map of the repos their token names: labels, kinds, edges, the tools' cited snippets, all of
  which already pass the egress scrub. Onyx's records reach them only through `context_query`'s cited results, as they reach a
  local agent today.

## 7. Testing

- **Unit:** pod-mode settings (unset is the desktop behaviour), the lease's stable host id, the shared listener's route and host
  rules, token hashing, expiry and repo checks, the per-token limit, `network: cluster` accepted only in pod mode and only for the
  listed hosts, `repo-pull`'s decisions (clone, fast-forward, dirty, diverged).
- **Integration:** the shared door against a temp home with two tokens (one repo each), every refusal code, the five tools'
  answers equal to `/sindri/mcp`'s goldens; `repo-pull` against a local bare repo; `sindri onyx connect` against
  `FakeOnyxServer` at a non-loopback name.
- **Heavy (nightly, and by hand per PR):** the image built and run (`docker run` with a volume: serve up, Temporal up, doctor ok,
  a restart takes the lease at once); the two charts on a local kind cluster (`deploy/test/kind-up.sh`): install, `sindri-pod.sh
  connect` opens the app, a share token answers `context_query` through the shared Service and is refused without it.
- **Owner check, once, on the real cluster:** install, connect, approve, set up Onyx, connect one source, mint a token, query from
  an agent through the internal MCP server.

## 8. Errors

New codes: `SND-AUTH-010` (no or bad bearer token), `SND-AUTH-011` (token doesn't allow that repo), `SND-API-014` (too many calls
in flight for this token), `SND-REPO-0xx` for `repo-pull` (clone failed, dirty tree, not a fast-forward, credential missing: the
next free range in `SindriErrors`), and pod-mode doctor rows. Part D owns `SND-CONTEXT-200..209`.

## 9. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Needs |
|---|---|---|---|
| **K-A1 image** | context | the Dockerfile, the start script, `deploy/images.lock` and its check, the heavy `docker run` proof, pod mode's switch (A2a) and lease host id (A2b) | nothing |
| **K-B1 shared door** | api | the second listener, its guard, share tokens (verbs, ledger, hashing), the five-tool MCP route, the new codes | nothing (pod-mode switch stubbed until K-A1 merges: the listener reads one env var) |
| **K-B2 Onyx by address** | context | `network: cluster`, `sindri onyx connect` | nothing |
| **K-C2 repo-pull** | engine | the service, `repo add --url`, its codes and doctor rows | nothing |
| **K-C1 charts** | context | the Sindri chart, the Onyx values, the NetworkPolicy, the snapshot schedule, the kind test | K-A1 (the image); K-B1 and K-B2 for the full kind test |
| **K-C3 connect** | ui | `scripts/sindri-pod.sh connect` (and `--onyx`), its bash test with a fake `kubectl` | K-A1 for the heavy proof only |
| **A2c doctor rows** | context | the pod-mode doctor rows | K-A1, K-B1 |
| **Part D W1–W4** | context, ui | as its own §9 | nothing (runs alongside everything) |
| **K-E2E** | context | the kind run of §7 end to end, the docs (`docs/sindri/deploy.md`) | everything above |

Parallel: at the start, five lanes are busy at once: context on K-A1 and Part D W1, api on K-B1, engine on K-C2, ui on Part D W3
(against W1's goldens) and K-C3. Context takes K-B2 after K-A1, then K-C1; ui takes W4 after W3. K-E2E is last. Every phase is one
PR with a scoped gate (build plus the unit and integration lanes of the touched projects; `Aw.Sindri.Ui.Tests` in any phase that
adds an API golden or a catalog row).

## 10. Left to the builders

The chart's exact resource numbers (measured on the kind run), the start script's details, the token format (random 32 bytes,
base64url, a `sk-sindri-` prefix so secret scanners know it), the snapshot class, and the kind cluster's version. Each is recorded
in the code or `docs/sindri/deploy.md` where it lands.
