# Sindri blocks over MCP: two generic tools, `list_tools` and `execute_tool_call`

**Status:** design, draft for the owner's read (not approved) · **Date:** 2026-10-18 · **Owner decisions:** 2026-10-17
(the two-tool shape, below), 2026-10-18 (sessions outside a Step may call blocks, C3) · **Amends:** the API surface spec `2026-10-10-sindri-api-surface-design.md` (API), §4.1, §4.2,
ARCH-CQRS-001, §8 and §9; the workflow engine spec `2026-10-10-sindri-workflow-engine-design.md` (ENG), §2, §3.4 and §6.5; the session
launcher spec `2026-10-15-sindri-session-launcher-design.md` (LCH), the per-launch MCP config · **Lanes:** the API lane
writes and builds §A (the MCP side); the engine lane writes and builds §B (run under the Step's policy, evidence) ·
**Next:** the owner reads it and answers §C; then phases (§D).

The owner decided (2026-10-17): an agent session reaches Sindri's blocks through **two generic MCP tools**, not one
tool per block.
- `list_tools` returns the blocks this Step may call, each with its input schema.
- `execute_tool_call` asks Sindri to run one named block under the Step's policy and records the result as the Step's
  evidence.

So the MCP tool list, and the tokens it costs every session, stay fixed as blocks are added. A session sees a new block
through `list_tools` only when a recipe lets its Step call it.

## Terms

- **Callable block:** a block whose descriptor opts in to being called from a session (§B.1).
- **Step session:** an agent session `ILauncher` started for one Step run (ENG §6.5), whose per-launch MCP config
  names that run (§A.2).
- **Plain session:** every other session that reaches `/sindri/mcp` with the repo header: the owner's own Claude Code
  in the repo, or a session opened by `OpenItem` with no run. It may call blocks too (owner, 2026-10-18), from a
  narrower set and with no Step to record against (§A.6, §B.5).
- **Call:** one `execute_tool_call`, named by its call id. Its state is `running` until it ends, and it ends `ok` (the
  block ran and passed or answered), `failed` (the block ran and failed, timed out, or was cut off) or `refused`
  (Sindri didn't run it; the session got a tool error).
- **Calls** is the word the app and the docs use for these. "Evidence" stays the evidence packs, `evidence:` facts and
  the evidence-provider block kind.

## A. The MCP side (API lane)

### A.1 The two tools

They join API §8's table after `sindri_job` and follow its rules: the repo comes from `X-Sindri-Repo`, every call is
stamped `session`, and errors are tool errors carrying the SND code. Their names are choice C2. The text below uses the
owner's literal names.

| Tool | Arguments | Handler | Kind |
|---|---|---|---|
| `list_tools` | none | `ListCallableBlocks` (also `GET /workflow/instances/{instance}/runs/{token}/tools` and `GET /tools`, §A.4) | query (`readOnlyHint`) |
| `execute_tool_call` | `name`, `arguments` (an object), `callId?` | `ExecuteBlockCall`: **MCP only**, no HTTP route (§A.2) | `block-call`, a new session-only action class (§A.4) |

**What the agent is told.**
- The `list_tools` description says: "The Sindri blocks you may run here, with their inputs. Call this before
  `execute_tool_call`."
- The `execute_tool_call` description says: "Run one Sindri block from `list_tools`. A long block answers `running`;
  send the same `callId` again to get its result. Every call is recorded."
- A Step's seed (UI §6.2's template) gains one line, but only when the state lists tools: "Sindri blocks you can run
  are listed by `list_tools`."

**`list_tools`** answers `{ scope, run?, tools, reason? }`:
- `scope` is `step` or `plain`, and `run` is the run's address for a Step session.
- `tools` is `[{ name, version, description, inputSchema, mode, timeoutSeconds }]`, ordered by name.
  - `name` is the block id.
  - `description` is the descriptor's `Description` (§B.1): Sindri's wording, so it isn't fenced.
  - `inputSchema` is a JSON Schema (draft 2020-12, the shape MCP uses for a tool's own input) built from the
    descriptor's `ParameterSpec`s, each with its description, its enum when it has one, and its item type for lists.
    Only the parameters a session may set are in it: pinned values and facts never are (§B.1).
  - `mode` is `sync` (the call answers within `timeoutSeconds`) or `async` (the call answers `running` at once).
    `timeoutSeconds` is the block's `MaxDuration` (§B.4).
- `reason` is set only when `tools` is empty: `no-tools-listed` (the state lists none), `run-not-open`, `taken-over`
  (C5), `none-callable-in-this-build`, or `no-plain-tools` (the profile's plain set is empty).

**`execute_tool_call`** answers
`{ callId, state, outcome?, result?, reasons?, truncated?, evidenceSeq?, startedAt, durationMs? }`:
- `state` is `running` or `ended`. `outcome` (`ok` or `failed`), `durationMs` and `evidenceSeq` are set once it
  has ended.
  - `evidenceSeq` is the ledger seq of the call's `step.tool-called` event, or `session.tool-called` for a plain
    session (§B.3, §B.5).
  - `result` is the block's answer and `reasons` its failure reasons. Both are scrubbed and capped at 64 KiB
    (`truncated: true` past it), and they are the same body the ledger points to, in the sync answer as in a
    polled one.
- **Validation, in order, before anything runs:**
  1. Shape: `name` is a string, `arguments` an object, and `callId` a UUID if given. Otherwise `SND-CONTEXT-022`, as
     for any MCP tool.
  2. Size: `arguments` are at most 16 KiB of JSON. Otherwise `SND-TOOL-002`.
  3. `name` must be in this session's `list_tools`. Otherwise `SND-TOOL-001`.
  4. `arguments` must validate against its `inputSchema`. Otherwise `SND-TOOL-002`, with each JSON-pointer path and
     message in the error's `violations` (A.3). The validator is built in Sindri from `ParameterSpec`'s closed type
     set; no JSON Schema library is added.
- **Sync and async come from the block** (`mode`), never from the call:
  - A `sync` block answers `ended`.
  - An `async` block answers `running` at once.
  - A `sync` block that is still running at its bound answers `ended`, `failed`, reason `timeout`.
- **One call id, one call.** The session may pass its own `callId`. With none, the server makes one.
  - Sending the same `callId` with the same `name` and `arguments` again runs nothing. It returns the call's current
    state, so this is how a session polls an `async` call (no job, no `JOB-` row; jobs don't survive a restart and
    sessions can't start them).
  - The same `callId` with a different `name` or `arguments` is `SND-TOOL-409`. The API checks it against the
    stored call before the engine is asked, so the answer doesn't depend on Temporal's Update dedupe.
  - A call still running when the host stops ends `failed`, reason `host-restart`. A poll after the restart returns
    that, and the session calls again with a new id.
- **Fencing.** Every string in `result` and `reasons` that isn't Sindri's own wording is fenced as `sindri:call`, and
  its object carries `"trust": "untrusted"`, as job results are since P8. The block's own reason codes aren't fenced;
  their free text is.

### A.2 Which session a call belongs to

A call has to name its Step run, because the run's policy picks the allowed blocks and the run gets the record.
P8 keeps only `X-Sindri-Repo` of the `X-Sindri-*` headers on MCP, and the launch secret never rides MCP (owner,
2026-10-16).
- **`X-Sindri-Run` (choice C1).** The per-launch MCP config that `ILauncher` writes (LCH: the `mcp` form,
  `{mcpConfig}`) sets `X-Sindri-Run: <instance>:<run token>` for a Step session.
  - `<instance>` is the instance address with its `/` written as `~` (`workflow.pr-review~ITM-42` …), and `<run token>`
    is the decimal run counter. The grammar is `^[a-z0-9.~-]+:[0-9]{1,9}$`, nothing else, so the value is safe to
    splice into Codex's `-c` overrides.
  - The endpoint's header filter becomes a two-entry allowlist (`X-Sindri-Repo`, `X-Sindri-Run`), and its existing
    header-drop test gains the second entry.
- **No proof, by design.** The run token is a public counter, so the header names the run without proving the caller
  is that run's session. Under the non-adversarial threat model, the guard is against a session reaching the wrong run
  by mistake, not against forging.
- **Server checks for a Step session:** the run is the instance's open attempt on the agent runtime, and its repo is
  the header's repo (`SND-TOOL-404` otherwise; where the engine's "not the open attempt" and 404 overlap, the engine
  decides and it's 404). Takeover is choice C5.
- **No HTTP route for `execute_tool_call` in v1.** An HTTP route would take the run from its URL, and any `session`
  curl could then reach another run by mistake. The MCP endpoint calls the `ExecuteBlockCall` service through the
  same chain as every command (gate, audit, authorization, validation). This is ARCH-CQRS-001's fifth carve-out, an
  MCP-only command; the list stays one place.
- **The per-launch server can't be shadowed.** A repo where the owner ran `sindri mcp register` already has a server
  named `sindri` (with no run header). The per-launch config names its server `sindri-step`, and the Claude form adds
  `--strict-mcp-config`, so only the per-launch server is loaded. Codex's `-c` overrides name `mcp_servers.sindri-step`.
  The proof checks it (A.5 step 7).
- **A session with no `X-Sindri-Run` is a plain session** (§A.6).
- **An agent with no per-launch MCP form** (`mcp: none`) can't call blocks as a Step, and its session is a plain one.

### A.3 Errors (API §9)

| Code | Kind (status) | When | Message (fix) |
|---|---|---|---|
| `SND-TOOL-001` | Invalid (400) | The named block isn't in this session's `list_tools` | "`<name>` isn't a block you can run here (fix: call `list_tools`)" |
| `SND-TOOL-002` | Invalid (400) | `arguments` are over 16 KiB, or don't validate against the block's `inputSchema`; extension `violations: [{path, message}]` | "The arguments for `<name>` don't fit its inputs (fix: see `violations`)" |
| `SND-TOOL-004` | Forbidden (403) | This run's call budget is spent (`workflow.calls.perRun`, §B.4). It doesn't refill within the run | "This Step has used its `<n>` calls (fix: finish the Step, or raise `workflow.calls.perRun` in the profile)" |
| `SND-TOOL-005` | RateLimited (429) | The repo's plain-call cap for today is spent (`workflow.calls.plainPerDay`, §A.6) | "`<repo>` has used today's `<n>` plain calls (fix: wait for the local day to roll over, or raise the cap)" |
| `SND-TOOL-006` | Conflict (409) | A call is already running for this run (or, for a plain session, this repo), §B.4 | "A call is still running: `<callId>` (fix: poll it with the same `callId`)" |
| `SND-TOOL-404` | NotFound (404) | No open agent run at `X-Sindri-Run` in this repo | "No open Step run `<run>` in `<repo>` (fix: the Step has ended; report or stop)" |
| `SND-TOOL-409` | Conflict (409) | The same `callId` came with a different `name` or `arguments` | "Call `<callId>` was sent with other arguments (fix: use a new `callId`)" |
| `SND-AUTH-003` | Forbidden (403) | Only if C5 is "no": the instance is taken over. Its message for MCP callers names the takeover, not the launch secret | "The Step is taken over by a human (fix: wait for handback)" |

- **The MCP error carries extensions.** `McpToolError` gains an optional `Extensions` object, written only when set,
  so a code's extension members (API §9: cleaned, arrays cut at 100) reach the agent. Today only the code and the
  message do. A golden covers `SND-TOOL-002`'s `violations`.
- **A block that runs and fails isn't an error.** Its call answers `ended`/`failed` with the block's reasons and is
  recorded like any call.
- **Refusals are recorded (C4).** A call refused with `-001`, `-002`, `-004`, `-005` or `-006` is still recorded as a
  `refused` entry with its code and no result (§B.3). `SND-CONTEXT-022`, `SND-TOOL-404` and `SND-TOOL-409` have
  nothing to record against. They go to the structured log with the repo and the run header.

### A.4 Catalog, CLI, goldens, telemetry, docs and the app

- **Action class `block-call`** (API §4.1): a session asks Sindri to run a callable block. It appends only the call's
  own record (`step.tool-called` or `session.tool-called`) and never a transition. The authorization table (§4.2)
  gives it to `session` only: a human runs a block through the CLI's read verb or a recipe, not this command.
  API §4.1, §4.2 and ARCH-CQRS-001 are amended to match.
- **Catalog rows:** `ListCallableBlocks` (query) and `ExecuteBlockCall` (`block-call`). The UI lane's pins change:
  labels, the row count and copy blocks.
- **Read routes:** `GET /workflow/instances/{instance}/runs/{token}/tools` (a Step's list) and `GET /tools?scope=plain`
  (the repo's plain set). `{instance}` is URL-encoded as every instance route is.
- **CLI:** `sindri workflow tools <address> [--run N] [--json]` and `sindri tools [--repo R] [--json]` print the same
  lists, so a block author checks a recipe's tools without launching a session.
- **Step report:** `CompleteStep` (`sindri workflow complete --run`) gains the optional `calls: [<callId>, ...]` of
  §B.3. The API binds it and passes it through; the engine validates it. The CLI flag is `--call <id>`
  (repeatable). The request golden changes.
- **Goldens:** both read routes, the `list_tools` answer for each scope and reason, the `execute_tool_call` answer
  for `ended`/`ok`, `ended`/`failed`, `running`, `truncated` and each error code, and the tools/list golden with the
  two tools after `sindri_job`.
- **Telemetry** (`Aw.Kernel.Telemetry`): one span per call, `sindri.block_call`, with the block, the scope, the outcome
  and the reason. Counters `sindri.block_calls` (by block, scope and outcome) and `sindri.block_call.duration`. They
  answer whether agents use calls at all.
- **Docs:** each phase updates them.
  - `docs/csharp/sindri.md`: the codes, the action class, the two tools and the CLI verbs.
  - `docs/csharp/sindri-workflow.md`: how to make a block callable, and the recipe's `tools:`.
  - `docs/csharp/sindri-profile.md`: the three profile keys.
  - The help text, the seed line, and the tool descriptions above.
- **The app (BU-1).** Calls go where the human already looks at a Step:
  - **A "Calls" list under the Step's trace** in the item sheet. One row per call, in seq order: a glyph plus a word
    for the state (`running`, `ok`, `failed`, `refused`), the block, the duration, and `cited` or `uncited`.
    Colour is never the only signal.
  - A running call shows as soon as it starts, read from the engine's run state (§B.3), not only when it ends.
  - A row opens the call's result in the read-only result view, rendered as untrusted text (the fence's rule), with
    `truncated` said in words.
  - Refusals show their code's message. The refusals-capped marker is one row.
  - The list says "No calls" when there are none.
  - **A Timeline line** per ended call: "call `<block>` · `<outcome>` · `<duration>`".
  - **Keyboard and focus:** the list is in the sheet's tab order, and Enter opens a row. A row that appears or
    changes while the sheet is open never moves focus, and the live region announces "call `<block>` ended
    `<outcome>`" (UI §5).
  - **Plain calls** show in the repo's activity list, not on any item, with the same row shape and the word "plain".
    If the call passed an `itemId`, it also shows on that item's Timeline as "plain call, not proof".

### A.5 Proof

API §8's proof shape, with a launched Step session in place of `claude -p`. A recipe with one state lists two
callable blocks, one `sync` and one `async`. The journey:
1. `list_tools` shows exactly those two, with their descriptions, schemas and modes.
2. A bad argument gets `SND-TOOL-002`, and the agent sees its `violations` path.
3. A good `sync` call answers `ended`/`ok`, and its `evidenceSeq` is in `workflow events`.
4. The same `callId` again runs nothing (the block's own counter is unchanged), and the same `callId` with other
   arguments gets `SND-TOOL-409`.
5. The `async` call answers `running`. The app's Calls list shows it running, and polling the same `callId` ends it.
6. A step report citing both call ids is accepted, and one citing an unknown id is refused.
7. With a repo-local `sindri` server registered too, the Step session still reaches `sindri-step`: `list_tools` says
   `scope: step`.
8. A plain session in the same repo (no run header) gets the plain set (`scope: plain`). Its call is a
   `session.tool-called` event and shows in the repo's activity, not on the Step.
9. With `workflow.calls.plainPerDay: 1`, a second plain call gets `SND-TOOL-005`, and the refusal is recorded.

### A.6 Calls outside a Step (owner, 2026-10-18)

The owner's own sessions may call blocks: "my own sessions should be able to use sindri block tools". The engine
side is §B.5.
- **Bound to the repo only.** No run header. MCP is stateless and Claude Code sends no session id, so "the session"
  is just `caller: session` plus the repo: the cap and the record are per repo.
- **`list_tools` for a plain session** is the repo's plain set (§B.5, choice C6). `scope: plain`.
- **`execute_tool_call`** takes an optional `itemId` and `ref`:
  - `itemId`: the item the call is about. The call shows on its Timeline as "plain call, not proof" (choice C8).
  - `ref`: the commit the block runs against. The default is the registered repo's HEAD, run in a scratch checkout
    Sindri makes and removes (choice C7). `ref` must resolve in the registered repo, else `SND-TOOL-002`.
- **Cap:** `workflow.calls.plainPerDay` per repo (default 200), then `SND-TOOL-005`. One plain call runs at a time
  per repo (`SND-TOOL-006`).
- **Record:** `session.tool-called` on the repo, kept 30 days (§B.5). It never satisfies a gate and is never citable
  in a step report.

## B. The engine side (engine lane)

A call never moves the instance. It runs one block for the session and records what happened, and the Step's own
report and the recipe's gates still decide the transition (ENG §6.5: "the session reports; the engine decides"). A
gate the session called does not stand in for the same gate on a transition: the engine runs its gates itself.

### B.1 Which blocks are callable

- **The descriptor opts in.** `BlockDescriptor` gains `Callable` (`bool?`, written only when true, so every existing
  descriptor hashes as before, like `HumanOnly`).
  - In v1 only two kinds may set it: `gate` and `evidenceProvider`. A `step`, `effect` or `stepInput` block is never
    callable: a Step inside a Step, an outbox effect or an injection fill has no meaning mid-run.
  - A callable block must not carry the `Effects`, `WriteScope` or `AutonomyTier` capability. A catalog test
    (`Every_callable_block_is_a_read_only_gate_or_evidence_provider`) pins this for every registered block, so a block
    pack that breaks it fails its build, not at run time.
  - The descriptor's `Parameters` become the call's `inputSchema` (§A.1). The same `ParameterSpec`s that check a
    recipe's `with:` check a call's `arguments`, so the two can't drift. A `with:` value that names a fact
    (`{ fact: ... }`) is resolved by the engine, as on a transition. A call's `arguments` are literals only in v1.
- **The recipe lists them per state.** An active state gains `tools: [<block id>, ...]`. The list is part of the
  recipe, so the recipe hash covers it, approval pins it, and `recipe show` and the approval preview print it. No
  state lists anything by default, so a recipe that says nothing gives its sessions an empty `list_tools`.
- **The validator checks the list** (new rows in `docs/csharp/sindri-workflow.md`, after `RCP-TIME-003`):

| Code | Error when |
|---|---|
| `RCP-TOOL-001` | A `tools:` entry is not a registered block, or its descriptor is not `Callable` |
| `RCP-TOOL-002` | `tools:` is on a state whose Step can't run in an agent session: a wait or terminal state, a `humanOnly` Step, or a Step with only the block or desk runtime. Nothing could make the call |
| `RCP-TOOL-003` | A listed block needs a capability (`Network`, `Budget`, `Tools`) that the state's own Step doesn't hold. A call runs with the Step's capabilities, never wider (owner, 2026-10-17: a Step only tightens) |

- **`list_tools` is computed, not stored.** For an open agent run it is the state's `tools:` list, filtered to blocks
  that are callable in the running build and fit the run's StepPolicy (B.2), ordered by id. A block named in an
  approved recipe but gone from the build drops out of the list. Calling it is `SND-TOOL-001`, and the approval
  drift check (ENG §5.8) reports the missing block as for any other.

### B.2 Running under the Step's policy

- **Where it runs.** `ExecuteBlockCall` is a Temporal Update on the instance's `RecipeWorkflow`, named
  `block-call`, with Update id `call:<run>:<callId>`. Temporal already dedupes an Update by its id, which is §A.1's
  per-`callId` idempotency. `SND-TOOL-409` (the same id with different arguments) is checked against the stored
  call's arguments hash.
  - The Update's validator refuses before anything enters history:
    - the run is not the instance's open attempt, or its Step is not on the agent runtime: `SND-TOOL-404`;
    - the instance is taken over: `SND-AUTH-003`;
    - the block is not in the run's `list_tools`: `SND-TOOL-001`;
    - the arguments fail the schema: `SND-TOOL-002`;
    - the budget is spent: `SND-TOOL-004`.
  - The engine returns these as typed refusals, and the API maps them to §A.3's codes, so no new `SND-WF` code is
    needed. Where `SND-TOOL-404` and the engine's "not the open attempt" overlap, the engine's check is the
    authority and the API reports it as `SND-TOOL-404`.
  - Refusals the validator turns away leave no history. When C4 holds, the API records them in the ledger directly
    (B.3).
- **The handler** runs one `RunBlockCall` activity, at most once (no retries; a block that should retry says so in
  its own code), bounded by the block's `MaxDuration` (DT-J2's field). It returns `ok`, `failed` with the block's
  reasons, or `failed` with reason `timeout`. It then appends the evidence event (B.3) and returns the answer. A
  call is in history, so it replays like every other activity, and a server restart mid-call resumes it rather than
  running it twice.
- **The policy.** The call's `BlockContext` carries the run's StepPolicy (the ILauncher contract: `Mode` and
  `WritablePath`; `AllowedTools` is the agent's own tool list and doesn't apply to Sindri's blocks) and the Step's
  capabilities. A callable block never writes (B.1), so `Mode` only matters for a future kind that does. The
  context also carries the run's repo, worktree and approved profile, so a gate that runs tests runs them in the
  run's own worktree, never the shared checkout, and through the heavy lock like any gate. The block runs under the
  host's identity with the context's limits. It never gets the session's environment, and it never sees the launch
  secret.
- **How a call ends early.** A call in flight when the run ends (step report, timeout, `session.ended`, cancel) or
  when the instance is taken over is cancelled through the activity's heartbeat, as DT-J2's block runtime is. It is
  recorded as `failed`, reason `run-ended` or `takeover`. Handback doesn't resume it: the session calls again.

### B.3 The call as the Step's evidence

- **One ledger event per call: `step.tool-called`.** It carries `instance`, `run`, `callId`, `block`, `version`
  (the descriptor hash of the running build), `argumentsSha256`, `outcome` (`ok` | `failed` | `refused`), `reason`
  (a failure or refusal code, null-omitted), `resultBytes`, `resultSha256` and `durationMs`.
  - The API's `evidenceSeq` is this event's seq; it keeps that name.
  - The event goes through `RecordEvent` like every transition event, so the views and `workflow events` show it
    (`tool-called gate.tests.green · ok · 12.4 s`).
  - It never triggers a transition (it isn't in the trigger table).
- **The result body** is scrubbed by `Aw.Redaction`, capped at 64 KiB, and stored in the instance's content store
  under `call:<callId>`, with only `{ bytes, sha256 }` in history: the same rule as Step inputs (W6). A longer result
  keeps its first 64 KiB and is marked `truncated: true` on the event and in the answer.
- **Failed calls** are recorded in full: the block's reasons are its result.
- **Refused calls** (C4, recommended yes): a call refused with `SND-TOOL-001`, `-002` or `-004` is appended
  by the API, through the same `RecordEvent` port, as `step.tool-called` with `outcome: refused`, its code as
  `reason`, the arguments hash, and no result.
  - These don't count against the call budget, so a session that keeps sending bad arguments shows in the history
    without locking itself out.
  - Refused entries have their own cap per run, equal to the call budget (B.4). Past it, refusals are still
    answered but no longer recorded, and one `step.tool-refusals-capped` event marks the cut, so the ledger can't grow
    without bound.
- **Using the evidence.**
  - A step report (`workflow complete --run`) may cite calls by id in a new optional `calls: [<callId>, ...]`
    field. Validation (ENG §5.5) refuses an id that isn't a recorded `ok` or `failed` call of this run (a new row
    for the existing report-invalid code).
  - The run's evidence list, which feeds the UI's row (§A.4), the Judge's `choose` facts when a later route reads the
    run (DT §8.3) and `workflow why` (DT-J3), lists every call of the run, cited or not, in seq order.
  - Uncited calls are shown as uncited, so a report that leaves out a failing call can be seen.

### B.4 Time and budget

- **Sync or job is the block's own.** A block whose `MaxDuration` is 30 s or less is answered in the call. A longer
  one is always a job (§A.1). `timeoutSeconds` in `list_tools` is the `MaxDuration` in seconds, so the session knows
  which it will get. A callable descriptor with no `MaxDuration` is a catalog-test failure: every callable block is
  bounded.
- **The job** is the API's handle on the in-flight Update. `sindri_job` reads its result from the same evidence
  event, so a job's answer and a sync answer are the same record.
- **The per-run call budget** is a profile key, `workflow.calls.perRun` (default 50; its row goes in
  `docs/csharp/sindri-profile.md`). Past it, a call is `SND-TOOL-004`.
  - A block with the `Budget` capability also spends the run's ENG §3.7 budget, as it would on a transition. An
    over-budget run parks with `budget` after the call returns, never in the middle of one.
- **The run's own clock keeps running.** Calls don't pause the state's `timeout`. A session that spends its time on
  calls times out like any other.

## C. Owner choices (plain words)

**Answered.** C3 (2026-10-18): "my own sessions should be able to use sindri block tools". Your sessions outside a
Step get the block tools (§A.6, §B.5). C6 to C8 are the details that answer opens.

1. **How Sindri knows which Step a call belongs to.** The Step's id goes in that session's tool config, with no
   secret, so anything in the session could read it. This loosens your 2026-10-16 rule that only the repo header
   travels over MCP. *Recommended:* yes. The other way is sending the launch secret over MCP, which you ruled out.
2. **Tool names.** Exactly `list_tools` and `execute_tool_call`, or `sindri_list_tools` and
   `sindri_execute_tool_call` to match Sindri's other tools. *Recommended:* the `sindri_` names, because some agent
   CLIs show tools without the server's name, and a bare `list_tools` could be mistaken for the agent's own.
3. *(answered, above)*
4. **Refused calls** (wrong block name, bad arguments, over the limit). *Recommended:* record them too, as a short
   entry with no result, so a session that keeps retrying until something passes is visible. Failed calls are
   always recorded.
5. **While you've taken a Step over.** Can block calls still run from that session, whether you or the agent makes
   them? *Recommended:* yes. They're recorded and marked "during takeover", and they can't be cited as the Step's
   proof. Otherwise the tools switch off exactly when you join the agent.
6. **Which blocks your own sessions get by default.** Every read-only, offline block, or none until you list them?
   *Recommended:* every read-only, offline block. You can narrow the list in the profile, and a block that reaches
   the network or spends money only runs if you name it there.
7. **What your own session's block runs against.** Your committed code, in a scratch copy Sindri makes and throws
   away, or your session's own folder, uncommitted changes included? *Recommended:* the scratch copy. A test run
   writes build output, and the scratch copy keeps that out of your working folder and the shared checkout. To test
   uncommitted work, commit it first or pass the commit.
8. **What your own session's calls count for.** *Recommended:* they're logged (repo, block, outcome, result) and
   kept 30 days, but they never count as proof for a work item or a Step, even when they name an item. Later, if
   you want, a Step could adopt one as proof (not in v1).

## D. Phases

Contract first, so the two lanes build in parallel.

| Phase | Lane | Holds | Waits on |
|---|---|---|---|
| **BM-0** | engine | The contract, red: the `Callable` field; the `tools:` recipe field with the RCP-TOOL-001..003 fixtures; the request and response records for `ListRunTools` and `ExecuteBlockCall`, with stub services; the `step.tool-called` event record; the typed refusals | P8 (the agent runtime), API P10a (#295: ILauncher's per-launch MCP config, where `X-Sindri-Run` is set) and DT-J2 (`MaxDuration`) merged |
| **BM-1** | engine | The validator rows; the catalog test; `list_tools` computed from the recipe, the build and the policy; the `block-call` Update with its validator, `RunBlockCall` and the evidence event; the content-store body; the budget key; `calls:` on the step report; the events line. Tests: unit (pure filtering and refusals), integration (an Update through the time-skipping server, replay after a restart mid-call, cancel on takeover) | BM-0 |
| **BA-1** | api | §A: the two MCP tools, the routes, the header, the refusal recording (C4), the catalog rows, the goldens. Built against BM-0's records and stubs, so it starts the day BM-0 lands | BM-0 |
| **BA-2** | api, with engine | §A.5's proof end to end, with a real launched Step session | BM-1, BA-1 |
| **BU-1** | ui | The evidence row for calls (cited and uncited) | BA-1's goldens |

BM-1 and BA-1 run at the same time, each lane with its own builders. Only BA-2 needs both.
