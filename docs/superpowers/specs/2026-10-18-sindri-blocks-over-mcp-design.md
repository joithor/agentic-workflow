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
  first answer and runs nothing again. With no `callId`, the server makes one.
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

*To be written by the engine lane.*

### B.1 Which blocks are callable

### B.2 Running under the Step's policy

### B.3 The call as the Step's evidence

### B.4 Time and budget

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

*Filled in with the engine lane once §B is agreed: an engine phase first (descriptor flag, the recipe's tool list,
run-under-policy, the evidence event), then the API phase (the two tools, the routes, the goldens, the proof), with
the UI's evidence row last.*
