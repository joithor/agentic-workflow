# Sindri workflow engine: recipes, blocks, validator and engine

**Status:** design, awaiting approval · **Date:** 2026-10-10 · **Builds:** factory addendum §17.28 (one workflow
state machine), §17.29.4 step 2 · **Next:** `/specToProvenPR` takes the Phases at the end to PRs.

This doc makes §17.28 concrete. It doesn't reopen §17.28 or §17.29, which are settled (decisions Q6, Q7). It
settles the six open questions: the block contract, the recipe schema, approval and invariant 13, the instance
row and the transition event, the Core/port boundary with S1's app shell, and the first recipes.

References: the factory addendum `2026-10-09-sindri-factory-addendum.md` (A), the core design
`2026-10-07-sindri-design.md` (D), `docs/csharp/bugfix.md` (the reference for gates and state),
`.agents/rules/csharp.md` and `csharp-testing.md`. S1 is the app shell on `feat/sindri-s1-app-shell`; its ports
are named here as they are on that branch (`ILedgerStore`, `ILease`, `LeaseGrant`, `LeaseDecision.Holds`,
`IScheduledService`, `ScheduledRun`, `SindriErrors`).

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| W1 | **Each block is a C# class with a kind interface (`IGate`, `IEvidenceProvider`, `IStep`, `IEffectKind`) plus a static, data-only `BlockDescriptor`.** Descriptors are registered explicitly into a `BlockCatalog` (no scanning). A separate runtime map goes from block id to implementation | The validator runs on descriptors alone, so it is pure Core and tests in the unit lane with no block code loaded. Explicit registration is AOT-safe and follows the C# DI rule. YAML manifests would drift from the code; a source generator is too much machinery for about 20 blocks |
| W2 | **Wiring is checked on every path.** A block's input must be produced on every path into the transition that uses it, by an earlier transition's `produces`, a Step's declared outputs, or the recipe's `inputs` | A fact produced on only some paths is a latent runtime refusal. Checking every path turns it into a validation error |
| W3 | **Recipe schema v1 is closed** (§3). It has three state kinds with their own fields, a closed `when` grammar, and engine-owned universal states | Generated recipes (§17.7) must not be able to slip logic in. A free expression language can't be checked statically |
| W4 | **The rung comes from the block's descriptor, never the recipe.** A recipe sets a `threshold` only on rung-2 and rung-3 gates | A recipe can't promote a judge question to "deterministic" |
| W5 | **Fail closed.** The validator refuses a recipe that names an unknown block. At run time, an unavailable gate or evidence runtime refuses the transition (`workflow.refused`, `SND-WF-010`). An unavailable Step runtime parks the instance at once with `needs-answer` and `SND-WF-010`, because nothing will ever complete that state. Nothing passes by default | Borrowed from `addyosmani/factory`'s `gates.sh` (`MISCONFIGURED`, exit 2). A missing check that passes silently is the worst failure a gate can have |
| W6 | **Terminal states declare an `outcome`**, one of `resolved`, `unresolved`, `alreadySatisfied` or `failed` | Receipts and metrics count outcomes, not state names. Borrowed from loki-mode's closed outcome set. For bugfix, `alreadySatisfied` is a baseline that doesn't reproduce |
| W7 | **A human answer to a `parked(rounds\|no-progress)` may carry `grant: n`**, recorded in the event. Without a grant, the counter is unchanged and the instance parks again | Borrowed from robi42/factory, where a note buys one more round. The grant is explicit and auditable, never implied |
| W8 | **Every recipe needs a human approval to run.** Approval lives in a store keyed by `(id, version, hash)`, never in the file | §17.19 lists workflow templates as capability fields, so they are approval-tier. A file can't approve itself |
| W9 | **Hand-written recipes are approved with `sindri recipe approve`**, a typed confirmation at an interactive terminal. **Generated recipes are approved by the packet freeze**, and must name an approved `base: <id>@<hash>` that Sindri's generator sets | Freezing is already a desk action (invariant 13). Provenance comes from how a recipe entered, not from a field a model could write; under the threat model (A §17.28.3) that is enough |
| W10 | **`RCP-CAP-001` to `003`:** a generated recipe's capability-bearing uses must be a subset of its base's; its capability `with:` parameters must equal the base's (narrowing counts as a change); an unapproved recipe never starts | Invariant 13: models never grant capabilities. Refusing narrowing keeps the rule exact. To narrow, a human edits the base |
| W11 | **Approval is re-checked at instance start and at effect dispatch.** `recipe revoke` stops new starts; running instances park with `spec-conflict` at their next transition; pending effects are refused at dispatch | A 22 D10: the gate re-checks every outbox command at dispatch |
| W12 | **The idempotency key is `wf:{instance}:{transition}:{sha}:{fromVersion}`** | The spec's `(recipe, item, attempt, transition, sha)` collides when a loop legitimately repeats a transition at the same SHA (a retry after a timeout; `evaluate → fix` with an empty diff). `fromVersion` tells the rounds apart. A replay or a retried command carries the same `fromVersion`, so it is still a no-op. The instance id is derived from `(recipe id, item, attempt, parent)` |
| W13 | **One port commits a transition: `IWorkflowStore.Commit(TransitionCommit, LeaseGrant)`.** In one SQLite transaction it checks the lease epoch, compares and sets the instance version, inserts the ledger event, and writes the instance row, its deadlines, its outbox commands and any engine-inbox rows | A 17.28.1: the event, the new version and the outbox commit together |
| W14 | **Timers are deadlines on the instance row**, applied by a `workflow.timers` scheduled service every 15 s through the same `Commit`. After a sleep gap larger than the interval, armed deadlines move later by the gap (a `workflow.deadlines.shifted` event) | Replayable and never a hidden side effect (A 17.28.1). D §9.2's sleep rule, applied to deadlines, so a laptop that slept doesn't time out work that never ran |
| W15 | **A takeover pauses deadlines** (it stores `remaining`, and `handback` re-arms with it). **A park stops them**, and the answer re-arms a fresh, full timeout | A takeover is a pause (D §9.3). An answer to a park starts a new round of effort |
| W16 | **Child-to-parent propagation goes through an engine inbox** written in the child's transaction and drained into a separate parent `Commit` | "As an event, never as shared state" (A 17.28.1). Each commit checks exactly one instance's version |
| W17 | **The engine lives in `Aw.Sindri.Core/Workflow` (pure) and its Sindri Features and Infrastructure.** Phase 1 is a **stacked PR on `feat/sindri-s1-app-shell`**, built in its own worktree off that branch | This keeps A 17.28.3's placement. Phase 1 builds before S1 merges, and merges after it |
| W18 | **The first recipes are `workflow.item` and `workflow.step`** (§6), with the Step runtimes failing closed until `ILauncher` exists | A 17.28.6 step 1. The item lifecycle exercises waits, effects, external events, retries and a child from the first recipe |
| W19 | **JSON Schemas are generated from the C# records** into `schemas/recipes/`, and a test fails when they're stale | The `aw-packets` precedent (`docs/csharp/packets.md`): one source of truth, with the schema for editors |

## 2. The block contract

```csharp
public enum BlockKind { Gate, EvidenceProvider, Step, Effect }

[Flags]
public enum Capability { None = 0, Tools = 1, Effects = 2, AutonomyTier = 4, WriteScope = 8, Network = 16, Budget = 32 }

public enum Rung { Deterministic = 1, JudgeQuestion = 2, BoundedModel = 3 }

public sealed record BlockDescriptor(
    BlockId Id,                                   // gate.baseline.fails
    BlockKind Kind,
    int Version,
    IReadOnlyList<FactKey> Inputs,                // facts it reads
    IReadOnlyList<FactKey> Outputs,               // facts it records (Steps, evidence providers)
    IReadOnlyList<OutcomeName> Outcomes,          // the names `when: { outcome }` may use (Steps, gates)
    Capability Capabilities,
    IReadOnlyList<ParameterSpec> Parameters,      // the `with:` parameters it accepts
    Rung? Rung,                                   // gates only
    bool ThresholdApplies);                       // gates on rung 2 or 3 only

public sealed record ParameterSpec(string Name, ParameterType Type, bool Required, bool IsCapabilityField);
```

- **Capability-bearing:** a block whose `Capabilities` isn't `None`, or a `with:` parameter whose `IsCapabilityField`
  is true (for example a glob allowlist). Effect blocks always carry `Effects`.
- **Runtime interfaces** (Features). Each returns a `Result` and takes the instance's read-only view, never the store:
  - `IGate.Evaluate(GateContext) → GateResult(verdict pass|fail|uncertain, score?, reasons)`;
  - `IEvidenceProvider` follows A 17.11 (`plan`, `run`, `verify`) and returns a `Result`;
  - `IStep.Run(StepContext) → StepResult(outcome, outputs, spent)`;
  - `IEffectKind.Build(EffectContext) → OutboxCommand payload`. The effect itself runs in the dispatcher, never here.
- **Registration:** each project's `Add…()` calls `catalog.Register(descriptor)` and `runtime.Register(id, factory)`.
  An `Aw.Sindri.Testing` contract base, `BlockContract`, runs each block's own contract tests, once for every recipe
  that uses it (A 17.28.5 "per block").
- **The wiring check (W2)** computes, for each transition, the facts available on every path from `initial`. These
  are recipe `inputs`, plus the `produces` and Step outputs that dominate the transition, intersected over all
  incoming paths. A gate, evidence or effect input outside that set is `RCP-WIRE-001`.

## 3. Recipe schema v1

Unknown fields are refused everywhere (`additionalProperties: false` in the schema; `JsonUnmappedMemberHandling.Disallow`
in the reader). YAML follows the 1.2 core schema (S1's `YamlToJson`), so the key `on` stays a string. A test pins this.

### 3.1 Top level

| Field | Type | Req. | Rule |
|---|---|---|---|
| `schema` | int | yes | `1`; any other version is refused |
| `id` | string | yes | `workflow.<name>`, lowercase, dots and dashes |
| `version` | int ≥ 1 | yes | Rises with each registered change to this `id` |
| `description` | string | no | |
| `base` | `<id>@sha256:<hex>` | generated only | Set by Sindri's generator; a hand-written recipe with `base` is `RCP-CAP-004` |
| `inputs` | list of fact keys | no | The roots of the wiring check |
| `initial` | state name | yes | An `active` or `wait` state |
| `states` | map name → state | yes | At least one non-terminal and one terminal state; reserved names are refused |
| `transitions` | list | yes | |
| `loops` | map name → loop | no | |
| `budget` | `{ tokens?, turns? }` | no | Covers the whole instance |
| `evals` | list of paths | to approve | Replayed event logs with expected states and outcomes (A 17.28.2 item 5); not required for a draft |
| `hash` | `sha256:<hex>` | no | Checked when present |

Reserved state names: `cancelled`, `escalated`, `parked`, `taken-over`.

### 3.2 Value grammars

| Grammar | Form |
|---|---|
| Fact key | `fact:<name>` or `evidence:<kind>@<base\|head>` |
| Block id | `<gate\|step\|effect\|evidence>.<area>.<name>` |
| Duration | `<n>m`, `<n>h` or `<n>d`, at least `1m` |
| Tokens | int, or with a `k`/`M` suffix |
| `with:` value | string, number, bool, a list of these, or `{ fact: <key> }` |

### 3.3 States

| Field | `active` | `wait` | `terminal` |
|---|---|---|---|
| `step` | **required**: a block id, or `{ use, with }` | — | — |
| `waitFor` | — | **required**, exactly one form | — |
| `timeout` | **required** `{ after, then: park\|escalate\|retry }` | **required** | — |
| `loop` | optional: entering the state counts one round | optional | — |
| `budget` | optional `{ tokens?, turns? }` | — | — |
| `next` | **required** | **required** | optional |
| `outcome` | — | — | **required** |

`waitFor` takes one of these forms:
- `{ event: <kind> }`;
- `{ answer: <questionId> }`;
- `{ resource: heavy|slot }`;
- `{ child: { recipe, inputs: { <name>: { fact } }, onParked: propagate|ignore, onEscalated: propagate } }`.

`retry` re-enters the state once. If it times out a second time, the instance parks with reason `timeout`.

### 3.4 Transitions

| Field | Type | Req. | Rule |
|---|---|---|---|
| `from` | state, or a list of states | yes | A non-terminal state |
| `to` | state | yes | A recipe state, not a reserved one |
| `on` | `step.completed` \| `step.failed` (from `active`) · `wait.satisfied` (from `wait`) · `command.<verb>` (any) | yes | `RCP-TRIG-001` if the trigger doesn't fit the `from` state's kind |
| `when` | condition | no | §3.5 |
| `gates` | list of a block id or `{ use, threshold?, with? }` | no | Run in order; the first failure stops the transition |
| `evidence` | list of an evidence key or `{ kind, at, expect: pass\|fail }` | no | The short form means `expect: pass` |
| `effects` | list of a block id or `{ use, with? }` | no | Emitted as outbox commands |
| `produces` | list of fact keys | no | Feeds the wiring check |

Transitions from the same state with the same trigger are tried in declared order. The first one whose `when`
holds and whose gates pass is applied. A gate failure on a transition that matched doesn't fall through to the
next one: it is recorded as `workflow.refused`.

### 3.5 `when` (closed grammar)

`{ outcome: <name> }` · `{ fact: <key>, is: present|absent }` · `{ loop: <name>, below: cap }` ·
`{ all: [...] }` · `{ any: [...] }` · `{ not: {...} }`.
The outcome name must be one that the Step's descriptor (or a child recipe's terminal states) declares.

### 3.6 Loops, budgets, `next`

- **Loops:** `{ cap: int ≥ 1, noProgress: [findings, emptyDiff, sameFailure] }`. `noProgress` defaults to all
  three and can't be empty. The rule is A §17.23.13: round *n*'s findings equal round *n−1*'s or are a superset of
  them, keyed by file, check and normalized message; or the round's diff is empty; or the same check failed with
  the same signature.
- **Budgets:** set per state and per instance. Exhausting one is `→ parked(budget)`, never an error.
- **`next`:** one template, with the variables `{item}`, `{state}`, `{reason}`, `{loop.<n>}`, `{loop.<n>.cap}` and
  `{budget.tokens.left}`. Any other variable is `RCP-NEXT-001`.

### 3.7 Universal states and transitions (engine-owned, never declared)

- `any → cancelled` (`command.cancel`, or the tracker closed the item);
- `any → escalated` (a gate below threshold or uncertain on the top rung, invariant 4);
- `active|wait → parked(reason)`, with the reason one of `rounds`, `budget`, `no-progress`, `timeout`,
  `needs-answer` or `spec-conflict`;
- `parked → resumeTo` (`command.answer`, optionally with `grant`);
- `any non-terminal → taken-over → resumeTo` (`command.takeover` / `command.handback`).

### 3.8 The hash

`sha256:` over the canonical JSON of the parsed document: the YAML converted to JSON, keys sorted, no comments,
and the `hash` field left out. Formatting and key order don't change the hash; content does, and so does
switching between a short form and its long form.

### 3.9 Left out of v1

| Item | When it comes |
|---|---|
| Fan-out children (`forEach`, `maxParallel`, join rules) | Schema v2, with the Step graph and stacks (A 17.28.6 steps 3, 6) |
| Per-transition timeouts | Only if a recipe needs one |
| A free expression language | Never, unless the closed grammar can't express a real case |
| Variables or includes | Not planned (they blur the hash and the frozen recipe) |

## 4. Approval and invariant 13

- **Store:** `IRecipeApprovals` (Sindri Core) records `(id, version, hash, approver, approvedAt, via: desk|freeze,
  base?)`. It is separate from S1's profile-only `IApprovals`. `IRecipeStore` keeps the registered recipe text by
  hash.
- **Verbs:**
  - `sindri recipe register <file>` (validates, stores, and reports `unapproved`);
  - `sindri recipe approve <file|id@hash>` (typed confirmation at a TTY; an API token alone can't do it, A 17.21);
  - `sindri recipe revoke <id@hash>`;
  - `sindri recipe list`.
- **Provenance** is set by the path a recipe came in by: `register` and `approve` record `human`, and the
  generator records `generated`. A hand-written file that carries `base` is refused (`RCP-CAP-004`).
- **Rules:** `RCP-CAP-001` (a generated recipe's capability-bearing uses are a subset of its base's, compared as
  block id plus `from → to`), `RCP-CAP-002` (capability `with:` values equal the base's), `RCP-CAP-003` (an
  instance can't start from an unapproved hash, and an effect from an unapproved or revoked hash is refused at
  dispatch).
- **Evolve:** any recipe change is approval-tier and never self-adopts (A 17.19). Prompt text inside a Step's own
  artifacts still self-adopts under A 7.4.

## 5. The instance row and the transition event

### 5.1 `WorkflowInstance` (Core record)

| Field | |
|---|---|
| `id` | `InstanceId`, derived from `(recipe id, item, attempt, parent)` |
| `tenant`, `ring` | The A 17.20 seams |
| `recipe` | `id`, `version`, `hash` (frozen at start) |
| `item`, `attempt`, `parent { instance, state }?` | |
| `state`, `resumeTo`, `parkReason?` | |
| `loops` | Per loop: `count`, `granted`, `lastSignature { findings, diffHash, failure }` |
| `spent` | Tokens and turns, for the instance and per state |
| `facts` | Per key: `{ ref, sha256, headSha }`. Only registered artifacts that are unchanged count (A 17.28.1 "Evidence") |
| `headSha` | |
| `deadlines` | `{ id, kind: state\|retry, due? , remaining?, armedAt }` |
| `version` | Rises by 1 per applied transition |

### 5.2 Events

| Event | Payload |
|---|---|
| `workflow.started` | instance, recipe, inputs |
| `workflow.transition` | `instance`, `recipeHash`, `from`, `to`, `trigger`, `fromVersion → toVersion`, `epoch`, `gates[] { block, rung, score, threshold, verdict }`, `evidence[] { kind, at, artifactSha, headSha }`, `loopDelta`, `spentDelta`, `grant?`, `outbox[]` |
| `workflow.refused` | The same, without `to`. State and version unchanged; keyed at the refused `fromVersion` so a retry doesn't write it twice |
| `workflow.deadlines.shifted` | gap, deadlines moved |

All events use S1's `LedgerEntry` with the §17.21 envelope. `actor` is set by the host process (the C# Seams rule).

### 5.3 `Commit`

`IWorkflowStore.Commit(TransitionCommit, LeaseGrant)` runs as one transaction on `ledger.db`:
1. Read the lease row; refuse with `SND-LEASE-003` unless `LeaseDecision.Holds(row, grant)`.
2. `UPDATE workflow_instances … WHERE id = @id AND version = @fromVersion`. If no row changes, the result is `VersionConflict` (`SND-WF-002`).
3. Insert the ledger event by its key. If the key exists, the transaction rolls back and the result is `AlreadyApplied`.
4. Write the deadlines, the outbox commands (`outbox_commands`, Kernel `OutboxCommand`) and the engine-inbox rows.

Result: `Applied(toVersion, sequence)`, `AlreadyApplied(sequence)`, `VersionConflict(current)` or `StaleEpoch`.

### 5.4 Timers, takeover, park

- `workflow.timers` (an S1 `IScheduledService`, cadence `15s`) lists due deadlines and applies each one through
  `Commit`, keyed by the deadline id.
- **Sleep:** when the gap since the last tick is larger than the interval, the service first shifts every armed
  deadline by the gap and records `workflow.deadlines.shifted`.
- **Takeover:** each deadline gets `remaining = due − now` and `due = null`. **Handback** re-arms
  `due = now + remaining`. Two guards: the tick's query skips paused deadlines, and `TransitionDecision` refuses a
  timeout trigger while the instance is `taken-over`.
- **Park:** deadlines are cleared. `command.answer` re-arms the state's full timeout.

### 5.5 Children

- The parent's `wait` with `child` starts the child in the same `Commit` (as a `workflow.started` inbox row for the
  child).
- A child's terminal, `parked` or `escalated` transition writes an inbox row addressed to the parent, in the
  child's transaction. `DrainInbox` applies the parent's transition as its own `Commit`, keyed by the child
  event's key:
  - terminal → `wait.satisfied` with the child's `outcome`;
  - `parked` with `onParked: propagate` → the parent parks with the child's reason and a reference to the child;
    the child's resume brings the parent back;
  - `escalated` always propagates.
- Cancelling the parent cancels its children through the same inbox.

## 6. The Core/port boundary and what S1 must add

### 6.1 Placement

| Layer | Holds |
|---|---|
| `Aw.Sindri.Core/Workflow` | Recipe records and typed ids (`RecipeId`, `BlockId`, `FactKey`, `InstanceId`, `OutcomeName`); `BlockDescriptor`, `BlockCatalog`; `RecipeHash`; `RecipeValidator` and the `RCP-*` rules; `TransitionDecision`, `LoopRules` (no-progress, grant), `BudgetRules`, `TimerDecision`, `ChildPropagation`; the ports `IWorkflowStore`, `IRecipeStore`, `IRecipeApprovals`; errors `SND-RECIPE-*`, `SND-WF-*` |
| `Aw.Sindri.Features` | Slices `ValidateRecipe`, `RegisterRecipe`, `ApproveRecipe`, `RevokeRecipe`, `ListRecipes`, `EvalRecipe`, `StartInstance`, `ApplyTrigger`, `GetInstance`, `TickTimers`, `DrainInbox`; the block runtime map and the kind interfaces |
| `Aw.Sindri.Infrastructure` | `YamlRecipeReader` (S1's `YamlToJson` → AwJson), `SqliteWorkflowStore`, `SqliteRecipeStore`, `SqliteRecipeApprovals`; migrations for `workflow_instances`, `workflow_deadlines`, `workflow_inbox`, `recipes`, `recipe_approvals` |
| `Aw.Sindri.Host` | CLI verbs `sindri recipe …` and `sindri workflow …`, their endpoints, JSON context entries |

### 6.2 Dependencies on S1

| # | Need | Status on the S1 branch | Needed by |
|---|---|---|---|
| S1-D1 | S1 merged: the Sindri package, Host, CLI → API path | In progress | Phase 1 builds stacked; every phase merges after it |
| S1-D2 | Ledger writes that join a transaction: an internal helper over `SqliteLedger.InsertOnce(session, entry, grant)` that the workflow store calls inside its own `SqliteUnitOfWork`, with the epoch check in the same transaction | Missing (`InsertOnce` is private; `Append` owns its transaction) | Phase 4 |
| S1-D3 | `outbox_commands` in `ledger.db`, and an outbox dispatcher under the lease that re-checks approval and the effect-kind allowlist at dispatch | Missing | Phase 6 |
| S1-D4 | Scheduler cadence below a minute | **Met**: `Cadence` accepts `5s`…`24h` | Phase 5 |
| S1-D5 | A typed terminal confirmation shared by more than one verb | Exists only for `profile approve` (`SND-PROFILE-004`) | Phase 2 |
| S1-D6 | A ledger read endpoint (`ReadAfter`, filterable by item) and a `sindri ledger` verb | Missing (only `GetStatus`) | Phase 4 |
| S1-D7 | The host sets `actor` on every envelope | To confirm | Phase 4 |

**Outside S1:**
- `ILauncher` (planned port) is needed for real Step runtimes; until then they fail closed (W18).
- A slot allocator (A 17.20 P1 resource classes) grants `resource: slot`; Phase 7 ships a stub over the
  profile's session cap.
- The §7.1 router: Phase 7 ships a stub that emits `item.routed` from the profile mode.
- Judge (#156) and Evidence (#158) as rung-2 gates and evidence providers come with the worker recipes, outside
  these phases.

## 6a. Error catalogue (new codes in `SindriErrors`, `docs/sindri/errors.md`)

| Code | Kind | Meaning |
|---|---|---|
| SND-RECIPE-001 | invalid | The recipe failed validation (the output lists the `RCP-*` rules) |
| SND-RECIPE-002 | notFound | No recipe with that id or hash is registered |
| SND-RECIPE-003 | conflict | The recipe hash isn't approved (start, or effect dispatch) |
| SND-RECIPE-004 | forbidden | Approving a recipe needs a typed confirmation at an interactive terminal |
| SND-RECIPE-005 | conflict | The recipe hash was revoked |
| SND-WF-001 | notFound | No such instance |
| SND-WF-002 | conflict | The instance version changed (optimistic concurrency); re-read and retry |
| SND-WF-003 | conflict | The trigger isn't allowed in the instance's state (including a timeout during takeover) |
| SND-WF-004 | conflict | A gate refused the transition (the `workflow.refused` event has the gate results) |
| SND-WF-010 | unavailable | A block named by the recipe has no available runtime (fail closed, W5) |

## 7. Validator rules mapped to §17.28.5

Each rule has an invalid fixture that fails it (`tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/<rule>.yaml`).
Output: one line per failure, `RCP-…: <what> at <path>; <fix>`. Exit 1 on any failure.

| §17.28.5 bullet | Rules |
|---|---|
| Matches the JSON Schema | `RCP-SCHEMA-001` (shape, types, unknown fields, missing required fields per state kind), `RCP-SCHEMA-002` (unknown `schema` version), `RCP-STATE-001` (reserved state name), `RCP-INIT-001` (`initial` isn't `active` or `wait`) |
| Every block id is registered | `RCP-BLOCK-001` (unknown id), `RCP-BLOCK-002` (wrong kind for the slot, e.g. a gate used as `step`), `RCP-BLOCK-003` (unknown `with:` parameter, a wrong type, or a required one missing), `RCP-EVID-001` (no provider for the evidence kind) |
| Every block's inputs are produced upstream | `RCP-WIRE-001` (an input not produced on every path), `RCP-WHEN-001` (an outcome name the Step or child doesn't declare), `RCP-CHILD-001` (an unknown child recipe, or child inputs that don't cover the child's `inputs`) |
| Capability-bearing blocks only in approved recipes | `RCP-CAP-001`…`004` (§4). `validate` reports `unapproved` as information; `RCP-CAP-003` is enforced at start and at dispatch |
| Every state is reachable | `RCP-REACH-001` |
| Every non-terminal state has a way out | `RCP-EXIT-001` (a non-terminal state with no outgoing transition, or none that reaches a terminal state) |
| Every `wait` and `active` state has a timeout | `RCP-TIME-001` (also covered by the schema; kept as a named rule so the message names it) |
| Every loop has a cap | `RCP-LOOP-001` (an undefined loop is used, or a loop has no `cap`), `RCP-LOOP-002` (a loop's state isn't on a cycle, so the counter can never pass 1), `RCP-LOOP-003` (an empty `noProgress`) |
| Effects only use allowlisted command kinds | `RCP-EFFECT-001` (the effect block's command kind isn't on the profile's allowlist) |
| The hash matches the content | `RCP-HASH-001` |
| (added) Trigger, ordering and template checks | `RCP-TRIG-001` (trigger kind against the `from` kind), `RCP-WHEN-002` (a transition always shadowed by an earlier one), `RCP-THRESH-001` (a threshold on a rung-1 gate), `RCP-NEXT-001` (unknown template variable), `RCP-DUR-001` (bad duration or token text) |

## 8. Engine properties mapped to tests

| Property (A 17.28.5 and this doc) | Test | Lane | Phase |
|---|---|---|---|
| Replaying an event log reproduces the same instance | `Replay_of_any_eval_log_reproduces_the_instance`: fold `TransitionDecision` over each recipe's `evals` and over seeded random trigger sequences (generator in `Aw.Sindri.Testing`, no new package) | unit | 3 |
| Applying any event twice is a no-op | `Applying_a_transition_twice_records_it_once`: decision level (the same `fromVersion` gives the same key) and `WorkflowStoreContract` (fake and SQLite) | unit / integration | 3, 4 |
| A stale lease epoch or instance version is refused | `WorkflowStoreContract.Commit_refuses_a_stale_epoch` / `…_a_stale_version` | unit (fake) / integration (SQLite) | 4 |
| A timer firing during a takeover does nothing | `Timeout_during_takeover_changes_nothing` (decision) and `TickTimers_skips_paused_deadlines` (store) | unit / integration | 5 |
| Parks and escalations propagate from child to parent | `Child_park_parks_the_parent_and_resume_returns_it`, `Child_escalation_escalates_the_parent`, `Cancel_cascades_to_children` | unit / integration | 7 |
| Fail closed (W5) | `Unavailable_block_refuses_the_transition` | unit | 3 |
| No-progress parks early | `Same_findings_twice_parks_no_progress`, `Empty_diff_parks_no_progress`, `Same_failure_signature_parks_no_progress` | unit | 3 |
| Budget exhaustion parks | `Exhausted_state_budget_parks_budget` | unit | 3 |
| A grant adds exactly n rounds | `Answer_with_grant_adds_rounds_and_records_it` | unit | 3 |
| Sleep shifts deadlines | `Gap_larger_than_interval_shifts_armed_deadlines` | unit | 5 |
| Approval is re-checked at dispatch | `Revoked_recipe_effect_is_refused_at_dispatch` | integration | 6 |
| Transition, outbox and event commit together | `Failed_outbox_write_rolls_back_the_transition` | integration | 4, 6 |

## 9. Test strategy by lane

| Lane | Budget (per package) | Holds |
|---|---|---|
| `unit` | < 10 s | Every `RCP-*` rule (one invalid fixture each, plus valid fixtures), the hash, the schema-is-current test (W19), the YAML-1.2 `on` key test, every pure decision, the eval replay of the real recipes, the seeded property runs (a bounded number of cases, so the lane fits its budget), `WorkflowStoreContract` / `RecipeStoreContract` / `RecipeApprovalsContract` on the in-memory fakes, `BlockContract` for each block, architecture tests |
| `integration` | < 60 s | The same contract bases on the SQLite adapters (temp files), migrations on a copy of the previous schema, endpoints on the loopback host, CLI golden files (`recipe validate --json`, `workflow status`), the timers and inbox services against SQLite with `FakeTimeProvider` |
| `heavy` | nightly | None of the engine's own tests. S1's AOT smoke covers the `sindri` binary; Phase 1 adds `recipe validate` to it |

- No mocking libraries. Fakes live in `Aw.Sindri.Testing`, each with its contract base in the same PR.
- `TimeProvider` is always faked. Ids come from a fake `IIdGenerator`.
- **Per-recipe eval suites** (`recipes/sindri/evals/*.jsonl`) are replayed in the unit lane and later by evolve
  (A 7.4).

## 10. The first recipes

The files land in `recipes/sindri/` with their `evals/`. The YAML is at the end of this section.

- **`workflow.item`:** `triaged`(wait `item.routed`) → `startable`(wait `resource: slot`) → `claimed`(wait
  `claim.created`, `retry` on timeout) → `running`(wait on child `workflow.step`, `onParked: propagate`) →
  `done`, with `dropped` (outcome `unresolved`) for `notStartable` or `claimExists`. Gates:
  `gate.route.startable` (a stub at first), `gate.budget.available` (real). Effect: `effect.claim.ref` (real, needs
  S1-D3).
- **`workflow.step`:** `produce` (loop `revisions`, cap 3) → `checks` → `verify` → `done`. `checks: failed` and
  `verify: rejected` return to `produce` with `fact:findings`. A budget of 200k tokens on `produce` and 400k on the
  instance.
- **Fixtures** (test only): an invalid recipe per rule, valid recipes for each engine property, and fake blocks
  `gate.fixture.{pass,fail,uncertain}`, `step.fixture.scripted` and `effect.fixture.echo`.
- **Honest first signal:** until `ILauncher` exists, `step.produce`, `step.checks` and `step.verify` have runtimes
  that are unavailable, so per W5 the instance parks at `produce` with `needs-answer` and `SND-WF-010`. An item
  started through the API reaches `running`, its child parks, the parent parks through propagation, and all of it
  shows in the ledger.

```yaml
schema: 1
id: workflow.item
version: 1
inputs: [fact:item]
initial: triaged
states:
  triaged:   { kind: wait, waitFor: { event: item.routed }, timeout: { after: 7d, then: escalate },
               next: "Waiting for the router to size and route {item}." }
  startable: { kind: wait, waitFor: { resource: slot }, timeout: { after: 3d, then: escalate },
               next: "{item} is startable; waiting for a session slot." }
  claimed:   { kind: wait, waitFor: { event: claim.created }, timeout: { after: 10m, then: retry },
               next: "Creating the claim ref sindri/<user>/{item}." }
  running:   { kind: wait, timeout: { after: 2d, then: park },
               waitFor: { child: { recipe: workflow.step, inputs: { spec: { fact: fact:stepSpec } },
                                   onParked: propagate, onEscalated: propagate } },
               next: "{item} is running; see its Step." }
  done:      { kind: terminal, outcome: resolved }
  dropped:   { kind: terminal, outcome: unresolved }
transitions:
  - { from: triaged,   to: startable, on: wait.satisfied, when: { outcome: startable }, gates: [gate.route.startable], produces: [fact:stepSpec] }
  - { from: triaged,   to: dropped,   on: wait.satisfied, when: { outcome: notStartable } }
  - { from: startable, to: claimed,   on: wait.satisfied, gates: [gate.budget.available], effects: [effect.claim.ref] }
  - { from: claimed,   to: running,   on: wait.satisfied, when: { outcome: created } }
  - { from: claimed,   to: dropped,   on: wait.satisfied, when: { outcome: claimExists } }
  - { from: running,   to: done,      on: wait.satisfied, when: { outcome: resolved } }
  - { from: running,   to: dropped,   on: wait.satisfied, when: { not: { outcome: resolved } } }
evals: [evals/workflow.item/*.jsonl]
```

```yaml
schema: 1
id: workflow.step
version: 1
inputs: [fact:spec]
initial: produce
states:
  produce: { kind: active, step: step.produce, loop: revisions, timeout: { after: 4h, then: park },
             budget: { tokens: 200k }, next: "Produce the Step output (round {loop.revisions}/{loop.revisions.cap})." }
  checks:  { kind: active, step: step.checks, timeout: { after: 30m, then: retry },
             next: "Running the Step's deterministic checks." }
  verify:  { kind: active, step: step.verify, timeout: { after: 1h, then: park },
             next: "The verifier is reviewing the output." }
  done:    { kind: terminal, outcome: resolved }
transitions:
  - { from: produce, to: checks,  on: step.completed, produces: [fact:output] }
  - { from: checks,  to: verify,  on: step.completed, when: { outcome: passed } }
  - { from: checks,  to: produce, on: step.completed, when: { outcome: failed },   produces: [fact:findings] }
  - { from: verify,  to: done,    on: step.completed, when: { outcome: accepted } }
  - { from: verify,  to: produce, on: step.completed, when: { outcome: rejected }, produces: [fact:findings] }
loops: { revisions: { cap: 3 } }
budget: { tokens: 400k }
evals: [evals/workflow.step/*.jsonl]
```

The `claim.created` and `claimExists` events reach the instance as the waited-for event's outcome
(`created` / `claimExists`), which the `effect.claim.ref` descriptor declares.

## 11. Review resolutions

(Filled in by the /autoplan pass below.)

## Phases

Each phase is one PR. Phase 1 is stacked on `feat/sindri-s1-app-shell`; later phases stack on the one before
until S1 merges, then target `main`. Sizes are a rough guide: S is a few files, M one subsystem slice, L a store
or several slices with migrations.

### Phase 1: the recipe model and validator

- **Goal:** recipes parse, hash and validate against the catalog; `sindri recipe validate` reports named rules.
- **Files:**
  - `src/Sindri/Aw.Sindri.Core/Workflow/{Recipes,Blocks,Validation,Ids}/**`;
  - `src/Sindri/Aw.Sindri.Features/ValidateRecipe/**`;
  - `src/Sindri/Aw.Sindri.Infrastructure/Workflow/YamlRecipeReader.cs`;
  - `src/Sindri/Aw.Sindri.Host/{Commands,Endpoints}/ValidateRecipe*.cs`;
  - `schemas/recipes/recipe.schema.json` (generated);
  - `tests/Aw.Sindri.Tests/Workflow/**`, `tests/Aw.Sindri.Testing/Workflow/**` (fixtures, fake descriptors);
  - `docs/sindri/recipes.md`.
- **Size:** M.
- **S1 dependency:** stacked on the S1 branch (S1-D1 for merging). None of D2–D7.
- **Signal:** `sindri recipe validate tests/…/invalid/RCP-REACH-001.yaml` exits 1 with
  `RCP-REACH-001: state 'orphan' is unreachable from 'initial' at states.orphan`; `recipes/sindri/workflow.step.yaml`
  exits 0 and prints its hash.

### Phase 2: registry and approval

- **Goal:** recipes are registered by hash and approved at the desk; `RCP-CAP-*` checks run against the real
  approval snapshot.
- **Files:**
  - Core ports `IRecipeStore`, `IRecipeApprovals`;
  - slices `RegisterRecipe`, `ApproveRecipe`, `RevokeRecipe`, `ListRecipes`;
  - SQLite adapters with migrations;
  - contract bases and fakes;
  - CLI verbs and endpoints;
  - the real descriptors for the first recipes' blocks.
- **Size:** M.
- **S1 dependency:** S1-D5 (shared terminal confirmation).
- **Signal:** `sindri recipe approve recipes/sindri/workflow.step.yaml` asks for typed confirmation, and then
  `sindri recipe list` shows `workflow.step@sha256:… approved (desk)`. Without a TTY it refuses with
  `SND-RECIPE-004`.

### Phase 3: the pure engine and recipe evals

- **Goal:** every transition decision is pure Core, proven by replay, and recipes ship eval suites.
- **Files:**
  - `Core/Workflow/Engine/{TransitionDecision,LoopRules,BudgetRules,TimerDecision,ChildPropagation}.cs`;
  - the `EvalRecipe` slice and `sindri recipe eval`;
  - `recipes/sindri/evals/*.jsonl`;
  - the in-memory `WorkflowStore` fake with `WorkflowStoreContract` (the fake half).
- **Size:** M.
- **S1 dependency:** none beyond D1.
- **Signal:** `sindri recipe eval recipes/sindri/workflow.step.yaml` prints each eval case as `pass`/`fail` with
  the diverging state, and exits 1 when an expected outcome doesn't match.

### Phase 4: the store, start and apply

- **Goal:** instances start and move through fenced, idempotent, single-transaction commits that are visible
  through the API.
- **Files:**
  - `SqliteWorkflowStore` and migrations;
  - the `WorkflowStoreContract` SQLite binding;
  - slices `StartInstance`, `ApplyTrigger` (commands, `cancel`, `answer` with `grant`) and `GetInstance`;
  - the `sindri workflow start|status|answer|cancel` verbs and endpoints.
- **Size:** L.
- **S1 dependency:** S1-D2, S1-D6, S1-D7.
- **Signal:** `sindri workflow start workflow.step --item T-1 --spec spec.json`, then `sindri ledger --item T-1`,
  shows `workflow.started` and a transition `produce → parked (needs-answer)` carrying `SND-WF-010`. Running
  `sindri workflow cancel T-1 --version <stale>` returns `SND-WF-002`, and the same cancel at the current version
  run twice records one event.

### Phase 5: timers, takeover and handback

- **Goal:** timeouts fire as replayable transitions, sleeps shift them, and takeovers pause them.
- **Files:**
  - `workflow_deadlines` and the `TickTimers` slice registered as `workflow.timers`;
  - `command.takeover` and `command.handback`;
  - the sleep shift;
  - `sindri takeover|handback <item>`.
- **Size:** M.
- **S1 dependency:** S1-D4 (met).
- **Signal:** on a fixture recipe registered in a dev state directory with a `1m` timeout, `sindri workflow
  status T-2` shows `parked (timeout)` after the minute. After `sindri takeover T-2`, status shows `taken-over,
  timers paused (remaining 0:40)` and nothing fires until `handback`.

### Phase 6: effects through the outbox

- **Goal:** effects become outbox commands in the transition's transaction; dispatch re-checks approval and the
  allowlist.
- **Files:**
  - the `IEffectKind` runtime;
  - outbox writes in `Commit`;
  - the effect-kind allowlist read from the profile;
  - the dispatch-time recheck hook;
  - `effect.claim.ref` and `effect.fixture.echo`.
- **Size:** M.
- **S1 dependency:** S1-D3 (`outbox_commands` and the dispatcher).
- **Signal:** a transition with `effect.fixture.echo` shows its outbox id in the `workflow.transition` event and a
  dispatched command in `sindri outbox list`. After `sindri recipe revoke`, the next effect is shown as refused at
  dispatch.

### Phase 7: children and the item lifecycle end to end

- **Goal:** `workflow.item` runs with a `workflow.step` child; parks, escalations and cancels propagate.
- **Files:**
  - `workflow_inbox` and the `DrainInbox` slice;
  - `ChildPropagation` wiring;
  - the router stub service (`item.routed`) and the slot stub;
  - `gate.route.startable` and `gate.budget.available`;
  - `recipes/sindri/workflow.item.yaml` with its evals.
- **Size:** M.
- **S1 dependency:** S1-D3 (the claim effect), and Phases 4–6.
- **Signal:** `sindri workflow start workflow.item --item T-3` shows `triaged → startable → claimed → running` in
  the ledger, and `git show-ref sindri/<user>/T-3` finds the claim ref. The child `workflow.step` parks at
  `produce` (`SND-WF-010`), and `sindri workflow status T-3` shows the parent `parked (needs-answer, child
  workflow.step/…)`. `sindri workflow cancel T-3` cancels both.
