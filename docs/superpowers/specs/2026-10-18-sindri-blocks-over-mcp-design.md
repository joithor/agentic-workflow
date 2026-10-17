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
- **Calls** is the word the app and the docs use for these, everywhere. "Evidence" stays the evidence packs,
  `evidence:` facts and the evidence-provider block kind.
- **Scope:** `run:<WFI-id>:<token>` for a Step session, `repo:<name>` for a plain session. A call id is unique only
  within its scope.

## A. The MCP side (API lane)

### A.1 The two tools

They join API §8's table after `sindri_job` and follow its rules: the repo comes from `X-Sindri-Repo`, every call is
stamped `session`, and errors are tool errors carrying the SND code. Their names are choice C2. The text below uses the
owner's literal names.

| Tool | Arguments | Handler | Kind |
|---|---|---|---|
| `list_tools` | none | `ListCallableBlocks` (also `GET /workflow/instances/{id}/runs/{token}/tools` and `GET /tools?repo=R`, §A.4) | query (`readOnlyHint`) |
| `execute_tool_call` | `name`, `arguments` (an object), `callId?`; plain sessions also `ref?`, `itemId?` (§A.6) | `ExecuteBlockCall`: **MCP only**, no HTTP route (§A.2) | `block-call`, a new session-only action class (§A.4) |

**What the agent is told.**
- The `list_tools` description says: "The Sindri blocks you may run here, with their inputs. Call this before
  `execute_tool_call`."
- The `execute_tool_call` description says: "Run one Sindri block from `list_tools`. A long block answers `running`;
  send the same `callId` again to get its result. Outside a Step, pass `ref` (the commit to run against). Every call is
  recorded."
- A Step's seed (UI §6.2's template) gains one line, but only when the state lists tools: "Sindri blocks you can run
  are listed by `list_tools`."

**`list_tools`** answers `{ scope, run?, tools, reason? }`:
- `scope` is `step` or `plain`, and `run` is the run's address for a Step session.
- `tools` is `[{ name, descriptorHash, description, inputSchema, mode, timeoutSeconds, readsCode }]`, ordered by name.
  - `name` is the block id.
  - `description` is the descriptor's `Description` (§B.1): Sindri's wording, so it isn't fenced.
  - `inputSchema` is a JSON Schema (draft 2020-12, the shape MCP uses for a tool's own input) built from the
    descriptor's `ParameterSpec`s, each with its description, its enum when it has one, and its item type for lists.
    Only the parameters a session may set are in it: pinned values and facts never are (§B.1). An enum whose values
    come from repo config (`gate.tests.green`'s `target`) is filled when the list is made (§B.1).
  - `readsCode` (from the descriptor) says the block runs against a checkout, so a plain call must pass `ref` (§A.6).
  - `mode` is `sync` (the call answers within `timeoutSeconds`) or `async` (the call answers `running` at once).
    `timeoutSeconds` is the block's `MaxDuration` (§B.4). The timing rule below is the only one; §B cites it.
- `reason` is set only when `tools` is empty: `no-tools-listed` (the state lists none), `run-not-open`, `taken-over`
  (C5), `none-callable-in-this-build`, or `no-plain-tools` (the profile's plain set is empty).

**`execute_tool_call`** answers
`{ callId, scope, state, waiting?, commit?, outcome?, result?, reasons?, truncated?, ledgerSeq?, startedAt, durationMs? }`:
- `state` is `running` or `ended`. `outcome` (`ok` or `failed`), `durationMs` and `ledgerSeq` are set once it
  has ended.
  - `waiting` is `heavy-lock` while the call waits for a heavy slot, and absent once it runs.
  - `commit` is the commit (and, for a Step, the worktree's tree hash and a `dirty` flag) the block ran against,
    on every answer of a block that `readsCode`.
  - `ledgerSeq` is the ledger seq of the call's `step.tool-called` event, or `session.tool-called` for a plain
    session (§B.3, §B.5).
  - `result` is the block's answer and `reasons` its failure reasons. Both are scrubbed and capped at 64 KiB
    (`truncated: true` past it), and they are the same body the ledger points to, in the sync answer as in a
    polled one.
- **Checks, in this order, before anything runs:**
  1. Shape: `name` is a string, `arguments` an object, `callId` a UUID if given, `ref` a string and `itemId` an
     item id if given. Otherwise `SND-CONTEXT-022`, as for any MCP tool.
  2. **The stored-call lookup comes first.** A `callId` already stored in this scope is answered from the store
     (below) and nothing else is checked: a poll never spends budget, never meets `-006`, and still works after
     `list_tools` has emptied.
  3. Size: `arguments` are at most 16 KiB of JSON. Otherwise `SND-TOOL-002`.
  4. `name` must be in this session's `list_tools`. Otherwise `SND-TOOL-001`.
  5. `arguments` must validate against its `inputSchema`. Otherwise `SND-TOOL-002`, with each JSON-pointer path and
     message in the error's `violations` (A.3). The validator is built in Sindri from `ParameterSpec`'s closed type
     set; no JSON Schema library is added.
  6. A plain call to a block that `readsCode` must pass `ref`, and `itemId` must be an item of this repo. Otherwise
     `SND-TOOL-002` or `SND-ITEM-404`.
  7. Budget and concurrency: `SND-TOOL-004`, `-005`, `-006`.
- **Timing: one rule** (`mode` comes from the block, never the call):
  - An `async` block answers `running` at once.
  - A `sync` block holds the request while it waits for a heavy slot, at most 30 s, then while it runs, at most
    `timeoutSeconds`. It answers `ended` if it finishes within both.
  - A `sync` call still waiting after 30 s answers `running` with `waiting: heavy-lock`, and the session polls.
  - Any block still running at `timeoutSeconds` after its slot was granted ends `failed`, reason `timeout`.
  - So `mode: sync` means "normally answered in the call": an agent must still handle `running`.
- **One call id, one call, per scope.** The session may pass its own `callId`. With none, the server makes one.
  The store key is (scope, `callId`), so two runs, or a run and a plain session, never see each other's calls.
  - Sending the same `callId` with the same `name`, `arguments`, `ref` and `itemId` again runs nothing. It returns
    the call's current state, so this is how a session polls (no job, no `JOB-` row; jobs don't survive a restart
    and sessions can't start them).
  - The same `callId` with any of those different is `SND-TOOL-409`. The API checks it against the stored call
    before the engine is asked, so the answer doesn't depend on Temporal's Update dedupe.
  - **A refusal doesn't claim the `callId`.** A refused call is recorded under a server-made id, with the sent
    `callId` as a field, so the session can fix its arguments and send the same `callId` again.
  - A call still running when the host stops ends `failed`, reason `host-restart`. A poll after the restart returns
    that, and the session calls again with a new id.
- **Fencing.** Every string in `result` and `reasons` that isn't Sindri's own wording is fenced as `sindri:call`, and
  its object carries `"trust": "untrusted"`, as job results are since P8. The block's own reason codes aren't fenced;
  their free text is. So is a block name the session sent that isn't a listed block, wherever it's shown.

### A.2 Which session a call belongs to

A call has to name its Step run, because the run's policy picks the allowed blocks and the run gets the record.
P8 keeps only `X-Sindri-Repo` of the `X-Sindri-*` headers on MCP, and the launch secret never rides MCP (owner,
2026-10-16).
- **`X-Sindri-Run` (choice C1).** The per-launch MCP config that `ILauncher` writes (LCH: the `mcp` form,
  `{mcpConfig}`) sets `X-Sindri-Run: <WFI-id>:<run token>` for a Step session. BA-1 adds it to the form.
  - `<WFI-id>` is the instance's opaque id (`WFI-` and 32 lower-case hex digits, as every instance route takes), and
    `<run token>` is the decimal run counter. The grammar is `^WFI-[0-9a-f]{32}:[0-9]{1,9}$`, so the value is safe
    to splice into Codex's `-c` overrides.
  - A header that doesn't match the grammar is refused (`SND-CONTEXT-022`), never dropped, so a Step session can't
    silently turn into a plain one.
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
- **The per-launch server isn't confused with a repo-local one.** A repo where the owner ran `sindri mcp register`
  already has a server named `sindri` (with no run header). The per-launch config names its server `sindri-step`, and
  the Step's rendered policy denies `mcp__sindri__*`, so a Step session can't call blocks as a plain session by
  mistake. There is no `--strict-mcp-config`: the session keeps every other MCP server it has (serena, github and the
  rest). Codex's `-c` overrides name `mcp_servers.sindri-step` and set `mcp_servers.sindri.enabled=false`. Sindri never
  registers a `sindri` server for Codex (API §8), and doctor warns on a user-wide one. The proof checks the Claude case
  (A.5 step 7).
- **A session with no `X-Sindri-Run` is a plain session** (§A.6).
- **An agent with no per-launch MCP form** (`mcp: none`) launches with no Sindri MCP at all (API §8), so it can't call
  blocks; it is never a plain session, and the seed line is left out.

### A.3 Errors (API §9)

| Code | Kind (status) | When | Message (fix) |
|---|---|---|---|
| `SND-TOOL-001` | Invalid (400) | The named block isn't in this session's `list_tools` | "`<name>` isn't a block you can run here (fix: call `list_tools`)" |
| `SND-TOOL-002` | Invalid (400) | `arguments` are over 16 KiB, or don't validate against the block's `inputSchema`; extension `violations: [{path, message}]` | "The arguments for `<name>` don't fit its inputs (fix: see `violations`)" |
| `SND-TOOL-004` | Forbidden (403) | This run's call budget is spent (`workflow.calls.perRun`, §B.4). It doesn't refill within the run | "This Step has used its `<n>` calls (fix: finish the Step, or raise `workflow.calls.perRun` in the profile)" |
| `SND-TOOL-005` | RateLimited (429) | The repo's plain-call cap for today is spent (`workflow.calls.plainPerDay`, §A.6) | "`<repo>` has used today's `<n>` plain calls (fix: wait for the local day to roll over, or raise the cap)" |
| `SND-TOOL-006` | Conflict (409) | A call is already running for this run, or the repo already runs `workflow.calls.plainConcurrent` plain calls (default 2), §B.4 | "Another call is still running (fix: wait for it to end, then call again; your own running call is polled with its `callId`)" |
| `SND-TOOL-404` | NotFound (404) | No open agent run at `X-Sindri-Run` in this repo | "No open Step run `<run>` in `<repo>` (fix: this Step has ended; there is nothing left to call or report)" |
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
  own records (`*.tool-started`, `*.tool-called`) and never a transition. The authorization table (§4.2)
  gives it to `session` only. A human runs a block through a recipe; the CLI only lists.
  API §4.1, §4.2 and ARCH-CQRS-001 are amended to match.
- **Catalog rows:** `ListCallableBlocks` (query) and `ExecuteBlockCall` (`block-call`). The UI lane's pins change:
  labels, the row count and copy blocks.
- **Read routes** (queries, all from the one `block_calls` store, §B.2):
  - `GET /workflow/instances/{id}/runs/{token}/tools` (a Step's list) and `GET /tools?repo=R` (the plain set);
  - `GET /workflow/instances/{id}/runs/{token}/calls` (the run's calls, running ones included, each with `cited`);
  - `GET /calls?repo=R[&item=ITM-…][&after=seq]` (plain calls);
  - `GET /calls/{scope}/{callId}/result` (one call's body, fenced as in the answer).
  The app refreshes on `step.tool-started`/`session.tool-started` and `*.tool-called` (ids and the block only, §B.3),
  so a running call shows as soon as it starts.
- **CLI:** `sindri workflow tools <address> [--run N] [--json]`, `sindri tools [--repo R] [--json]`, `sindri calls
  [--repo R] [--item I] [--json]`, and `sindri recipe tools <file> --state S [--json]`, which runs in process (like
  `recipe validate`), so a block author checks a recipe's tools before anything is launched.
- **Step report:** `CompleteStep` (`sindri workflow complete --run`) gains the optional `calls: [<callId>, ...]` of
  §B.3. The API binds it and passes it through; the engine validates it. The CLI flag is `--call <id>`
  (repeatable). The request golden changes.
- **Goldens:** every read route, the `list_tools` answer for each scope and reason, the `execute_tool_call` answer
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
    for the state (`waiting`, `running`, `ok`, `failed`, `refused`), the block, the duration, the failure reason in
    words (`timed out`, `ended with the Step`, `Sindri restarted`, `taken over`), and, once the Step has reported,
    `cited` or `uncited`; before that, `not yet reported`. A call made during a takeover says `during takeover`.
    Colour is never the only signal.
  - A running call shows as soon as it starts, from the run's calls route.
  - A row opens the call's result in the read-only result view, rendered as untrusted text (the fence's rule), with
    `truncated` said in words.
  - Refusals show their code's message and the fenced name the session sent. The refusals-capped marker is one
    row. Refusals get no Timeline line.
  - The list says "No calls" when there are none.
  - **A Timeline line** per ended call: "call `<block>` · `<outcome>` · `<duration>`".
  - **Keyboard and focus:** the list is in the sheet's tab order, and Enter opens a row. A row that appears or
    changes while the sheet is open never moves focus. The live region announces only an ended `ok` or `failed`
    call ("call `<block>` ended `<outcome>`"), and refusals at most once per 10 s as "`<n>` calls refused" (UI §5).
  - **Plain calls** have no Step to sit under. In v1 they show on the linked item's Timeline as "plain call, not
    proof" when the call passed an `itemId`, and in `sindri calls`. A repo-wide list waits for the Repos screen,
    which isn't served yet.

### A.5 Proof

API §8's proof shape, with a launched Step session in place of `claude -p`. A recipe with one state lists two
callable blocks, one `sync` and one `async`. The journey:
1. `list_tools` shows exactly those two, with their descriptions, schemas and modes.
2. A bad argument gets `SND-TOOL-002`, and the agent sees its `violations` path.
3. A good `sync` call answers `ended`/`ok`, and its `evidenceSeq` is in `workflow events`.
4. The same `callId` again runs nothing (the block's own counter is unchanged), and the same `callId` with other
   arguments gets `SND-TOOL-409`.
5. The `async` call answers `running`, the run's calls route lists it running, and polling the same `callId` ends it.
   (BU-1's proof shows the same in the app.)
6. A step report citing both call ids is accepted, and one citing an unknown id is refused.
7. With a repo-local `sindri` server registered too, the Step session reaches `sindri-step` (`list_tools` says
   `scope: step`), its `mcp__sindri__*` tools are denied, and serena is still listed among its MCP servers.
8. A plain session in the same repo (no run header) gets the plain set (`scope: plain`). A call without `ref` to a
   `readsCode` block is refused; with `ref` it runs, answers the `commit`, and is a `session.tool-called` event on
   the item's Timeline it names, not on the Step.
9. With `workflow.calls.plainPerDay: 1`, a second plain call gets `SND-TOOL-005`, and the refusal is recorded.

### A.6 Calls outside a Step (owner, 2026-10-18)

The owner's own sessions may call blocks: "my own sessions should be able to use sindri block tools". The engine
side is §B.5.
- **Bound to the repo only.** No run header. MCP is stateless and Claude Code sends no session id, so "the session"
  is just `caller: session` plus the repo: the cap and the record are per repo.
- **`list_tools` for a plain session** is the repo's plain set (§B.5, choice C6). `scope: plain`.
- **`execute_tool_call`** takes an optional `itemId` and `ref`:
  - `itemId`: the item the call is about. The call shows on its Timeline as "plain call, not proof" (choice C8).
  - `ref`: the commit the block runs against, in a scratch checkout Sindri makes and removes (choice C7). It is
    required for a block that `readsCode`, because the registered repo's HEAD is rarely what a session in a lane
    worktree is working on. It must resolve in the registered repo, else `SND-TOOL-002`, and the answer names the
    resolved `commit`. A session gets it with `git rev-parse HEAD` after committing.
- **Cap:** `workflow.calls.plainPerDay` per repo (default 200), then `SND-TOOL-005`. At most
  `workflow.calls.plainConcurrent` (default 2) plain calls run at once per repo, then `SND-TOOL-006`.
- **Record:** `session.tool-called` on the repo, kept 30 days (§B.5). It never satisfies a gate and is never citable
  in a step report.

## B. The engine side (engine lane)

A call never moves the instance. It runs one block for the session and records what happened, and the Step's own
report and the recipe's gates still decide the transition (ENG §6.5: "the session reports; the engine decides").

### B.1 Which blocks are callable

- **The descriptor opts in.** `BlockDescriptor` gains three fields, each written only when set, so every existing
  descriptor hashes as before (like `HumanOnly`):
  - `Callable` (`bool?`).
  - `Description` (`string?`), Sindri's own one-line text, which `list_tools` serves (§A.1).
  - `MaxDuration`, already added by DT-J2.
- **`ParameterSpec` gains** these fields (R1-05, R1-09), also written only when set:
  - `Description`;
  - `Enum` (the allowed literals);
  - `ItemType` (for a list);
  - `SessionSettable` (`bool?`): the session may set this parameter per call. A parameter without it never appears
    in `inputSchema`.
- **The catalog test** `Every_callable_block_is_described_bounded_and_read_only` requires, for every Callable block:
  - its kind is `gate` or `evidenceProvider` (a `step`, `effect` or `stepInput` block is never callable);
  - no `Effects`, `WriteScope` or `AutonomyTier` capability;
  - a `Description` and a `MaxDuration`;
  - a `Description` on each session-settable parameter;
  - no session-settable parameter that is a capability field.

  A block pack that breaks any of these fails its own build.
- **What the session may set (R1-09).** `inputSchema` (§A.1) is built from the session-settable parameters only.
  The following never come from a call; the recipe pins them, or the block's defaults supply them:
  - capability fields;
  - fact inputs (`{ fact: ... }`);
  - every other parameter.

  A session-settable `path` parameter resolves inside the call's worktree. An absolute path, a `..` that leaves the
  worktree, or a symlink out of it is refused with `SND-TOOL-002`. A call can't widen what the Step reaches.
- **The recipe lists them per state.** An active state gains `tools:`. Each entry is a block id (`- gate.tests.green`)
  or `{ block: <id>, with: { ... } }`, which pins parameters. A pinned value is a literal, a `{ fact }` or a
  `{ profile }`, checked like a Step's `with:`.
  - `tools:` is written only when non-empty, so every approved recipe without it keeps its hash (R1-19).
  - With it, the recipe hash covers it, approval pins it, and `recipe show` and the approval preview print it.
  - There is no recipe-level default list in v1 (R1-31): each state says what its session may call.
- **The validator checks the list** (new rows in `docs/csharp/sindri-workflow.md`, after `RCP-TIME-003`):

| Code | Error when |
|---|---|
| `RCP-TOOL-001` | A `tools:` entry is not a registered block, or its descriptor is not `Callable` |
| `RCP-TOOL-002` | `tools:` is on a state whose Step can't run in an agent session: a wait or terminal state, a `humanOnly` Step, or a Step with only the block or desk runtime |
| `RCP-TOOL-003` | A listed block needs a capability (`Network`, `Budget`, `Tools`) that the state's own Step doesn't hold. A call runs with the Step's capabilities, never wider (owner, 2026-10-17: a Step only tightens) |
| `RCP-TOOL-004` | A required parameter that isn't session-settable has no pinned value and no default, or a pin names a parameter the block doesn't have, or a pin sets a capability field to anything but a literal or `{ profile }` |

- **`list_tools` is computed, not stored.** For an open agent run, it is the state's `tools:` entries whose block is
  Callable in the running build, ordered by id.
  - Each entry carries `mode`: `sync` when `MaxDuration` ≤ 30 s, otherwise `async` (§A.1).
  - A block named in an approved recipe but gone from the build drops out. Calling it is `SND-TOOL-001`, and the
    approval drift check (ENG §5.8) reports it.
- **The first callable block (R1-01)** is `gate.tests.green`, new in BM-1.
  - It runs one test target that the repo declares in `.agentic-workflow/checks.json` (per-repo config: the
    command, its working directory, and a filter template). Its pass or fail is its outcome.
  - Its only session-settable parameters are `target` (an `Enum` of the declared targets) and `filter` (a string
    passed to the filter template as one argument, never through a shell).
  - The command itself is never session-set.
  - Capabilities: `Tools`. `MaxDuration`: 20 min, so it is `async`.
  - The first shipped state that lists it is `workflow.implement`'s `implement` state, as `tools: [gate.tests.green]`.
    This changes that recipe's hash before it is approved.

  `evidence.check` and the two shipped gates stay non-callable: they read engine state, so they mean nothing called
  from a session.

### B.2 Running a call

- **The call store.** One table, `block_calls`, in the Sindri database (a migration in BM-1), owned by an
  `IBlockCallStore` port in Core with a SQLite implementation.
  - **Columns:** `callId`, `scope` (`run:<instance>/<run>` or `repo:<repo>`), `block`, `descriptorHash`,
    `argumentsSha256`, `state` (`running` | `ok` | `failed` | `refused`), `reason`, `resultSha256`, `resultBytes`,
    `started`, `ended`, `duringTakeover`.
  - **What reads it:**
    - API dedupe and `SND-TOOL-409` (R1-14);
    - the same-`callId` poll (R1-07);
    - the in-flight calls the app lists (R1-28);
    - the single-flight check (R1-15).
  - **Bodies** go to the content store, scrubbed (B.3):
    - the arguments under `call:<run>:<callId>:arguments`;
    - the result under `call:<run>:<callId>`.

  History holds only ids and hashes, never a body (R1-18).
- **The path of a Step call:**
  1. The API checks `SND-TOOL-409` against the store.
  2. The API writes the arguments body, then sends the `block-call` Update to the instance's `RecipeWorkflow`. The
     Update carries `{ callId, block, argumentsSha256 }` only. Because the store holds the call, a retry is served
     from the store, not from the Update id, so dedupe holds across Continue-As-New (R1-14).
  3. The Update's validator refuses before anything enters history. Each refusal is a typed result that the API maps
     to §A.3, so no new `SND-WF` code is needed:
     - the run is not the instance's open attempt, or its Step isn't on the agent runtime: `SND-TOOL-404`;
     - the block is not in `list_tools`: `SND-TOOL-001`;
     - another call of this run is `running`: `SND-TOOL-006`, busy (R1-15: one call in flight per run);
     - the budget is spent: `SND-TOOL-004`.

     Schema validation (`SND-TOOL-002`) is the API's, before the Update.
  4. The handler marks the row `running`, through the `MarkCallStarted` local activity.
  5. The handler runs one `RunBlockCall` activity: one attempt, no retries, with a heartbeat. It then appends the
     evidence event (B.3) and marks the row ended.
  6. The API answers the call: `state: ended` with its `outcome` when the block ends within 30 s. Otherwise it answers
     `state: running`, and the session polls with the same `callId` (§A.1).
- **Restarts (R1-13).** A single-attempt activity is not resumed. When the host restarts, the activity's heartbeat
  times out and it ends `failed` with reason `host-restart`. On startup, a sweep also ends every `running` row the
  workflow no longer owns that way. The session calls again with a new `callId`.
- **The heavy lock (R1-15).**
  - A block that builds or tests takes a heavy slot at the low priority (`AW_HEAVY_PRIORITY=call`), below the
    transition gates and meta's gates.
  - `MaxDuration` starts when the slot is granted, so time spent waiting for a slot is not run time. The poll
    answers `running` with `waiting: heavy-lock` while it waits.
  - With one call in flight per run, a run holds at most one slot for calls.
- **The policy and the environment (R1-29).** The call's `BlockContext` carries:
  - the run's StepPolicy (the ILauncher contract; a callable block never writes, so `Mode` matters only for a future
    kind that does);
  - the Step's capabilities;
  - the run's repo and worktree;
  - the approved profile.

  The block runs as the host, in the run's own worktree. Its process environment is a fixed allowlist, never the
  host's or the session's:
  - `PATH`, `LANG`;
  - a scratch `TMPDIR` and `HOME` under the call's directory;
  - `DOTNET_ROOT`, `DOTNET_CLI_HOME` and `NUGET_PACKAGES` pointing at the shared caches (never a fresh cache);
  - the names the repo's `checks.json` lists.

  It never carries the launch secret, a GitHub token or a provider key.
- **Takeover (R1-10, owner choice C5).**
  - *If C5 is yes (recommended):* a call during a takeover runs, and is recorded with `duringTakeover: true`. A step
    report can't cite it (B.3). A call in flight when the takeover starts keeps running.
  - *If C5 is no:* the validator refuses with `SND-AUTH-003`, and a call in flight is cancelled through its
    heartbeat and ends `failed` with reason `takeover`.

  Either way, the engine can do it.
- **How a call ends early.** A call in flight when its run ends (step report, the state's timeout, `session.ended`,
  cancel) is cancelled through its heartbeat and ends `failed` with reason `run-ended`.
- **A called gate does not stand in for a transition's gate (R1-12).** v1 runs the transition's gates itself, so a
  green `gate.tests.green` call followed by the same gate on the transition runs the tests twice. That is by design:
  the engine decides from its own runs.

  A later change may reuse a call when the descriptor hash, the arguments hash and the worktree's commit all match.
  It is listed under follow-ups, and is not in v1.

### B.3 The call as the Step's evidence

- **One ledger event per call: `step.tool-called`,** appended by the handler after the call ends. Its fields:
  - `instance`, `run`, `callId`, `block`;
  - `descriptorHash` (R1-35; the running build's);
  - `argumentsSha256`;
  - `outcome` (`ok` | `failed` | `refused`) and `reason` (null-omitted);
  - `resultBytes`, `resultSha256`, `truncated`;
  - `durationMs`, `lockWaitMs`;
  - `duringTakeover` (null-omitted);
  - `launch` and `client` (R1-39: the run's launch id, and the MCP client name the API stamps).

  `evidenceSeq` (§A.1) is this event's seq. The event never triggers a transition.
- **Example lines (R1-36), as `workflow events` prints them:**
  - `tool-called gate.tests.green · ok · 4m 12s (waited 40s for a heavy slot)`
  - `tool-called gate.tests.green · failed · 1m 3s · 3 tests failed`
  - `tool-called gate.tests.green · refused · SND-TOOL-002 arguments: /filter must be a string`
  - `tool-refusals-capped · 50 refused calls recorded; later ones are not`
- **The result body (R1-38).**
  1. It is scrubbed by `Aw.Redaction` first.
  2. Then it is cut to 64 KiB. A longer one keeps its first 64 KiB and is marked `truncated`.
  3. It is stored in the content store. The order (scrub, then cut) means a secret split across the cut is still
     removed.

  A failed call's body is the block's reasons.
- **Refused calls (C4).** The API records a call refused with `SND-TOOL-001`, `-002`, `-004`, `-005` or `-006`. It writes a
  `refused` row and a `step.tool-called` event, with no result, through the `IBlockCallLog.RecordRefused` port (R1-14).
  - That is a Features service the API calls directly. It is not the `RecordEvent` activity.
  - Refused calls don't count against the call budget. They have their own cap per run, equal to the budget (B.4).
    Past it, one `step.tool-refusals-capped` event marks the cut.
  - `SND-TOOL-404` refusals have no run. They go to the structured log only (`Aw.Kernel.Telemetry`, at the warning
    level, scrubbed; R1-39), never to the ledger.
- **Using the evidence.**
  - A step report may cite calls in `calls: [<callId>, ...]` (§A.4). Validation (ENG §5.5) refuses an id that isn't
    a recorded `ok` or `failed` call of this run, or that is `duringTakeover`.
  - The run's evidence list, which feeds the app's row (§A.4), the Judge's facts for a later route (DT §8.3) and
    `workflow why` (DT-J3), lists every call of the run in seq order. Calls the report didn't cite are marked
    uncited, so a report that leaves out a failing call can be seen.
- **Telemetry (R1-23).**
  - One span per call, `sindri.block_call`, with the attributes `block`, `scope` (`run` | `repo`), `outcome` and
    `reason`.
  - The counters `sindri.block_calls` (by `outcome` and `scope`) and `sindri.block_call_refusals` (by code).
  - The histograms `sindri.block_call.duration` and `sindri.block_call.lock_wait`.
  - All through `Aw.Kernel.Telemetry`, exporting off by default.

### B.4 Time and budget

- **Sync or async is the block's own.** A block whose `MaxDuration` is 30 s or less is answered in the call.
  Otherwise the call answers `running`, and the session polls with the same `callId` (§A.1). No P6 job is involved:
  jobs don't survive a restart, and a session can't start one (R1-07). Every callable block is bounded, which the
  catalog test enforces (B.1).
- **The per-run call budget** is `workflow.calls.perRun` (default 50, with a row in `docs/csharp/sindri-profile.md`).
  Past it, a call is refused with `SND-TOOL-004`.
  - A block with the `Budget` capability also spends the run's ENG §3.7 budget. An over-budget run parks with
    `budget` after the call ends, never in the middle of one.
- **The state's clock keeps running (R1-33).** Calls don't pause the state's `timeout`, so a long call can use up the
  Step's time. That is by design: the recipe's timeout bounds the whole Step, calls included.

### B.5 Calls outside a Step

The owner decided (2026-10-18, C3) that their own sessions, outside any Step, may use the block tools as well. A call
like this has no instance and no run. It never moves anything, and it is never proof for a work item.

- **Which blocks.**
  - By default, a plain session gets the **plain set**: every Callable block whose capabilities are within
    `{ Tools }`.
  - `workflow.tools.plain: [<block id> | { block, with }]`, at box level or under a `repos[]` entry, replaces that
    default. A repo's list wins over the box's, as for `workflow.judge.providers`. `ProfileRules` checks each entry,
    as RCP-TOOL-001 and RCP-TOOL-004 do.
  - A block with `Network` or `Budget` is in the plain set only when the owner lists it by name. A Step session never
    inherits the plain set.
  - The default is owner choice C6.
- **Where it runs.** In a fresh detached worktree that Sindri makes, at the commit the caller names (`ref`; by
  default the registered repo's `HEAD`), removed when the call ends. It never runs in the shared checkout or in the
  session's own folder (C7).
  - The policy is fixed: `Mode` readOnly, the worktree as the only writable path, and the approved profile.
  - B.2's environment allowlist and heavy-lock rules apply unchanged.
- **Who runs it.** `BlockCallRunner`, a hosted service in the Sindri host. It is a bounded queue, one call at a time
  per repo (`SND-TOOL-006` when busy), running the same `RunBlockCall` body as B.2 with a `BlockContext` that has no
  instance or run.
  - The calls live in `block_calls` with `scope: repo:<repo>`. The content keys are `call:<repo>:<callId>` and
    `call:<repo>:<callId>:arguments`.
  - Sync, poll, `SND-TOOL-409` and the restart sweep work as in B.2. On a restart, a `running` row ends `failed`
    with reason `host-restart`. No Temporal and no P6 job are involved.
- **How it is recorded.** One `session.tool-called` event on the repo's stream. It has B.3's fields without
  `instance`, `run` and `launch`, plus `caller: session` and the `ref`'s commit sha.
  - The bodies are kept 30 days (C8).
  - Refused calls are recorded the same way (C4).
  - There is no session id on MCP, so calls are counted and shown per repo: in `sindri ledger --type
    session.tool-called` and in the repo's activity in the app (BU-1).
- **Linking to an item.** With an optional `itemId`, the call also shows on that item's timeline as "session call,
  not proof". It never satisfies a gate, a step report's `calls:` can't cite it, and it is not a fact (C8).
- **Budget.** `workflow.calls.plainPerDay` per repo (default 200). Past it, a call is `SND-TOOL-005`. Refused
  entries have their own cap, as in B.3.

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
7. **What your own session's block runs against.** A commit your session names, in a scratch copy Sindri makes and
   throws away, or your session's own folder, uncommitted changes included? *Recommended:* the named commit in a
   scratch copy. A test run writes build output, and the scratch copy keeps that out of your working folder and the
   shared checkout. The session commits first and passes the commit, and every answer says which commit ran. Sindri
   can't see which folder your session is in, so "your own folder" would need the session to send its path.
8. **What your own session's calls count for.** *Recommended:* they're logged (repo, block, outcome, result) and
   kept 30 days, but they never count as proof for a work item or a Step, even when they name an item. Later, if
   you want, a Step could adopt one as proof (not in v1).

## D. Phases

Contract first, so the two lanes build in parallel.

| Phase | Lane | Holds | Waits on | Docs it updates |
|---|---|---|---|---|
| **BM-0** | engine | The contract, red: `Callable`, `Description` and the `ParameterSpec` fields; `tools:` with the RCP-TOOL-001..004 fixtures; `IBlockCallStore`, `IBlockCallLog`, the request and response records for `ListRunTools` and `ExecuteBlockCall`, with stub services; the `step.tool-called` and `session.tool-called` records; the typed refusals | P8 (the agent runtime), API P10a #295 (the per-launch MCP config with `X-Sindri-Run`), DT-J2 (`MaxDuration`) | none |
| **BM-1** | engine | The validator rows; the catalog test; `gate.tests.green` and `checks.json`; `list_tools`; the `block_calls` migration; the `block-call` Update, `MarkCallStarted` and `RunBlockCall`; the restart sweep; the heavy-lock priority; the environment allowlist; the evidence events, bodies and telemetry; the budget keys; `calls:` validation; `BlockCallRunner` for plain calls; `workflow.implement`'s `tools:`. Tests: unit (filtering, refusals, schema building, path confinement); integration (an Update through the time-skipping server, single flight, cancel on run end, takeover per C5, the restart sweep, a plain call in a scratch worktree) | BM-0 | `docs/csharp/sindri-workflow.md` (`tools:`, RCP-TOOL rows, a "Block calls" section), `docs/csharp/sindri-profile.md` (three keys), `docs/csharp/sindri.md` (the two events), a `checks.json` reference in `docs/csharp/` |
| **BA-1** | api | §A: the two MCP tools, the routes, the header, schema validation, the 409 check, the refusal recording, the catalog rows, the goldens. Built against BM-0's records and stubs, so it starts the day BM-0 lands | BM-0 | §A's list |
| **BA-2** | api, with engine | §A.5's proof end to end, with a real launched Step session and a plain session | BM-1, BA-1 | the proof log |
| **BU-1** | ui | The calls on a Step's evidence list (cited, uncited, in flight) and in the repo's activity | BA-1's goldens | `docs/csharp/sindri-ui.md` |

BM-1 and BA-1 run at the same time, each lane with its own builders. Only BA-2 needs both.

**Follow-ups, not in v1:** reusing a call for a transition's gate on an exact match (R1-12); a recipe-level default
`tools:` (R1-31); per-session attribution for Sindri-launched sessions (`X-Sindri-Launch`).
