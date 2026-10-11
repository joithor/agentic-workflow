# Sindri API surface: commands, queries, the event stream and the MCP server

Status:
- Approved in conversation, decision by decision, on 2026-10-10.
- Revised the same day after `/autoplan`: rev 2 after review pass 1, and rev 3 after pass 2. The owner capped review at
  three passes. §13 lists every finding and its resolution.
- This document is the written spec for review.

The spec designs the surface every Sindri client uses: the `sindri` CLI, the Avalonia app (`SindriUi`), the Sindri MCP
server (`sindri mcp`), and the workflow engine's calls into the command layer.
- `/specToProvenPR` takes the **Phases** at the end and turns them into PRs, using an agent team.
- The UI design builds on §3, §5 and §3.6.

**Builds on:**
- The factory addendum (`2026-10-09-sindri-factory-addendum.md`):
  - §17.27.1, one command layer;
  - §17.27.2, action classes;
  - §17.27.4, the command registry;
  - §17.21, the event envelope and the API;
  - §17.29, the application, `sindri open`, and MCP calls being requests;
  - §17.12 and §17.23.7, decisions.
- The core design (`2026-10-07-sindri-design.md`): §9.3 (takeover) and §10.3 (the CLI).
- The dashboard spec (`2026-10-09-sindri-dashboard-design.md`): what the UI shows.
- The workflow engine spec (`2026-10-10-sindri-workflow-engine-design.md`): it owns instances, park, takeover and
  handback (§3.5).
- `.agents/rules/csharp.md` and `docs/csharp/scaffolds/new-slice.md`.

**Fixed, from stage S1** (the app shell; branch `feat/sindri-s1-app-shell`; contract `docs/csharp/sindri.md`):

| Area | What S1 fixes |
|---|---|
| Routes | `GET /sindri/status`, `GET /sindri/ping`, `POST /sindri/stop`, `POST /sindri/profile/approve` and `GET /sindri/events` |
| Host | `LoopbackBinding`, `LoopbackGuard` and `HttpResults` |
| `Core` types | Those under `Control`, `Ledger`, `Lease`, `Profile`, `Scheduling` and `Doctor`, including `EventFactory` |
| `SindriErrors` | `SND-LEASE-001…003`, `SND-PROFILE-001…005`, `SND-LEDGER-001/002`, `SND-API-001…003`, `SND-CLI-001/002` and `SND-SCHED-001` |
| Process model | `sindri serve`, `control.json`, the spawn, and the pid and epoch check against `/ping` |
| CLI-local verbs | `doctor` and `profile validate` |

This design adds to these. Where it has to change one of them, §10 lists the conflict.

## 1. Decisions

| # | Question | Decision |
|---|---|---|
| D1 | Resource model | **CQRS** over resource paths:<ul><li>commands are `POST`s that append one domain event, plus the audit row, and return `CommandAccepted`;</li><li>queries are `GET`s that read projections;</li><li>one route = one slice = one `IService` (§2, §3).</li></ul> |
| D2 | Commands vs queries, decisions vs requests | <ul><li>Every command declares an **action class**.</li><li>Every caller has a **caller class**, and humans also have a **terminal** flag.</li><li>One table authorizes the combination.</li><li>Sessions may run queries, file **requests** and **pause** automation, but never make decisions (§4).</li></ul> |
| D3 | Event stream | **Server-Sent Events**, `GET /events?after=`. The cursor is the ledger sequence, and events go out in a public envelope. It's a "go re-read" signal for live clients, never the source of truth (§5) |
| D4 | Local auth | **v0 is local-only, with no tokens:**<ul><li>the caller class comes from `X-Sindri-Caller`, and a missing header counts as `session`;</li><li>only the CLI at a terminal and the app send `human`;</li><li>the CLI also sends `X-Sindri-Terminal: 1`;</li><li>commands refuse any `Origin` header.</li></ul>Tokens, passkeys and the tailnet come later (§6) |
| D5 | Finding or starting the process | S1's `control.json`, spawn and ping check, plus one rule for which verbs may start the process. Nothing run inside an agent session starts it (§7) |
| D6 | MCP server | `sindri mcp` (stdio, the MCP C# SDK) is a thin client with the session caller class. It offers queries and `sindri_request` only. **It is proven by a real Claude Code subagent**, run as a fresh `claude -p` process (§8) |
| D7 | Webhooks | **Cut for now** (§11) |
| D8 | Errors, versioning, JSON | <ul><li>S1's `Result` → `ProblemDetails` mapping stays unchanged.</li><li>New codes are reserved per phase and documented in `docs/csharp/sindri.md`.</li><li>No version in the path in v0.</li><li>One JSON context.</li><li>`--json` prints the API body unchanged (§9).</li></ul> |

## 2. The CQRS model

### 2.1 Write side: commands

**What a command is.**
- A command is a request record that implements the marker `ICommand<TResult>` (`Aw.Sindri.Core.Commands`).
- `TResult` is either `CommandAccepted`, or a record that carries it plus the ids the command created.
- It is served by an `IService<TCommand, TResult>`, like every slice.
- A command whose class depends on its arguments implements `IClassifiedCommand.ActionClass`. For example, the
  engine's `ApplyCommand` is `protected` when it carries a `grant` or `accept`, and `desk` otherwise.

**What a handler does.**
1. It reads the state it needs from projections (§2.4 says when it waits for them).
2. It decides with a pure method on a `Core` type.
3. It appends **exactly one domain event** under the process's lease grant (S1's `ILedgerStore.Append`).

Everything that follows from that event, such as a request's pending decision, is derived in the projections. The
audit row (below) is a separate append.

**Handlers never do slow work.**
- A handler decides and appends; it never calls a model, a judge, git or the network.
- Slow work runs as a job (§3.2, P6) or as an engine Step runtime after the command is accepted.

**`CommandAccepted(Guid CommandId, long Sequence)`.** `Sequence` is the ledger sequence the domain event was written
at. It's the read-your-writes token in §2.4.

**Commands run one at a time.** A process-wide `CommandGate` serializes every command. It's a `SemaphoreSlim(1)`,
acquired with a 5 s limit; past that, the command fails with `SND-API-009` ("busy", 503). Commands decide and append
only, so the gate is never held for long.

**Waiting for the views.**
- A command that reads views waits, inside the gate, until the projector has reached the ledger head. The limit is
  2 s, after which it fails with `SND-API-004`.
- `StopProcess` and `RebuildViews` read no views and **don't wait**. So the fix for a stalled projector, and stopping
  the process, both always work.

**Fact keys make conflicts lose in the ledger.** Each event's idempotency key names the *fact*, not the request:

| Event | Key |
|---|---|
| `decision.recorded` | `decision.recorded:<decisionId>` |
| `item.state-changed` | `item.state-changed:<itemId>:v<newVersion>` |
| `request.filed` | `request.filed:<requestId>` |

- If two writers race anyway, S1's `Append` returns `AlreadyRecorded` with the earlier event's sequence.
- The handler then reads that event. Every domain payload carries `commandId`, so when the event's `commandId` isn't
  the handler's own, the result is `SND-ITEM-409`: "decided elsewhere", or "changed since you looked".
- There is **no client `Idempotency-Key` header in v0** (§11). Commands that create things aren't deduplicated, and the
  CLI never retries a command on its own.

**The command chain.** One helper, `AddCommand<TCommand, TResult, TService, TValidator>()` in `AddSindriFeatures()`,
registers these decorators, so no command can skip them. Every command needs a validator; the default
`AcceptAllValidator<T>` is allowed. A test resolves every registered `ICommand` and asserts this order, outermost
first:

| # | Decorator | What it does |
|---|---|---|
| 1 | `CommandGate` | Serializes commands, as above |
| 2 | `AuditingService` | <ul><li>Wraps everything inside it.</li><li>Appends `command.handled {commandId, command, class, caller, outcome, code?}`, keyed `command.handled:<commandId>`.</li><li>Writes a row for every attempt that reaches the chain: accepted, refused, invalid or failed.</li></ul> |
| 3 | `AuthorizingService` | Checks the command's action class against the caller (§4). It runs before validation, so an unauthorized caller learns nothing about valid input. A refusal returns `SND-AUTH-002` and writes nothing except the audit row |
| 4 | `ValidatingService` (Kernel) | Runs the slice's `IValidator` |

Requests the host can't bind never reach the chain. That covers malformed JSON, a bad id, a missing `Content-Type`,
and a body over 64 KiB. They get 400 or 415, and the host logs them; they aren't audited.

**When the audit can't be written.**
- **`StopProcess` is self-audited.** Its handler releases the lease, so no row could be written after it. It is
  flagged `SelfAudited` in the catalog, and S1's `process.stopped` event is its audit.
- **An audit write fails after an accepted command.** The command's result is still returned, because its domain event
  is already recorded, and the failure is logged as `SND-LEDGER-002`.
- **An audit write fails on a refused or invalid command.** The caller gets 503 `SND-LEDGER-002`.

**The caller context.**
- `CallerContext(CallerClass Class, bool Terminal, string Actor, Guid CommandId, Guid CorrelationId)` is filled by the
  Host from the request (§6). In-process callers, the engine and the scheduler, fill it themselves.
- It is reached through a singleton `ICallerContextAccessor`, which is `AsyncLocal`-based like
  `IHttpContextAccessor`. That way S1's singleton `EventFactory` can read it (conflict C6).
- `Actor` is composed by the host, never taken from the caller (the C# Seams rule). Its values are:
  - `human:cli`, `human:ui`, `human:prover`, `human:http`;
  - `session:mcp`, `session:cli`, `session:http`;
  - `service:<name>`.

### 2.2 Read side: queries and projections

**What a query is.** A request record that implements `IQuery<TView>`, served by `IService<TQuery, TView>`.

**What a query handler reads.**
- Projections only, through read ports such as `IItemViews`, `IQueueView`, `INeedsYouView` and `IDecisionViews`.
- Their adapters open a read-only connection (`SqliteConnectionFactory.OpenReadOnly`).
- A query handler takes no write port and appends nothing.
- The one exception is `ReadLedger` (§3.1). It reads through `ILedgerReader`, a read-only port whose adapter opens
  `ledger.db` read-only.

**Where projections live.**
- In their own SQLite file, `$AW_SINDRI_HOME/views.db` (0600), separate from `ledger.db`.
- Each view is a table keyed by its id.
- A `projector_state` row holds the cursor, the schema version and the stall state.

**Freshness: every view response carries `asOf`.**
- It holds `{ cursor, ledgerHead, projectedAt, rebuilding, stalled }`.
- `ledgerHead` comes from the projector's in-memory status (`IProjectorStatus`), not from the ledger store.
- `projectedAt` is when the cursor last moved. It is the clock behind the UI's "updated Ns ago".
- With `asOf`, a client can tell "empty" from "still loading" from "views are broken" (§3.6).

**Times, not ages.** Views carry `since` and `updatedAt` timestamps, never an `age`. That keeps a view byte-stable;
clients compute ages themselves.

### 2.3 The projector

**How it runs.** It is an in-process hosted service in the Host, not an S1 `IScheduledService`. In a loop it:
1. calls `ReadAfter(cursor, 100)`;
2. applies each event to every registered `IProjection` whose types match;
3. stores the new cursor.

Each page is one `views.db` transaction. Between pages it sleeps on the ledger signal (§5.5), or for 1 s.

**Where the ledger head comes from.**
- S1's `ILedgerStore` gains an additive `Head()` member (conflict C9) with a contract test. The projector calls it each
  loop and publishes the value through `IProjectorStatus`.
- Commands and queries read the head from `IProjectorStatus`, never from `ILedgerStore`.

**Projections are pure folds.**
- Each is `Apply(row?, event) → row?`, unit-tested from lists of events.
- One projection may write rows in several views from one event. For example, `request.filed` creates both the request
  row and its pending decision row.

**Rebuild.** Three things trigger a rebuild: `sindri views rebuild` (desk class), a schema-version change, and a missing
`views.db` at startup.
- A rebuild sets `rebuilding`, empties the view tables, resets the cursor to 0 and replays the ledger.
- While it runs, queries still answer, with `asOf.rebuilding = true`. The UI shows its loading state, and the CLI
  prints the `EmptyText` "rebuilding" sentence (§3.6).
- A new or changed read model is therefore a replay, never a data migration.

**Failure.** A projection that throws on an event stops the projector at that sequence:
- it sets `stalled`, logs `SND-VIEW-001` with the sequence and event type, and retries every 30 s;
- `GET /status` reports `views: stalled at <sequence>`;
- `sindri doctor` gains a `views` line, which fails with the fix `sindri views rebuild`;
- queries answer with `asOf.stalled = true`, and the UI shows its error banner;
- `RebuildViews` doesn't wait for the head (§2.1), so the fix always runs.

**Why asynchronous.** Commands are serialized and wait for the head, so the async projector gives the same answers an
inline one would. Inline projection stays a later option (§11).

### 2.4 Read-your-writes

**Queries.** Every query accepts `X-Sindri-After: <sequence>`. The handler waits on the projector's progress signal
until the cursor is at or past that sequence. After 2 s it returns `SND-API-004` (503), naming the cursor and the
target.

**Commands** that read views wait for the head before deciding (§2.1).

**Clients.**
- The CLI sends the `Sequence` of its own previous command when one verb chains a command and a query.
- The UI sends the last `Sequence` it received. If it gets `SND-API-004` after an accepted command, it keeps its "done"
  confirmation, shows the view as stale, and re-queries on the next event.
- When the UI is on the event stream, it marks views stale if no event or ping has arrived for 30 s.

### 2.5 Architecture tests (`tests/Aw.Architecture.Tests`)

| Id | Rule |
|---|---|
| `ARCH-CQRS-001` | <ul><li>Every `MapGet` endpoint resolves an `IService<TQuery, TView>` where `TQuery : IQuery<TView>`.</li><li>Every `MapPost` resolves an `IService<TCommand, _>` where `TCommand : ICommand<_>`.</li><li>S1's `GET /ping` and `GET /status` are queries, and `GET /events` is the stream query.</li><li>It is checked over the endpoint data source in an integration test, because minimal-API lambdas aren't visible to ArchUnitNET.</li></ul> |
| `ARCH-CQRS-002` | No query handler's constructor takes `ILedgerStore`, `ILease`, `IApprovals` or a projection writer |
| `ARCH-CQRS-003` | Every `ICommand` (and every action variant) has a row in the command catalog (§3.3) |

Each rule gets a rule record under `rules/engineering/csharp/` in P1 (`node scripts/check-rules.mjs`).

## 3. Resources, endpoints and commands

All routes sit under S1's group `/sindri`; the tables below show them without it.
- Request and response records live in `Aw.Sindri.Features.<Slice>`, and every one is registered in
  `SindriJsonContext`.
- **Class** is the action class (§4).
- **Slice** is the slice name (§12 says how it is scaffolded).
- **Phase** is the PR that adds it.

New typed ids are strings: a prefix plus a compact GUIDv7 from `IIdGenerator`. Each follows `docs/csharp/typed-ids.md`
and gets its route-binding test.

| Id | Format |
|---|---|
| `ItemId` | `ITM-…`, or a tracker key such as `ENG-142` |
| `RequestId` | `REQ-…` |
| `DecisionId` | `DEC-…` |
| `JobId` | `JOB-…` |
| `RepoName` | The repo's name |

### 3.1 Queries

Every list view also returns `empty`, the `EmptyText` sentence (§3.6) for its current state.

| Route | Slice | Request | Response | Phase |
|---|---|---|---|---|
| `GET /status` | `GetStatus` (S1) | — | S1's response, plus `apiVersion` and `views` (§2.3) | S1; extended in P1 and P2 |
| `GET /ping` | `PingProcess` (S1) | — | S1's `{pid, epoch}` | S1 |
| `GET /commands` | `ListCommands` | — | `CommandList(rows)`. Each row: <ul><li>action id (`<Command>` or `<Command>:<variant>`);</li><li>class;</li><li>allowed callers;</li><li>whether it needs a terminal;</li><li>method and route;</li><li>CLI form;</li><li>UI status: `ready`, `pending:<phase>` or `cli-only:<reason>`.</li></ul> | P1 |
| `GET /services` | `ListServices` | — | `ServiceList(services, empty, asOf)`. Each service: <ul><li>name;</li><li>cadence;</li><li>paused;</li><li>last run (slot, outcome, code);</li><li>next slot.</li></ul> | P2 |
| `GET /profile` | `GetProfile` | — | `ProfileView(asOf)`: <ul><li>approved hash and when it was approved;</li><li>live hash, as of the last drift check;</li><li>whether it changed since approval;</li><li>the changed key paths.</li></ul> | P2 |
| `GET /queue` | `GetQueue` | — | `QueueView(groups, empty, asOf)`: the §3.6 groups in their fixed order, each a list of `ItemSummary` | P3 |
| `GET /items?state=&repo=` | `ListItems` | `ListItemsQuery(ItemState?, RepoName?)` | `ItemList(items, empty, asOf)`. Each item: <ul><li>id, title, repo, state, step;</li><li>`since`;</li><li>its next action;</li><li>`actions`: the action ids allowed now.</li></ul> | P3 |
| `GET /items/{itemId}` | `GetItem` | `GetItemQuery(ItemId)` | `ItemView`: <ul><li>the summary, plus version and who created it;</li><li>what it's waiting on (a decision id);</li><li>the engine instance id, once S2 exists;</li><li>timestamps;</li><li>`actions`;</li><li>`asOf`.</li></ul> | P3 |
| `GET /items/{itemId}/timeline?after=` | `GetItemTimeline` | `GetItemTimelineQuery(ItemId, long?)` | `Timeline(entries, next, asOf)`. Each entry: sequence, time, type, actor, and one scrubbed line | P3 |
| `GET /needs-you` | `GetNeedsYou` | — | `NeedsYouList(rows, empty, asOf)` (§3.6) | P4 |
| `GET /requests?state=` | `ListRequests` | `ListRequestsQuery(RequestState?)` | `RequestList(requests, empty, asOf)`. Each request: <ul><li>id, kind, item;</li><li>text (scrubbed, at most 2,000 characters);</li><li>actor, state, decision id.</li></ul> | P4 |
| `GET /decisions?pending=&item=` | `ListDecisions` | `ListDecisionsQuery(bool?, ItemId?)` | `DecisionList(decisions, empty, asOf)`. Each decision: <ul><li>id, kind, class, item, item version;</li><li>question, options and the recommended option;</li><li>state, and who decided it;</li><li>`recordedOnly`;</li><li>the CLI command.</li></ul> | P4 |
| `GET /events?after=&item=&types=` | `StreamEvents` (S1, extended) | `StreamEventsQuery(long, ItemId?, TypeFilter?)` | An SSE stream of `PublicEvent` (§5) | S1; extended in P5 |
| `GET /ledger?after=&item=&type=&max=` | `ReadLedger` | `ReadLedgerQuery(long, ItemId?, string?, int)`, with `max` ≤ 500 | `LedgerPage(events, next)`: public envelopes with sanitized payloads. It's an audit read, so `human` callers also get the non-public types | P5 |
| `GET /jobs/{jobId}` | `GetJob` | `GetJobQuery(JobId)` | `JobView`: <ul><li>id, kind, item?;</li><li>state: `running`, `succeeded`, `failed` or `cancelled`;</li><li>when it started and ended;</li><li>the last 200 progress lines;</li><li>result summary and code;</li><li>`asOf`.</li></ul> | P6 |

**Session callers are scoped to one repo.**
- A `session` caller's list and item queries are filtered to the repo named in `X-Sindri-Repo`, which `sindri mcp` and
  the CLI set from the working directory.
- An item in another repo answers `SND-ITEM-404`.
- A `session` query without `X-Sindri-Repo` answers only items that have no repo.
- `human` callers see everything.

**CLI-local verbs.** `sindri doctor` and `sindri profile validate` run inside the CLI and read local files, as in S1.
They're in the catalog with class `local` and are never routes. `doctor` gains a `views` line in P2.

### 3.2 Commands

| Route | Slice | Body (beyond the route) | Response | Class | CLI | Phase |
|---|---|---|---|---|---|---|
| `POST /stop` | `StopProcess` (S1) | — | S1's response, plus `sequence` | desk (self-audited) | `sindri stop` | S1; class in P1 |
| `POST /profile/approve` | `ApproveProfile` (S1) | S1's body | S1's response, plus `sequence` | **protected** | `sindri profile approve [<hash>]` | S1; class in P1 |
| `POST /views/rebuild` | `RebuildViews` | — | `CommandAccepted` | desk | `sindri views rebuild` | P2 |
| `POST /items` | `CreateItem` | `title`, `repo?`, `body?` | `ItemCreated(accepted, ItemId)` | desk | `sindri item add "<title>" [--repo R]` | P3 |
| `POST /items/{itemId}/close` | `CloseItem` | `reason` (`done` or `cancelled`), `itemVersion` | `CommandAccepted` | desk | `sindri item close <item> [--cancelled]` | P3 |
| `POST /requests` | `FileRequest` | `kind` (`rescope`, `add-task`, `note` or `handback`), `itemId?`, `text` | `RequestFiled(accepted, RequestId, DecisionId)` | **request** | `sindri request <kind> [--item X] "<text>"` | P4 |
| `POST /decisions/{decisionId}` | `Decide` | `choice`, `note?` | `CommandAccepted` | decision | `sindri decide <decisionId> <choice> [--note]` | P4 |
| `POST /services/{name}/run` | `RunService` | — | `JobStarted(accepted, JobId)` | job | `sindri services run <name>` | P6 |
| `POST /services/{name}/pause` | `PauseService` | — | `CommandAccepted` | desk | `sindri services pause <name>` | P6 |
| `POST /services/{name}/resume` | `ResumeService` | — | `CommandAccepted` | desk | `sindri services resume <name>` | P6 |
| `POST /jobs/{jobId}/cancel` | `CancelJob` | — | `CommandAccepted` | job | Ctrl-C while following; `sindri jobs cancel <id>` | P6 |
| `POST /items/{itemId}/open` | `OpenItem` | — | `ItemOpened(accepted, launchedIn)` | desk | `sindri open <item>` | P10 |

Notes:
- **`ApproveProfile` keeps S1's body.** The `protected` class means only a `human` caller **with a terminal** reaches
  the handler (§4). How the human confirms is a separate question:
  - S1 uses a `confirmed` flag set by the CLI after a TTY prompt.
  - The engine spec's S1-D5 replaces it with a server-issued challenge.
  - Whichever lands is behind this class check.
- **Item versions.**
  - `Decide` reads the item version from the decision's own row.
  - `CloseItem` sends the version the human saw.
  - Conflicts are settled by fact keys (§2.1).
- **The engine spec owns the instance commands**, together with their routes and the verbs `sindri workflow …`, which
  keep `sindri handback` as the short alias from §9.3:
  - `StartInstance`;
  - `ApplyCommand` (answer, with an optional `grant` or `accept`; takeover; handback; cancel);
  - `CompleteStep`;
  - `GetInstance`;
  - `ListInstances`.

  See §3.5.

### 3.3 The command catalog

`ICommandCatalog` is a port in `Core`. The Host implements it as `CommandCatalog`, because routes and CLI forms are Host
facts.

**Rows.** There is one row per action id: a command, or a command variant such as `ApplyCommand:handback` or
`ApplyCommand:answer-grant`. Each row has:
- the command type and variant;
- the action class;
- the allowed callers;
- whether it needs a terminal;
- `SelfAudited`;
- the method and route;
- the CLI form;
- the UI status: `ready`, `pending:<phase>`, or `cli-only:<reason>`. `cli-only` is accepted only for
  terminal-bound rows, for example `cli-only:typed-confirmation`.

**Who uses it.**
- `ListCommands` serves it.
- The UI renders its buttons and forms from it, keyed by action id (§17.27.4).
- `doctor` lists the rows still `pending`.
- The engine spec registers its rows here too.

**The test.** A unit test fails when:
- a command or variant has no row;
- a row's route isn't in the endpoint table;
- a `pending:<phase>` names a phase that has already shipped;
- a `cli-only` row isn't terminal-bound.

### 3.4 Events these phases add

Every event is a `LedgerEntry` (S1) with the Kernel `EventEnvelope`. The phase that adds an event registers it in
`EventCatalog` (`Core`), with its type, whether it's public, its public payload type and its fact-key shape. The
catalog itself lands in P1. Every domain payload carries `commandId` (or the `service:<name>` run id).

| Type | Payload (besides `commandId`) | Key | Public | Phase |
|---|---|---|---|---|
| `command.handled` | command, class, caller, outcome (`accepted`, `refused`, `invalid` or `failed`), code? | `command.handled:<commandId>` | no | P1 |
| `views.rebuilt` | cursor before, events replayed | `views.rebuilt:<commandId>` | no | P2 |
| `service.run-failed` | service, slot, code | `service.run-failed:<name>:<slot>` | yes | P2 (C8) |
| `item.created` | item, title, repo | `item.created:<itemId>` | yes | P3 |
| `item.state-changed` | item, from, to, version (the new one), reason? | `item.state-changed:<itemId>:v<version>` | yes | P3 |
| `profile.drifted` | approved hash, live hash, changed paths | `profile.drifted:<liveHash>` | yes | P4 (C8) |
| `request.filed` | request, decision, kind, item?, text (scrubbed, at most 2,000 characters) | `request.filed:<requestId>` | yes (text left out) | P4 |
| `decision.recorded` | decision, choice, note?, by | `decision.recorded:<decisionId>` | yes | P4 |
| `job.started` | job, kind, item? | `job.started:<jobId>` | yes | P6 |
| `job.progress` | job, round, line | `job.progress:<jobId>:<n>` | yes | P6 |
| `job.finished` | job, outcome, summary, code? | `job.finished:<jobId>` | yes | P6 |
| `job.cancelled` | job | `job.cancelled:<jobId>` | yes | P6 |
| `service.paused`, `service.resumed` | service | `service.paused:<name>:<commandId>` | yes | P6 |

**How a request becomes a decision** (one domain event per command):
1. `FileRequest` appends `request.filed`, with both ids chosen by the handler.
2. The projections derive the request row (`open`) and one pending decision for it:
   - kind `accept-request`;
   - options `accept` and `decline`;
   - class `decision`.
3. `Decide` appends `decision.recorded`.
4. The projections mark the decision as decided, and the request as `accepted` or `declined`.

**Before the engine (S2) exists,** an accepted request is **recorded only**. Its decision row and needs-you row carry
`recordedOnly: true`, shown as "recorded; acted on once the workflow engine runs".

**Engine parks and gates aren't `Decide` decisions.** The engine answers them with `ApplyCommand`. The needs-you
projection folds the engine's park and gate events into rows of kind `park`, whose `primaryAction` is the engine's
action id, for example `ApplyCommand:answer`.

### 3.5 Ownership with the workflow engine spec

| Concern | Owner | The other spec's part |
|---|---|---|
| Instances: their states, park reasons, takeover, handback, answering a park (`grant`, `accept`), timers, `CompleteStep` | **Engine**: `StartInstance`, `ApplyCommand`, `CompleteStep`, `GetInstance`, `ListInstances`; engine P4 and P5 | This spec gives their catalog rows a class and allowed callers:<ul><li>start, answer, handback, cancel and complete are `desk`;</li><li>answer with `grant` or `accept` is `protected` (terminal);</li><li>takeover is `pause` (§4.1).</li></ul> |
| The verbs `sindri workflow start`, `answer`, `takeover`, `handback`, `cancel` and `complete`, plus the `sindri handback` alias | **Engine** | — |
| One "needs you" list | **This spec**: `GET /needs-you`, with engine parks as kind `park` | The engine's `workflow list --needs-me` should read `GET /needs-you`, so there is one list. This is a note for the engine spec |
| Item rows, the queue, requests and decisions | **This spec** | The engine emits the events these views fold. Once S2 runs, an item's `state` reflects its instance state |
| The item filter on `/sindri/events` (engine S1-D6) | **This spec**, in P5 | The engine's `workflow events` uses it. If engine P4 merges first, it adds the filter and P5 keeps it |
| `ILauncher` | **This spec**, in P10 (`OpenItem`) | The engine replaces its desk runtime with it (engine W20) |
| The authorization model: caller, terminal flag, action classes, the chain, and `SND-AUTH-002` | **This spec**, in P1 | Every engine command goes through the same chain. **The engine spec should drop `SND-WF-007` and its `human-terminal` caller in favour of this model**: `protected` means human with a terminal, and the code is `SND-AUTH-002`. Flagged for the engine spec's owner |
| `EventFactory` reading the caller (C6) and the engine's S1-D7 | One change, made by whichever phase lands first | — |
| Starting the process from a verb | **This spec**, §7: never from inside an agent session | The engine spec's "starts if needed" follows §7 |

This spec has no item `hold`, and no item-level park, unpark or handback: those are engine instance commands.

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

How items move through the states:
- **Before S2:**
  - an item is `new`;
  - it is `needs-decision` while a pending decision points at it, and back to `new` once the decision is recorded;
  - it is `done` or `cancelled` after `CloseItem`.
- **Once S2 runs:**
  - `needs-answer`, `taken-over`, `parked`, `running` and `queued` come from the engine's item projection;
  - `needs-answer` is produced only by the engine.

**`GET /needs-you`** is one list across every source, built by the needs-you projection. Each `NeedsYouRow` has:

| Field | What it holds |
|---|---|
| `id` | A stable id, such as `decision:DEC-…` or `failure:service:heartbeat`. The UI keys focus and the "seen" pulse on it |
| `kind` | `decision`, `park`, `failure` or `profile` |
| `subject` | A reference to an item, decision, instance, job or service |
| `title`, `reason` | Scrubbed text |
| `repo?` | The repo the row belongs to |
| `urgency` | `pulse`, `colour` or `calm` (dashboard spec §5.1) |
| `since` | When the row appeared |
| `recordedOnly` | Whether the decision is recorded only (before S2) |
| `primaryAction` | A catalog action id, or `copy:<cli>` for a `cli-only` row |
| `cli` | The CLI form |

**Rows by kind.** A filed request appears **once**, as its `decision` row; there is no separate request row.

| Kind | Source | Urgency | Cleared when |
|---|---|---|---|
| `decision` | Pending decisions, including filed requests (P4) | `pulse` | `decision.recorded` |
| `park` | Engine parks and gates (S2) | `pulse` | The engine resumes the instance |
| `failure` | `service.run-failed` (C8, P2) | `colour` | The service's next successful run |
| `profile` | `profile.drifted` (C8, P4) | `calm` | `profile.approved` for the live hash |

- The collector adds more sources later (§11): sessions waiting on approval or input, stale indexes, judge health and
  PRs. Each is one more projection writing rows of the same shape.
- An explicit acknowledge (`POST /needs-you/{id}/ack`) comes later too.
- Rows are ordered by urgency, then oldest first.

**Empty, loading and error text.** One `EmptyText` table in `Core` holds the sentences for each list: its empty
sentence (for example "Nothing is waiting on you. 3 running, 1 queued."), plus `rebuilding` ("Rebuilding views (cursor
N of M)…") and `stalled` ("Views are stalled at N: run `sindri views rebuild`").
- The CLI prints them, and every list view returns the right one in `empty`. The UI never keeps its own copy.
- For errors, `ProblemDetails.detail` already carries S1's `(fix: …)` text, and the UI shows `detail` as it is.

**Agent text.**
- Text from agents or trackers is scrubbed, then escaped with the shared sanitizer, when the projection writes it. The
  sanitizer covers control characters, invisible characters, and OSC and ANSI sequences.
- `ReadLedger` passes payloads through the same sanitizer.
- The API never returns raw control characters, and the UI renders text as plain text only.

**Dashboard views fed by v0 (P1–P5):**
- **Needs you**, except its session, PR, index and judge sources;
- the queue, and item detail;
- decisions and requests;
- the services part of **Automatic work**;
- live refresh.

**Fed later (§11):** **Agents**, **Repos**, the rest of **Automatic work**, and **Today**.

## 4. Commands vs queries, decisions vs requests

### 4.1 Action classes (one per command or variant)

| Class | Meaning | Commands |
|---|---|---|
| `request` | A fact filed for a human or the engine to act on; changes no item state | `FileRequest` |
| `pause` | Only stops automation. It is safe in one direction: resuming is `desk` | The engine's `ApplyCommand:takeover` |
| `job` | Read-only work, possibly long-running; writes only its own job events | `RunService`, `CancelJob` |
| `decision` | A non-protected human decision (the §17.12 list) | `Decide` |
| `desk` | A human action that changes item or process state (§17.27.2) | `StopProcess`, `RebuildViews`, `CreateItem`, `CloseItem`, `PauseService`, `ResumeService`, `OpenItem`; the engine's start, answer, handback, cancel and complete |
| `protected` | The approval tier: a typed confirmation, bound to the content, at a terminal | `ApproveProfile`, and the engine's `ApplyCommand:answer-grant`, `answer-accept` and `recipe approve`. Later: evolve adopt, channel promote and tier raises |
| `local` | Runs in the CLI, never through the API | `doctor`, `profile validate` |

### 4.2 Callers and the authorization table

| Class \ caller | `human` + terminal | `human` (app, no terminal) | `session` | `service` (in-process only) |
|---|---|---|---|---|
| any query | yes | yes | yes, scoped to its repo (§3.1) | yes |
| `request` | yes | yes | **yes**: at most 20 open requests per repo, 50 in total (`SND-REQ-002`) | yes |
| `pause` | yes | yes | **yes** | yes |
| `job` | yes | yes | no: file a `request` | yes |
| `decision` | yes | yes | **no** | no: it may only *raise* a decision |
| `desk` | yes | yes | **no** | only the engine commands its recipe step owns (the engine spec lists them) |
| `protected` | yes | **no**: shown as `cli-only` | **no** | no |

**Why `pause` is open to sessions.** Takeover is detected by the hook, which runs inside the agent session and is
therefore a `session` caller. Pausing an instance only stops automation, so it is safe in that direction; handing back
is `desk`. That's why a session may take over, but never hand back.

**`service` never arrives over HTTP.** The engine and the scheduler call handlers in-process with a `service:<name>`
caller context, so nothing on the wire can claim it.

**A refusal is a 403:** `SND-AUTH-002 <Command> is a human decision. Ask the human to run it in a terminal or the
Sindri app: sindri <verb> …`. Inside Claude Code, `!sindri …` runs with the agent's environment and is refused too.
Nothing is written except the audit row.

A question answered inside a session stays an `answer` turn, classified by the hook (§9.3). The API doesn't replace
that path.

## 5. The event stream

### 5.1 What it's for

It tells live clients "something changed, re-read":
- **The Avalonia app:** refreshes the affected view, and drives the needs-you pulse, the tray badge and OS
  notifications.
- **The CLI's long-running verbs:** job progress and cancel.
- **`sindri status --follow`**.
- **Later:** the notifier, the phone head, and other instances.

It is not:
- the source of truth (the projections are);
- the engine's or the projector's input (they read the ledger by cursor, in-process);
- a way to send anything to Sindri.

### 5.2 Transport and filters

The endpoint is `GET /events?after=<sequence>&item=<itemId>&types=<glob,glob>`, sent as Server-Sent Events
(`TypedResults.ServerSentEvents`, as in S1).
- On a reconnect, `Last-Event-ID` wins over `after` (conflict C4).
- A read error ends the stream with `event: error` and the SND code, instead of being swallowed.
- A `session` caller's stream is filtered to its repo (§3.1).
- Not WebSocket: the stream is one-way, SSE resumes natively, it works with `curl -N`, and the Avalonia client reads it
  with a streaming `HttpClient`.

### 5.3 The public envelope

```
id: 4211
event: item.state-changed
data: {"sequence":4211,"id":"0192…","type":"item.state-changed","schemaVersion":1,"occurredAt":"2026-10-10T14:02:11Z","actor":"human:cli","tenant":"local","ring":0,"item":"ITM-0192…","correlationId":"0192…","causationId":null,"traceId":"00-…","payload":{"commandId":"0192…","item":"ITM-0192…","from":"new","to":"done","version":3}}
```

- `PublicEvent` is S1's `LedgerEvent`, flattened:
  - `sequence` and `item` come from the ledger row;
  - the rest comes from the Kernel `EventEnvelope`;
  - `payload` is the event's public payload record from `EventCatalog`.
- Event types that aren't public are never sent.
- Payloads carry ids, states and short scrubbed titles. They never carry packet, request or session text.
- Clients re-query projections when an event arrives. The UI never folds events into state itself (§17.27.1).

### 5.4 Back-pressure and limits

- **No server-side buffer.** The server reads one page at a time with `ReadAfter(cursor, 100)` and writes it out. A
  slow reader only slows its own loop, because writes wait on TCP.
- **No retention, so no `reset`.** The ledger never deletes events, so any `after` can be served. An `after` greater
  than the head is a 400. A `reset` event is reserved for when retention lands.
- **Keep-alive:** a `: ping` comment every 15 s.
- **Stream limits:** at most 16 concurrent streams, with 4 reserved for `human` callers, so `session` streams are
  capped at 12. Past a limit the stream gets `SND-API-005` (429), and the client falls back to re-querying every 5 s.

### 5.5 Latency

- S1 polls every 1 s.
- P5 adds `ILedgerSignal`, a port in `Core`. Its in-memory adapter is pulsed by the SQLite ledger adapter after a
  committed `Append`.
- P5 also connects it: the projector and the stream both wait on it, so they wake within milliseconds.
- The 1 s poll stays as the fallback.

## 6. Auth, v0

v0 is local-only. The loopback binding and S1's `LoopbackGuard` are the only network controls. There are **no tokens,
passkeys or tailnet listener** (§11).

What v0 keeps is the caller class, so "session calls are requests, never decisions" holds as a guard against honest
mistakes. That's the repo's threat model: gates guard against honest mistakes, not a hostile agent.

**The headers.**

| Header | Values | Meaning |
|---|---|---|
| `X-Sindri-Caller` | `human`; `session` or **none**; any other value | `human` gives `human`. `session`, or a missing header, gives `session`, which is the safe default for an agent's `curl`, a script it wrote, or a fetch tool. Any other value is `SND-API-007` (400) |
| `X-Sindri-Terminal` | `1` | Sent only by the CLI when stdin and stdout are a terminal. It's honoured only alongside `human` |
| `X-Sindri-Client` | `cli`, `ui`, `prover` | Labels the actor (`human:cli`, `human:ui`, `human:prover`). With no label, a human caller is `human:http` |
| `X-Sindri-Repo` | a repo name | Scopes a `session` caller (§3.1) |

**Who sends what.**
- **The CLI** sends `human` with `X-Sindri-Terminal: 1` only when stdin and stdout are a terminal **and** none of these
  agent-session variables is set:
  - `CLAUDECODE` (Claude Code);
  - `AW_SINDRI_CHILD` (Sindri);
  - `CODEX_SANDBOX` (Codex);
  - `CURSOR_AGENT` (Cursor's agent).

  Otherwise it sends `session`. These are variables the tools set, not ones Sindri reads as config. They go in
  `scripts/env-scan-ignore.txt`, next to the existing `CLAUDECODE` and `AW_SINDRI_CHILD` lines, not in `.env.example`.
- **The Avalonia app** sends `human` with no terminal.
- **`sindri mcp`** sends `session`.

**Commands refuse any `Origin` header** with 403 `SND-API-008`.
- No first-party client sends one, so this stops a web page in a local browser from driving a command that has no body.
- A browser also can't send `X-Sindri-Caller` without a CORS preflight, which Sindri never answers, so its requests are
  `session` anyway.
- Queries keep S1's guard (conflict C3).
- Refusals with `SND-API-007` and `SND-API-008` are logged.

**Protected actions in the UI** show the CLI command to copy (`cli-only:typed-confirmation`), because the app has no
terminal until passkeys exist.

**Out of scope for v0:** a session that deliberately forges `human` (§11, tokens).

**When tokens arrive,** only the source of the caller class and the terminal flag changes. The authorization table, the
decorators and every test stay.

## 7. Finding or starting the process

S1's model stands:
- the server writes `control.json {url, pid, epoch}` (0600) once it listens;
- a client reads it, checks that `url` is a loopback address, and checks the pid and epoch against `GET /ping` with a
  2 s timeout, so a stale file reads as not running (`SND-API-001`);
- bare `sindri` spawns `sindri serve` detached and waits up to 10 s (`SND-CLI-002`).

This design adds which verbs start the process:

| Verb | When the process isn't running |
|---|---|
| `sindri`, `sindri --headless` | Start it (S1) |
| Commands and queries run by a human (`decide`, `item add`, `queue`, …) | Start it, then run. `--no-start` prints `SND-API-001` instead |
| **Any verb inside an agent session** (the §6 variables) | Never start it. Print `SND-API-001` ("Sindri isn't running; ask the human to run `sindri`"), as `sindri mcp` does |
| `status`, `stop` | Never start it. As in S1, `status` prints `not running` and exits 1, and `stop` exits 1 |
| `doctor`, `profile validate` | Run locally (S1) |
| `sindri mcp` | Never start it. Its tools return `SND-API-001` |

## 8. The MCP server

`sindri mcp` is a subcommand of the same NativeAOT `sindri` binary:
- it uses stdio and the official MCP C# SDK, which the readiness AOT spike approved;
- tools are registered explicitly, and stdout is reserved for the protocol, so logs go to stderr;
- it is a thin client of the HTTP API: every tool is exactly one API call, with `X-Sindri-Caller: session` and
  `X-Sindri-Repo` set from the session's working directory (its git top-level name);
- the server holds no logic, and errors come back as tool errors carrying the SND code and message;
- repo scope is enforced by the server (§3.1), so the tools have no "all repos" argument.

| Tool | Arguments | API call | Kind |
|---|---|---|---|
| `sindri_status` | — | `GET /status` | query |
| `sindri_queue` | — | `GET /queue` | query |
| `sindri_item` | `itemId` | `GET /items/{itemId}` | query |
| `sindri_item_timeline` | `itemId`, `after?` | `GET /items/{itemId}/timeline` | query |
| `sindri_pending_decisions` | `itemId?` | `GET /decisions?pending=true` | query |
| `sindri_job` | `jobId` | `GET /jobs/{jobId}` | query, **from P6** |
| `sindri_request` | `kind`, `itemId?`, `text` | `POST /requests` (`FileRequest`) | request |
| `sindri_context` | `query`, `itemId?` | `GET /context?q=` | query, **with S3** |

There is no decision, desk, protected or job tool, so the MCP surface can't even attempt one. Query tools carry the MCP
`readOnlyHint` annotation.

**Proof.** The proof run starts a fresh Claude Code process as the subagent:
```
claude -p --mcp-config <tmp>/sindri-mcp.json --strict-mcp-config \
  --allowedTools 'mcp__sindri__*' 'Bash(curl:*)' "<journey>"
```
- The config points at the phase's built binary and the phase's `AW_SINDRI_HOME`.
- The tools load when that process starts.
- Nothing is registered with the provider, so the cutover rule holds and there is nothing to clean up.
- The user's own hooks may load; the journey doesn't depend on them.

## 9. Errors, versioning and JSON

### Errors

S1's `HttpResults` mapping is unchanged: `ErrorKind` sets the status, and `ProblemDetails` carries `title` = the SND
code and `detail` = the message with its `(fix: …)`. The CLI prints `SND-… message` and exits 0 for ok, 1 for attention
and 2 for an error (S1).

The phase that reserves a code registers it in `SindriErrors` and its `All` list, and documents it in the error table
of **`docs/csharp/sindri.md`**. Not `docs/sindri/errors.md`, which is generated from the TS build.

| Code | Kind (status) | When | Phase |
|---|---|---|---|
| `SND-AUTH-002` | Forbidden (403) | The caller may not run the command's action class | P1 |
| `SND-API-007` | Invalid (400) | An unknown `X-Sindri-Caller` value | P1 |
| `SND-API-008` | Forbidden (403) | A command carried an `Origin` header | P1 |
| `SND-API-009` | Unavailable (503) | The command gate wasn't acquired within 5 s | P1 |
| `SND-API-004` | Unavailable (503) | The views didn't reach `X-Sindri-After`, or the head, within 2 s | P2 |
| `SND-VIEW-001` | Unavailable (503) | The projector stalled on an event (status, doctor, logs) | P2 |
| `SND-ITEM-404` | NotFound (404) | No such item, or it's outside a session's repo | P3 |
| `SND-ITEM-409` | Conflict (409) | The item changed since you looked, or the decision was decided elsewhere | P3, P4 |
| `SND-REQ-001` | Invalid (400) | An unknown request kind, or text that's empty or over 2,000 characters | P4 |
| `SND-REQ-002` | RateLimited (429) | Too many open requests (§4.2) | P4 |
| `SND-DEC-404` | NotFound (404) | No such decision | P4 |
| `SND-DEC-001` | Invalid (400) | A choice that isn't one of the decision's options | P4 |
| `SND-API-005` | RateLimited (429) | The stream limits (§5.4) | P5 |
| `SND-JOB-404` | NotFound (404) | No such job | P6 |
| `SND-SVC-404` | NotFound (404) | No such service in the approved profile | P6 |
| `SND-LAUNCH-001` | Unavailable (503) | The launcher couldn't open the terminal | P10 |

- `SND-AUTH-001` and `SND-API-006` are reserved for the token phase (§11).
- The engine spec reserves `SND-RECIPE-*` and `SND-WF-*`, and should not add its own authorization code (§3.5).

### Versioning

- There is no version in the path in v0. The CLI, `sindri mcp` and the UI are built from this repo and ship together.
- `GET /status` reports `apiVersion: 1`, and every response carries `Sindri-Api-Version: 1`. The UI checks it on
  connect and shows "Sindri and the app are different versions" on a mismatch.
- A breaking change later adds a `/sindri/v2` group and keeps `v1` for one release.
- Events carry their own per-type `schemaVersion`.

### JSON

- There is one context, S1's `SindriJsonContext`, used through `AwJson`.
- `--json` prints the API response body **exactly as received**. P1 changes S1's `ControlPlaneClient` to keep the raw
  body (conflict C5).

### Telemetry

Spans and metrics use the `Aw.Sindri` source and meter, as `docs/csharp/telemetry.md` describes.
- **Spans:** `sindri.command <name>`, tagged with class, caller and outcome; and `sindri.query <name>`.
- **Metrics:**
  - `sindri.commands`, a counter by class and outcome;
  - `sindri.command.gate_wait`, a histogram;
  - `sindri.projector.lag`, a gauge of head − cursor;
  - `sindri.streams`, a gauge by caller.

## 10. Conflicts with S1 (flagged, not redesigned)

| # | S1 today | What this design needs | Resolution |
|---|---|---|---|
| C1 | ~~`/sindri/sindri/status`~~ | — | Already fixed in S1 |
| C2 | No caller class: `ApproveProfile` trusts `confirmed` from any loopback caller | Only a `human` caller with a terminal reaches protected handlers | P1 adds the caller middleware and wraps S1's commands in the chain. Their handler bodies don't change; their registrations move to `AddCommand` |
| C3 | `LoopbackGuard` allows a browser `Origin` on loopback | Commands refuse any `Origin` | P1 adds an endpoint filter on commands. S1's guard stays for queries |
| C4 | `EventStream` lets `after` beat `Last-Event-ID`, swallows read errors, and sends every type | `Last-Event-ID` wins; errors end the stream with their code; public types only; session repo filter | P5 changes S1's `EventStream` and `StreamEvents`. The route stays |
| C5 | Command responses have no `sequence`, and `ControlPlaneClient` re-serializes `--json` | `sequence` for read-your-writes, and the raw body printed | P1 adds `sequence` to `StopProcessResponse` and `ApproveProfileResponse`, and keeps the raw body. Both are additive |
| C6 | The singleton `EventFactory` stamps `host:sindri`, uses a new correlation id per event, and leaves `Item = null` | The actor from the caller, one correlation per command, the item set | P1 adds `ICallerContextAccessor` (an `AsyncLocal` singleton) and `EventFactory` reads it. The same change covers the engine's S1-D7 |
| C7 | `EndpointTable` and `CommandTable`/`Verbs` have no anchors, and `/new-slice` edits shared files at its own anchors even with `--files-only` | Slices can be scaffolded in parallel without touching shared files | P1 adds a `--slice-only` mode to `scripts/scaffold/new-slice.mjs`, with a case in `scripts/tests/scaffold.test.sh`. It writes only the slice folder, the test, the builder and new ids, and edits no shared file. P1 also restructures both tables to one line per slice under an anchor comment |
| C8 | The scheduler only logs a failed run (`SND-SCHED-001`), and a changed profile is a file on disk with no event | `service.run-failed` and `profile.drifted` events for the views | <ul><li>P2 makes the scheduler append `service.run-failed` (keyed by service and slot) alongside its log line.</li><li>P4 adds a `profile-watch` scheduled service. It runs every 30 s, hashes the live profile, and appends `profile.drifted` when the hash differs from the approved one.</li></ul> |
| C9 | `ILedgerStore` has no way to read the head | The ledger head, for `asOf` and command waits | P2 adds `Head()` to `ILedgerStore` (additive, with a case in its contract suite) and `ILedgerReader` (the read-only port for `ReadLedger` and the projector) |

## 11. Later (designed in outline, not phased)

- **Auth:**
  - per-class bearer tokens minted on each `serve` start (`auth/human.token` and `auth/session.token`, 0600);
  - `SND-AUTH-001` for a missing or stale token;
  - an `aw:sindri-auth` guard hook;
  - containerized factory sessions get only the session token;
  - passkeys through `IPasskeys` for the UI's desk and protected actions (§17.27.2);
  - a client `Idempotency-Key` with a durable command journal (`SND-API-006` when a key is reused with a different
    body);
  - an `Origin` allowlist, if a browser head of the app is ever built.
- **Tailnet:** a Unix-socket listener that `tailscale serve` proxies to, with caller class `remote`. Tailscale identity
  headers are trusted only there (§17.22 D10). It is query-only until passkeys.
- **Webhooks:**
  - `POST /webhooks/{source}` → `ReceiveWebhook`, with class `trigger` and the delivery id as the fact key;
  - profile `triggers:` match deliveries to standing workflows;
  - per-source signature checks.
- **Dashboard collector queries** (dashboard spec §4), served by the collector service inside the process:
  - `GET /sessions` and `/sessions/{provider}/{id}` (+ `/timeline`), for **Agents**;
  - `GET /repos`, for **Repos**;
  - `GET /work`, for the rest of **Automatic work**;
  - `GET /today`, for **Today**;
  - `POST /sessions/{provider}/{id}/attach` (desk);
  - `POST /needs-you/{id}/ack` (desk);
  - needs-you sources for sessions, stale indexes, judge health and PRs.

  These need their own spec once the collector is designed in C#.
- **Receipts and metrics:** `GET /items/{itemId}/receipt`, `POST …/receipt/verify` (§17.23.12), and
  `GET /metrics/today`.
- **Context retrieval:** `GET /context?q=&item=` and `sindri_context`, with S3.
- **Inline projections:** a unit of work around `Append`, if read-your-writes waits ever matter in practice.
- **Retention:** if the ledger ever drops events, the stream's `reset` event (§5.4).

## 12. Test strategy, and how agents build and prove each phase

**Tests by layer.**

| Layer | Lane | What |
|---|---|---|
| Domain decisions (`Core`) | unit | <ul><li>Item transitions.</li><li>Request → decision derivation.</li><li>The authorization table: every class × caller × terminal combination.</li><li>`IClassifiedCommand` variants.</li><li>Fact-key composition.</li><li>Needs-you ordering and clearing rules.</li><li>`ItemActions.For(state)`.</li></ul> |
| Projections | unit | Each `IProjection` folded over event lists. Replaying the same events gives the same rows |
| Command chain | unit | <ul><li>The helper wraps every command in the §2.1 order.</li><li>A refused or invalid command writes only `command.handled`.</li><li>Losing a fact-key race gives `SND-ITEM-409`.</li><li>The gate times out with `SND-API-009`.</li><li>`StopProcess` and `RebuildViews` don't wait for the head.</li></ul> |
| Catalogs | unit | <ul><li>`ARCH-CQRS-003`.</li><li>Every route exists.</li><li>No stale `pending`.</li><li>`cli-only` only on terminal-bound rows.</li><li>Every event type is in `EventCatalog`.</li></ul> |
| Endpoints | integration | `WebApplicationFactory` over the real `Compose`, with S1's contract-tested in-memory fakes and `FakeTimeProvider`. For each endpoint: <ul><li>the happy path;</li><li>its error codes;</li><li>no header, or `session`, on every decision, desk and protected route → 403, and nothing written but the audit row;</li><li>`human` without a terminal on protected → 403;</li><li>an `Origin` on a command → 403;</li><li>a session repo scope test.</li></ul> `ARCH-CQRS-001` runs over the endpoint data source |
| Concurrency | integration | Two `Decide` calls on one decision, started together: exactly one `decision.recorded`, and the other gets `SND-ITEM-409` |
| Read-your-writes and freshness | integration | <ul><li>A command, then a query with `X-Sindri-After`, sees the write.</li><li>A stalled projector gives `SND-API-004` and `asOf.stalled`.</li><li>`views rebuild` still runs while the projector is stalled.</li></ul> |
| Projector rebuild | integration | Real SQLite in a temp dir: delete `views.db` and restart, and the same views come back (bodies equal, apart from `asOf`). During the rebuild, queries answer with `asOf.rebuilding` |
| Event stream | integration | <ul><li>Resume with `Last-Event-ID`.</li><li>The filters, including the session repo filter.</li><li>The stream limits.</li><li>Non-public types never appear.</li><li>An OSC or ANSI payload arrives escaped.</li></ul> |
| CLI | unit + integration | <ul><li>Verb routing.</li><li>The headers taken from the terminal and the environment.</li><li>The auto-start rules (§7).</li><li>`--json` equal to the API body.</li></ul> |
| Scaffold | bash | `scripts/tests/scaffold.test.sh` covers `--slice-only` (C7) |
| MCP server | **proof run** | A fresh `claude -p` subagent (§8, P8) |
| AOT | CI AOT job | S1's `aot-smoke`, extended in each phase with its new verbs |

**Scaffolding a slice into Sindri** (C7).
1. Run `node scripts/scaffold/new-slice.mjs --slice-only --package Sindri --slice <Name> --surface http …`. It writes
   the request and response records, the service stub, the validator, the red test, the builder and any new id. It
   touches no shared file.
2. The implementer adds the slice's lines at the anchors in its own branch:
   - one `EndpointTable` line;
   - one `Verbs`/`CommandTable` line, plus its `Usage` line;
   - the `AddCommand` or `AddQuery` registration;
   - the `SindriJsonContext` entries;
   - the catalog rows;
   - the error codes;
   - the `A.cs` builder method.
3. The CLI verb is written by hand in `Host/Commands`.

**Proving a phase.** Every phase signal below assumes this recipe. The recipe lives in the plan and in this spec. It is
not in `docs/csharp/sindri.md`, because it isn't a user feature.
- **Wrapper.** The prover uses `scripts/sindri-prove.sh`, added in P1. It refuses to run unless `AW_SINDRI_HOME` is
  under `$TMPDIR` or the phase's proof folder, so it can never touch the owner's real ledger. It sets:
  - `AW_SINDRI_PORT=0`;
  - `SINDRI="dotnet run --project src/Sindri/Aw.Sindri.Host --"`, or the binary published to the phase's temp dir.
    Never the `sindri` on `PATH`, which is the TS CLI.
- **Isolation.** Every agent gets its own temp home. The process picks a free port and records it in `control.json`,
  so parallel agents never share a ledger, a lease or a port.
- **Acting as the human.** Implementers run inside Claude Code, so the CLI correctly classes them as `session`. A human
  step is run in one of two ways:
  - `sindri-prove.sh human <verb>`, which runs the CLI under `script -q /dev/null` with the agent variables unset.
    `script` provides the terminal; strip the CRLF from its output before comparing.
  - `curl -H 'X-Sindri-Caller: human' -H 'X-Sindri-Terminal: 1' -H 'X-Sindri-Client: prover'
    -H 'Content-Type: application/json' …`.

  The ledger records these as `human:prover`, so the evidence shows that the prover stood in for the owner.
- **Seeding an approved profile.** Write the test profile, read its hash from `$SINDRI profile validate --json`, then
  run `printf '<first 6>\n' | sindri-prove.sh human profile approve <hash>`. This is the real terminal flow, so it works
  whichever confirmation S1 or the engine lands (C2).
- **Evidence.** Keep the commands, their output, and ledger excerpts. Before P5 ships `GET /ledger`, read them with
  `sqlite3 -readonly "$AW_SINDRI_HOME/ledger.db" 'select …'`. Store everything under the phase's proof folder.

**Docs per phase.**
- `docs/csharp/sindri.md` gains the phase's routes, verbs, events and codes.
- `docs/sindri/README.md` gets a pointer once v0 lands.
- `AGENTS.md`'s commands block changes only for a verb a human runs at the desk.
- Every phase runs the AGENTS.md merge gate.

## 13. Review resolutions (`/autoplan`, 2026-10-10; at most 3 passes)

The lens reviews are under `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-api-surface/`, with pass 1 in
`pass1/`, pass 2 in `pass2/`, and pass 3 at the top level.

### Pass 1 → rev 2

| Finding | Resolution |
|---|---|
| C-01 `SND-API-003` clash | New API codes start at 004 |
| C-02 a missing header counts as `human` | A missing header counts as `session` (§6) |
| C-03 a local browser page can drive commands | Commands refuse `Origin` (`SND-API-008`) |
| C-04 the audit is innermost | The audit wraps authorization and validation (§2.1) |
| C-05 the first-decision-wins and version race | Fact keys, the command gate, and waiting for the head |
| C-06 idempotent replay | No client key in v0; fact keys only (§11 keeps the journal) |
| C-07 no `POST /profile/validate` | `profile validate` and `doctor` are class `local` |
| C-08 park, takeover and handback have two owners | The engine owns them (§3.5) |
| C-09 needs-you is a queue field | It's now `GET /needs-you` (§3.6) |
| C-10 dashboard views aren't scoped | §3.6 says what v0 feeds, and §11 names the collector queries |
| C-11 no freshness signal | `asOf` (§2.2) |
| C-12 the wrong error doc | `docs/csharp/sindri.md` |
| C-13 `/new-slice` anchors | C7 (revised in rev 3) |
| C-14 agents can't act as the human | The prover recipe (§12) |
| C-15 shared home and port | Per-agent home and port |
| C-16 the MCP proof can't see its tools | A fresh `claude -p` |
| C-17 two events per command | One domain event per command |
| C-18 `EventFactory` | C6 (revised in rev 3) |
| C-19 the projector failing | `stalled` |
| C-20 missing dependencies | `EventCatalog` in P1, and the dependency fixes |
| C-21 the agent CLI auto-starts the process | Never inside an agent session |
| C-22 accepted requests do nothing before S2 | `recordedOnly` |
| C-23 building ahead | v0 = P1–P5 |
| C-24 design details | Adopted |
| C-25 cross-repo data | Repo scope (made server-side in rev 3) |
| C-26 `sindri open` injection | Ids only |
| C-27 stale S1 facts | Fixed |
| C-28 LOW items | Caps, stream reservation, the loopback URL check, the 429 fallback, `Last-Event-ID`, the 200-line view cap, audit per attempt |

### Pass 2 → rev 3

The pass-2 verdicts:

| Lens | Verdict |
|---|---|
| Product | PASS |
| Security | PASS |
| Architecture | NEEDS_WORK |
| Design | NEEDS_WORK |
| Devex | NEEDS_WORK |

Every HIGH and MEDIUM is fixed below.

| Finding (lens) | Resolution |
|---|---|
| The gate and the wait for the head have no limits, so the fix itself deadlocks (arch N1 HIGH, security N2) | The gate gets a 5 s limit (`SND-API-009`). Only commands that read views wait, for up to 2 s. `StopProcess` and `RebuildViews` never wait. Handlers never do slow work (§2.1) |
| Two authorization models across the specs (arch N2 HIGH) | One model, owned here: a terminal flag on `human`, per-variant classes through `IClassifiedCommand`, `protected` meaning human with a terminal, and `SND-AUTH-002`. The engine spec should drop `SND-WF-007` and `human-terminal` (§3.5, §4) |
| `--files-only` still edits shared files (devex H1) | C7 adds a real `--slice-only` mode with a scaffold test |
| P1's signal can't pass (devex H2) | The signal now uses the live hash, `Content-Type`, and `sqlite3 -readonly` evidence. The audit covers attempts that reach the chain, and binding failures are logged (§2.1) |
| Needs-you sources with no events (design N1 HIGH, arch N7) | C8: `service.run-failed` (P2) and `profile.drifted` (P4) |
| No ledger head (arch N8) | C9: `ILedgerStore.Head()` and `IProjectorStatus` |
| `stop` can't be audited; audit failures undefined (arch N3) | `StopProcess` is self-audited, and the audit failure rules are in §2.1 |
| `EventFactory` is a singleton (arch N4) | `ICallerContextAccessor`, shared with the engine's S1-D7 (C6) |
| The takeover route can't be reached (arch N5, security N3, product #1) | The new class `pause` is open to sessions; handback stays `desk` (§4) |
| Two answer paths and two needs-me lists (arch N6) | Engine parks are rows of kind `park` answered by `ApplyCommand`. There's one list, `GET /needs-you` (§3.4, §3.5) |
| v0 items can't close (product #2) | `CloseItem` in P3 |
| A request's needs-you row is unclear (product #3, design N3) | One `decision` row per request, carrying `recordedOnly` |
| No clearing rule (design N2) | A clearing rule per kind (§3.6) |
| `actions` can't name a variant (design N4) | Action ids `<Command>:<variant>`, with one catalog row each |
| Item state before S2 (design N5) | Defined in §3.6 |
| CLI text for rebuilding and stalled (design N6) | `EmptyText` has both, and every list view has `empty` |
| `asOf` has no clock (design N7) | `projectedAt`, plus the 30 s stream staleness rule |
| Urgency per kind (design N8) | The table in §3.6 |
| The byte-match is flaky (design N9, arch L2) | Signals compare bodies with `asOf` removed (`jq 'del(.asOf)'`) |
| Repo scope is MCP-only and can be bypassed (security R1) | Enforced on the server for `session` callers, with no `all` argument |
| The prover recipe could touch the real ledger (security N1) | `sindri-prove.sh` refuses the default home; the `human:prover` actor label; the recipe kept out of the user docs |
| Shared-file rules contradict each other (devex) | Implementers add their anchor lines in their own branch, and the integrator resolves adjacent-line conflicts (Parallel delivery) |
| Evidence before P5 (devex) | `sqlite3 -readonly` |
| Variable names (devex) | `CLAUDECODE`, `AW_SINDRI_CHILD`, `CODEX_SANDBOX` and `CURSOR_AGENT`, listed in `scripts/env-scan-ignore.txt`. `CURSOR_TRACE_ID` (set in Cursor's normal terminal) and `CODEX_SESSION_ID` (unverified) are dropped |
| `claude -p` tool permissions (devex) | `--allowedTools 'mcp__sindri__*' 'Bash(curl:*)'` |
| `reset` can't happen (arch L3) | Dropped until retention exists |
| LOW items | <ul><li>Authorization runs before validation, and every command has a validator.</li><li>`ReadLedger` is sanitized, capped at `max` ≤ 500, and uses a read-only port.</li><li>The request cap is per repo and overall.</li><li>`SND-API-007` and `SND-API-008` are logged.</li><li>The body limit is 64 KiB.</li><li>P10 takes the repo path from the profile's repo entry.</li><li>The version key is unified as `v<version>`.</li><li>The engine's "starts if needed" follows §7.</li><li>An `Origin` allowlist waits for a browser head (§11).</li></ul> |

## Phases

Each phase is one PR and follows the slice recipe, red first.
- **v0 is P1–P5.** After v0 the app has its queue, needs-you, decisions and live refresh.
- P6, P8 and P10 follow.
- Every phase depends on **S1 merged**, because conflicts C1–C9 assume S1's code is on the default branch. Until S1
  merges, phases stack on `feat/sindri-s1-app-shell`.
- Every signal uses §12's prover recipe.

| # | Goal | Files (main ones) | Size | Depends on | Observable signal | Unblocks |
|---|---|---|---|---|---|---|
| **P1** | **CQRS spine.** <ul><li>`--slice-only` and the anchors (C7).</li><li>`ICommand`, `IQuery`, `IClassifiedCommand`.</li><li>The callers: `CallerContext`, `ICallerContextAccessor`, the four headers (no header means `session`), the `Origin` refusal.</li><li>The chain: `CommandGate`, `AuditingService`, `AuthorizingService`, `ValidatingService`, and the `AddCommand`/`AddQuery` helpers.</li><li>`CommandAccepted`.</li><li>The catalogs: `ICommandCatalog`/`CommandCatalog`, `EventCatalog`, `ListCommands`.</li><li>`apiVersion`.</li><li>S1's commands get their classes; plus `sequence` and the raw `--json` (C5), and `EventFactory` (C6).</li><li>The CLI headers and the no-start rule for agent sessions.</li><li>`scripts/sindri-prove.sh`.</li><li>`ARCH-CQRS-001..003`.</li><li>Codes: `SND-AUTH-002`, `SND-API-007/008/009`.</li></ul> | `scripts/scaffold/new-slice.mjs`, `scripts/tests/scaffold.test.sh`, `scripts/sindri-prove.sh`, `Core/Commands/*`, `Core/Events/EventCatalog.cs`, `Features/Commands/*`, `Features/ListCommands/`, `Host/CallerContextMiddleware.cs`, `Host/CommandCatalog.cs`, `Host/Endpoints/EndpointTable.cs`, `Host/Commands/*`, `tests/Aw.Sindri.Tests/Commands/*`, `tests/Aw.Architecture.Tests/Cqrs*`, rule records, `docs/csharp/sindri.md`, `scripts/env-scan-ignore.txt` | M | S1 | <ul><li>With `H=$($SINDRI profile validate --json \| jq -r .hash)`, the call `curl -X POST -H 'Content-Type: application/json' …/sindri/profile/approve -d "{\"hash\":\"$H\",\"confirmed\":true}"` with **no caller header** gets 403 `SND-AUTH-002`. `sqlite3 -readonly ledger.db` shows a `command.handled` row with `refused` and no `profile.approved`.</li><li>The same call as `human` but with no `X-Sindri-Terminal` also gets 403.</li><li>Adding `-H 'Origin: http://localhost:3000'` gets `SND-API-008`.</li><li>`curl …/sindri/commands` lists every action id with its class.</li><li>In an agent session, `$SINDRI stop` gets `SND-AUTH-002`.</li></ul> | UI, engine |
| **P2** | **Projector, read-your-writes and the head.** <ul><li>`views.db`, `IProjection`, the projector, `IProjectorStatus`, `asOf`.</li><li>Rebuild and stall handling.</li><li>`X-Sindri-After`.</li><li>`ILedgerStore.Head()` and `ILedgerReader` (C9).</li><li>`service.run-failed` (C8).</li><li>The slices `RebuildViews`, `ListServices`, `GetProfile`.</li><li>doctor's `views` line.</li><li>Codes: `SND-API-004`, `SND-VIEW-001`.</li></ul> | `Core/Views/*`, `Infrastructure/Views/*` (+ migrations), `Infrastructure/Ledger/*` (Head, reader), `Host/Projector.cs`, `Features/{ListServices,GetProfile,RebuildViews}/` | M | P1 | <ul><li>Delete `views.db` and restart: `GET /services` matches the earlier body, apart from `asOf`.</li><li>During `views rebuild`, a query shows `"rebuilding":true`.</li><li>`X-Sindri-After: <head+1000>` gets 503 `SND-API-004` after 2 s.</li><li>A test profile with a service that always fails shows `lastRun.outcome` `failed`.</li></ul> | UI, engine |
| **P3** | **Items and the queue.** <ul><li>`ItemId` and the item events.</li><li>The `item_view` and `queue_view` projections.</li><li>`ItemActions` and `EmptyText`.</li><li>The slices `CreateItem`, `CloseItem`, `GetQueue`, `ListItems`, `GetItem`, `GetItemTimeline`.</li><li>The CLI verbs `queue`, `items`, `item add` and `item close`.</li></ul> | `Core/Items/*`, `Features/{CreateItem,CloseItem,GetQueue,ListItems,GetItem,GetItemTimeline}/`, projections, `Host/Commands/*` | M | P2 | <ul><li>`sindri-prove.sh human item add "Try the queue"`, then `curl -s -H 'X-Sindri-Caller: human' …/queue \| jq -S 'del(.asOf)'` equals `$SINDRI queue --json \| jq -S 'del(.asOf)'`.</li><li>`item close` with a stale version gets `SND-ITEM-409`.</li><li>An empty queue prints its `EmptyText`.</li><li>A `POST /items` with no header gets 403.</li></ul> | **UI** |
| **P4** | **Requests, decisions and needs-you.** <ul><li>`RequestId` and `DecisionId`.</li><li>The slices `FileRequest`, `ListRequests`, `ListDecisions`, `Decide`, `GetNeedsYou`.</li><li>The `profile-watch` service and `profile.drifted` (C8).</li><li>The CLI verbs `request`, `requests`, `decide` and `needs-you`.</li><li>Codes: `SND-REQ-*`, `SND-DEC-*`.</li></ul> | `Core/{Requests,Decisions,NeedsYou}/*`, `Features/{FileRequest,ListRequests,ListDecisions,Decide,GetNeedsYou}/`, `Infrastructure/ProfileWatch/*`, projections, `Host/Commands/*` | M | P3 | <ul><li>`curl -X POST -H 'Content-Type: application/json' -H 'X-Sindri-Repo: demo' …/requests -d '{"kind":"rescope","text":"…"}'` (no caller header, so `session`) shows up as exactly one `decision` row in `GET /needs-you`, with `recordedOnly: true`.</li><li>The same `decide` with no caller header gets 403.</li><li>`sindri-prove.sh human decide DEC-… accept` clears the row.</li><li>Two `decide` calls started together: one succeeds and the other gets `SND-ITEM-409 decided elsewhere`.</li><li>Editing the live profile adds a `profile` row within 30 s.</li></ul> | **UI, engine** |
| **P5** | **Event stream and ledger read.** <ul><li>Public types from `EventCatalog`, `PublicEvent`.</li><li>The `item` and `types` filters, the session repo filter, `Last-Event-ID`.</li><li>The ping and the stream limits (`SND-API-005`).</li><li>`ILedgerSignal`, wired to the projector and the stream.</li><li>`ReadLedger` and `sindri ledger`.</li><li>`sindri status --follow` (C4).</li></ul> | `Core/Events/*`, `Infrastructure/Ledger/LedgerSignal.cs`, `Features/{StreamEvents,ReadLedger}/`, `Host/Endpoints/EventStream.cs`, `Host/Projector.cs` | S | P1, plus P2 for the projector wiring (its signal uses `service.heartbeat`; the item filter is tested once P3 lands) | <ul><li>`curl -N '…/sindri/events?after=0&types=service.*'` shows each `service.heartbeat` within 100 ms of its append.</li><li>Reconnecting with `Last-Event-ID: n` resumes at n+1.</li><li>`command.handled` never appears.</li><li>`sindri-prove.sh human ledger --json --after 0` lists it.</li></ul> | **UI, engine** (S1-D6) |
| **P6** | **Jobs and services.** <ul><li>`JobId` and the job events.</li><li>The slices `GetJob`, `CancelJob`, `RunService` (as a job), `PauseService`, `ResumeService`; the scheduler honours pause and resume.</li><li>The CLI follows progress on the stream, and Ctrl-C cancels.</li><li>Codes: `SND-JOB-404`, `SND-SVC-404`.</li></ul> | `Core/Jobs/*`, `Features/{GetJob,CancelJob,RunService,PauseService,ResumeService}/`, a scheduler hook in Host | M | P2 + P5 | <ul><li>`sindri-prove.sh human services run heartbeat` prints progress and the result.</li><li>Ctrl-C during a long fake job prints `cancelled`, and the ledger shows `job.cancelled`.</li><li>After `services pause heartbeat`, there's no heartbeat for 3 cadences.</li><li>A run with no caller header gets 403.</li></ul> | UI, **engine** |
| **P8** | **`sindri mcp`.** <ul><li>The MCP server: stdio, the MCP C# SDK, and the §8 tools.</li><li>`X-Sindri-Repo` set from the working directory.</li><li>`SND-API-001` when the process isn't running.</li></ul> | `Host/Mcp/*`, the SDK in `Directory.Packages.props` (pinned, at least 14 days old), `deps.yaml` | M | P4 + P6 | A fresh `claude -p --mcp-config … --strict-mcp-config --allowedTools …` subagent: <ul><li>lists the queue and an item;</li><li>files a `rescope` request, which shows up in `GET /needs-you`;</li><li>finds no tool to decide with;</li><li>gets `SND-AUTH-002` from a `curl` with no caller header.</li></ul> Its transcript is the evidence | — |
| **P10** | **`sindri open`.** <ul><li>`ILauncher`, a Warp launch configuration as in the dashboard plan's decision 15.</li><li>`OpenItem` passes only the item id and the repo path from the profile, and the session pulls its context through `sindri mcp`.</li><li>`SND-LAUNCH-001`.</li></ul> | `Core/Launch/ILauncher.cs`, `Infrastructure/Launch/WarpLauncher.cs`, `Features/OpenItem/` | M | P3 + P8 (the engine marks the takeover once S2 runs) | <ul><li>`sindri-prove.sh human open ITM-…` opens a Warp tab running Claude Code in the item's repo.</li><li>The session's first `sindri_item` call returns the item.</li><li>The launch config contains no item text.</li></ul> | **engine** (`ILauncher`) |

Rev 1's P7 and P9 are gone; the engine spec owns them (§3.5).

**What each consumer needs:**
- **The UI's v0 views** (§3.6) need P1–P5.
- **The engine** needs:
  - P1: the chain, the callers and the catalogs;
  - P2: projections and the head;
  - P4: decisions and needs-you;
  - P5: the event filter;
  - P6: jobs;
  - P10: `ILauncher`.

## Parallel delivery with an agent team

An **agent team orchestrated with `/specToProvenPR`** builds the phases. The lead session only orchestrates, and keeps
every agent working. `/specToProvenPR`'s plan for this spec follows these rules.

### Dependency graph and waves

```
S1 ──► P1 ──┬──► P2 ──► P3 ──► P4 ──┬──► P8 ──► P10
            │     │                  │     ▲
            └──► P5 (projector wiring after P2)
                  │                  └──► P6 ┘   (P6 needs P2 + P5; P8 needs P4 + P6)
```

| Wave | Runs at once | Stacked on | Notes |
|---|---|---|---|
| W1 | **P1**, then **P2** ∥ **P5** once P1's contract unit is committed | P1 on S1; P2 and P5 on P1 | P5 wires `ILedgerSignal` into the projector after P2's projector lands, by rebasing onto P2 |
| W2 | **P3** ∥ **P6** | P3 on P2; P6 on P2 + P5 | |
| W3 | **P4** | P3 | Other agents review W2 or write P8's contract unit |
| W4 | **P8**, then **P10** | P8 on P4 + P6; P10 on P8 | |

- Each phase is one PR, based on its parent phase's branch (stacked, §17.10).
- A phase is **ready** for review once its parent is ready, not merged.
- Merges go in graph order, and each child rebases onto its merged parent.

### Units inside a phase

As in the engine spec, each phase starts with a **contract unit**: the records, ports, catalog rows and red stubs that
everything else compiles against. The contract unit runs alone. Then one implementer per slice fans out, each slice in
a folder of its own. P1's contract unit also holds `--slice-only` and the anchor restructuring (C7).

### The team

| Role | Who | Does | Never does |
|---|---|---|---|
| **Lead (orchestrator)** | The session running `/specToProvenPR` on this spec | Keeps the wave board, dispatches the next ready unit the moment an implementer frees up, rebases stacked branches, runs the merge order, and relays owner questions | Write phase code |
| **Implementer** (one per ready unit) | A subagent in its own worktree (`isolation: worktree`) | Runs the slice recipe red to green, adding its own lines at the shared anchors in its branch (§12), with its own temp home through `sindri-prove.sh` | Edit another unit's slice folder, or a shared line that isn't its own |
| **Integrator** (one per phase) | A subagent started when the phase's last unit lands | Merges the unit branches, resolves the adjacent-line conflicts at the anchors, regenerates `PublicAPI.Unshipped.txt` from `RS0016`, runs the full merge gate, and opens the draft PR | — |
| **Prover** (one per phase PR) | A subagent following §12's recipe; for P8, a fresh `claude -p` with the MCP config | Runs the phase's signal in the running app and records the evidence | Treat green tests as proof |
| **Reviewers** | A fresh subagent per round (`/review`) | Review loops until there are no blocking findings and no issues. Fixes go back to implementers as new units | Fix code itself |

**Throughput:**
- At most **3 implementers** and **2 builds** at once, because the heavy-job lock has two slots.
- An agent waiting on a review round is handed the next ready unit, or the next phase's contract unit.
- A unit whose parent changes rebases at its next green point, not mid-slice.

### Shared files

**What's shared.** These files take one line or row per slice, command, event or code. Implementers add only their own
lines, at the anchor, in their own branch. When unit branches merge, the integrator resolves the adjacent-line
conflicts.
- `Features/SindriFeaturesServiceCollectionExtensions.cs`;
- `Host/Endpoints/EndpointTable.cs`;
- `Host/Commands/{CommandTable,Verbs,Usage}.cs`;
- `Host/SindriJsonContext.cs`;
- `Host/CommandCatalog.cs`;
- `Core/Events/EventCatalog.cs`;
- `Core/SindriErrors.cs`, with its `All` list and the error table in `docs/csharp/sindri.md`;
- `tests/Aw.Sindri.Testing/A.cs`;
- the AOT smoke's `expected.txt`.

**Reserved so phases never collide.**
- Error codes are reserved per phase in §9.
- `views.db` migration numbers are reserved per phase: P2 `V0xx`, P3 `V1xx`, P4 `V2xx`, P6 `V3xx`.

**`PublicAPI.Unshipped.txt`** is regenerated from `RS0016` by the integrator, never by hand.

Every other file belongs to exactly one unit.

### Done

A phase is done when its PR is ready; no agent ever merges one. Ready means:
- the signal has been shown in the running app, with evidence;
- the merge gate is green;
- the review loop has reached zero blocking findings and zero issues, with every suggestion answered in writing.

The lead reports each phase as it becomes ready, and stops for the owner's merge.
