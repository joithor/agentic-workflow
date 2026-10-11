# Sindri API surface: commands, queries, the event stream and the MCP server

Status: approved in conversation, decision by decision, 2026-10-10. This document is the written spec for review.
It designs the surface that every Sindri client uses: the `sindri` CLI, the Avalonia app (`SindriUi`), the Sindri MCP
server (`sindri mcp`) and, later, the workflow engine's own calls into the command layer. `/specToProvenPR` takes the
**Phases** at the end to PRs; the UI design builds on §3 and §5.

**Builds on:** factory addendum (`2026-10-09-sindri-factory-addendum.md`) §17.27.1 (one command layer), §17.27.2 (action
classes), §17.27.4 (command registry), §17.21 (event envelope, API), §17.29 (the application, `sindri open`, MCP calls
are requests), §17.12 and §17.23.7 (decisions); core design (`2026-10-07-sindri-design.md`) §9.3 (takeover and
handback) and §10.3 (CLI); the dashboard spec (`2026-10-09-sindri-dashboard-design.md`), which says what the UI shows;
`.agents/rules/csharp.md` and `docs/csharp/scaffolds/new-slice.md`.

**Fixed, from stage S1 (the app shell, branch `feat/sindri-s1-app-shell`):** the GetStatus endpoint, `LoopbackBinding`
and `LoopbackGuard`, the `Core` types under `Control`, `Ledger`, `Lease`, `Profile`, `Scheduling` and `Doctor`, the
error codes in `SindriErrors`, `HttpResults`, and S1's process model (`sindri serve`, `control.json`, the spawn and
epoch check). This design adds to them. Where it touches S1, §10 lists the conflict instead of redesigning S1's part.

## 1. Decisions

| # | Question | Decision |
|---|---|---|
| D1 | Resource model | **CQRS** over resource paths. Commands are `POST`s that append ledger events and return `CommandAccepted`; queries are `GET`s that read projections. One route = one slice = one `IService` (§2, §3) |
| D2 | Commands vs queries, decisions vs requests | Every command declares an **action class**; every caller has a **caller class**; one table authorizes the pair. Sessions may run queries and file **requests**, never decisions (§4) |
| D3 | Event stream | **Server-Sent Events**, `GET /events?after=`, cursor = ledger sequence, public envelope, `reset` on overflow. A "go re-read" signal for live clients, never the source of truth (§5) |
| D4 | Local auth | **v0 is local-only with no tokens.** The caller class comes from an `X-Sindri-Caller` header that the MCP server and an agent-session CLI set. Tokens, passkeys, the tailnet and a guard hook come later (§6) |
| D5 | Finding or starting the process | S1's `control.json` + spawn + epoch check, plus one rule for which verbs start the process (§7) |
| D6 | MCP server | `sindri mcp` (stdio, MCP C# SDK), a thin client with session caller class: queries and `sindri_request` only. **Proven with a real Claude Code subagent** (§8) |
| D7 | Webhooks | **Cut for now** (§11 Later) |
| D8 | Errors, versioning, JSON | S1's `Result` → `ProblemDetails` mapping unchanged; new codes per phase; no path version in v0 (`apiVersion` in status and a header); one JSON context; `--json` prints the API body verbatim (§9) |

## 2. The CQRS model

### 2.1 Write side: commands

- A command is a request record implementing the marker `ICommand<TResult>` (in `Aw.Sindri.Core.Commands`), where
  `TResult` is `CommandAccepted` or a record that extends its fields. It is served by an
  `IService<TCommand, TResult>`, as every slice is (`.agents/rules/csharp.md`, request flow).
- The handler loads only the state it needs to decide (a projection row, or the ledger's latest event of a type), makes
  the decision with a pure method on a `Core` type, and appends one or more ledger events under the process's lease
  grant (`ILedgerStore.Append`, S1). It never returns a view.
- `CommandAccepted(Guid CommandId, long Sequence, bool AlreadyRecorded)`: `Sequence` is the last ledger sequence the
  command wrote (or the earlier one on a replay), the read-your-writes token of §2.4.
- **The command chain** is a set of decorators registered in `AddSindriFeatures()`, outermost first:
  1. `ValidatingService` (Kernel): the slice's `IValidator`.
  2. `AuthorizingService`: the command's action class against the caller class (§4). Refused: `SND-AUTH-002`, nothing
     written.
  3. `IdempotentService`: the client's `Idempotency-Key` header (a ULID the CLI and UI generate per invocation; absent
     means the handler's own natural key) becomes part of every ledger key the command writes. The same key with a
     different request body: `SND-API-005`.
  4. `AuditingService`: appends `command.handled {command, class, caller, outcome, code?}` for every command, accepted
     or refused, under the same correlation id.
- The caller context (`CallerContext(CallerClass Class, string Actor)`) is an ambient scoped service the Host fills
  from the request (§6) and the in-process callers fill directly. `Actor` is composed by the host
  (`human:cli`, `human:ui`, `session:mcp`, `session:cli`, `service:<name>`), never taken from the caller (rule: "actor is
  set by the host process").

### 2.2 Read side: queries and projections

- A query is a request record implementing `IQuery<TView>`, served by `IService<TQuery, TView>`. Its handler reads
  **projections only**, through read ports (`IItemViews`, `IQueueView`, `IDecisionViews`, …) whose adapters open a
  read-only connection (`SqliteConnectionFactory.OpenReadOnly`). It takes no write port and appends nothing.
- Projections live in their own SQLite file, `$AW_SINDRI_HOME/views.db` (0600), separate from `ledger.db`, so they
  can be deleted and rebuilt without touching the record.
- Each view is a table with the columns the UI and CLI need, keyed by its id, plus a `projector_cursor` row.

### 2.3 The projector

- An in-process background service (a hosted service in the Host, not a scheduled `IScheduledService`) that loops:
  `ReadAfter(cursor, 100)` → for each event, every registered `IProjection` whose types match applies it → store the
  new cursor, all in one `views.db` transaction per page. It sleeps on the ledger signal (§5.5) or 1 s.
- Projections are pure folds: `Apply(view row?, event) → view row?`, unit-tested from event lists.
- **Rebuild:** `sindri views rebuild` (desk class) stops the projector, deletes the view tables, sets the cursor to 0
  and replays. The process also rebuilds on start when `views.db` is missing or its schema version differs from the
  build's. A new or changed read model is therefore a replay, never a data migration.
- Why asynchronous: S1's `ILedgerStore.Append` owns its transaction, so a projection can't join it without a change to
  S1's port. Inline projection (strong consistency) stays possible later through a unit of work around `Append`
  (§11 Later); in a single process the lag is milliseconds.

### 2.4 Read-your-writes

- Every query accepts `X-Sindri-After: <sequence>`. The handler waits (up to 2 s, on the projector's progress signal)
  until the projector's cursor is at or past it, then reads. Past 2 s: `SND-API-003` (503), with the cursor and the
  target in the message.
- The CLI sends the `Sequence` of its own previous command when one verb chains a command and a query (for example
  `sindri decide` then printing the item); the UI sends the last `Sequence` it received.

### 2.5 Architecture tests (added to `tests/Aw.Architecture.Tests`)

| Id | Rule |
|---|---|
| `ARCH-CQRS-001` | Every endpoint mapped with `MapGet` resolves an `IService<TQuery, TView>` whose `TQuery : IQuery<TView>`; every `MapPost` an `IService<TCommand, _>` whose `TCommand : ICommand<_>` (checked over the endpoint data source in an integration test, since minimal-API lambdas aren't visible to ArchUnitNET) |
| `ARCH-CQRS-002` | A query handler's constructor takes no `ILedgerStore`, `ILease`, `IApprovals` or projection writer |
| `ARCH-CQRS-003` | Every `ICommand` type has a row in `CommandRegistry` (§3.3) |

Each gets a rule record under `rules/engineering/csharp/` in the phase that adds it (`node scripts/check-rules.mjs`).

## 3. Resources, endpoints and commands

All routes are under S1's group `/sindri` (shown without it). Request and response records live in
`Aw.Sindri.Features.<Slice>`; every one is registered in `SindriJsonContext`. **Class** is the action class (§4);
**Slice** is the `/new-slice` name (`--surface http --route "<route>"`), and the phase is the PR that adds it.

### 3.1 Queries

| Route | Slice | Request | Response | Phase |
|---|---|---|---|---|
| `GET /status` | `GetStatus` (S1) | — | S1's `GetStatusResponse`, plus `apiVersion` | S1, P1 |
| `GET /commands` | `ListCommands` | — | `CommandList(IReadOnlyList<CommandInfo>)`: name, class, allowed callers, method + route, CLI form, UI status (`ready` \| `pending:<phase>`) | P1 |
| `GET /services` | `ListServices` | — | `ServiceList(IReadOnlyList<ServiceView>)`: name, cadence, paused, last run (slot, outcome, code), next slot | P2 |
| `GET /profile` | `GetProfile` | — | `ProfileView`: approved hash and time, live hash, changed since approval, the changed key paths | P2 |
| `GET /queue` | `GetQueue` | — | `QueueView`: groups `waitingOnYou`, `withYou`, `parked`, `running`, `queued` (each `IReadOnlyList<ItemSummary>`), and `needsYou` (`IReadOnlyList<NeedsYouRow>`: kind, item, title, age, the CLI command) | P3 |
| `GET /items?state=&repo=` | `ListItems` | `ListItemsQuery(ItemState?, RepoName?)` | `ItemList(IReadOnlyList<ItemSummary>)`: id, title, repo, state, step, age, next action | P3 |
| `GET /items/{itemId}` | `GetItem` | `GetItemQuery(ItemId)` | `ItemView`: summary plus version, created by, waiting on (decision id, request id), taken over by, timestamps | P3 |
| `GET /items/{itemId}/timeline?after=` | `GetItemTimeline` | `GetItemTimelineQuery(ItemId, long?)` | `Timeline(IReadOnlyList<TimelineEntry>, long Next)`: sequence, time, type, actor, one scrubbed line | P3 |
| `GET /requests?state=` | `ListRequests` | `ListRequestsQuery(RequestState?)` | `RequestList(IReadOnlyList<RequestView>)`: id, kind, item, text (scrubbed, capped), actor, state, decision id | P4 |
| `GET /decisions?pending=&item=` | `ListDecisions` | `ListDecisionsQuery(bool?, ItemId?)` | `DecisionList(IReadOnlyList<DecisionView>)`: id, kind, class, item, item version, question, options, recommended option, state, decided by, CLI command | P4 |
| `GET /events?after=&item=&types=` | `StreamEvents` (S1, extended) | `StreamEventsQuery(long, ItemId?, TypeFilter?)` | SSE stream of `PublicEvent` (§5) | S1, P5 |
| `GET /jobs/{jobId}` | `GetJob` | `GetJobQuery(JobId)` | `JobView`: id, kind, item?, state (`running` \| `succeeded` \| `failed` \| `cancelled`), started, ended, progress lines, result summary, code | P6 |
| `GET /items/{itemId}/instances` | `ListItemInstances` | `ListItemInstancesQuery(ItemId)` | `InstanceList`: recipe, state, step, attempt, timers | P9 (with S2) |

### 3.2 Commands

| Route | Slice | Request (body fields beyond the route) | Response | Class | CLI | Phase |
|---|---|---|---|---|---|---|
| `POST /stop` | `StopProcess` (S1) | — | `CommandAccepted` | desk | `sindri stop` | S1; class in P1 |
| `POST /profile/validate` | `ValidateProfile` (S1) | — | S1's response | query-like command, class `job` | `sindri profile validate` | S1; class in P1 |
| `POST /profile/approve` | `ApproveProfile` (S1) | `hash?`, `confirmHash` | S1's response | **protected** | `sindri profile approve [<hash>]` | S1; class in P1 |
| `POST /views/rebuild` | `RebuildViews` | — | `CommandAccepted` | desk | `sindri views rebuild` | P2 |
| `POST /items` | `CreateItem` | `title`, `repo?`, `body?` | `ItemCreated(CommandAccepted, ItemId)` | desk | `sindri item add "<title>" [--repo R]` | P3 |
| `POST /requests` | `FileRequest` | `kind` (`rescope` \| `add-task` \| `note` \| `handback`), `itemId?`, `text` | `RequestFiled(CommandAccepted, RequestId, DecisionId)` | **request** | `sindri request <kind> [--item X] "<text>"` | P4 |
| `POST /decisions/{decisionId}` | `Decide` | `choice`, `itemVersion`, `note?` | `CommandAccepted` | decision | `sindri decide <decisionId> <choice> [--note]` | P4 |
| `POST /jobs/{jobId}/cancel` | `CancelJob` | — | `CommandAccepted` | job | Ctrl-C on a following CLI; `sindri jobs cancel <id>` | P6 |
| `POST /services/{name}/run` | `RunService` | — | `JobStarted(CommandAccepted, JobId)` | job | `sindri services run <name>` | P6 |
| `POST /services/{name}/pause` | `PauseService` | — | `CommandAccepted` | desk | `sindri services pause <name>` | P6 |
| `POST /services/{name}/resume` | `ResumeService` | — | `CommandAccepted` | desk | `sindri services resume <name>` | P6 |
| `POST /items/{itemId}/hold` | `HoldItem` | `itemVersion` | `CommandAccepted` | desk | `sindri hold <item>` | P7 |
| `POST /items/{itemId}/park` | `ParkItem` | `itemVersion`, `reason?` | `CommandAccepted` | desk | `sindri park <item>` | P7 |
| `POST /items/{itemId}/unpark` | `UnparkItem` | `itemVersion`, `hint?` | `CommandAccepted` | desk | `sindri unpark <item> [--hint]` | P7 |
| `POST /items/{itemId}/handback` | `HandBack` | — | `CommandAccepted` | desk | `sindri handback <item>` | P7 |
| `POST /items/{itemId}/start` | `StartItem` | `itemVersion` | `CommandAccepted` | desk | `sindri start <item>` | P9 (with S2) |
| `POST /instances/{instanceId}/cancel` | `CancelInstance` | — | `CommandAccepted` | desk | `sindri cancel <instance>` | P9 (with S2) |
| `POST /items/{itemId}/open` | `OpenItem` | — | `ItemOpened(CommandAccepted, string LaunchedIn)` | desk | `sindri open <item>` | P10 |

Notes:
- `ValidateProfile` is a `POST` in S1 because it reads the live files. It writes nothing; P1 classes it `job` (a
  read-only job, §17.27.2) so `ARCH-CQRS-001` holds without changing S1's route.
- `itemVersion` is optimistic concurrency (§17.21): a command against a changed item is `SND-ITEM-409` ("ENG-142
  changed since you looked; re-read it"). Two decisions on one decision id: the first wins, the second gets
  `SND-ITEM-409` with "decided elsewhere".
- Ids added by these phases: `ItemId` (string, `ITM-` + ULID or a tracker key), `RequestId`, `DecisionId`, `JobId`
  (string, prefixed ULIDs), `InstanceId` (with S2), `RepoName` (string). Each follows `docs/csharp/typed-ids.md` and
  gets its RDG route-binding test from `/new-slice`.

### 3.3 The command registry

`CommandRegistry` (in `Aw.Sindri.Features`, a static table, not reflection) has one row per command: type, action
class, allowed caller classes, method and route, CLI form, UI status. `GET /commands` serves it; the UI renders the
actions it offers from it (§17.27.4); `doctor` lists the `pending` UI actions. A unit test fails when a command has no
row, a row's route isn't in the endpoint table, or a `pending:<phase>` names a phase that has shipped (`ARCH-CQRS-003`).

### 3.4 Events these phases add

Every event is a `LedgerEntry` (S1) with the Kernel `EventEnvelope`. The idempotency key is the command's key plus the
event type, so a replay writes nothing.

| Type | Payload | Public | Phase |
|---|---|---|---|
| `command.handled` | command, class, caller, outcome (`accepted` \| `refused`), code | no | P1 |
| `item.created` | item, title, repo | yes | P3 |
| `item.state-changed` | item, from, to, version, reason? | yes | P3, P7 |
| `item.taken-over` / `item.handed-back` | item, by | yes | P7 |
| `request.filed` | request, kind, item?, text (scrubbed, ≤ 500 chars) | yes (text omitted) | P4 |
| `decision.requested` | decision, kind, class, item?, question, options, recommended | yes | P4 |
| `decision.recorded` | decision, choice, note?, by | yes | P4 |
| `request.accepted` / `request.declined` | request, decision | yes | P4 |
| `job.started` / `job.progress` / `job.finished` / `job.cancelled` | job, kind, item?; progress: round, line; finished: outcome, summary, code? | yes | P6 |
| `service.paused` / `service.resumed` | service | yes | P6 |
| `views.rebuilt` | cursor before, events replayed | no | P2 |

**Requests become decisions.** `FileRequest` appends `request.filed` and then `decision.requested` (kind
`accept-request`, options `accept` / `decline`, class `decision`). The human's `Decide` appends `decision.recorded` and
`request.accepted` or `request.declined`. Before the engine (S2) exists, an accepted request is a recorded fact that the
engine acts on once it does; this gives the decision path a real end-to-end run in P4. The engine later emits
`decision.requested` for its own gates through the same command (`service` caller) and reads `decision.recorded`.

## 4. Commands vs queries, decisions vs requests

### 4.1 Action classes (on every command)

| Class | Meaning | Commands in this design |
|---|---|---|
| `request` | A fact filed for a human or the engine to act on; changes no item state | `FileRequest` |
| `job` | Read-only work (may run long); writes only its own job events | `ValidateProfile`, `RunService`, `CancelJob` |
| `decision` | A non-protected human decision (§17.12 list) | `Decide` |
| `desk` | A human action that changes item or process state (§17.27.2) | `StopProcess`, `RebuildViews`, `CreateItem`, `HoldItem`, `ParkItem`, `UnparkItem`, `HandBack`, `StartItem`, `CancelInstance`, `OpenItem`, `PauseService`, `ResumeService` |
| `protected` | Approval tier: content-bound typed confirmation | `ApproveProfile` (later: evolve adopt, channel promote, tier raises) |

### 4.2 Caller classes and the authorization table

| Class \ caller | `human` | `session` | `service` (in-process only) |
|---|---|---|---|
| any query | yes | yes | yes |
| `request` | yes | **yes** | yes |
| `job` | yes | no (file a `request`) | yes |
| `decision` | yes | **no** | no (it can *request* a decision, never make one) |
| `desk` | yes | **no** | only the commands its recipe step owns (S2 decides that list) |
| `protected` | yes, with `confirmHash` equal to the content hash | **no** | no |

- `service` never arrives over HTTP: the engine and scheduler call handlers in-process with a `service:<name>`
  caller context, so the same table governs them, and nothing an agent sends can claim it.
- Refusal: 403, `SND-AUTH-002 <Command> is a human decision; ask the human to run: sindri <verb> …`. Nothing is written
  except the `command.handled` audit row.
- A question answered **inside** a session stays an `answer` turn classified by the hook (core design §9.3); the API
  doesn't replace that path.

## 5. The event stream

### 5.1 What it is for

A "something changed, re-read" signal for live clients: the Avalonia app (refresh the affected view, the needs-you
pulse, the tray badge, OS notifications), the CLI's long-running verbs (job progress and cancel), `sindri status
--follow`, and later the notifier, the phone head and other instances. It is not the source of truth (projections
are), not the engine's or projector's input (they read the ledger by cursor in-process), and not a way to send
anything to Sindri.

### 5.2 Transport and filters

`GET /events?after=<sequence>&item=<itemId>&types=<glob,glob>` as Server-Sent Events (`TypedResults.ServerSentEvents`,
as in S1). `Last-Event-ID` overrides `after` on reconnect. Not WebSocket: the stream is one-way, SSE resumes natively,
works with `curl -N`, and the Avalonia client reads it with a streaming `HttpClient`.

### 5.3 The public envelope

```
id: 4211
event: item.taken-over
data: {"sequence":4211,"id":"0192…","type":"item.taken-over","schemaVersion":1,"occurredAt":"2026-10-10T14:02:11Z",
data:  "actor":"human:cli","tenant":"local","ring":0,"item":"ITM-01J…","correlationId":"0192…",
data:  "causationId":null,"traceId":"00-…","payload":{"item":"ITM-01J…","by":"human:cli"}}
```

`PublicEvent` is the S1 `LedgerEvent` flattened: `sequence` and `item` from the ledger row, the rest from the Kernel
`EventEnvelope`, and `payload` as the event's public payload record. Each event type registers whether it is public and
its public payload type (§3.4); non-public types are never sent. Payloads carry ids, states and short scrubbed titles,
never packet, request or session text. Clients re-query projections on an event; the UI never folds events into
state (§17.27.1).

### 5.4 Back-pressure and limits

- The server pages `ReadAfter(cursor, 100)` into a bounded channel per connection (1,000 events).
- A client that falls behind the bound, or asks for `after` older than the ledger keeps, gets
  `event: reset` / `data: {"head": <sequence>}` and the stream closes. The client re-runs its queries and reconnects
  from `head`. Nothing is dropped silently mid-stream.
- `: ping` every 15 s. At most 16 concurrent streams; the 17th gets `SND-API-004` (429).

### 5.5 Latency

S1 polls every 1 s. P5 adds `ILedgerSignal` (port in `Core`, an in-memory adapter that the SQLite ledger adapter
pulses after a committed `Append`): the stream and the projector wake within milliseconds, and the 1 s poll stays as
the fallback. S1's `ILedgerStore` doesn't change.

## 6. Auth, v0

v0 is local-only: the loopback binding and S1's `LoopbackGuard` are the only network controls. **No tokens, passkeys or
tailnet listener in v0** (§11 Later). What v0 keeps is the caller class, so "session calls are requests, never
decisions" holds as an honest-mistake guard (the repo's threat model: gates guard honest mistakes, not a hostile agent):

- The Host reads `X-Sindri-Caller`: `session` → caller class `session`; absent → `human`. Any other value: 400.
- `sindri mcp` always sends `session`.
- The CLI sends `session` when it runs inside an agent session: `CLAUDECODE`, `AW_SINDRI_CHILD`, or the Codex or Cursor
  session variables are set. So an agent that runs `sindri decide …` gets `SND-AUTH-002` and the command to hand to the
  human.
- The UI sends no header (human), so its query, request, decision and desk actions run. Its protected actions show the
  CLI command to copy, because the typed confirmation needs a terminal until passkeys exist (Plan 6's rule).
- Protected commands keep S1's flow: the CLI prompts at a TTY for the first characters of the hash and sends
  `confirmHash`; the handler checks it against the live content hash (`SND-PROFILE-003`/`-004`).

When tokens arrive (§11), only the source of the caller class changes: the token replaces the header, and the
authorization table, the decorators and every test stay.

## 7. Finding or starting the process

S1's model stands: the server writes `control.json {url, pid, epoch}` (0600) once it listens; a client reads it with a
2 s timeout and checks the epoch against `GET /status`, so a stale file reads as not running (`SND-API-001`); bare
`sindri` spawns `sindri serve` detached and waits up to 10 s (`SND-CLI-002`), and a child that loses the lease race
reports the winner. This design adds which verbs start it:

| Verb | When the process isn't running |
|---|---|
| `sindri`, `sindri --headless` | Start it (S1) |
| Commands (`decide`, `handback`, `item add`, …) and queries (`queue`, `items`, …) | Start it, then run. `--no-start` prints `SND-API-001` instead |
| `status`, `stop` | Never start. `status` prints `not running`, exit 1; `stop` is a no-op, exit 0 |
| `doctor` | Never start; local and read-only (S1) |
| `sindri mcp` | Never start. Tools return `SND-API-001`: "Sindri isn't running; ask the human to run `sindri`". The process lifecycle is the human's |

## 8. The MCP server

`sindri mcp`: a subcommand of the same NativeAOT `sindri` binary, stdio transport, the official MCP C# SDK (approved in
the readiness AOT spike). A thin client of the HTTP API: every tool is exactly one API call with
`X-Sindri-Caller: session`, and the server holds no logic. Errors come back as tool errors carrying the SND code and
message.

| Tool | Arguments | API call | Kind |
|---|---|---|---|
| `sindri_status` | — | `GET /status` | query |
| `sindri_queue` | — | `GET /queue` | query |
| `sindri_item` | `itemId` | `GET /items/{itemId}` | query |
| `sindri_item_timeline` | `itemId`, `after?` | `GET /items/{itemId}/timeline` | query |
| `sindri_pending_decisions` | `itemId?` | `GET /decisions?pending=true` | query |
| `sindri_job` | `jobId` | `GET /jobs/{jobId}` | query |
| `sindri_request` | `kind`, `itemId?`, `text` | `POST /requests` (`FileRequest`) | request |
| `sindri_context` | `query`, `itemId?` | `GET /context?q=` | query, **with S3** |

There is no decision, desk, protected or job tool, so the MCP surface can't attempt one; the 403 backs that up for a
session that calls the API another way. Query tools carry the MCP `readOnlyHint` annotation.

**Registration:** the cutover rule holds (nothing in `setup.sh` or the providers points at the C# build before the
switch). The proof run registers it for the verification session only, with `claude mcp add --scope local sindri --
<path>/sindri mcp`, and removes it afterwards.

## 9. Errors, versioning and JSON

- **Errors:** S1's `HttpResults` mapping is unchanged (`ErrorKind` → status; `ProblemDetails` `title` = SND code,
  `detail` = message). The CLI prints `SND-… message` and the fix line from `docs/sindri/errors.md`, and exits 0 ok,
  1 attention, 2 error (S1).
- **New codes**, registered in `SindriErrors` and `docs/sindri/errors.md` by the phase that first uses them:

| Code | Kind (status) | When | Phase |
|---|---|---|---|
| `SND-AUTH-002` | Forbidden (403) | The caller class may not run the command's action class | P1 |
| `SND-API-003` | Unavailable (503) | `X-Sindri-After` not reached within 2 s | P2 |
| `SND-API-004` | RateLimited (429) | More than 16 event streams | P5 |
| `SND-API-005` | Conflict (409) | An `Idempotency-Key` reused with a different body | P1 |
| `SND-API-006` | Invalid (400) | An unknown `X-Sindri-Caller` value | P1 |
| `SND-ITEM-404` | NotFound (404) | No such item | P3 |
| `SND-ITEM-409` | Conflict (409) | The item changed since `itemVersion`, isn't in a state that allows the command, or the decision was decided elsewhere | P3, P4, P7 |
| `SND-REQ-001` | Invalid (400) | An unknown request kind, or empty text | P4 |
| `SND-DEC-404` | NotFound (404) | No such decision | P4 |
| `SND-DEC-001` | Invalid (400) | A choice that isn't one of the decision's options | P4 |
| `SND-JOB-404` | NotFound (404) | No such job | P6 |
| `SND-SVC-404` | NotFound (404) | No such service in the approved profile | P6 |

  `SND-AUTH-001` is reserved for the token phase (§11).
- **Versioning:** no version in the path in v0. The CLI, `sindri mcp` and the UI are built from this repo and ship
  together, and S1's routes are already under `/sindri`. `GET /status` reports `apiVersion: 1`, and every response
  carries `Sindri-Api-Version: 1`; the UI checks it on connect and says "Sindri and the app are different versions" on
  a mismatch. A breaking change later adds a `/sindri/v2` group and keeps `v1` for one release. Events carry their own
  per-type `schemaVersion`.
- **JSON:** S1's one `SindriJsonContext`, through `AwJson`. Every request, response, view, `CommandAccepted` and public
  payload is registered there. `--json` on a CLI verb prints the API response body exactly as received, so
  `sindri queue --json` byte-matches `curl http://127.0.0.1:7410/sindri/queue`.

## 10. Conflicts with S1 (flagged, not redesigned)

| # | S1 today | What this design needs | Proposed resolution |
|---|---|---|---|
| C1 | `GetStatusEndpoint` maps `/sindri/status` inside `MapGroup("/sindri")`, so the live route is `/sindri/sindri/status` | `GET /sindri/status` | S1 fixes it (its own digest lists it); otherwise P1 does, as a one-line change with a route test |
| C2 | No caller class: `ApproveProfile` trusts `confirmed: true` / the hash from any loopback caller | Session callers refused on protected commands | P1 classifies S1's commands (§3.2) and adds the authorizing decorator around them; S1's handlers don't change |
| C3 | `LoopbackGuard` allows a browser `Origin` on loopback; the dashboard spec refuses any `Origin` | — in v0 | Left as S1 built it; tightened in the token phase (§11) |
| C4 | `StreamEvents` sends ledger events as stored | Public types only, public payloads, filters, reset | P5 replaces the event mapping inside S1's slice; the route stays |
| C5 | S1's commands return their own response records, not `CommandAccepted` | `Sequence` for read-your-writes | P1 adds `Sequence` to S1's command responses where they write (`StopProcess`, `ApproveProfile`), additively |

## 11. Later (designed in outline, not phased)

- **Auth:** per-class bearer tokens minted per `serve` start (`auth/human.token`, `auth/session.token`, 0600),
  `SND-AUTH-001` on a missing or stale token; an `aw:sindri-auth` guard hook that denies agent tool calls touching the
  human token; containerized factory sessions get only the session token; passkeys through an `IPasskeys` port for the
  UI's desk and protected actions (§17.27.2); refuse any `Origin` header (C3).
- **Tailnet:** a second listener on a Unix socket that `tailscale serve` proxies to, class `remote`, Tailscale
  identity headers trusted only there (§17.22 D10); query-only until passkeys.
- **Webhooks:** `POST /webhooks/{source}` → `ReceiveWebhook` (class `trigger`, delivery id as the idempotency key),
  profile `triggers:` matched to standing workflows, per-source signature checks.
- **Receipts and metrics:** `GET /items/{itemId}/receipt`, `POST …/receipt/verify` (§17.23.12), `GET /metrics/today`,
  once the shipper and scorer blocks exist.
- **Context retrieval:** `GET /context?q=&item=` and `sindri_context`, with S3.
- **Inline projections:** a unit of work around `ILedgerStore.Append` so core views update in the ledger's
  transaction, if read-your-writes waits ever show up in practice.

## 12. Test strategy

| Layer | Lane | What |
|---|---|---|
| Domain decisions (`Core`) | unit | Item state transitions, request → decision, decision resolution, the authorization table (every class × caller pair), idempotency-key composition |
| Projections | unit | Each `IProjection` folded over event lists; replaying the same events twice gives the same rows |
| Command chain | unit | Each decorator with fakes: refused calls write only the audit row; a replayed `Idempotency-Key` returns `AlreadyRecorded`; a different body with the same key is `SND-API-005` |
| Registry | unit | `ARCH-CQRS-003`: every command has a row; routes exist; no stale `pending:<phase>` |
| Endpoints | integration | `WebApplicationFactory` over the real `Compose` with in-memory fakes (S1's contract-tested `InMemory*`) and `FakeTimeProvider`: per endpoint the happy path, its error codes, and its auth class (a session header on every decision, desk and protected route → 403 and nothing in the ledger); `ARCH-CQRS-001` over the endpoint data source |
| Read-your-writes | integration | A command then a query with `X-Sindri-After` sees the write; a stalled projector → `SND-API-003` |
| Projector rebuild | integration | Real SQLite in a temp dir: delete `views.db`, restart, the same views come back byte for byte |
| Event stream | integration | Resume with `Last-Event-ID`; filters; a slow reader gets `reset`; the 17th stream gets 429; non-public types never appear |
| CLI | unit + integration | Verb routing, the session-header rule from the environment, auto-start rules (§7), `--json` byte-equals the API body |
| MCP server | **proof run** | A real Claude Code subagent with `sindri mcp` registered for the session (§8, P8 signal) |
| AOT | CI AOT job | S1's `aot-smoke` extended per phase with the new verbs |

Every phase runs the AGENTS.md merge gate, and its PR carries the phase's signal as evidence.

## Phases

Every phase is one PR, depends on **S1 merged** (C1–C5 assume S1's code on the default branch), and follows the slice
recipe per slice (`/new-slice`, red first). "Unblocks" marks what the UI app (S1b and later) and the workflow engine
(S2) need.

| # | Goal | Files (main ones) | Size | Depends on | Observable signal | Unblocks |
|---|---|---|---|---|---|---|
| **P1** | **CQRS spine.** `ICommand`/`IQuery`, `CallerContext` from `X-Sindri-Caller`, the command chain (`AuthorizingService`, `IdempotentService`, `AuditingService`), `CommandAccepted`, `CommandRegistry` + `ListCommands`, classes for S1's commands, `ARCH-CQRS-001..003`, `SND-AUTH-002`, `SND-API-005`, `SND-API-006`, the CLI's session-header rule, C1 if still open | `Core/Commands/*`, `Features/Commands/*` (decorators, registry), `Features/ListCommands/`, `Host/CallerContextMiddleware.cs`, `Host/Endpoints/ListCommandsEndpoint.cs`, `tests/Aw.Sindri.Tests/Commands/*`, `tests/Aw.Architecture.Tests/Cqrs*`, rule records | M | S1 | `curl -X POST -H 'X-Sindri-Caller: session' …/sindri/profile/approve -d '{…}'` → 403 `SND-AUTH-002`, and the ledger gains only the `command.handled` row with outcome `refused`; `curl …/sindri/commands` lists every command with class and callers; `CLAUDECODE=1 sindri stop` → `SND-AUTH-002` | UI, engine |
| **P2** | **Projector and read-your-writes.** `views.db`, `IProjection`, the projector hosted service and cursor, rebuild on schema change, `X-Sindri-After`, `SND-API-003`, `RebuildViews`, `ListServices`, `GetProfile` | `Core/Views/*`, `Infrastructure/Views/*` (+ migrations), `Host/Projector.cs`, `Features/ListServices/`, `Features/GetProfile/`, `Features/RebuildViews/` | M | P1 | Delete `views.db`, restart: `curl …/sindri/services` returns the same JSON; `sindri views rebuild` then `sindri services --json` matches; a query with `X-Sindri-After: <seq+1000>` → 503 `SND-API-003` after 2 s | UI, engine |
| **P3** | **Items and the queue.** `ItemId`, item events, `item_view` and `queue_view`, `CreateItem`, `GetQueue`, `ListItems`, `GetItem`, `GetItemTimeline`, CLI `queue`, `items`, `item add`, `SND-ITEM-404/409` | `Core/Items/*`, `Features/{CreateItem,GetQueue,ListItems,GetItem,GetItemTimeline}/`, projections, `Host/Commands/{Queue,Items,ItemAdd}Command.cs` | M | P2 | `sindri item add "Try the queue"`, then `curl -s …/sindri/queue` byte-matches `sindri queue --json`; a session-header `POST /items` → 403 | **UI** |
| **P4** | **Requests and decisions.** `RequestId`, `DecisionId`, `FileRequest` (request → `decision.requested`), `ListRequests`, `ListDecisions`, `Decide` (item-version check, first decision wins), needs-you rows, CLI `request`, `requests`, `decide`, `SND-REQ-001`, `SND-DEC-*` | `Core/Requests/*`, `Core/Decisions/*`, `Features/{FileRequest,ListRequests,ListDecisions,Decide}/`, projections, CLI commands | M | P3 | `curl -X POST -H 'X-Sindri-Caller: session' …/sindri/requests -d '{"kind":"rescope","text":"…"}'` → appears in `sindri queue` under needs-you with `sindri decide D-… accept`; the same decide with the session header → 403; `sindri decide D-… accept` → resolved, `request.accepted` in the timeline; a second decide → `SND-ITEM-409 decided elsewhere` | **UI, engine** |
| **P5** | **The event stream, full.** Public-event registry and payloads, `PublicEvent`, `item` and `types` filters, `Last-Event-ID`, bounded channel and `reset`, ping, the 16-stream limit (`SND-API-004`), `ILedgerSignal`, `sindri status --follow` | `Core/Events/*`, `Infrastructure/Ledger/LedgerSignal.cs`, `Features/StreamEvents/*` (S1's slice), `Host/Endpoints/StreamEventsEndpoint.cs` | S | S1 only (stacked on S1, not on P1–P4) | `curl -N '…/sindri/events?after=0&types=service.*'` shows each `service.heartbeat` within 100 ms of its append; reconnecting with `Last-Event-ID: n` resumes at n+1; a reader that stops reading gets `event: reset`; `command.handled` never appears | **UI** |
| **P6** | **Jobs and services.** `JobId`, job events, `GetJob`, `CancelJob`, `RunService` (as a job), `PauseService`/`ResumeService` (the scheduler honours them), CLI following progress on the stream with Ctrl-C → cancel, `SND-JOB-404`, `SND-SVC-404` | `Core/Jobs/*`, `Features/{GetJob,CancelJob,RunService,PauseService,ResumeService}/`, scheduler hook in Host | M | P1 + P5 | `sindri services run heartbeat` prints progress lines and the result; Ctrl-C during a long fake job prints `cancelled` and the ledger shows `job.cancelled`; `sindri services pause heartbeat` → no heartbeat for 3 cadences; a session-header run → 403 | UI, **engine** |
| **P7** | **Item desk commands.** `HoldItem`, `ParkItem`, `UnparkItem`, `HandBack`, the takeover state in the item view (core design §9.3; the engine sets it later) | `Core/Items/*` transitions, `Features/{HoldItem,ParkItem,UnparkItem,HandBack}/`, CLI commands | S | P3 | `sindri park ITM-…` → `sindri queue` shows it under parked; `sindri handback` on an item not taken over → `SND-ITEM-409 … is not taken over`; a stale `itemVersion` → `SND-ITEM-409` | **engine** |
| **P8** | **`sindri mcp`.** The MCP server (stdio, MCP C# SDK, the §8 tools), `SND-API-001` when not running | `Host/Mcp/*` (or `Aw.Sindri.Mcp` if the Host's fan-out needs it), the SDK in `Directory.Packages.props` (≥ 14 days old, pinned), `deps.yaml` | M | P4 | **A Claude Code subagent** with `sindri mcp` registered for the session (`claude mcp add --scope local`): lists the queue and an item, files a `rescope` request that appears in `sindri queue` as needs-you, finds no tool to decide, and via `curl` with the session header gets `SND-AUTH-002`. Its transcript is the evidence | — |
| **P9** | **Engine hooks (with S2).** `InstanceId`, `ListItemInstances`, `StartItem`, `CancelInstance`; the engine's in-process `service` caller and its allowed desk commands | `Features/{ListItemInstances,StartItem,CancelInstance}/` | M | P7 + S2 | `sindri start ITM-…` creates an instance; `curl …/items/ITM-…/instances` shows it; a session-header start → 403 | — |
| **P10** | **`sindri open`.** `ILauncher` (Warp launch configuration, as the dashboard plan's decision 15), `OpenItem` seeds a Claude Code session with the item's context and marks it taken over | `Core/Launch/ILauncher.cs`, `Infrastructure/Launch/WarpLauncher.cs`, `Features/OpenItem/` | M | P7 | `sindri open ITM-…` opens a Warp tab running Claude Code in the item's repo with the item's context, and `sindri queue` shows the item `with you` until `sindri handback` | — |

**Unblocks the UI:** P1 (registry), P2 (views), P3 (queue and items), P4 (decisions, needs-you), P5 (live stream).
**Unblocks the engine:** P1 (command layer, `service` caller), P2 (projections to read), P4 (decision requests and
answers), P6 (jobs), P7 (park, hold, handback semantics).

## Parallel delivery with an agent team

The phases are built by an **agent team orchestrated with `/specToProvenPR`**, so that agents are always working and the
lead session only orchestrates. The rules below are part of this spec; `/specToProvenPR`'s plan for this spec must
follow them.

### Dependency graph and waves

```
S1 ──► P1 ──► P2 ──► P3 ──┬──► P4 ──► P8
 │      │                 └──► P7 ──┬──► P10
 │      └────────┐                  └──► P9 (also needs S2)
 └──► P5 ────────┴──► P6
```

| Wave | Runs at once | Stacked on | Notes |
|---|---|---|---|
| W1 | **P1**, **P5**, **P2** | P1 and P5 on S1; P2 on P1's branch | P5 needs only S1 (its signal uses `service.heartbeat`). P2 starts against P1's branch and rebases as P1 changes |
| W2 | **P3**, **P6** | P3 on P2; P6 on P1 + P5 | |
| W3 | **P4**, **P7** | both on P3 | Disjoint slices; they share only the anchored files below |
| W4 | **P8**, **P10** | P8 on P4; P10 on P7 | P9 waits for S2 and joins whichever wave is running when S2 merges |

Branches are **stacked** (one PR per phase, each based on its parent phase's branch, as in §17.10). A phase is
**ready** for review when its parent is ready, not merged. Merges go in graph order, and each child rebases on its
merged parent.

### The team

| Role | Who | Does | Never does |
|---|---|---|---|
| **Lead (orchestrator)** | The session running `/specToProvenPR` on this spec | Keeps the wave board, dispatches the next ready phase the moment an implementer frees up, resolves conflicts in shared files, runs the merge order, and relays owner questions | Write phase code |
| **Implementer** (one per phase in flight) | A subagent in its own worktree (`isolation: worktree`), on the phase's stacked branch | Runs the slice recipe red to green for its phase, the phase's signal by hand in the running app, and the merge gate; then opens the PR | Touch another phase's slices |
| **Reviewer** (one per open PR) | A fresh subagent per review round (`/review`) | Reviews the diff against this spec and the C# rules; findings go to the implementer through `/addressReview` | Fix code itself |
| **Prover** (P8 only) | A Claude Code subagent with `sindri mcp` registered for its session | Runs the §8 MCP journey and leaves its transcript as evidence | — |

**Keeping agents busy:**
- The lead dispatches as soon as a phase's parent is ready. While an implementer waits on a review round, it doesn't
  idle: the lead hands it the next ready phase, or the red tests for a later phase's `Core` (pure domain types and
  their unit tests need no parent branch).
- Concurrency: at most **3 implementers** at once. The heavy-job lock has two slots (builds and test lanes), so two
  agents can build while the third writes.
- A phase whose parent changes under it rebases at its next green point, not mid-slice.

### Conflict rules for shared files

The phases share a few files. Each edit to one of them goes **only** at the file's anchor (the `/new-slice` anchors),
so parallel branches conflict only as adjacent appended lines, and the lead resolves them with a merge commit:
- `Features/SindriFeaturesServiceCollectionExtensions.cs`, `Host/Endpoints/EndpointTable.cs`,
  `Host/Commands/CommandTable.cs`, `Host/SindriJsonContext.cs`;
- `Features/CommandRegistry.cs` (one row per command);
- `Core/SindriErrors.cs`, its `All` list, and `docs/sindri/errors.md`. **The codes are reserved per phase in §9**, so two
  phases never pick the same code;
- the `PublicAPI.Unshipped.txt` files (sorted lines; regenerate from `RS0016` after a merge);
- the typed ids, reserved per phase in §3.2.

Every other file belongs to exactly one phase.

### Done

A phase is done when its PR is ready (never merged by an agent):
- its signal is shown by hand in the running app, with evidence;
- the merge gate is green;
- its review loop reaches zero blocking findings and zero issues, and every suggestion is answered in writing.

The lead reports each phase as it becomes ready, and stops for the owner's merge.
