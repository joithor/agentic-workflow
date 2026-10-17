# Sindri blocks over MCP: two generic tools, `list_tools` and `execute_tool_call`

**Status:** design, draft for the owner's read (not approved) · **Date:** 2026-10-18 · **Owner decision:** 2026-10-17
(the two-tool shape, below) · **Amends:** the API surface spec `2026-10-10-sindri-api-surface-design.md` (API), §8 and
§9; the workflow engine spec `2026-10-10-sindri-workflow-engine-design.md` (ENG), §2, §3.4 and §6.5; the session
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
- **Step session:** an agent session `ILauncher` started for one Step run (ENG §6.5). Every other session, including a
  human's own session in the repo and one opened by `OpenItem` with no run, is a **plain session**.
- **Call:** one `execute_tool_call`. It has a call id, and its outcome is `ok`, `failed` (the block ran and failed) or
  `refused` (Sindri didn't run it).

## A. The MCP side (API lane)

### A.1 The two tools

They join API §8's table after `sindri_job`. They follow its rules: one API call each, through the same handlers, the
repo from `X-Sindri-Repo`, every call stamped `session`, errors as tool errors carrying the SND code.

| Tool | Arguments | API call | Kind |
|---|---|---|---|
| `list_tools` | none | `GET /runs/{run}/tools` | query (`readOnlyHint`) |
| `execute_tool_call` | `name`, `arguments` (an object), `callId?` | `POST /runs/{run}/calls` (`ExecuteBlockCall`) | issue |

The names are the owner's literal ones. The `sindri_` prefix is choice C2.

**`list_tools`** returns `{ run, tools: [{ name, version, description, inputSchema, readOnly, timeoutSeconds }] }`.
- `name` is the block id (for example `gate.tests.green`).
- `inputSchema` is a JSON Schema (draft 2020-12) built from the descriptor's `ParameterSpec`s (§B.1). It is the same
  shape MCP uses for a tool's own `inputSchema`, so the agent reads it as it reads any tool.
- `description` is the descriptor's own text: Sindri's wording, so it isn't fenced.
- The list is the Step's allowed set at the moment of the call. It is ordered by name and is empty for a plain session
  (C3).

**`execute_tool_call`** answers `{ callId, outcome, result?, error?, evidenceSeq }`.
- **Validation before any run.** `name` must be in this run's `list_tools`, else `SND-TOOL-001`. `arguments` must
  validate against its `inputSchema`, else `SND-TOOL-002`, with each JSON-pointer path and message as the extension
  `violations` (API §9: cut at 100 entries, cleaned).
- **Sync, with a bound.** The call waits for the block up to its `timeoutSeconds` (§B.4).
  - A block that finishes in time answers `ok` or `failed`.
  - A longer block answers at once with `outcome: "running"` and a `jobId`, which the session polls with `sindri_job`.
  - Which shape a block gets is fixed by its descriptor, not chosen per call.
- **Idempotent per `callId`.** The session may pass its own `callId` (a UUID). A retry with the same id returns the
  first answer and runs nothing again. With no `callId`, the server makes one. The engine keys the call as the
  Temporal Update id `call:<run>:<callId>` (§B.2).
- **Results are fenced.** Any string in `result` or `error` that isn't Sindri's own wording is fenced, and its object
  carries `"trust": "untrusted"`, as the job result's strings are since P8 (`sindri:job`). The new fence label is
  `sindri:call`.
- **`evidenceSeq`** is the ledger seq of the call's evidence event (§B.3). The session can cite it in its step report.

### A.2 Which run a call belongs to

A call has to name its Step run: the run's policy picks the allowed blocks, and the run gets the evidence. P8 keeps
only `X-Sindri-Repo` of the `X-Sindri-*` headers on MCP, and the launch secret never rides MCP (owner, 2026-10-16).
- **Proposed (C1):** the per-launch MCP config that `ILauncher` already writes (LCH: the `mcp` form, `{mcpConfig}`)
  also sets `X-Sindri-Run: <instance>/<run token>` for a Step session. The endpoint keeps it next to `X-Sindri-Repo`,
  and still drops every other `X-Sindri-*` header.
- **No proof.** The run token is a public counter (ENG §6.5), so the header names the run without proving the caller is
  that run's session. Under the non-adversarial threat model, the guard is against a session reaching the wrong run by
  mistake, not against forging.
- **Server checks.** The server serves the call only while the run is open, the instance isn't taken over (else
  `SND-AUTH-003`, as for `step-report`), and the run's repo is the header's repo (else `SND-TOOL-404`).
- **No header.** A session with no `X-Sindri-Run` is a plain session: `list_tools` is empty, and `execute_tool_call` is
  `SND-TOOL-003` (C3).
- **Agents with no per-launch MCP form.** A Step can't call blocks; `list_tools` says so in its empty answer (`reason`).

### A.3 Errors (API §9)

| Code | Kind (status) | When |
|---|---|---|
| `SND-TOOL-001` | Invalid (400) | The named block isn't in this run's allowed set (not callable, not listed for the state, or unknown) |
| `SND-TOOL-002` | Invalid (400) | `arguments` don't validate against the block's `inputSchema` (extension `violations`) |
| `SND-TOOL-003` | Forbidden (403) | `execute_tool_call` from a plain session (no `X-Sindri-Run`) |
| `SND-TOOL-404` | NotFound (404) | No open run at `X-Sindri-Run` in this repo |
| `SND-TOOL-409` | Conflict (409) | The same `callId` was sent with different `name` or `arguments` |
| `SND-TOOL-004` | RateLimited (429) | The run's call budget is spent (§B.4) |

A block that runs and fails isn't an error. Its call answers `outcome: "failed"` with the block's reasons, and is
recorded like any other call. A call refused with `SND-TOOL-001`, `-002` or `-004` answers the tool error and, if C4
holds, is still recorded as a `refused` entry with no result (§B.3). The `-003` and `-404` refusals have no run to
record against.

### A.4 Catalog, goldens and the app

- Two command-catalog rows: `ListRunTools` (query) and `ExecuteBlockCall` (issue, caller `session` only; the CLI and
  the human have no verb in v1). The UI lane's pins change: labels, row count, copy blocks.
- API goldens for both routes, and a tools/list golden showing the two tools after `sindri_job`.
- The step report's request (`CompleteStep`, `sindri workflow complete --run`) gains the optional `calls: [<callId>,
  ...]` of §B.3. The API binds and passes it through, and the engine validates it. Its JSON, the CLI's `--call <id>`
  (repeatable) and the request golden change with it.
- The app shows calls in the Step's evidence list (§B.3) with no new screen. The UI lane gets one row there.

### A.5 Proof

API §8's proof shape, with a launched Step session in place of `claude -p`. A recipe with one state lists two callable
blocks. The journey checks:
1. `list_tools` shows exactly those two blocks with their schemas.
2. A bad argument gives `SND-TOOL-002`, with its path.
3. A good call answers `ok`, and its `evidenceSeq` appears in the run's evidence.
4. A retry with the same `callId` runs nothing.
5. A plain session sees an empty list and `SND-TOOL-003`.
6. A block past its timeout answers `running` and finishes through `sindri_job`.

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

1. **How Sindri knows which Step a call belongs to.** The Step's tool config carries the Step's id, readable by
   anything in that session, with no secret. This loosens the 2026-10-16 rule that only the repo header rides MCP.
   *Recommended:* yes. The other way is to send the launch secret over MCP, which the 2026-10-16 decision ruled out.
2. **Tool names.** Exactly `list_tools` and `execute_tool_call`, or `sindri_list_tools` and `sindri_execute_tool_call`
   to match Sindri's other tools. *Recommended:* the `sindri_` names, because some agent CLIs show tools without the
   server's name, and a bare `list_tools` could be mistaken for the agent's own tools.
3. **Sessions outside a Step** (your own Claude Code in the repo). They see no blocks, or they can run read-only
   blocks without recording evidence. *Recommended:* no blocks in v1. Evidence always belongs to a Step.
4. **Failed and refused calls.** A failed block run is always recorded. Is a refused call (wrong name, bad arguments)
   recorded too? *Recommended:* yes, as a short entry with no result, so a session that keeps trying shows in the
   Step's history.

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
