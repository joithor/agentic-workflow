# Sindri API surface: commands, queries, the event stream and the MCP server

Status: approved in conversation, decision by decision, 2026-10-10. Revised the same day after `/autoplan` (rev 2;
§13 lists every finding and how it was resolved). This document is the written spec for review.

It designs the surface every Sindri client uses:
- the `sindri` CLI;
- the Avalonia app (`SindriUi`);
- the Sindri MCP server (`sindri mcp`);
- the workflow engine's calls into the command layer.

`/specToProvenPR` takes the **Phases** at the end to PRs with an agent team. The UI design builds on §3, §5 and §3.6.

**Builds on:**
- the factory addendum (`2026-10-09-sindri-factory-addendum.md`):
  - §17.27.1, one command layer;
  - §17.27.2, action classes;
  - §17.27.4, the command registry;
  - §17.21, the event envelope and the API;
  - §17.29, the application, `sindri open`, and MCP calls being requests;
  - §17.12 and §17.23.7, decisions;
- the core design (`2026-10-07-sindri-design.md`): §9.3 (takeover) and §10.3 (the CLI);
- the dashboard spec (`2026-10-09-sindri-dashboard-design.md`), which says what the UI shows;
- the workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`), which owns instances, park, takeover and
  handback (§3.5);
- `.agents/rules/csharp.md` and `docs/csharp/scaffolds/new-slice.md`.

**Fixed, from stage S1** (the app shell, branch `feat/sindri-s1-app-shell`, contract `docs/csharp/sindri.md`):
- the routes `GET /sindri/status`, `GET /sindri/ping`, `POST /sindri/stop`, `POST /sindri/profile/approve` and
  `GET /sindri/events`;
- `LoopbackBinding` and `LoopbackGuard`;
- the `Core` types under `Control`, `Ledger`, `Lease`, `Profile`, `Scheduling` and `Doctor`;
- `SindriErrors`: `SND-LEASE-001…003`, `SND-PROFILE-001…005`, `SND-LEDGER-001/002`, `SND-API-001…003`,
  `SND-CLI-001/002`, `SND-SCHED-001`;
- `HttpResults`;
- the process model: `sindri serve`, `control.json`, the spawn, and the pid and epoch check against `/ping`;
- the CLI-local verbs `doctor` and `profile validate`.

This design adds to those. Where it has to change one of them, §10 lists the conflict.

## 1. Decisions

| # | Question | Decision |
|---|---|---|
| D1 | Resource model | **CQRS** over resource paths. Commands are `POST`s that append one ledger event and return `CommandAccepted`. Queries are `GET`s that read projections. One route = one slice = one `IService` (§2, §3) |
| D2 | Commands vs queries, decisions vs requests | Every command declares an **action class**; every caller has a **caller class**; one table authorizes the pair. Sessions may run queries and file **requests**, never decisions (§4) |
| D3 | Event stream | **Server-Sent Events**, `GET /events?after=`. The cursor is the ledger sequence, events use a public envelope, and a client too far behind gets `reset`. It tells live clients to re-read; it's never the source of truth (§5) |
| D4 | Local auth | **v0 is local-only, with no tokens.** The caller class comes from `X-Sindri-Caller`. A missing header counts as `session`; only the CLI at a terminal and the app send `human`. Commands refuse any `Origin` header. Tokens, passkeys and the tailnet come later (§6) |
| D5 | Finding or starting the process | S1's `control.json`, spawn and ping check, plus one rule for which verbs start the process. Nothing run inside an agent session starts it (§7) |
| D6 | MCP server | `sindri mcp` (stdio, the MCP C# SDK) is a thin client with the session caller class: queries and `sindri_request` only. **It's proven by a real Claude Code subagent**, run as a fresh `claude -p` process (§8) |
| D7 | Webhooks | **Cut for now** (§11) |
| D8 | Errors, versioning, JSON | S1's `Result` → `ProblemDetails` mapping is unchanged. New codes are reserved per phase and documented in `docs/csharp/sindri.md`. There's no path version in v0, and one JSON context. `--json` prints the API body unchanged (§9) |

## 2. The CQRS model

### 2.1 Write side: commands

**What a command is.**
- A command is a request record that implements the marker `ICommand<TResult>` (`Aw.Sindri.Core.Commands`).
- `TResult` is `CommandAccepted`, or a record carrying it plus the ids the command created.
- It is served by an `IService<TCommand, TResult>`, like every slice.

**What a handler does.**
- It loads the state it needs from projections, after waiting for the projector to reach the ledger head (§2.4).
- It decides with a pure method on a `Core` type.
- It appends **exactly one** ledger event under the process's lease grant (S1's `ILedgerStore.Append`).
- Everything that follows from the event is derived in the projections, for example a request's pending decision. So
  a command is atomic: one event, one transaction.

**`CommandAccepted(Guid CommandId, long Sequence)`.** `Sequence` is the ledger sequence the command wrote. It's the
read-your-writes token of §2.4.

**Commands run one at a time.**
- A process-wide `CommandGate` (a `SemaphoreSlim(1)`, the outermost step of the chain) serializes every command.
- Each command then waits for the projector to reach the head before it reads.
- With one process and one human, this costs nothing measurable, and it removes the race between checking and
  appending.

**Fact keys make conflicts lose in the ledger.** Each event's idempotency key names the *fact*, not the request:

| Event | Key |
|---|---|
| `decision.recorded` | `decision.recorded:<decisionId>` |
| `item.state-changed` | `item.state-changed:<itemId>:v<n+1>` |
| `request.filed` | `request.filed:<requestId>` |

- If two writers ever race, S1's `Append` returns `AlreadyRecorded` for the second.
- The handler then reads the recorded event at that sequence. If another command id wrote it, the result is
  `SND-ITEM-409` ("decided elsewhere", or "changed since you looked").
- There is **no client `Idempotency-Key` header in v0** (§11). Commands that create things aren't deduplicated, and the
  CLI never retries a command by itself.

**The command chain.** One helper, `AddCommand<TCommand, TResult, TService, TValidator>()` in `AddSindriFeatures()`,
registers these decorators, so no command can skip them. A test resolves every registered `ICommand` and asserts this
exact order, outermost first:

| # | Decorator | Does |
|---|---|---|
| 1 | `CommandGate` | Serializes commands (above) |
| 2 | `AuditingService` | Wraps everything inside it. Appends `command.handled {commandId, command, class, caller, outcome, code?}`, keyed `command.handled:<commandId>`, for every attempt: accepted, refused, invalid or failed |
| 3 | `ValidatingService` (Kernel) | Runs the slice's `IValidator` |
| 4 | `AuthorizingService` | Checks the command's action class against the caller class (§4). A refusal returns `SND-AUTH-002`, and the only thing written is the audit row |

**The caller context.** `CallerContext(CallerClass Class, string Actor, Guid CommandId, Guid CorrelationId)` is a
scoped service.
- The Host fills it from the request (§6). In-process callers, the engine and the scheduler, fill it directly.
- The host composes `Actor`; it never comes from the caller (the C# Seams rule). The values are `human:cli`,
  `human:ui`, `session:mcp`, `session:cli`, `session:http` and `service:<name>`.
- S1's `EventFactory` reads it, so every event carries the right actor, the command's correlation id and its item
  (conflict C6).

### 2.2 Read side: queries and projections

**What a query is.** A query is a request record that implements `IQuery<TView>`, served by `IService<TQuery, TView>`.

**What its handler reads.**
- Projections only, through read ports: `IItemViews`, `IQueueView`, `INeedsYouView`, `IDecisionViews`, and so on.
- Their adapters open a read-only connection (`SqliteConnectionFactory.OpenReadOnly`).
- A query handler takes no write port and appends nothing.
- The one exception is `ReadLedger` (§3.1), an audit query that reads `ledger.db` read-only through
  `ILedgerStore.ReadAfter`.

**Where projections live.**
- In their own SQLite file, `$AW_SINDRI_HOME/views.db` (0600), separate from `ledger.db`.
- Each view is a table keyed by its id.
- One `projector_state` row holds the cursor, the schema version and whether the projector has stalled.

**Freshness on every view.**
- Every view response carries `asOf { cursor, ledgerHead, rebuilding, stalled }`.
- With it, a client can tell "empty" from "still loading" and from "views are broken" (§3.6).
- It also gives the UI's "updated Ns ago" its clock.

**Times, not ages.** Views carry `since` and `updatedAt` timestamps, never `age`. That keeps a view byte-stable, and
clients compute ages themselves.

### 2.3 The projector

**How it runs.** It's an in-process hosted service in the Host, not an S1 `IScheduledService`. Its loop:
1. `ReadAfter(cursor, 100)`.
2. For each event, every registered `IProjection` whose types match applies it.
3. Store the new cursor.

Each page is one `views.db` transaction. Between pages it sleeps until the ledger signal (§5.5), or for 1 s.

**Projections are pure folds.**
- Each is `Apply(row?, event) → row?`, unit-tested from lists of events.
- One projection may write rows in several views from one event. For example, `request.filed` creates both the
  request row and its pending decision row.

**Rebuild.**
- `sindri views rebuild` (desk class), a schema-version change, or a missing `views.db` at startup each start one.
- A rebuild sets `rebuilding`, empties the view tables, resets the cursor to 0, and replays.
- Queries still answer during a rebuild, with `asOf.rebuilding = true`. The UI shows its loading state, not its empty
  one.
- A new or changed read model is therefore a replay, never a data migration.

**Failure.**
- A projection that throws on an event stops the projector at that sequence.
- The projector sets `stalled`, logs `SND-VIEW-001` with the sequence and event type, and retries every 30 s.
- `GET /status` reports `views: stalled at <sequence>`.
- `sindri doctor` gains a `views` line that fails with the fix `sindri views rebuild`.
- Queries answer with `asOf.stalled = true`, and the UI shows its error banner.

**Why asynchronous.**
- S1's `Append` owns its transaction.
- Because commands are serialized and wait for the head, the asynchronous projector gives the same answers as an
  inline one.
- Inline projection stays a later option (§11).

### 2.4 Read-your-writes

- **Queries:** every query accepts `X-Sindri-After: <sequence>`.
  - The handler waits, up to 2 s, on the projector's progress signal, until the cursor is at or past that sequence.
  - Past 2 s it returns `SND-API-004` (503), naming the cursor and the target.
- **Commands** always wait for the head before deciding (§2.1).
- **The CLI** sends the `Sequence` from its previous command when one verb chains a command and a query.
- **The UI** sends the last `Sequence` it received. On `SND-API-004` after an accepted command, it keeps its "done"
  confirmation, shows the view as stale, and re-queries on the next event.

### 2.5 Architecture tests (`tests/Aw.Architecture.Tests`)

| Id | Rule |
|---|---|
| `ARCH-CQRS-001` | Every `MapGet` endpoint resolves an `IService<TQuery, TView>` where `TQuery : IQuery<TView>`. Every `MapPost` resolves an `IService<TCommand, _>` where `TCommand : ICommand<_>`. S1's `GET /ping` and `GET /status` are queries, and `GET /events` is the stream query. It's checked over the endpoint data source in an integration test, because ArchUnitNET can't see minimal-API lambdas |
| `ARCH-CQRS-002` | No query handler's constructor takes `ILedgerStore` (except `ReadLedger`'s), `ILease`, `IApprovals` or a projection writer |
| `ARCH-CQRS-003` | Every `ICommand` has a row in the command catalog (§3.3) |

Each rule gets a rule record under `rules/engineering/csharp/` in P1 (`node scripts/check-rules.mjs`).

## 3. Resources, endpoints and commands

All routes sit under S1's group `/sindri`; the tables show them without it.
- Request and response records live in `Aw.Sindri.Features.<Slice>`, and every one is registered in
  `SindriJsonContext`.
- **Class** is the action class (§4).
- **Slice** is the `/new-slice` name (§12 shows how to scaffold one into Sindri).
- **Phase** is the PR that adds it.

New typed ids are strings: a prefix plus a compact GUIDv7 from `IIdGenerator`. They are `ItemId` (`ITM-…`, or a tracker
key such as `ENG-142`), `RequestId` (`REQ-…`), `DecisionId` (`DEC-…`), `JobId` (`JOB-…`) and `RepoName`. Each follows
`docs/csharp/typed-ids.md` and gets its route-binding test.

### 3.1 Queries

| Route | Slice | Request | Response | Phase |
|---|---|---|---|---|
| `GET /status` | `GetStatus` (S1) | — | S1's response, plus `apiVersion` and `views` (§2.3) | S1; extended in P1, P2 |
| `GET /ping` | `PingProcess` (S1) | — | S1's `{pid, epoch}` | S1 |
| `GET /commands` | `ListCommands` | — | `CommandList(commands)`. For each command: <ul><li>name;</li><li>class;</li><li>allowed callers;</li><li>method and route;</li><li>CLI form;</li><li>UI status (`ready`, `pending:<phase>` or `cli-only:<reason>`).</li></ul> | P1 |
| `GET /services` | `ListServices` | — | `ServiceList(services, asOf)`. For each service: <ul><li>name;</li><li>cadence;</li><li>paused;</li><li>last run (slot, outcome, code);</li><li>next slot.</li></ul> | P2 |
| `GET /profile` | `GetProfile` | — | `ProfileView(asOf)`: <ul><li>the approved hash and when it was approved;</li><li>the live hash;</li><li>whether it changed since approval;</li><li>the changed key paths.</li></ul> | P2 |
| `GET /queue` | `GetQueue` | — | `QueueView(groups, asOf)`: the groups of §3.6 in a fixed order, each a list of `ItemSummary` | P3 |
| `GET /items?state=&repo=` | `ListItems` | `ListItemsQuery(ItemState?, RepoName?)` | `ItemList(items, asOf)`. For each item: <ul><li>id;</li><li>title;</li><li>repo;</li><li>state;</li><li>step;</li><li>`since`;</li><li>the next action;</li><li>`actions`, the command names allowed now.</li></ul> | P3 |
| `GET /items/{itemId}` | `GetItem` | `GetItemQuery(ItemId)` | `ItemView`: <ul><li>the summary, plus its version and who created it;</li><li>what it's waiting on (a decision or request id);</li><li>its engine instance id, once S2 exists;</li><li>timestamps;</li><li>`actions`;</li><li>`asOf`.</li></ul> | P3 |
| `GET /items/{itemId}/timeline?after=` | `GetItemTimeline` | `GetItemTimelineQuery(ItemId, long?)` | `Timeline(entries, next, asOf)`. Each entry: <ul><li>sequence;</li><li>time;</li><li>type;</li><li>actor;</li><li>one scrubbed line.</li></ul> | P3 |
| `GET /needs-you` | `GetNeedsYou` | — | `NeedsYouList(rows, asOf)` (§3.6) | P4 |
| `GET /requests?state=` | `ListRequests` | `ListRequestsQuery(RequestState?)` | `RequestList(requests, asOf)`. Each request: <ul><li>id;</li><li>kind;</li><li>item;</li><li>text (scrubbed, at most 2,000 characters);</li><li>actor;</li><li>state;</li><li>decision id.</li></ul> | P4 |
| `GET /decisions?pending=&item=` | `ListDecisions` | `ListDecisionsQuery(bool?, ItemId?)` | `DecisionList(decisions, asOf)`. Each decision: <ul><li>id;</li><li>kind;</li><li>class;</li><li>item and item version;</li><li>question;</li><li>options and the recommended one;</li><li>state;</li><li>who decided it;</li><li>`recordedOnly` (true for an accepted request before S2);</li><li>the CLI command.</li></ul> | P4 |
| `GET /events?after=&item=&types=` | `StreamEvents` (S1, extended) | `StreamEventsQuery(long, ItemId?, TypeFilter?)` | An SSE stream of `PublicEvent` (§5) | S1; extended in P5 |
| `GET /ledger?after=&item=&type=&max=` | `ReadLedger` | `ReadLedgerQuery(long, ItemId?, string?, int)` | `LedgerPage(events, next)`: public envelopes with their payloads. It's an audit read, so `human` callers also get non-public types. Provers use it for evidence | P5 |
| `GET /jobs/{jobId}` | `GetJob` | `GetJobQuery(JobId)` | `JobView`: <ul><li>id;</li><li>kind;</li><li>item (optional);</li><li>state (`running`, `succeeded`, `failed` or `cancelled`);</li><li>started and ended;</li><li>progress lines (the last 200);</li><li>result summary;</li><li>code;</li><li>`asOf`.</li></ul> | P6 |

**CLI-local verbs, which aren't API calls.**
- `sindri doctor` and `sindri profile validate` run inside the CLI and read local files (S1).
- They're in the catalog with class `local`, and they're never routes.
- `doctor` gains a `views` line in P2.

### 3.2 Commands

| Route | Slice | Body (beyond the route) | Response | Class | CLI | Phase |
|---|---|---|---|---|---|---|
| `POST /stop` | `StopProcess` (S1) | — | S1's response, plus `sequence` | desk | `sindri stop` | S1; class in P1 |
| `POST /profile/approve` | `ApproveProfile` (S1) | S1's `{hash, confirmed}` | S1's response, plus `sequence` | **protected** | `sindri profile approve [<hash>]` | S1; class in P1 |
| `POST /views/rebuild` | `RebuildViews` | — | `CommandAccepted` | desk | `sindri views rebuild` | P2 |
| `POST /items` | `CreateItem` | `title`, `repo?`, `body?` | `ItemCreated(accepted, ItemId)` | desk | `sindri item add "<title>" [--repo R]` | P3 |
| `POST /requests` | `FileRequest` | `kind` (`rescope`, `add-task`, `note` or `handback`), `itemId?`, `text` | `RequestFiled(accepted, RequestId, DecisionId)` | **request** | `sindri request <kind> [--item X] "<text>"` | P4 |
| `POST /decisions/{decisionId}` | `Decide` | `choice`, `note?` | `CommandAccepted` | decision | `sindri decide <decisionId> <choice> [--note]` | P4 |
| `POST /services/{name}/run` | `RunService` | — | `JobStarted(accepted, JobId)` | job | `sindri services run <name>` | P6 |
| `POST /services/{name}/pause` | `PauseService` | — | `CommandAccepted` | desk | `sindri services pause <name>` | P6 |
| `POST /services/{name}/resume` | `ResumeService` | — | `CommandAccepted` | desk | `sindri services resume <name>` | P6 |
| `POST /jobs/{jobId}/cancel` | `CancelJob` | — | `CommandAccepted` | job | Ctrl-C while following; `sindri jobs cancel <id>` | P6 |
| `POST /items/{itemId}/open` | `OpenItem` | — | `ItemOpened(accepted, launchedIn)` | desk | `sindri open <item>` | P10 |

Notes:
- **`ApproveProfile`** keeps S1's body. `confirmed` is set only by the CLI after the TTY prompt. The `protected` class
  means only a `human` caller reaches the handler (§4), so a session can't send `confirmed: true`.
- **`Decide`** reads the item version from the decision's own row; the client doesn't send one. A conflict is settled
  by the fact key (§2.1).
- **Instance commands live in the engine spec**, together with their routes and the verbs `sindri workflow …`
  (keeping `sindri handback` as the short alias from core design §9.3). They are:
  - `StartInstance`;
  - `ApplyCommand` (answer with an optional `grant`, takeover, handback, cancel);
  - `CompleteStep`;
  - `GetInstance` and `ListInstances`.

  See §3.5.

### 3.3 The command catalog

`ICommandCatalog` is a port in `Core`. `CommandCatalog` implements it in the Host, because routes and CLI forms are
Host facts.

**One row per command:**
- the command type;
- its action class;
- the allowed caller classes;
- method and route;
- the CLI form;
- the UI status: `ready`, `pending:<phase>` or `cli-only:<reason>`. `cli-only` is allowed only for `protected`
  commands, for example `cli-only:typed-confirmation`.

**Who reads it:**
- `ListCommands` serves it.
- The UI renders its actions from it (§17.27.4).
- `doctor` lists the `pending` ones.
- The engine spec's commands register their rows here too, so the UI renders them the same way.

**A unit test fails when:**
- a command has no row;
- a row's route isn't in the endpoint table;
- a `pending:<phase>` names a phase that has already shipped;
- a `cli-only` row isn't protected.

### 3.4 Events these phases add

Every event is a `LedgerEntry` (S1) with the Kernel `EventEnvelope`. Each is registered in `EventCatalog` (`Core`) by
the phase that adds it, with its type, whether it's public, its public payload type and its fact-key shape.
`EventCatalog` itself lands in P1.

| Type | Payload | Key | Public | Phase |
|---|---|---|---|---|
| `command.handled` | commandId, command, class, caller, outcome (`accepted`, `refused`, `invalid` or `failed`), code? | `command.handled:<commandId>` | no | P1 |
| `views.rebuilt` | cursor before, events replayed | `views.rebuilt:<commandId>` | no | P2 |
| `item.created` | item, title, repo | `item.created:<itemId>` | yes | P3 |
| `item.state-changed` | item, from, to, version, reason? | `item.state-changed:<itemId>:v<n>` | yes | P3 |
| `request.filed` | request, decision, kind, item?, text (scrubbed, at most 2,000 characters) | `request.filed:<requestId>` | yes (text left out) | P4 |
| `decision.recorded` | decision, choice, note?, by | `decision.recorded:<decisionId>` | yes | P4 |
| `job.started` | job, kind, item? | `job.started:<jobId>` | yes | P6 |
| `job.progress` | job, round, line | `job.progress:<jobId>:<n>` | yes | P6 |
| `job.finished` | job, outcome, summary, code? | `job.finished:<jobId>` | yes | P6 |
| `job.cancelled` | job | `job.cancelled:<jobId>` | yes | P6 |
| `service.paused` / `service.resumed` | service | `service.paused:<name>:<commandId>` | yes | P6 |

**How a request becomes a decision** (one event per command):
1. `FileRequest` appends `request.filed`, with both ids chosen by the handler.
2. The projections derive the request row (`open`) and a pending decision: kind `accept-request`, options `accept` and
   `decline`, class `decision`.
3. `Decide` appends `decision.recorded`.
4. The projections mark the decision as decided, and the request as `accepted` or `declined`.

**Before the engine (S2) exists,** an accepted request is **recorded only**. Its decision row says so:
`recordedOnly: true`, shown as "recorded; acted on once the workflow engine runs". Later the engine raises its own gates
as pending decisions through the same catalog, and reads `decision.recorded`.

### 3.5 Ownership with the workflow engine spec

| Concern | Owner | The other spec's part |
|---|---|---|
| Instances, their states, park reasons, takeover, handback, answering a park (`grant`), timers, `CompleteStep` | **Engine** (`StartInstance`, `ApplyCommand`, `CompleteStep`, `GetInstance`, `ListInstances`; engine P4 and P5) | This spec gives their command-catalog rows an action class and allowed callers. Start, answer, handback, cancel and complete are `desk`. Takeover is set by the hook's turn classification (§9.3) through the `service` caller |
| The verbs `sindri workflow start`, `answer`, `handback`, `cancel`, `complete`, and the `sindri handback` alias | **Engine** | — |
| Item rows, the queue, needs-you, and requests and decisions as a list for the human | **This spec** | The engine emits the events these views fold. Once S2 runs, an item's `state` reflects its instance state |
| The item filter on `/sindri/events` (engine S1-D6) | **This spec**, P5 | The engine's `workflow events` uses it. If engine P4 merges first, it adds the filter, and P5 keeps it |
| `ILauncher` | **This spec**, P10 (`OpenItem`) | The engine replaces its desk runtime with it (engine W20) |
| The authorization table, the caller context, the command chain | **This spec**, P1 | Every engine command goes through the same chain |

This spec has no item `hold`, and no item-level `park`, `unpark` or `handback`: those are engine instance commands.

### 3.6 Views the UI renders: states, groups and needs-you

**Item states and their queue groups.** Groups appear in this order. Within a group, the oldest `since` comes first.

| Group | `ItemState` values |
|---|---|
| `waitingOnYou` | `needs-decision`, `needs-answer` |
| `withYou` | `taken-over` |
| `parked` | `parked` |
| `running` | `running` |
| `queued` | `new`, `queued` |
| (not in the queue) | `done`, `cancelled` |

Before S2 exists, an item stays `new` until a decision or request points at it. The engine's item projection maps
instance states onto these values.

**`GET /needs-you`** is one list across every source, built by the needs-you projection. A `NeedsYouRow` has these
fields:

| Field | What it holds |
|---|---|
| `id` | Stable across refreshes, for example `decision:DEC-…` or `failure:service:heartbeat`. The UI keys focus and the "seen" pulse on it |
| `kind` | `decision`, `request`, `failure` or `profile` |
| `subject` | A reference to an item, decision, job or service |
| `title`, `reason` | Scrubbed text |
| `repo?` | The repo, when there is one |
| `urgency` | `pulse`, `colour` or `calm` (dashboard spec §5.1) |
| `since` | When it started needing you |
| `primaryAction` | A catalog command name, or `copy:<cli>` for a `cli-only` command |
| `cli` | The CLI form |

- **Sources in v0:**
  - pending decisions and requests (P4);
  - failed service runs (`service.failed`, from S1);
  - a profile that changed since approval (P4).
- **Sources the collector adds later** (§11): sessions waiting on approval or input, stale indexes, judge health and
  PRs. Each is one more projection that writes rows of the same shape.
- **Order:** by urgency, then oldest first.

**Empty and error text.**
- One `EmptyText` table in `Core` holds the empty-state sentences, such as "Nothing is waiting on you. 3 running, 1
  queued."
- The CLI prints them, and every list view returns its sentence in `empty`, so the UI never keeps its own copy.
- For errors, `ProblemDetails.detail` already carries S1's `(fix: …)` text, and the UI shows `detail` as it is.

**Agent text.**
- Text from agents or trackers is scrubbed, then escaped with the shared sanitizer, when the projection writes it. That
  covers control characters, invisible characters, and OSC and ANSI sequences.
- The API never returns raw control characters, and the UI renders text as plain text only.

**Dashboard views fed by v0 (P1–P5):**
- **Needs you**, except its session, PR, index and judge sources;
- the queue and the item detail;
- decisions and requests;
- the services part of **Automatic work**;
- live refresh.

**Fed later (§11):** **Agents**, **Repos**, the rest of **Automatic work**, and **Today**.

## 4. Commands vs queries, decisions vs requests

### 4.1 Action classes (one per command)

| Class | Meaning | Commands |
|---|---|---|
| `request` | A fact filed for a human or the engine to act on. It changes no item state | `FileRequest` |
| `job` | Read-only work that may run long. It writes only its own job events | `RunService`, `CancelJob` |
| `decision` | A non-protected human decision (the §17.12 list) | `Decide` |
| `desk` | A human action that changes item or process state (§17.27.2) | `StopProcess`, `RebuildViews`, `CreateItem`, `PauseService`, `ResumeService`, `OpenItem`; the engine's start, answer, handback, cancel and complete |
| `protected` | The approval tier: a typed confirmation, bound to the content, at a terminal | `ApproveProfile`. Later: evolve adopt, channel promote, tier raises, the engine's `recipe approve` |
| `local` | Runs in the CLI, never through the API | `doctor`, `profile validate` |

### 4.2 Caller classes and the authorization table

| Class \ caller | `human` | `session` | `service` (in-process only) |
|---|---|---|---|
| any query | yes | yes (`ReadLedger` without the non-public types) | yes |
| `request` | yes | **yes**, at most 20 open requests per actor (`SND-REQ-002`) | yes |
| `job` | yes | no; file a `request` instead | yes |
| `decision` | yes | **no** | no; it may only *raise* a decision |
| `desk` | yes | **no** | only the engine commands its recipe step owns (the engine spec lists them) |
| `protected` | yes, with S1's terminal confirmation | **no** | no |

- **`service` never arrives over HTTP.** The engine and the scheduler call handlers in-process with a `service:<name>`
  caller context, so nothing sent over the wire can claim it.
- **A refusal** is a 403: `SND-AUTH-002 <Command> is a human decision. Ask the human to run it in a terminal or the
  Sindri app: sindri <verb> …`. Inside Claude Code, `!sindri …` still runs with the agent's environment and is refused
  too, which is why the message says where to run it. The only thing written is the audit row.
- **Questions answered inside a session** stay `answer` turns, classified by the hook (core design §9.3). The API
  doesn't replace that path.

## 5. The event stream

### 5.1 What it's for

It's a "something changed, re-read" signal for live clients:
- the Avalonia app, to refresh the affected view and drive the needs-you pulse, the tray badge and OS notifications;
- the CLI's long-running verbs, for job progress and cancel;
- `sindri status --follow`;
- later, the notifier, the phone head and other instances.

It is not:
- the source of truth (the projections are);
- the engine's or the projector's input (they read the ledger by cursor, in-process);
- a way to send anything to Sindri.

### 5.2 Transport and filters

`GET /events?after=<sequence>&item=<itemId>&types=<glob,glob>`, sent as Server-Sent Events
(`TypedResults.ServerSentEvents`, as in S1).
- On a reconnect, `Last-Event-ID` wins over `after` (conflict C4).
- A read error ends the stream with `event: error` and the SND code; it is never swallowed.
- Why not WebSocket: the stream is one-way, SSE resumes natively, it works with `curl -N`, and the Avalonia client reads
  it with a streaming `HttpClient`.

### 5.3 The public envelope

```
id: 4211
event: item.state-changed
data: {"sequence":4211,"id":"0192…","type":"item.state-changed","schemaVersion":1,"occurredAt":"2026-10-10T14:02:11Z","actor":"human:cli","tenant":"local","ring":0,"item":"ITM-0192…","correlationId":"0192…","causationId":null,"traceId":"00-…","payload":{"item":"ITM-0192…","from":"new","to":"parked","version":3}}
```

- `PublicEvent` is S1's `LedgerEvent`, flattened:
  - `sequence` and `item` come from the ledger row;
  - the rest comes from the Kernel `EventEnvelope`;
  - `payload` is the event's public payload record, from `EventCatalog`.
- Types that aren't public are never sent.
- Payloads carry ids, states and short scrubbed titles. They never carry packet, request or session text.
- Clients re-query the projections when an event arrives. The UI never folds events into state itself (§17.27.1).

### 5.4 Back-pressure and limits

- **No server-side buffer.** The server reads a page at a time with `ReadAfter(cursor, 100)` and writes it out. A slow
  reader only slows its own loop, because the writes wait on TCP.
- **Retention:** a client that asks for an `after` older than what the ledger keeps gets `event: reset` and
  `data: {"head": <sequence>}`, and the stream closes. The client re-runs its queries and reconnects from `head`.
- **Keep-alive:** a `: ping` comment every 15 s.
- **Stream limits:**
  - at most 16 concurrent streams;
  - 4 of them are reserved for `human` callers, so `session` streams are capped at 12;
  - the stream over the limit gets `SND-API-005` (429), and the client falls back to re-querying every 5 s.

### 5.5 Latency

- S1 polls the ledger every 1 s.
- P5 adds `ILedgerSignal`, a port in `Core`. Its in-memory adapter is pulsed by the SQLite ledger adapter after each
  committed `Append`.
- So the stream and the projector wake within milliseconds, and the 1 s poll stays as the fallback.
- S1's `ILedgerStore` doesn't change.

## 6. Auth, v0

v0 is local-only. The loopback binding and S1's `LoopbackGuard` are the only network controls. There are **no tokens,
passkeys or tailnet listener** (§11).

What v0 keeps is the caller class, so "session calls are requests, never decisions" holds as a guard against honest
mistakes. That's the repo's threat model: gates guard honest mistakes, not a hostile agent.

**The Host reads `X-Sindri-Caller`:**
- `human` gives `human`.
- `session`, **or no header at all**, gives `session`. This is the safe default: an agent's `curl`, a script it writes,
  or a fetch tool all land in `session`.
- Any other value is a 400 (`SND-API-007`).

**Who sends `human`:**
- The CLI, but only when stdin and stdout are a terminal and none of these agent-session variables is set:
  - `CLAUDECODE` (Claude Code);
  - `AW_SINDRI_CHILD` (a Sindri child session);
  - `CODEX_SANDBOX`, `CODEX_SANDBOX_NETWORK_DISABLED`, `CODEX_SESSION_ID` (Codex);
  - `CURSOR_AGENT`, `CURSOR_TRACE_ID` (Cursor).

  These go in `.env.example` (`scripts/check-env-example.sh`).
- The Avalonia app.

**Who sends `session`:** `sindri mcp`, and the CLI in every other case.

**Commands refuse any `Origin` header** with 403 `SND-API-008`.
- No first-party client sends one, so this stops a web page in a local browser from driving a command that has no
  body.
- A browser also can't send `X-Sindri-Caller` without a CORS preflight, which Sindri never answers. So browser requests
  are `session` regardless.
- Queries keep S1's guard (conflict C3).

**The UI's protected actions** show the CLI command to copy (`cli-only:typed-confirmation`), because the typed
confirmation needs a terminal until passkeys exist.

**Out of scope for v0:** a session that deliberately forges `human` (§11, tokens).

When tokens arrive, only the source of the caller class changes. The authorization table, the decorators and every
test stay as they are.

## 7. Finding or starting the process

S1's model stands:
- The server writes `control.json {url, pid, epoch}` (0600) once it listens.
- A client reads it, checks that `url` is a loopback address, and checks the pid and epoch against `GET /ping` with a
  2 s timeout. A stale file therefore reads as not running (`SND-API-001`).
- Bare `sindri` spawns `sindri serve` detached and waits up to 10 s (`SND-CLI-002`).

This design adds which verbs start it:

| Verb | When the process isn't running |
|---|---|
| `sindri`, `sindri --headless` | Start it (S1) |
| Commands and queries run by a human (`decide`, `item add`, `queue`, …) | Start it, then run. With `--no-start`, print `SND-API-001` instead |
| **Any verb run inside an agent session** (the §6 variables) | Never start it. Print `SND-API-001`: "Sindri isn't running; ask the human to run `sindri`". This is the same rule as `sindri mcp` |
| `status`, `stop` | Never start it. As in S1, `status` prints `not running` and exits 1; `stop` exits 1 |
| `doctor`, `profile validate` | They run locally (S1) |
| `sindri mcp` | Never start it. Its tools return `SND-API-001` |

## 8. The MCP server

`sindri mcp` is a subcommand of the same NativeAOT `sindri` binary.
- It uses stdio and the official MCP C# SDK, which the readiness AOT spike approved.
- Tools are registered explicitly, with no reflection. stdout is reserved for the protocol, so logs go to stderr.
- It's a thin client of the HTTP API: every tool is exactly one API call with `X-Sindri-Caller: session`, and the
  server holds no logic.
- Errors come back as tool errors that carry the SND code and the message.

**Repo scope.**
- `sindri mcp` starts with the session's working directory.
- Item queries default to that repo (`repo=<git top-level name>`), so one repo's items don't reach another repo's
  session or its model provider.
- `all: true` widens a query deliberately.

| Tool | Arguments | API call | Kind |
|---|---|---|---|
| `sindri_status` | — | `GET /status` | query |
| `sindri_queue` | `all?` | `GET /queue` (filtered to the repo) | query |
| `sindri_item` | `itemId` | `GET /items/{itemId}` | query |
| `sindri_item_timeline` | `itemId`, `after?` | `GET /items/{itemId}/timeline` | query |
| `sindri_pending_decisions` | `itemId?`, `all?` | `GET /decisions?pending=true` | query |
| `sindri_job` | `jobId` | `GET /jobs/{jobId}` | query, **from P6** |
| `sindri_request` | `kind`, `itemId?`, `text` | `POST /requests` (`FileRequest`) | request |
| `sindri_context` | `query`, `itemId?` | `GET /context?q=` | query, **with S3** |

- There are no decision, desk, protected or job tools, so the MCP surface can't even attempt one.
- Query tools carry the MCP `readOnlyHint` annotation.

**Proof.**
- The proof run starts a fresh Claude Code process as the subagent:
  `claude -p --mcp-config <tmp>/sindri-mcp.json --strict-mcp-config "<journey>"`.
- The config points at the phase's built binary and the phase's `AW_SINDRI_HOME`.
- The tools load when that process starts. Nothing is registered with any provider, so the cutover rule holds and
  there's nothing to clean up afterwards.

## 9. Errors, versioning and JSON

**Errors.**
- S1's `HttpResults` mapping is unchanged: `ErrorKind` sets the status, and `ProblemDetails` carries `title` = the SND
  code and `detail` = the message with its `(fix: …)`.
- The CLI prints `SND-… message` and exits 0 for ok, 1 for attention, 2 for an error (S1).
- Each new code is added by the phase that reserves it, in two places: `SindriErrors` with its `All` list, and the error
  table in **`docs/csharp/sindri.md`**. Not `docs/sindri/errors.md`, which is generated from the TS build.

| Code | Kind (status) | When | Phase |
|---|---|---|---|
| `SND-AUTH-002` | Forbidden (403) | The caller class may not run the command's action class | P1 |
| `SND-API-007` | Invalid (400) | An unknown `X-Sindri-Caller` value | P1 |
| `SND-API-008` | Forbidden (403) | A command carried an `Origin` header | P1 |
| `SND-API-004` | Unavailable (503) | `X-Sindri-After` wasn't reached within 2 s | P2 |
| `SND-VIEW-001` | Unavailable (503) | The projector stalled on an event (status, doctor and logs) | P2 |
| `SND-ITEM-404` | NotFound (404) | No such item | P3 |
| `SND-ITEM-409` | Conflict (409) | The item changed since you looked, or the decision was decided elsewhere | P3, P4 |
| `SND-REQ-001` | Invalid (400) | An unknown request kind, or text that's empty or over 2,000 characters | P4 |
| `SND-REQ-002` | RateLimited (429) | More than 20 open requests from one actor | P4 |
| `SND-DEC-404` | NotFound (404) | No such decision | P4 |
| `SND-DEC-001` | Invalid (400) | A choice that isn't one of the decision's options | P4 |
| `SND-API-005` | RateLimited (429) | The stream limits (§5.4) | P5 |
| `SND-JOB-404` | NotFound (404) | No such job | P6 |
| `SND-SVC-404` | NotFound (404) | No such service in the approved profile | P6 |
| `SND-LAUNCH-001` | Unavailable (503) | The launcher couldn't open the terminal | P10 |

- `SND-AUTH-001` and `SND-API-006` are reserved for the token phase (§11).
- The engine spec reserves `SND-RECIPE-*` and `SND-WF-*`.

**Versioning.**
- There's no version in the path in v0. The CLI, `sindri mcp` and the UI are built from this repo and shipped together,
  and S1's routes are already under `/sindri`.
- `GET /status` reports `apiVersion: 1`, and every response carries `Sindri-Api-Version: 1`. The UI checks it when it
  connects, and on a mismatch says "Sindri and the app are different versions".
- A breaking change later adds a `/sindri/v2` group and keeps `v1` for one release.
- Events carry their own per-type `schemaVersion`.

**JSON.**
- S1's one `SindriJsonContext`, through `AwJson`.
- `--json` on a CLI verb prints the API response body **exactly as received**. P1 changes S1's `ControlPlaneClient` to
  keep the raw body (conflict C5).
- So `sindri queue --json` byte-matches `curl -H 'X-Sindri-Caller: human' http://127.0.0.1:<port>/sindri/queue`.

**Telemetry.** Spans and metrics follow `docs/csharp/telemetry.md`, under the `Aw.Sindri` source and meter.
- Spans: `sindri.command <name>` (tagged with class, caller and outcome) and `sindri.query <name>`.
- Metrics:
  - `sindri.commands`, a counter by class and outcome;
  - `sindri.projector.lag`, a gauge of head minus cursor;
  - `sindri.streams`, a gauge by caller class.

## 10. Conflicts with S1 (flagged, not redesigned)

| # | S1 today | What this design needs | Resolution |
|---|---|---|---|
| C1 | ~~`/sindri/sindri/status`~~ | — | Already fixed in S1: `EndpointTable` maps `/status` inside the group |
| C2 | No caller class. `ApproveProfile` trusts `confirmed` from any loopback caller | Only `human` callers reach protected handlers | P1 adds the caller middleware and the command chain around S1's commands. S1's handlers don't change |
| C3 | `LoopbackGuard` allows a browser `Origin` on loopback | Commands refuse any `Origin` | P1 adds the check as an endpoint filter on commands. S1's guard stays as it is for queries |
| C4 | `EventStream` lets `after` beat `Last-Event-ID`, swallows read errors, and sends every event type | `Last-Event-ID` wins; errors end the stream with their code; only public types are sent | P5 changes S1's `EventStream` and its `StreamEvents` slice. The route stays |
| C5 | S1's command responses have no `sequence`, and `ControlPlaneClient` re-serializes `--json` | `sequence` for read-your-writes, and the raw body printed | P1 adds `sequence` to `StopProcessResponse` and `ApproveProfileResponse`, and the client keeps the raw body. Both are additive |
| C6 | `EventFactory` stamps the fixed actor `host:sindri`, a new correlation id on every event, and `Item = null` | The actor from the caller, one correlation id per command, and the item set | P1 makes `EventFactory` read `CallerContext` |
| C7 | `EndpointTable` and `CommandTable`/`Verbs` have no `/new-slice` insertion anchors | Anchors, so slices can be added in parallel | P1's first commit restructures both tables to one line per slice, with a fixed anchor comment. `/new-slice --files-only` is then used against them (§12) |

## 11. Later (designed in outline, not phased)

- **Auth:**
  - bearer tokens per caller class, minted on each `serve` start (`auth/human.token` and `auth/session.token`, 0600);
  - `SND-AUTH-001` for a missing or stale token;
  - an `aw:sindri-auth` guard hook;
  - containerized factory sessions get only the session token;
  - passkeys through `IPasskeys` for the UI's desk and protected actions (§17.27.2);
  - a client `Idempotency-Key` backed by a durable command journal, with `SND-API-006` when a key is reused with a
    different body.
- **Tailnet:** a Unix-socket listener that `tailscale serve` proxies to, with caller class `remote`. Tailscale identity
  headers are trusted only there (§17.22 D10). It stays query-only until passkeys exist.
- **Webhooks:** `POST /webhooks/{source}` → `ReceiveWebhook`, with class `trigger` and the delivery id as the fact key.
  The profile's `triggers:` match deliveries to standing workflows, and each source's signature is checked.
- **Dashboard collector queries** (dashboard spec §4), served by a collector service inside the process. They feed the
  views v0 doesn't, and need their own spec once the collector is designed in C#:
  - `GET /sessions` and `/sessions/{provider}/{id}` (+ `/timeline`), for **Agents**: its 13 states and the nesting;
  - `GET /repos`, for **Repos**;
  - `GET /work`, for the rest of **Automatic work**;
  - `GET /today`, for **Today**;
  - `POST /sessions/{provider}/{id}/attach` (desk);
  - `POST /needs-you/{id}/ack` (desk; acknowledges a failure);
  - the needs-you sources for sessions, stale indexes, judge health and PRs.
- **Receipts and metrics:** `GET /items/{itemId}/receipt`, `POST …/receipt/verify` (§17.23.12) and
  `GET /metrics/today`.
- **Context retrieval:** `GET /context?q=&item=` and `sindri_context`, with S3.
- **Inline projections:** a unit of work around `Append`, if read-your-writes waits ever matter in practice.

## 12. Test strategy, and how agents build and prove each phase

**Tests by layer.**

| Layer | Lane | What |
|---|---|---|
| Domain decisions (`Core`) | unit | Item transitions, deriving a decision from a request, the authorization table (every class × caller pair), fact-key composition, needs-you ordering, `ItemActions.For(state)` |
| Projections | unit | Each `IProjection` folded over lists of events. Replaying the same events gives the same rows |
| Command chain | unit | The registration helper wraps every command in the §2.1 order. A refused or invalid command writes only `command.handled`. Losing a fact-key race gives `SND-ITEM-409` |
| Catalogs | unit | `ARCH-CQRS-003`; every route exists; no stale `pending`; `cli-only` only on protected commands; every event type is in `EventCatalog` |
| Endpoints | integration | `WebApplicationFactory` over the real `Compose`, with S1's contract-tested in-memory fakes and `FakeTimeProvider`. For every endpoint: <ul><li>the happy path;</li><li>its error codes;</li><li>no header and `session` on every decision, desk and protected route: 403, and nothing written but the audit row;</li><li>an `Origin` on any command: 403.</li></ul> `ARCH-CQRS-001` runs over the endpoint data source |
| Concurrency | integration | Two `Decide` calls on one decision, started together: exactly one `decision.recorded`, and the other gets `SND-ITEM-409` |
| Read-your-writes and freshness | integration | A command, then a query with `X-Sindri-After`, sees the write. A stalled projector gives `SND-API-004` and `asOf.stalled` |
| Projector rebuild | integration | Real SQLite in a temp dir: delete `views.db`, restart, and the same views come back byte for byte. Queries during the rebuild answer with `asOf.rebuilding` |
| Event stream | integration | Resuming with `Last-Event-ID`; the filters; `reset` on retention; the stream limits; non-public types never appear; an OSC or ANSI payload arrives escaped |
| CLI | unit + integration | Verb routing; the caller header from the terminal and the environment; the auto-start rules (§7); `--json` byte-equal to the API body |
| MCP server | **proof run** | A fresh `claude -p` subagent (§8, P8) |
| AOT | CI AOT job | S1's `aot-smoke`, extended in every phase with the new verbs |

**Scaffolding into Sindri** (conflict C7):
1. Run `/new-slice --files-only --surface http` for every slice. It generates the request and response records, the
   service stub, the validator, the red test and the builder.
2. The implementer adds the slice's lines at the anchors:
   - one line in `EndpointTable`;
   - one line in `Verbs`/`CommandTable`;
   - the `AddCommand`/`AddQuery` registration;
   - the `SindriJsonContext` entries.
3. The CLI verb is written by hand in `Host/Commands`. Sindri's Host is a web project, so `--surface cli` is refused.

**How an agent proves a phase.** Every phase signal below assumes this prover recipe:
- **Run the phase's own build, never `sindri` on `PATH`** (that's the TS CLI). Use
  `SINDRI="dotnet run --project src/Sindri/Aw.Sindri.Host --"`, or the binary published to the phase's temp dir.
- **Isolate each agent's state:** `export AW_SINDRI_HOME=$(mktemp -d)` and `AW_SINDRI_PORT=0`. The process picks a
  free port and records it in `control.json`, so parallel agents never share a ledger, a lease or a port.
- **Act as the human.** Implementers run inside Claude Code, so the CLI correctly classes them as `session`. A human
  step uses either of:
  - `env -u CLAUDECODE -u AW_SINDRI_CHILD script -q /dev/null $SINDRI <verb>` (`script` provides the terminal);
  - `curl -H 'X-Sindri-Caller: human' …`.

  This is the prover standing in for the owner, and the evidence says so.
- **Seed an approved profile:**
  1. Write the test profile.
  2. Read its hash from `$SINDRI profile validate --json`.
  3. Run `curl -X POST -H 'X-Sindri-Caller: human' …/sindri/profile/approve -d '{"hash":"<h>","confirmed":true}'`.
- **Evidence:** the commands, their output, and the `GET /ledger` excerpt, saved under the phase's proof folder.

**Docs per phase.**
- `docs/csharp/sindri.md` gains the phase's routes, verbs, events and codes.
- `AGENTS.md`'s commands block changes only for a verb a human runs at the desk.
- Every phase runs the AGENTS.md merge gate.

## 13. Review resolutions (`/autoplan`, 2026-10-10)

The five lens reviews and the consolidated review are in
`~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-api-surface/`. Every HIGH finding is fixed in this revision,
and every MEDIUM and LOW finding is answered below. The owner capped review at **three passes in total**, and this was
pass 1.

| Finding | Resolution |
|---|---|
| C-01: `SND-API-003` clash (arch F1, devex) | New API codes start at 004 (§9) |
| C-02: a missing header counts as `human` (security H1) | A missing header now counts as `session`. Only the CLI at a terminal and the app send `human` (§6) |
| C-03: a local browser page can drive commands (security H2) | Commands refuse any `Origin` header (`SND-API-008`), in P1 (§6, C3) |
| C-04: the audit step is innermost (arch F4, devex, security) | `AuditingService` sits outside validation and authorization, and a test pins the order (§2.1) |
| C-05: races on first-decision-wins and `itemVersion` (arch F2, security) | Fact keys, serialized commands, and waiting for the head; plus a concurrency test (§2.1, §12) |
| C-06: idempotent replay (arch F3, security) | The client `Idempotency-Key` is dropped from v0. Only natural fact keys remain, and the journal moves to Later (§2.1, §11) |
| C-07: no `POST /profile/validate` (arch F5, devex, product) | `profile validate` and `doctor` are class `local`, not routes (§3.1, §4.1) |
| C-08: two owners for park, takeover and handback (product) | The engine owns instance commands; §3.5 has the ownership table. Rev 1's P7 and P9 are removed |
| C-09: needs-you was a field of the queue (design D1) | `GET /needs-you`, with source-neutral rows and stable ids (§3.6) |
| C-10: dashboard views not scoped (design D2) | §3.6 says exactly what v0 feeds, and §11 names the collector queries |
| C-11: no freshness signal (design D3, arch F9) | `asOf { cursor, ledgerHead, rebuilding, stalled }` on every view (§2.2) |
| C-12: the wrong error doc (devex) | Codes go in `docs/csharp/sindri.md` (§9) |
| C-13: `/new-slice` anchors (devex) | C7: anchors in P1, plus `--files-only` and hand-written lines (§12) |
| C-14: agents can't run human steps (devex) | The prover recipe (§12) |
| C-15: shared home and port, and the TS `sindri` on `PATH` (devex) | A per-agent `AW_SINDRI_HOME`, `AW_SINDRI_PORT=0`, and `$SINDRI` (§12) |
| C-16: the MCP proof can't see its tools (devex) | A fresh `claude -p --mcp-config … --strict-mcp-config` subagent (§8) |
| C-17: two events per command (arch F6) | One event per command; the decision is derived (§3.4) |
| C-18: S1's `EventFactory` (arch F7) | Conflict C6, fixed in P1 |
| C-19: the projector failing (arch F8) | `stalled`, `SND-VIEW-001`, status, doctor and the UI banner (§2.3) |
| C-20: missing dependencies (arch F10) | `EventCatalog` lands in P1; P6 depends on P2; `sindri_job` comes with P6 (Phases) |
| C-21: an agent's CLI auto-starts the process (product, arch F11) | A CLI in an agent session never starts it (§7) |
| C-22: accepted requests do nothing before the engine (product) | `recordedOnly`, with text that says so (§3.4) |
| C-23: building ahead of the clients, and where `ILauncher` lives (product) | The v0 milestone is P1–P5, and the waves start there. P10 is marked as feeding the engine too (Phases) |
| C-24: `age` vs `since`, per-row actions, `cli-only`, states to groups, empty text (design D5–D11) | All adopted (§2.2, §3.1, §3.3, §3.6) |
| C-25: data from other repos reaching sessions (security) | MCP queries are scoped to the session's repo by default (§8) |
| C-26: injection through `sindri open` (security) | `ILauncher` receives only the item id and the repo path. The new session fetches the item's context itself, through `sindri mcp` or the CLI. No item or tracker text reaches argv, the launch config or the prompt (P10) |
| C-27: stale S1 facts, unnamed variables, docs, metrics (devex) | Fixed inline: `/ping`; `stop` exits 1; S1's `{hash, confirmed}`; ids based on GUIDv7; the raw `--json` body; the variables named and listed in `.env.example`; docs per phase; telemetry names (§6, §9, §12) |
| C-28: LOW items | <ul><li>Request text is capped at 2,000 characters and sanitized.</li><li>An actor may have at most 20 open requests.</li><li>Stream slots are reserved for `human`.</li><li>The `control.json` URL must be loopback.</li><li>On 429 the client falls back to 5 s re-queries.</li><li>`Last-Event-ID` wins.</li><li>There's no buffer per connection.</li><li>Progress is capped at 200 lines in the view. The ledger keeps every line, and at about 30 per job that's negligible.</li><li>Refusals are audited per attempt.</li><li>Logging `LoopbackGuard` refusals is S1's concern, left as built.</li><li>Routes and CLI forms live in the Host's catalog (§3.3).</li></ul> |
| Engine rev 2 alignment (found while revising) | The engine renamed `ApplyTrigger` to `ApplyCommand`, added `CompleteStep`, and moved its verbs under `sindri workflow …`. Its S1-D6 is now the item filter on `/events`, which P5 owns (§3.5) |

## Phases

Every phase is one PR and follows the slice recipe, red test first.
- **v0 is P1–P5.** After it, the app has its queue, needs-you, decisions and live refresh.
- P6, P8 and P10 follow.
- Every phase depends on **S1 merged**, because conflicts C1–C7 assume S1's code is on the default branch. Until then,
  phases stack on `feat/sindri-s1-app-shell`.

| # | Goal | Files (main ones) | Size | Depends on | Observable signal (with §12's prover recipe) | Unblocks |
|---|---|---|---|---|---|---|
| **P1** | **The CQRS spine:** <ul><li>the anchors (C7);</li><li>`ICommand` and `IQuery`;</li><li>`CallerContext` from `X-Sindri-Caller`, where a missing header means `session`;</li><li>the `Origin` refusal on commands;</li><li>the chain (`CommandGate` → `AuditingService` → `ValidatingService` → `AuthorizingService`) and the `AddCommand`/`AddQuery` helpers;</li><li>`CommandAccepted`, `ICommandCatalog` with `CommandCatalog`, `EventCatalog`, `ListCommands`, `apiVersion`;</li><li>classes for S1's commands, plus `sequence` and the raw `--json` body (C5), and the `EventFactory` change (C6);</li><li>the CLI's caller header from the terminal and environment, and the agent-session no-start rule;</li><li>`ARCH-CQRS-001..003`, `SND-AUTH-002`, `SND-API-007/008`.</li></ul> | `Core/Commands/*`, `Core/Events/EventCatalog.cs`, `Features/Commands/*`, `Features/ListCommands/`, `Host/CallerContextMiddleware.cs`, `Host/CommandCatalog.cs`, `Host/Endpoints/EndpointTable.cs` (anchors), `Host/Commands/*`, `tests/Aw.Sindri.Tests/Commands/*`, `tests/Aw.Architecture.Tests/Cqrs*`, rule records, `docs/csharp/sindri.md` | M | S1 | <ul><li>`curl -X POST …/sindri/profile/approve -d '{"hash":"x","confirmed":true}'` with **no header** gives 403 `SND-AUTH-002`, and `GET /ledger` shows only `command.handled` with outcome `refused`.</li><li>The same call with `-H 'Origin: http://localhost:3000'` gives 403 `SND-API-008`.</li><li>`curl …/sindri/commands` lists every command with its class and callers.</li><li>From an agent session, `$SINDRI stop` gives `SND-AUTH-002`.</li></ul> | UI, engine |
| **P2** | **The projector and read-your-writes:** <ul><li>`views.db`, `IProjection`, the projector service and `asOf`;</li><li>rebuild and stall handling;</li><li>`X-Sindri-After`, `SND-API-004`, `SND-VIEW-001`;</li><li>`RebuildViews`, `ListServices`, `GetProfile`;</li><li>doctor's `views` line.</li></ul> | `Core/Views/*`, `Infrastructure/Views/*` (+ migrations), `Host/Projector.cs`, `Features/{ListServices,GetProfile,RebuildViews}/` | M | P1 | <ul><li>Delete `views.db` and restart: `GET /services` returns the same JSON apart from `asOf`.</li><li>During `views rebuild`, a query shows `"rebuilding":true`.</li><li>`X-Sindri-After: <head+1000>` gives 503 `SND-API-004` after 2 s.</li></ul> | UI, engine |
| **P3** | **Items and the queue:** <ul><li>`ItemId` and the item events;</li><li>`item_view` and `queue_view`, `ItemActions`, `EmptyText`;</li><li>`CreateItem`, `GetQueue`, `ListItems`, `GetItem`, `GetItemTimeline`;</li><li>the CLI verbs `queue`, `items` and `item add`.</li></ul> | `Core/Items/*`, `Features/{CreateItem,GetQueue,ListItems,GetItem,GetItemTimeline}/`, projections, `Host/Commands/*` | M | P2 | <ul><li>Run `$SINDRI item add "Try the queue"` as the human. Then `curl -s -H 'X-Sindri-Caller: human' …/sindri/queue` byte-matches `$SINDRI queue --json`.</li><li>An empty queue prints its `EmptyText` sentence.</li><li>A `POST /items` with no header gives 403.</li></ul> | **UI** |
| **P4** | **Requests, decisions and needs-you:** <ul><li>`RequestId` and `DecisionId`;</li><li>`FileRequest` (one event; the decision is derived), `ListRequests`, `ListDecisions`, `Decide` (fact key);</li><li>`GetNeedsYou` with its v0 sources;</li><li>the CLI verbs `request`, `requests`, `decide` and `needs-you`;</li><li>`SND-REQ-*`, `SND-DEC-*`, `SND-ITEM-409`.</li></ul> | `Core/{Requests,Decisions,NeedsYou}/*`, `Features/{FileRequest,ListRequests,ListDecisions,Decide,GetNeedsYou}/`, projections, `Host/Commands/*` | M | P3 | <ul><li>`curl -X POST …/sindri/requests -d '{"kind":"rescope","text":"…"}'` with no header (so `session`) appears in `GET /needs-you`, with `primaryAction` `Decide` and `recordedOnly`.</li><li>The same `decide` with no header gives 403.</li><li>`$SINDRI decide DEC-… accept`, run as the human, resolves it.</li><li>Two `decide` calls started together: one succeeds, the other gives `SND-ITEM-409 decided elsewhere`.</li></ul> | **UI, engine** |
| **P5** | **The event stream and the ledger read:** <ul><li>public types from `EventCatalog`, and `PublicEvent`;</li><li>the `item` and `types` filters, and `Last-Event-ID`;</li><li>`reset` on retention, the ping, the stream limits (`SND-API-005`);</li><li>`ILedgerSignal`;</li><li>`ReadLedger` and `sindri ledger`;</li><li>`sindri status --follow` (C4).</li></ul> | `Core/Events/*`, `Infrastructure/Ledger/LedgerSignal.cs`, `Features/{StreamEvents,ReadLedger}/`, `Host/Endpoints/EventStream.cs` | S | P1. The signal uses `service.heartbeat`; the item filter is tested once P3 lands | <ul><li>`curl -N '…/sindri/events?after=0&types=service.*'` shows each `service.heartbeat` within 100 ms of its append.</li><li>Reconnecting with `Last-Event-ID: n` resumes at n+1.</li><li>`command.handled` never appears on the stream.</li><li>`$SINDRI ledger --json --after 0` lists it for the human.</li></ul> | **UI, engine** (S1-D6 item filter) |
| **P6** | **Jobs and services:** <ul><li>`JobId` and the job events;</li><li>`GetJob`, `CancelJob`;</li><li>`RunService` as a job, `PauseService`, `ResumeService` (the scheduler honours them);</li><li>the CLI follows progress on the stream, and Ctrl-C cancels;</li><li>`SND-JOB-404`, `SND-SVC-404`.</li></ul> | `Core/Jobs/*`, `Features/{GetJob,CancelJob,RunService,PauseService,ResumeService}/`, the scheduler hook in Host | M | P2 + P5 | <ul><li>`$SINDRI services run heartbeat` prints its progress lines and the result.</li><li>Ctrl-C during a long fake job prints `cancelled`, and `ledger` shows `job.cancelled`.</li><li>After `services pause heartbeat`, there's no heartbeat for 3 cadences.</li><li>A run with no header gives 403.</li></ul> | UI, **engine** |
| **P8** | **`sindri mcp`:** <ul><li>the MCP server: stdio, the MCP C# SDK, the §8 tools with repo scope;</li><li>`SND-API-001` when Sindri isn't running.</li></ul> | `Host/Mcp/*`, the SDK in `Directory.Packages.props` (pinned, at least 14 days old), `deps.yaml` | M | P4 + P6 | A fresh `claude -p --mcp-config … --strict-mcp-config` subagent: <ul><li>lists the queue and an item;</li><li>files a `rescope` request, which shows up in `GET /needs-you`;</li><li>finds no tool to decide;</li><li>gets `SND-AUTH-002` when it tries with `curl` and no header.</li></ul> Its transcript is the evidence | — |
| **P10** | **`sindri open`:** <ul><li>`ILauncher` (a Warp launch configuration, as in the dashboard plan's decision 15);</li><li>`OpenItem` passes only the item id and the repo path, and the session pulls its context through `sindri mcp`;</li><li>`SND-LAUNCH-001`.</li></ul> | `Core/Launch/ILauncher.cs`, `Infrastructure/Launch/WarpLauncher.cs`, `Features/OpenItem/` | M | P3 + P8. The engine marks the takeover once S2 runs | <ul><li>`$SINDRI open ITM-…` opens a Warp tab running Claude Code in the item's repo.</li><li>The session's first `sindri_item` call returns the item.</li><li>The launch config contains no item text.</li></ul> | **engine** (`ILauncher`) |

Rev 1's P7 and P9 are gone; the engine spec owns them (§3.5).

**What each client needs:**
- **The UI's v0 views** (§3.6) need P1–P5.
- **The engine** needs:
  - P1: the chain, the `service` caller and the catalogs;
  - P2: projections;
  - P4: decisions;
  - P5: the item filter on events;
  - P6: jobs;
  - P10: `ILauncher`.

## Parallel delivery with an agent team

An **agent team orchestrated with `/specToProvenPR`** builds the phases. The lead session only orchestrates, and keeps
every agent working. `/specToProvenPR`'s plan for this spec follows these rules.

### Dependency graph and waves

```
S1 ──► P1 ──┬──► P2 ──► P3 ──► P4 ──┬──► P8 ──► P10
            │     │                  │     ▲
            └──► P5 ─────────────────┼──► P6
                  (P6 needs P2 + P5) └─────┘ (P8 needs P4 + P6)
```

| Wave | Runs at once | Stacked on | Notes |
|---|---|---|---|
| W1 | **P1**, then **P2** ∥ **P5** as soon as P1's contract unit is committed | P1 on S1; P2 and P5 on P1 | P2 and P5 touch disjoint files |
| W2 | **P3** ∥ **P6** | P3 on P2; P6 on P2 + P5 | |
| W3 | **P4** | P3 | P4 needs P3's item model. Meanwhile, the other agents review W2 or write P8's contract unit |
| W4 | **P8**, then **P10** | P8 on P4 + P6; P10 on P8 | |

**How the branches stack:**
- One PR per phase, based on its parent phase's branch (stacked, as in §17.10).
- A phase is **ready** for review once its parent is ready; the parent doesn't have to be merged.
- Merges go in graph order, and each child rebases onto its merged parent.

### Units inside a phase

As in the engine spec:
- Each phase starts with a **contract unit**: the records, ports, catalog rows and red stubs that everything else
  compiles against. It runs alone.
- Then one implementer per slice fans out, with each slice a folder of its own.
- P1's contract unit also includes the anchor restructuring (C7).

### The team

| Role | Who | Does | Never does |
|---|---|---|---|
| **Lead (orchestrator)** | The session running `/specToProvenPR` on this spec | <ul><li>Keeps the wave board.</li><li>Dispatches the next ready unit the moment an implementer frees up.</li><li>Merges the anchor lines in shared files.</li><li>Rebases the stacked branches.</li><li>Runs the merge order.</li><li>Relays owner questions.</li></ul> | Write phase code |
| **Implementer** (one per ready unit) | A subagent in its own worktree (`isolation: worktree`) | Runs the slice recipe from red to green, with its own `AW_SINDRI_HOME` and `AW_SINDRI_PORT=0` (§12) | Touch another unit's folder |
| **Integrator** (one per phase) | A subagent started when the phase's last unit lands | <ul><li>Merges the anchor fragments.</li><li>Regenerates `PublicAPI.Unshipped.txt` from `RS0016`.</li><li>Runs the full merge gate.</li><li>Opens the draft PR.</li></ul> | — |
| **Prover** (one per phase PR) | A subagent following §12's prover recipe; for P8, a fresh `claude -p` with the MCP config | Runs the phase's signal in the running app and records the evidence | Treat green tests as proof |
| **Reviewers** | A fresh subagent per round (`/review`) | Review until there are no blocking findings and no issues. Fixes go back to implementers as new units | Fix code itself |

**Throughput:**
- At most **3 implementers** and at most **2 builds** at once; the heavy-job lock has two slots.
- An agent waiting on a review round is handed the next ready unit, or the next phase's contract unit.
- A unit whose parent changes rebases at its next green point, never in the middle of a slice.

### Conflict rules for shared files

A unit never edits these files directly. It writes its lines in its own fragment, and the integrator merges the
fragments at the anchors:
- `Features/SindriFeaturesServiceCollectionExtensions.cs`: one `AddCommand` or `AddQuery` line per slice.
- `Host/Endpoints/EndpointTable.cs`, `Host/Commands/{CommandTable,Verbs,Usage}.cs` and `Host/SindriJsonContext.cs`.
- `Host/CommandCatalog.cs` (one row per command) and `Core/Events/EventCatalog.cs` (one row per event).
- `Core/SindriErrors.cs`, its `All` list, and the error table in `docs/csharp/sindri.md`. **Codes are reserved per phase
  in §9**, so phases never collide.
- `PublicAPI.Unshipped.txt`, which the integrator regenerates from `RS0016`.
- `tests/Aw.Sindri.Testing/A.cs`, and the AOT smoke test's `expected.txt`.
- `views.db` migrations. Their numbers are reserved per phase: P2 `V0xx`, P3 `V1xx`, P4 `V2xx`, P6 `V3xx`.

Every other file belongs to exactly one unit.

### Done

A phase is done when its PR is ready; no agent ever merges it. Ready means:
- the signal has been shown in the running app, with evidence;
- the merge gate is green;
- the review loop has reached zero blocking findings and zero issues, with every suggestion answered in writing.

The lead reports each phase as it becomes ready, and stops for the owner's merge.
