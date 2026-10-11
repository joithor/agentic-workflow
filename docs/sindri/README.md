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
| API surface: commands, queries, projections, the event stream, auth v0, the MCP server, `sindri open` | `2026-10-10-sindri-api-surface-design.md` | 1 (the command layer and API) | API P0–P10 |
| **UI app design** (Avalonia, Linear design language): the control plane's views, actions and launching sessions | `2026-10-11-sindri-ui-design.md` (mockups in `2026-10-11-sindri-ui-design/`). It settles X2, X5, X15 and X17 for the UI and adds X18–X22 | 1 (the UI app) | S1b |
| Workflow engine: recipes, blocks, validator, instances, timers, approval | `2026-10-10-sindri-workflow-engine-design.md` | 2 | S2 (P1–P7) |
| Context retrieval engine: the product graph, adapters, planner, evals | `2026-10-10-sindri-context-retrieval-design.md` | 3 | S3 (Phases 1–10) |
| What the UI shows (input to the UI design) | `2026-10-09-sindri-dashboard-design.md` | — | — |

The specs were written in parallel and don't fully agree yet. "Cross-spec reconciliation" below lists where they differ, and which spec each fix belongs to. Settle those before the next phase of any of them is built.

## The application

### What it is

One process per host, started by `sindri`, with a UI app as its client. It provides three engines (A §17.29.2):

| Engine | What it does |
|---|---|
| Control plane | The ledger, the command layer, the scheduler and host lease, the outbox, notifications, the API (HTTP plus an event stream) and webhook endpoints. Every surface is a client: the CLI, the UI app, the phone and Claude Code sessions |
| Workflow engine | Runs every piece of work as a workflow instance from a recipe (A §17.28) |
| Context retrieval engine | The context graph and its query planner over trackers, PRDs, notes, memory, the code index and the product map, all scrubbed and cited (A §17.3) |

### What it is not

It doesn't replace your terminal or your agent tool. Agent work, yours and the factory's, happens in Claude Code sessions in your terminal (Warp today). Sindri starts those sessions, feeds them context and tracks their work. It has no chat surface. The UI app is a window onto the control plane (the queue, status, decisions, receipts, metrics and "open in Warp"), not a chat (A §17.29.3). Its design is the UI app design spec (`2026-10-11-sindri-ui-design.md`, listed in "Design specs for the application" above).

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

- **A live session:** attach in Warp and type. That's a takeover: the workflow pauses, timers stopped, until `sindri handback`.
- **No live session** (between steps, at a gate, parked): `sindri open <item>`, or "open in Warp" in the UI, starts a Claude Code session in your terminal, seeded with the item's state, packet, evidence and context. The item is taken over until handback.
- **From your own sessions:** the `sindri` CLI and a Sindri MCP server give an item's state and context, run a retrieval query, or file a request ("re-scope this"). Calls from a session are requests, never decisions. Approvals stay human actions, through authenticated verbs and UI forms.

### Build order

Not a module-by-module port of `sindri/` (A §17.29.4):
1. app shell: the process and CLI, profile, ledger, host lease, command layer and API, scheduler, UI app shell;
2. the workflow engine and recipe format;
3. the context retrieval engine, over the sources already reachable;
4. blocks, each when the first recipe needs it (the code index, scorer, scoping and evolve are rebuilt from their TS reference);
5. workflows down the factory line, intake first;
6. the switch, once every desk tool has its C# form.

### Cross-spec reconciliation (settle before building further)

Where the API (API), workflow engine (WF), context retrieval (CTX) and UI app design specs disagree. The UI spec (§13) recommends answers for X2, X5, X15 and X17 and adds X18–X25. Each row names the recommended answer and the spec that changes. Every row is the owner's call; none is decided by this list.

| # | Where they differ | Recommended answer | Changes |
|---|---|---|---|
| X1 | **Who may do what.** API §4/§6: caller class (`human`, `session`, `service`) plus a terminal flag, action classes, `SND-AUTH-002`, the `X-Sindri-*` headers. WF §6.2: actor classes `session`, `human`, `script`, `ui`, `human-terminal`, `system`, `SND-WF-007` | API's model is the one model. WF keeps its server challenge (§4.2) as *how* a `protected` action is confirmed, replacing S1's `confirmed` flag, and its harness probe (P2) becomes the source of API §6's agent-variable list | WF §1 W22, §6.2, §6.3, §8, §10 |
| X2 | **What the UI may do.** WF §6.2: the UI is read-only "until the UI's own design adds its forms". API §4.2: the app is `human` without a terminal: decisions, desk, job, request and pause; `protected` shows the CLI to copy | API's table. The UI design spec confirms it screen by screen | WF §6.2; UI design |
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
| X17 | **Build order versus what's built.** Every spec has its later phases wait for S1 to merge. S2 (#167), S3 (#166), S4a (#169) and the UI shell S1b (#168) are already built on S1, ahead of API P1 (the authorization spine), so they bring their own surfaces outside it | Merge S1, then API P0–P2, then rebase S2 and S3 onto the chain. Keep S1b in draft until the UI design spec is approved | Plan, not a spec |
| X18 | **Opening is a takeover.** API P10: "the engine marks the takeover once S2 runs", with no mechanism | A WF trigger kind `open-request` applied in `Commit` (actor `service:engine`); the launcher opens Warp only after it lands (UI spec E-6) | WF §5.4, §5.7; API P10b |
| X19 | **Launching is I/O inside a command.** API P10's `OpenItem` returns `launchedIn` | `OpenItem` records the request; a launcher service performs it once, inside a 30 s deadline, and records the outcome (UI spec E-1) | API P10a, §3.2, §3.4 |
| X20 | **Taking over a running Step.** WF says nothing about a worker that is mid-run | The engine phase that adds a headless worker runtime owns stopping it (`run.stopped`); until then Take over only pauses | WF (a later phase) |
| X21 | **Secondary actions and labels on needs-you rows.** API §3.6 has one `primaryAction` and a four-value `kind` | `NeedsYouRow.actions` and `label` (UI spec E-7) | API §3.6 |
| X22 | **DESIGN.md fails WCAG** in its focus ring (1.8:1) and primary hover (2.87:1), and has no error colour | Ring 2 px `#828fff`; hover `#535fc6`; `Error` `#eb5757` (UI spec §11.1, §14.1) | `docs/sindri/DESIGN.md` |
| X23 | **The engine's nonce mint is a POST that appends nothing**, against `ARCH-CQRS-001` | A tested carve-out, or a catalog row with the class of the command it serves | WF §5.4; API §2.5 |
| X24 | **The dashboard's "every agent" goal is deferred** by the UI spec | Agents is the first view after the collector spec | API §11; DB §1 |
| X25 | **API §5.1 and WF §6.2 still describe the old UI role** (the app notifying; a read-only `ui` actor) | Per X2 and X5 as settled in the UI spec §13 | API §5.1; WF §6.2 |

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
