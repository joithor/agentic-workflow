# Sindri

Sindri is a local, always-on harness that picks up work, sizes it, starts agent sessions with cited context, keeps them on track and ships them. It asks the human only when it has to. Two things carry the name today:

- **The Sindri application**, designed below and built new in C# (not ported from TypeScript). None of it is built or installed yet.
- **The TypeScript `sindri/` package** ([below](#today-the-typescript-reference-sindri)). It runs at the desk now and is the reference for what each part does. It keeps running until the switch (readiness spec §4a step 3).

Specs: the core design `docs/superpowers/specs/2026-10-07-sindri-design.md`, and the factory addendum `docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` (A below), whose §17.28 (the workflow engine) and §17.29 (Sindri as an application) define the app. Decisions Q6 and Q7 are in A §17.17.

## The application

### What it is

One process per host, started by `sindri`, with a UI app as its client. It provides three engines (A §17.29.2):

| Engine | What it does |
|---|---|
| Control plane | The ledger, the command layer, the scheduler and host lease, the outbox, notifications, the API (HTTP plus an event stream) and webhook endpoints. Every surface is a client: the CLI, the UI app, the phone and Claude Code sessions |
| Workflow engine | Runs every piece of work as a workflow instance from a recipe (A §17.28) |
| Context retrieval engine | The context graph and its query planner over trackers, PRDs, notes, memory, the code index and the product map, all scrubbed and cited (A §17.3) |

### What it is not

It doesn't replace your terminal or your agent tool. Agent work, yours and the factory's, happens in Claude Code sessions in your terminal (Warp today). Sindri starts those sessions, feeds them context and tracks their work. It has no chat surface. The UI app is a window onto the control plane (the queue, status, decisions, receipts, metrics and "open in Warp"), not a chat (A §17.29.3).

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
