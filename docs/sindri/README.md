# Sindri

Sindri is a local, always-on harness that picks up work, sizes it, starts agent sessions with cited context, keeps them on track and ships them. It asks the human only when it has to. Two things carry the name today:

- **The Sindri application**, designed below and built new in C# (not ported from TypeScript). None of it is built or installed yet.
- **The TypeScript `sindri/` package** ([below](#today-the-typescript-reference-sindri)). It runs at the desk now and is the reference for what each part does. It keeps running until the switch (readiness spec §4a step 3).

Specs: the core design `docs/superpowers/specs/2026-10-07-sindri-design.md`, and the factory addendum `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (A below), whose §17.28 (the workflow engine) and §17.29 (Sindri as an application) define the app. Decisions Q6 and Q7 are in A §17.17.

### Design specs for the application

Each part of the app has its own design spec under `docs/superpowers/specs/`. They refine A §17.27–§17.29 and win over it for their part. The stage names match `docs/csharp/sindri.md`.

| Part | Spec | Build step (A §17.29.4) | Stage |
|---|---|---|---|
| App shell: process, CLI, lease, ledger, profile, scheduler, doctor | `docs/csharp/sindri.md` (on `feat/sindri-s1-app-shell` until it merges) | 1 | S1 |
| API surface: commands, queries, projections, the event stream, auth v0, the MCP server, `sindri open` | `2026-10-10-sindri-api-surface-design.md` | 1 (the command layer and API) | API P0–P11 |
| **UI app design** (Avalonia, Linear design language): the control plane's views, actions and launching sessions | *Slot: `<date>-sindri-ui-design.md`, not written yet. It comes next, before any more building* | 1 (the UI app) | S1b |
| Workflow engine: recipes, blocks, validator, instances, timers, approval, the agent runtime (revision 7) | `2026-10-10-sindri-workflow-engine-design.md` | 2 | S2 (P1–P7) |
| Context retrieval engine: the product graph, adapters, planner, evals | `2026-10-10-sindri-context-retrieval-design.md` | 3 | S3 (Phases 1–10) |
| What the UI shows (input to the UI design) | `2026-10-09-sindri-dashboard-design.md` | — | — |

The specs were written in parallel. "Cross-spec reconciliation" below lists where they differed and the answer each spec now carries.

## The application

### What it is

One process per host, started by `sindri`, with a UI app as its client. It provides three engines (A §17.29.2):

| Engine | What it does |
|---|---|
| Control plane | The ledger, the command layer, the scheduler and host lease, the outbox, notifications, the API (HTTP plus an event stream) and webhook endpoints. Every surface is a client: the CLI, the UI app, the phone and Claude Code sessions |
| Workflow engine | Runs every piece of work as a workflow instance from a recipe (A §17.28) |
| Context retrieval engine | The context graph and its query planner over trackers, PRDs, notes, memory, the code index and the product map, all scrubbed and cited (A §17.3) |

### What it is not

It doesn't replace your terminal or your agent tool. Agent work, yours and the factory's, happens in Claude Code sessions in your terminal (Warp today). Sindri starts those sessions, feeds them context and tracks their work. It has no chat surface. The UI app is a window onto the control plane (the queue, status, decisions, receipts, metrics and "open in Warp"), not a chat (A §17.29.3). Its design is the UI app design spec (the slot in "Design specs for the application" above).

### Starting it

`sindri` starts the control-plane process under the host lease, starts the UI app and prints what's running: the approved profile, the index, intake sources, background services, the API address and what needs you. `sindri --headless` starts it without the UI, for the always-on box. Every other `sindri <verb>` is a client call to the running process, and starts the process first if needed.

Config is the profile (`profile.md`); changes still go through `sindri profile approve`. Background services run inside the process on the profile's cadence: the code index (watch, quick builds on change, nightly full build), intake polling, scorer and metrics, the weekly evolve job, and webhook listeners. These are today's launchd jobs moved into the process; launchd only keeps the process alive (A §17.29.1).

### The factory line

Each workflow is a recipe, and each starts the next (A §17.29.2):

intake → design studio → scoping → design → builder → tester → multi-lens validation → verification → shipper → PR babysitter

Design freezes the **packet** (`packet.json`, A §17.4), the handoff spec the rest of the line works from. Verification is the human's review and decision. Webhooks (alerts, errors, new tickets) start triage, incident response and comms workflows.

### Recipes and blocks

- A **recipe** is declarative YAML, validated by a JSON Schema, that names states, transitions, gates, evidence, effects, loop caps, timeouts and budgets. A new workflow, or a change to one, is a config change. Recipes can be generated per deliverable and evolved like other artifacts.
- A **block** is a typed C# module with its own contract and tests: a gate, an evidence provider, a Step implementation or an effect kind. New code is needed only for a new block.
- The **engine** owns everything else: state, fencing, idempotency, timers, park, escalate and takeover, loops, budgets and receipts.
- **Tools become blocks, sequences become recipes** (A §17.28.2). The finished C# tool ports (`Redaction`, `Packets`, `Readiness`, `BugFix`, and `Judge` and `Evidence` once they land) are blocks; a skill's phase sequence, such as `/bugFixOrchestrator`'s, becomes a recipe.
- Gates guard against an agent's honest mistakes. Containing a deliberately adversarial agent is the sandbox's job (A §17.28.3, `docs/csharp/bugfix.md` "Threat model").

### How you interject

- **A live session:** opening an item that has a running instance takes it over (the workflow pauses, timers stopped) until you hand back (`sindri handback`, or the app's button). "Attach" only focuses a session that's already live; typing into it changes nothing in Sindri.
- **No live session** (between steps, at a gate, parked): `sindri open <item> [--with claude|codex|cursor]`, or "open" in the app, starts a session in Warp with Claude Code, Codex or Cursor, seeded with the item id and repo; the session pulls the item's state, packet, evidence and context through Sindri's MCP endpoint. If the item has a live instance, opening it takes it over (`pause`) until you hand back, which the app offers. `--attach` focuses a session that's already live.
- **From your own sessions:** the `sindri` CLI and a Sindri MCP server give an item's state and context, run a retrieval query, or file a request ("re-scope this"). Calls from a session are requests, never decisions. Approvals stay human actions, through authenticated verbs and UI forms.

### Build order

Not a module-by-module port of `sindri/` (A §17.29.4):
1. app shell: the process and CLI, profile, ledger, host lease, command layer and API, scheduler, UI app shell;
2. the workflow engine and recipe format;
3. the context retrieval engine, over the sources already reachable;
4. blocks, each when the first recipe needs it (the code index, scorer, scoping and evolve are rebuilt from their TS reference);
5. workflows down the factory line, intake first;
6. the switch, once every desk tool has its C# form.

Within steps 1–3 the specs' phases land in this order. It is the only cross-spec order: each wave is one more than the latest wave a phase needs, from each spec's own dependency column. Each spec's internal wave table only orders its own phases inside it (X19, X39).

| Wave | Lands | Needs |
|---|---|---|
| 0 | S1 (#163); API P0; context S-1C, S-2R, S-FX; engine P1 and P3 (stacked on S1, add files only) | — |
| 1 | API P1 (command chain, callers, the `human` token, `sindri-prove.sh`) | S1, API P0 |
| 2 | API P2 (projector); engine P2 (registry, challenge); context S-1A | API P1 (+ engine P1; S-1C, S-FX) |
| 3 | API P3 (items), P5 (stream); context S-1M (`/sindri/mcp`), S-2W, S-3, S-5A | API P2; S-1A |
| 4 | API P4 (decisions, needs-you), P6 (jobs); engine P4; context S-3M | API P3; API P2 + P5; engine P2 + P3 + API P3; S-1M + S-3 |
| 5 | API P8 (`sindri_*` tools), P11 (engine item states); engine P5, P6; context S-4, S-5B, S-10 | API P4 + P6; API P4 + engine P4; engine P4; S-3 (+ S-5A) + API P6; engine P4 |
| 6 | API P10 (launch contract, `sindri open`); engine P7; context S-6, S-7A, S-CF | API P3 + P8 + the UI design spec; engine P5 + P6; S-5B; S-3 + S-4; S-4 + S-5B |
| 7 | Engine P8 (agent runtime); context S-7B | engine P7 + API P10 + Judge (#156); S-4 + S-6 + S-7A |
| 8 | Context S-8 | S-7B |

The UI design spec comes before any app phase beyond the S1b shell, and before API P10 (it sets the launch targets). The scoping recipe's spec comes after it. Context S-9 waits for the code index block.

### Cross-spec reconciliation

Where the API (API), workflow engine (WF) and context retrieval (CTX) specs disagreed. The owner settled X2 (the app is a full desk client, not read-only), X21, X22 and X17's PR decision, and asked for the recommended answer on the rest; X19–X42 come from the epic's two `/autoplan` passes (2026-10-11); each answer is now written into the spec named in "Changes" (2026-10-11). X17 is a build-order plan, not a spec change.

| # | Where they differed | Answer | Changes |
|---|---|---|---|
| X1 | **Who may do what.** API §4/§6: caller class (`human`, `session`, `service`) plus a terminal flag, action classes, `SND-AUTH-002`, the `X-Sindri-*` headers. WF §6.2: actor classes `session`, `human`, `script`, `ui`, `human-terminal`, `system`, `SND-WF-007` | API's model is the one model. WF keeps its server challenge (§4.2) as *how* a `protected` action is confirmed, replacing S1's `confirmed` flag, and its harness probe (P2) becomes the source of API §6's agent-variable list | WF §1 W22, §6.2, §6.3, §8, §10 |
| X2 | **What the UI may do.** WF §6.2: the UI is read-only "until the UI's own design adds its forms". API §4.2: the app is `human` without a terminal: decisions, desk, job, request and pause; `protected` shows the CLI to copy | API's table (owner, 2026-10-11): the app starts workflows (scoping included), answers parks, takes over and hands back, and hands work off to Warp with Claude Code, Codex or Cursor. The UI design spec maps it screen by screen | WF §6.2; UI design |
| X3 | **Takeover.** API: `pause`, open to sessions (the hook detects it inside the session). WF: a `human` verb | API's answer: a session may take over, never hand back | WF §6.3 |
| X4 | **One write path.** API §2.1: every command runs through `CommandGate` (5 s), audit and authorization, decides without slow work, and appends one domain event. WF §5.3: `Commit` writes its own tables and events, `--accept` re-evaluates gates (which may call a model), and WF says one event per `Commit` (§5.2) but one per row changed (W13) | Engine commands are handlers behind API's chain. Gates that call a model run before the gate is taken (WF already stores gate answers in their own transaction); the handler only decides. WF picks one of "one event per `Commit`" or "one per row" | WF §1 W13, §5.2, §5.3; API §2.1 note |
| X5 | **One needs-you list, one notifier.** WF: `workflow list --needs-me` and `notify.desk` OS notifications. API: `GET /needs-you`, and the app drives OS notifications and the tray badge from the stream | `GET /needs-you` is the list; `--needs-me` reads it. `notify.desk` is the only source of OS notifications; the app shows its badge and in-app pulse but doesn't notify on its own, so nothing fires twice | WF §6.3; API §5.1 |
| X6 | **Item identity.** API: `ItemId` is `ITM-…` or a tracker key (`ENG-142`). CTX D16: a Linear key changes when an issue moves team, so records key on Linear's UUID | `ItemId` is always `ITM-…`; tracker keys are aliases and linked records (`linear:issue/<uuid>`). WF's `fact:item.id` and addresses (`T-3/item/1`) use `ItemId` | API §3; WF §12 |
| X7 | **Item state from the engine.** API §3.6: once S2 runs, `needs-answer`, `parked`, `running`, `queued` and `taken-over` "come from the engine's item projection". Nobody owns that projection | WF P4 emits the events; API owns the projection, in the first API phase after WF P4 | API §3.6, Phases |
| X8 | **One MCP server.** API §8: `sindri mcp`, stdio, `sindri_*` tools, repo from the working directory. CTX §8.5: Streamable HTTP at `/mcp`, `context_*` tools, registered per work repo | One server with both tool sets, stamped `session`, repo-scoped the same way. Recommended transport: HTTP on the API port (it survives a restart, and S3 is building toward it), with the repo carried by the per-repo registration | API §8; CTX §3, §8.5 |
| X9 | **Context routes and CQRS.** CTX: `POST /sindri/context/query` (a query on a POST), and map import, build, promote and review as routes with their own terminal rule. API: `ARCH-CQRS-001` (every POST is a command), the action classes, and `GET /context?q=` in §11. S3 (#166) already maps the POST | Query by `GET /context` (or an explicit, tested carve-out for query-by-POST). Map `build` is `job`, `import` and `promote` are `desk`, `review --apply` is `protected`. Every route gets a catalog row | CTX §8.5; API §3, §11 |
| X10 | **State home.** S1 and API: `$AW_SINDRI_HOME` (default `$AW_STATE_DIR/sindri-app`). WF §6.1 and §6.3: `AW_STATE_DIR=$(mktemp -d)` and `$AW_STATE_DIR/sindri/notifications.jsonl`, which is the TS Sindri's folder | Everything under `$AW_SINDRI_HOME`; WF's dev loop uses API §12's `scripts/sindri-prove.sh` | WF §6.1, §6.3, Phases |
| X11 | **Where the C# docs and error codes live.** API: `docs/csharp/sindri.md`. WF: `docs/csharp/sindri-workflow.md`. CTX: `docs/sindri/context.md` and `docs/sindri/errors.md` (which is generated from the TS build) | Contracts and codes in `docs/csharp/sindri*.md` (add `sindri-context.md`); `docs/sindri/` stays the human overview | CTX §8.4–§8.6, Phases |
| X12 | **Exit codes.** S1, API and WF: 0, 1, 2. CTX adds 75 for a busy heavy-job lock | Adopt 75 app-wide for "lock busy" (Evidence already uses it) and add it to S1's table | `docs/csharp/sindri.md`; API §9 |
| X13 | **The heavy-job lock.** WF: `waitFor: { resource: heavy }` has no allocator. CTX: `IHeavyJobLock`, supplied by Sindri over the two-slot lock | One Sindri lock port is both the engine's `heavy` allocator and CTX's `IHeavyJobLock` | WF §3.4; CTX §4 |
| X14 | **The `context.retrieve` block** (CTX Phase 10) fills a Step's injection plan. WF's block kinds are gate, evidence provider, Step and effect, and none of them is "input to a Step" | WF adds a Step-input kind (or a documented use of `IEvidenceProvider`) before CTX Phase 10 | WF §2; CTX Phase 10 |
| X15 | **Launching sessions.** API P10: `OpenItem` and `ILauncher`, Warp only. WF §7.2: `ILauncher` is "the next engine design". README: Claude Code only | API P10 owns `ILauncher`. The UI design defines the launch targets: Warp plus Claude Code, Codex or Cursor, chosen per item or by profile default, and "attach" for a live session | API P10, §3.5; WF §7.2; UI design |
| X16 | **Two timelines.** API `GET /items/{id}/timeline`; WF `workflow events <address>` | The item timeline includes its instances' events; `workflow events` is the instance-level filter of the same read | API §3.1; WF §6.3 |
| X17 | **Build order versus what's built.** S2 (#167), S3 (#166), S4a (#169) and the UI shell S1b (#168) were built on S1 ahead of API P1, and differ from the specs | **The specs win, and the built PRs are references (owner, 2026-10-11).** #166, #167 and #169 stay open as draft reference branches and are never merged; each phase is rebuilt from its spec in the build-order waves, reusing their code where it fits. Divergences to drop on rebuild: #167's built-in recipes that ship approved, its block types, routes and verbs; #166's placement in `src/Sindri`, `POST /context/query`, the `LINEAR_API_KEY` variable (secrets are pointers) and the ConnectAI source (declined); #169's routes, its doc and `collect` verb. #169's Judge and Evidence blocks are rebuilt in engine P8 and the first recipe that needs evidence. #168 stays draft until the UI design spec is approved | Plan; the four PRs |
| X18 | **The model provider.** Judge (#156) has `IModelProvider` and `IProviderCatalog`. CTX's labeler called the `claude` CLI, and no spec said every model call goes through one port | Every model call in Sindri (rung-2 and rung-3 gates, labeling, rerank, adjudication) goes through Judge's provider port, and agent sessions go through `ILauncher` with a target (Claude Code, Codex or Cursor). Switching provider, an API key included, is a profile choice and an adapter, never an interface change. Context reaches it through its own `IModelPort`, adapted in Sindri's Host (tiers forbid Context → Judge). Provider keys are secret pointers only, never values in the profile | CTX §4, §11; API P10 |
| X19 | One build order, stated four ways (E3) | The build-order table above; every spec cites it | README; all three |
| X20 | `SND-API-004` and `-005` were reserved anew but S1 already uses them (E1) | The API's new codes are `SND-API-011` (views not reached) and `-012` (stream limits) | API §1, §9 |
| X21 | Nothing let a launched agent session report its Step (E2) | Owner decision: an engine-owned agent runtime launches sessions through `ILauncher`; the session reports with a run-scoped `step-report` class; the engine still decides through gates; tokens are metered through the provider port | WF §6.5, P8; API §4.1, P10 |
| X22 | Codex and Cursor MCP registration (E6) | Owner decision: never registered; `ILauncher` passes the endpoint at launch time. Until P10 proves a target's form, it launches without Sindri MCP. Doctor warns on user-wide entries for all three providers | API §8, P10 |
| X23 | Engine P4 started instances on items nothing created; `T-n` ids (E4) | Engine P4 depends on API P3; signals create the item first. Context Phase 6 links items to Linear records (`item.linked`) | WF Phases; CTX Phase 6 |
| X24 | Three ways to act as the human in proofs (E5) | `scripts/sindri-prove.sh` everywhere: `<verb>` as the agent, `human <verb>` as the owner | API §12; WF Phases; CTX |
| X25 | Context reads skipped the egress switch and repo scope on CLI and HTTP (E7) | `context.sessions.enabled` gates every session read on any transport; repo scope defined over files, nodes and mapped Linear teams | CTX §3 |
| X26 | Nonce and challenge `POST`s broke `ARCH-CQRS-001` (E8) | Two listed carve-outs with catalog rows of kind `issue`; a session may get a nonce for `takeover` | API §2.5 |
| X27 | Context jobs would run inside the 5 s command gate (E10) | Map build and sync stages wait for API P6 and run as jobs | CTX stage table |
| X28 | Engine states without item states (E12) | The engine-to-item state map; every park and escalation sits in `waitingOnYou` | API §3.6 |
| X29 | Opening vs takeover vs attach (E13, E14, E21) | Opening an item with a live instance takes it over; launches are recorded (`session.launched`/`ended`, `ItemView.session`); attach only when live; no double launch | API P10; README "How you interject" |
| X30 | Protected actions listed three ways (E15–E17) | `answer-grant-tokens` named; map review, `import` and `promote` are `protected` with the shared challenge; `SND-CONTEXT-041` retired | API §2.1, §3.7; CTX §8.6–§8.7 |
| X31 | The trusted `human` header now guards every desk action (E19) | A per-start `human` token before the app's first write | API §6 |
| X32 | Exit codes, ledger events, fencing, notifications (E18, E23, E24, E26) | Context uses S1's exit codes (1 when not running, 0 when empty); context commands write ledger events; `sindri_*` text is fenced like `context_*`; the app notifies only until engine P6 | CTX §3, §8.6; API §5.1, §8 |
| X33 | Leftovers (E25, E27, E28) | `workflow.request` dropped (accepted requests reach instances as `decision.recorded`); `EventFactory` owned by API P1; the engine scaffolds with `--slice-only`; `sindri mcp register` ships with whoever creates `/sindri/mcp` | WF §5.2, §7; API §3.4, §8 |
| X34 | The app should start scoping, but no spec owned a scoping workflow, and recipes had no input metadata for a start form (E11) | Recipe `inputs` carry a label and type, returned by `recipe show` and `GET /workflow/recipes`, so the app's start form lists approved recipes and their inputs. The scoping recipe is factory step 5's first workflow, built on the context engine; its spec is the next one after the UI design. Until it exists, "start" offers whatever recipes are approved | WF §3.2; README build order |
| X35 | Nothing wrote `session.ended`, so attach and the double-launch guard never cleared and dead agent Steps waited out their timeout (P2-1) | Every launched command runs under `sindri session wrap`, which heartbeats and reports exit; `service:launcher` writes `session.ended` (`exit` or `lost` after 120 s) | API §8.1, P10 |
| X36 | `step-report` had no credential; the run token is public; one fixed launch file let launches cross (P2-2) | A per-launch secret, read once from a 0600 file by wrap and passed only by environment, sent as `X-Sindri-Launch-Secret`; per-launch Warp files; `SND-AUTH-003` | API §6, §8.1; WF §6.5 |
| X37 | The engine had no route table; addresses contain `/`; forms had no fields (P2-3) | An engine route table with typed instance ids, request bodies, base64 files, and `GetInstance` returning the open Step's outcomes and outputs | WF §6.3 |
| X38 | The state map contradicted the group table, and park rows didn't say which answer they need (P2-4) | One group table (`parked` is in `waitingOnYou`); escalation by gate, timeout and child; `answerKind` on park rows | API §3.6 |
| X39 | Several build orders (P2-5) | The derived wave table above; each spec's graph gained its cross-spec edges and points here | README; all three |
| X40 | The `human` token had no phase, code or header; the prover couldn't act as human (P2-6) | API P1 builds it (`X-Sindri-Token`, `SND-AUTH-001`); `sindri-prove.sh human` reads it from the guarded home and sets a marker the server honours only for a temp home | API §1 D4, §6 |
| X41 | `OpenItem` broke the chain's rules; attach meant three things; unattended runs needed Warp (P2-7, P2-16) | `OpenItem` appends one event; `service:launcher` takes over and launches after commit; attach only focuses; two target kinds (`interactive` Warp, `process` for the agent runtime and `--headless`); ids only at launch, context pulled through MCP; usage read, never self-reported | API §8.1; WF §6.5 |
| X42 | Leftovers (P2-9 to P2-15) | CTX §3 follows X22 and S-1M owns the three-provider doctor check and the worktree probe; only `protected` promote makes a map current; `issue` kind in API §4; `item.linked` and `CreateItem` `link?`; `EventFactory` owned by API P1; engine P8 owns the Sindri → Judge edge with a recorded fan-out exception; `context.repoTeams` in the profile example | CTX §3, §8.7, §8.8; API §3, §4; WF P8 |

## Today: the TypeScript reference (`sindri/`)

The TS package was built in plans, and is what runs now. What exists (the code index is documented in `index.md`):

| Command | What it does |
|---|---|
| `sindri profile init [--ring0 [--plans <pattern>]...] [--dir DIR] [--force]` | Scaffold a profile in `mode: shadow`. `--ring0` makes one for the current repo, using its plan files as the backlog; `--plans` limits it to matching plan files; `--force` overwrites an existing profile |
| `sindri profile validate \| explain <key> \| migrate \| approve [hash]` | Check the profile, show where a value comes from, upgrade it, approve a change. Changes take effect only once approved, and approving needs you at a terminal |
| `sindri observe [--no-record]` | List open work with sizes and what auto-small would start. Records it in the ledger when the profile is approved and this is the active host |
| `sindri ledger [--item ID] [--since 7d]` | Show recorded events |
| `sindri scrub [--staged] [--install-pre-commit [--repo PATH]]` | Redact secrets from stdin, check staged changes, or install the pre-commit hook that refuses secret-shaped strings (in the current repo, or the one `--repo` names) |
| `sindri index setup \| build \| status \| query` | The code index: set up Ollama, graphify and the sandbox; build it; show its freshness and layers; look up a symbol's clones. See `index.md` |
| `sindri repo add <path> [--name NAME]` | Add a repo to the profile (then `sindri profile approve`) |
| `sindri repo onboard [<path>] [--name NAME] [--no-build]` | Onboard a repo in one command: add it to the profile, stop for `sindri profile approve` (exit 1 until a human approves at a terminal), then install the pre-commit hook and build its index |
| `sindri repo onboard --template` / `sindri repo status [<path>] [--nudge]` | Opt-in git template so new clones get the pre-commit hook (a no-op until the repo is approved); whether a repo is onboarded (the SessionStart nudge uses `--nudge`) |
| `sindri scope <brief.md \| linear:<project>> [--section N] [--sources LIST] [--out DIR] [--dry-run]` | Turn a brief into a cited scope map: surfaces, implications, workstreams, acceptance checks, open questions. Inside a git worktree only `--sources file,code` is allowed. See `scope.md` |
| `sindri scope --backtest linear:<project> [--window 1d] [--with-index]` | Replay a past project: recall, precision and a brief-only baseline against a pass bar |
| `sindri scope runs [--json]` | The latest scoping runs: mode, status, surfaces, recall, precision, output file |
| `sindri shape --record --staged \| report` | Record shape signals for staged changes (the pre-commit hook runs this); report signals, outcomes and precision. Record-only: nothing blocks a commit |
| `sindri evolve <init\|status\|check\|telemetry\|reflect\|correct\|proposals\|show\|reject\|tier\|compare\|stage\|publish\|adopt\|revert\|weekly>` | Self-evolution: an artifact registry, module eval suites, typed proposals from merged PRs, repeated corrections and hook telemetry, offline blind comparisons. See `evolve.md` |
| `sindri channel <status\|promote\|rollback>` | Stable and next installs of sindri itself; promotion needs a passing suite at that sha, a soak and a typed confirmation. See `evolve.md` |
| `/reflect`, `/correct` (skills) | Review finished work, or find a repeated mistake class, and print typed proposals without editing anything; `sindri evolve reflect` and `correct` record them. See `evolve.md` |
| `sindri doctor` | One line per health check, `ok` / `warn` / `fail`, each with a fix |

Every read command takes `--json`. Exit codes: `0` ok, `1` attention needed, `2` error. Errors carry a stable code (`SND-<AREA>-<NNN>`); see `errors.md`.

## Design language

Sindri's UI (the dashboard and any other screen it grows) follows `DESIGN.md` in this folder: Linear's design language (near-black canvas, light gray text, one lavender-blue accent, hairline-bordered charcoal panels). It was added with `npx getdesign@latest add linear.app --out docs/sindri/DESIGN.md` from [awesome-design-md](https://github.com/VoltAgent/awesome-design-md/tree/main/design-md/linear.app) (MIT; also at https://getdesign.md/linear.app/design-md). Agents read it before writing any Sindri UI, and it's the reference to feed `/design-language` when the dashboard's tokens are made. Edit it as Sindri's look evolves.

## Quick start (this repo, ring 0)

```bash
scripts/install-sindri.sh                 # or ./setup.sh --with-sindri
sindri profile init --ring0 --plans '*-sindri-plan-*'   # profile for this repo; backlog = the Sindri plans
sindri profile validate
sindri profile approve                    # shows the hash and what changed
sindri profile approve <hash>             # you, at a terminal: type the first 6 characters to confirm
sindri repo onboard .              # add, approve (at a terminal), hook, index
sindri doctor                             # expect every line ok
sindri observe                            # the remaining Sindri plan tasks, in order
```

## Where things live

| Path | What |
|---|---|
| `$AW_STATE_DIR/sindri/ledger.db` | The ledger (SQLite, 0600). Schema in `planning/ERD.md` |
| `$AW_STATE_DIR/sindri/sindri.lock/` | The singleton lock (spec §9.1) |
| `$AW_STATE_DIR/sindri/profile-approved/<hash>/` | Snapshots of approved profiles. Runtime commands such as `observe` load the latest approval's snapshot, not the live files (spec §8.7); `approve` diffs against it |
| `$AW_STATE_DIR/sindri/index/<repo>.db` | The code index, one per repo (rebuilt, never migrated). See `index.md` |
| `$AW_STATE_DIR/sindri/spool/` | Shape-signal files the pre-commit hook writes; the hourly `observe` and `shape reconcile` move them into the ledger |
| `$AW_STATE_DIR/sindri/mirrors/<repo>.git` | Bare mirror of each indexed repo, made by `index build` (full history, 0700) |
| `$AW_STATE_DIR/profile` | The profile, or a link to your private profile repo (`--profile` and `AW_PROFILE_DIR` override it) |

`$AW_STATE_DIR` defaults to `~/.agentic-workflow`.

Not to be confused with `./setup.sh --profile <web-app|ios|personal>`, which applies a Claude Code settings profile to a repo. A Sindri profile is the `profile.yaml` described in `profile.md`.

## Plan files as a backlog

The `plan-file` tracker reads each `### Task N: …` heading as one item and its `- [ ]` / `- [x]` steps as progress. A task is done when every step is ticked. Code inside fenced blocks is ignored, so keep shell snippets fenced: an unfenced `# comment` line would end the task early.

## When to use which

| You want to… | Use |
|---|---|
| Fix one bug ticket, by hand, now | `/bugFixOrchestrator <ticket>` |
| Turn an approved spec into proven PRs, by hand | `/specToProvenPR` |
| Review a plan before building it | `/autoplan` |
| See the backlog and what Sindri would start | `sindri observe` |
| Have work picked up, run and shipped for you | The factory line in the Sindri application (not built yet; see "The application") |
