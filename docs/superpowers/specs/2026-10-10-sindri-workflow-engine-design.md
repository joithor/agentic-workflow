# Sindri workflow engine: recipes, blocks, validator and engine

**Status:** design, revision 2 (after /autoplan), awaiting approval · **Date:** 2026-10-10 · **Builds:** factory
addendum §17.28 (one workflow state machine) and §17.29.4 step 2 · **Next:** `/specToProvenPR` takes the Phases at
the end to PRs.

This doc makes §17.28 concrete. It doesn't reopen §17.28 or §17.29, which are settled (decisions Q6 and Q7). It
settles the block contract, the recipe schema, approval and invariant 13, the instance and its events, the CLI and
API surface, the boundary with S1's app shell, and the first recipes. Revision 2 resolves the /autoplan findings
C1–C32 (§13).

References:
- the factory addendum `2026-10-09-sindri-factory-addendum.md` (A) and the core design `2026-10-07-sindri-design.md` (D);
- `docs/csharp/bugfix.md`, the reference for gates and state;
- `.agents/rules/csharp.md` and `csharp-testing.md`;
- S1, the app shell on `feat/sindri-s1-app-shell`, whose C# catalogue and verbs are in `docs/csharp/sindri.md`.
  Its ports are named as they are on that branch (`ILedgerStore`, `ILease`, `LeaseGrant`, `LeaseDecision.Holds`,
  `IScheduledService`, `ScheduledRun`, `EventFactory`, `SindriErrors`).

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| W1 | **A block is a DI-registered class that implements its kind interface (`IGate`, `IEvidenceProvider`, `IStep`, `IEffectKind`) and exposes a static `BlockDescriptor`.** Registration is generic and explicit (`services.AddGate<BudgetAvailableGate>()`). The `BlockCatalog` (descriptors only) and the `BlockRuntime` (built from `IEnumerable<IGate>` and the others) come from those registrations | The validator runs on descriptors alone, so it stays pure Core. Explicit, generic registration is AOT-safe and uses no service locator (`ARCH-DI-001`) |
| W2 | **Wiring is checked on every path.** A block input must be produced on every path into its transition | A fact produced on only some paths is a latent refusal at run time; checking turns it into a validation error |
| W3 | **Recipe schema v1 is closed** (§3). Short forms are normalized to the long form by the reader before typed binding, and the schema and the hash describe the long form | Generated recipes can't slip logic in. No union converters, so the generated schema is closed exactly where it matters |
| W4 | **The rung comes from the block's descriptor.** A rung-2 or rung-3 gate **must** have a `threshold` | A recipe can't promote a judge question to deterministic, and can't leave a model gate without a bar |
| W5 | **Fail closed.** An unknown block is a validation error. At run time, an unavailable gate or evidence runtime refuses the transition, and a Step with no runtime parks with `needs-answer` and `SND-WF-010` (the `next` says only a build with that runtime helps) | A missing check must never pass silently (as in `addyosmani/factory`'s `MISCONFIGURED`) |
| W6 | **Outcomes are a closed set:** `resolved`, `unresolved`, `alreadySatisfied` and `failed` for recipe terminals, plus the engine's `cancelled` | Receipts and metrics count outcomes (as loki-mode does) |
| W7 | **Answering `parked(rounds\|no-progress)` may carry `grant: n`**, where 1 ≤ n ≤ the loop's cap. It needs a human at a terminal (W22) and is recorded in the event | An explicit, auditable extra round (as robi42/factory allows); a session can never grant itself one |
| W8 | **Every recipe needs a human approval to run.** Approvals live in a store keyed by `(id, version, hash)`, never in the file | A §17.19 workflow template is a capability field, so it is approval-tier |
| W9 | **Hand-written recipes are approved with `sindri recipe approve`**, through a server-issued challenge typed at a terminal. **Generated recipes are approved by the packet freeze**, and their `base` is chosen by Sindri code from the packet's workflow template, never by a model | Freezing is a desk action (invariant 13). A request-body flag alone isn't a confirmation (C23) |
| W10 | **`RCP-CAP-001`–`007` keep a generated recipe at or below its base** (§4.3): no new capability uses or children, the same capability parameters, guards kept and thresholds at or above the base's, budgets and caps at or below the base's, and capability parameters only as literals or profile references | Invariant 13 also covers budgets and guards (C16, C17). Widening is a desk edit of the base |
| W11 | **Approval is re-checked when an instance starts and when an effect is dispatched**, and it pins the descriptor hashes of the blocks the recipe names and the hashes of its child recipes. Revoking a recipe also revokes the generated recipes approved from it | A changed block, child or base can't change what an approved recipe means (C13, C24) |
| W12 | **A transition is keyed by its trigger, not by the instance version:** `wf:{instance}:{trigger}`, where the trigger id is a command nonce, a step-run token, a source event's ledger sequence, a deadline id or a child event's key. `Commit` checks the key first, then the epoch, then the version. A Step result whose run token isn't the current state entry's is stale and is dropped | A replay or redelivery is a no-op, and a late result from an earlier visit can't move the instance (C1) |
| W13 | **One port commits:** `IWorkflowStore.Commit(TransitionCommit, LeaseGrant)` writes the event, the instance row (and a new child row), its deadlines, its outbox commands and its inbox rows in one SQLite transaction | A 17.28.1 |
| W14 | **Each refusal consumes its trigger, and what follows is defined** (§5.4): an `active` state re-runs its Step within its loop, a wait parks with `needs-answer`, a budget gate parks with `budget`, an uncertain top-rung gate escalates, and a command gets `SND-WF-004` | Nothing sits until a timeout (C2) |
| W15 | **External events reach instances through the engine inbox.** An event router reads the ledger with a durable cursor and resolves each event to its target instance, by the command it answers or by its item. The inbox keeps a row until the instance waits for that event, and expires it when the instance leaves that wait | Delivery is durable and never lost while the instance is parked or taken over (C3) |
| W16 | **`resume` is a stack**, at most two deep: a takeover over a park pushes, and a handback pops back to the park with its reason | Opening a parked item in Warp is the main §17.29.3 path (C5) |
| W17 | **Children are created in the parent's `Commit`**, with an id that carries the parent's state entry. Every child change propagates through the inbox (§5.6) | No stranded parent and no id collision (C6) |
| W18 | **Timers are deadlines on the instance row**, applied by built-in services through `Commit`. A sleep is detected as wall-clock time minus monotonic time (and downtime from the last recorded tick) of at least 60 s. It is applied per instance as a keyed `shift` trigger | Replayable and versioned, and a slow tick can't starve deadlines (C19) |
| W19 | **A takeover pauses deadlines. A park stops them**, and its answer re-arms the full timeout. Each takeover also arms an engine-owned `takeoverIdle` deadline (2 h, D §9.3) that notifies the human and never hands back on its own | D §9.3; C30 |
| W20 | **Until `ILauncher` exists, Steps use the desk runtime.** The state stays `active`, `next` tells the human what to do, and `sindri workflow complete` reports the Step's outcome and outputs | Every phase ends in a real outcome (C7). Agent work stays in the human's tool (§17.29.3) |
| W21 | **Events carry the full post-state delta**: state, resume stack, park, loops, spent, facts and deadlines | Folding the log rebuilds the row exactly (C20) |
| W22 | **Triggers have typed sources.** Every command needs an actor class: `human-terminal`, `human`, `session` or `system`. Recipe `command.<verb>` triggers need at least `human`. Sessions and MCP can only file requests | Text typed into a session is never a decision (§8.2 W6); C18 |
| W23 | **The engine lives in `Aw.Sindri.Core/Workflow` (pure) and in Sindri's Features and Infrastructure. Phase 1 is a stacked PR on `feat/sindri-s1-app-shell`**, built in its own worktree off that branch | A 17.28.3's placement, buildable before S1 merges |
| W24 | **The first recipes are `workflow.item` and `workflow.step`** (§12), plus small dev recipes in `recipes/dev/` that use real, harmless blocks for the phase signals | A 17.28.6 step 1. No fixture code ships in the binary |
| W25 | **The JSON Schema is generated from the long-form records**, into `schemas/recipes/recipe.schema.json`, with a hand-written editor overlay for the short forms. Tests fail when either is stale | The `aw-packets` precedent (`docs/csharp/packets.md`) |
| W26 | **Engine phases own their S1 changes** (§7.2) once S1 merges, in Sindri's own code. If the S1 session lands one first, the phase drops it | No dependency without an owner (C15) |

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
    IReadOnlyList<FactKey> Outputs,               // facts it records; the engine checks a result carries them
    IReadOnlyList<OutcomeName> Outcomes,          // what `when: { outcome }` may name (Steps, gates)
    Capability Capabilities,
    IReadOnlyList<ParameterSpec> Parameters,      // the `with:` parameters it accepts
    Rung? Rung);                                  // gates only; rung 2 and 3 need a threshold

public sealed record ParameterSpec(string Name, ParameterType Type, bool Required, bool IsCapabilityField);

public sealed record EventDescriptor(             // an event kind a wait may name
    EventKind Kind,                               // claim.created, item.routed, review.submitted
    IReadOnlyList<OutcomeName> Outcomes,          // read from the event's `outcome` field
    IReadOnlyList<FactKey> Facts);                // facts the event records

public interface IDescribedBlock { public static abstract BlockDescriptor Descriptor { get; } }
```

- **Capability-bearing** means a block whose `Capabilities` isn't `None`, a parameter with `IsCapabilityField`, or
  (for generated recipes) a child recipe. Effect blocks always carry `Effects`.
- **Runtime interfaces:**
  - `IGate.Evaluate(GateContext) → GateResult(verdict pass|fail|uncertain, score?, reasons)`;
  - `IEvidenceProvider`, per A 17.11;
  - `IStep.Start(StepContext) → StepRun` and `Complete(StepRun, StepReport) → StepResult(outcome, outputs, spent)`;
  - `IEffectKind.Build(EffectContext) → payload`. The effect itself runs only in the dispatcher.
- **Registration and placement:**
  - `services.AddGate<T>() where T : class, IGate, IDescribedBlock` (and `AddStep`, `AddEffect`, `AddEvidence`,
    `AddEvent(descriptor)`).
  - Blocks are scoped. `BlockRuntime` (scoped) maps ids to instances from the injected enumerables, and
    `BlockCatalog` (a singleton) collects the static descriptors.
  - Pure blocks live in `Aw.Sindri.Features/Blocks/<Kind>/` and I/O blocks in `Aw.Sindri.Infrastructure/Blocks/<Kind>/`.
  - A block over a lower-tier package (Judge #156, Evidence #158, Packets, BugFix's rules) lives in Sindri and
    reaches that package through its `Core` or its CLI, under a declared `deps.yaml` edge, never inside the package.
- **The desk runtime** (W20) is an `IStep` implementation that every Step id resolves to until a launcher-backed
  runtime is registered:
  - `Start` records an open task with the state's `next`;
  - `sindri workflow complete <address> --outcome <o> [--output <fact>=<file>]…` (actor `human`) supplies the
    `StepReport`;
  - each output file is hashed and stored as a fact artifact under the state directory.
- **The descriptor hash** is SHA-256 over the RFC 8785 canonical JSON of the descriptor. Approval pins it (W11).
- **Tests:** `BlockContract` in `Aw.Sindri.Testing` runs each block's contract tests once, for every recipe that
  uses it.
- **Authoring guide:** `docs/csharp/sindri-workflow.md` (P1), which also holds the recipe reference. A `/new-block`
  scaffold follows once three real blocks exist (§13, C27).

## 3. Recipe schema v1

### 3.1 Reading

- **Refused YAML:** duplicate keys, anchors, aliases, merge keys and explicit tags (`RCP-SCHEMA-003`). Also refused:
  a document over 256 KiB, or nested more than 32 levels.
- **Scalars:** the YAML 1.2 core schema, so the key `on` stays a string (pinned by a test).
- **Errors:** the reader keeps line and column for every node and reports **every** schema error, not just the first.
- **Normalization:** short forms (a gate as a bare id, `evidence:test@head`, `step: <id>`) become long forms before
  typed binding with `JsonUnmappedMemberHandling.Disallow`.
- **Hash:** `sha256:` over RFC 8785 (JCS) of the normalized document without `hash`, so a short form and its long
  form hash the same.

### 3.2 Top level

| Field | Type | Req. | Rule |
|---|---|---|---|
| `schema` | int | yes | `1` |
| `id` | string | yes | `workflow.<name>`, lowercase, dots and dashes |
| `version` | int ≥ 1 | yes | Registering the same `id` and `version` with another hash is `RCP-VERSION-001`; a version not above the highest registered is `RCP-VERSION-002` |
| `description` | string | no | |
| `base` | `<id>@sha256:<hex>` | generated only | Written by Sindri's generator. A hand-written recipe with `base` is `RCP-CAP-004` |
| `inputs` | list of fact keys | no | |
| `initial` | state | yes | `active` or `wait` |
| `states` | map | yes | At least one non-terminal and one terminal state; reserved names refused |
| `transitions` | list | yes | |
| `loops` | map | no | |
| `budget` | `{ tokens?, turns? }` | no | |
| `evals` | list of paths under `evals/<id>/` | to approve | Must stay inside the recipe's directory (§9.2 format) |
| `hash` | `sha256:<hex>` | no | Checked when present |

Reserved state names: `cancelled`, `escalated`, `parked`, `taken-over`.

### 3.3 Value grammars

| Grammar | Form |
|---|---|
| Fact key | `fact:<name>` or `evidence:<kind>@<base\|head>` |
| Block id | `<gate\|step\|effect\|evidence>.<area>.<name>` |
| Event kind | `<area>.<name>`, registered as an `EventDescriptor` |
| Duration | `<n>m`, `<n>h` or `<n>d`, at least `1m` |
| Tokens | int, or with a `k`/`M` suffix |
| `with:` value | string, number, bool or a list of these; `{ fact: <key> }`; `{ profile: <key> }`. Capability parameters take only literals or `{ profile }` (`RCP-CAP-007`) |

### 3.4 States

Every state has `kind: active | wait | terminal`.

| Field | `active` | `wait` | `terminal` |
|---|---|---|---|
| `step` | **required**: `{ use, with? }` | — | — |
| `waitFor` | — | **required**, exactly one form | — |
| `timeout` | **required** `{ after, then: park\|escalate\|retry }` | **required** | — |
| `loop` | optional; each entry, and each re-run after a refusal, counts one round | optional | — |
| `budget` | optional | — | — |
| `next` | **required** | **required** | optional |
| `outcome` | — | — | **required** (W6, not `cancelled`) |

`waitFor` takes one of four forms:
- `{ event: <kind> }`: the kind must be registered, and its outcomes come from the descriptor;
- `{ answer: <questionId> }`;
- `{ resource: heavy | slot }`;
- `{ child: { recipe: <id>, inputs: { <name>: { fact } }, onParked: propagate|ignore } }`. The child's
  `escalated` and `cancelled` always propagate. The child's hash is pinned at approval.

`retry` re-enters the state once and re-emits the effects of the transition that entered it, under the same derived
command ids (`cmd:{instance}:{entry}:{n}`), so the dispatcher sees a duplicate, not a new command. A second
timeout parks with `timeout`.

### 3.5 Transitions

| Field | Type | Req. | Rule |
|---|---|---|---|
| `from` | state, or a list | yes | Non-terminal |
| `to` | state | yes | A recipe state |
| `on` | `step.completed`, `step.failed` (from `active`) · `wait.satisfied` (from `wait`) · `command.<verb>` (any non-terminal; actor ≥ `human`) | yes | `RCP-TRIG-001` |
| `when` | condition (§3.6) | no | |
| `gates` | list of `{ use, threshold?, with? }` | no | In order; the first failure refuses |
| `evidence` | list of `{ kind, at, expect: pass\|fail }` | no | |
| `effects` | list of `{ use, with? }` | no | Outbox commands |
| `produces` | list of fact keys | no | Each one must be an output of this transition's source: the Step, the event or the child (`RCP-WIRE-002`) |

Transitions from one state with one trigger are tried in declared order. The first whose `when` holds is the
**matched** transition. If its gates refuse, that is a refusal (§5.4), not a fall-through to the next transition.
A `step.failed` with no transition defined parks with `needs-answer`.

### 3.6 `when` (closed)

`{ outcome: <name> }` · `{ fact: <key>, is: present|absent }` · `{ loop: <name>, below: cap }` · `{ all }` ·
`{ any }` · `{ not }`. The outcome names come from the trigger's source: a Step, an event, or a child's terminal
outcomes plus `cancelled`.

### 3.7 Loops, budgets, `next`

- **Loops:** `{ cap ≥ 1, noProgress: [findings, emptyDiff, sameFailure] }`. `noProgress` defaults to all three and
  can't be empty. The rule is A §17.23.13.
- **Budgets:** exhausting one is `→ parked(budget)`. A Step's `spent` is what its runtime reports, and the model
  proxy is the backstop (§13, L5).
- **`next`:** one template per state. The variables are `{item}`, `{instance}`, `{state}`, `{user}`, `{child}`,
  `{loop.<n>}`, `{loop.<n>.cap}` and `{budget.tokens.left}`. Wait states are written as a description ("Waiting
  for …") and human-action states as an instruction ("Run …"). `next` never names an approval-tier verb
  (`RCP-NEXT-002`).

### 3.8 Engine-owned states

| State | Entered by | `next` (engine text) | Ways out |
|---|---|---|---|
| `parked` | Caps, budget, no-progress, timeout, needs-answer, spec-conflict, a child | "Parked ({reason}): {question}. Answer with `sindri workflow answer {instance}`." When the park came from a child, it names the child | `answer` (actor `human`; `grant` needs `human-terminal`), `cancel` |
| `escalated` | An uncertain or below-threshold top-rung gate | "Escalated: {gate} scored {score}, below {threshold}. Decide with `sindri workflow answer {instance} --accept` or `--reject`." | `--accept` (`human-terminal`) applies the escalated transition, recorded as a human override. `--reject` is a refusal (§5.4). `cancel` |
| `taken-over` | `takeover` | "With you since {since}. Run `sindri workflow handback {instance}` when you're done." | `handback` |
| `cancelled` | `cancel`, the tracker closing the item, a cancelled parent | (terminal) | none; outcome `cancelled` |

After `revoke`, a `spec-conflict` park says: "This recipe was revoked. Approve a new version and start a new
attempt, or cancel."

### 3.9 Left out of v1

| Item | When it comes |
|---|---|
| Fan-out children (`forEach`, `maxParallel`, joins) | Schema v2, with the Step graph and stacks (A 17.28.6 steps 3 and 6) |
| Per-transition timeouts | Only if a recipe needs one |
| A free expression language | Never, unless the closed grammar can't express a real case |
| Variables or includes | Not planned |

## 4. Approval and invariant 13

### 4.1 Store and events

`IRecipeApprovals` records:
- the recipe: `id`, `version`, `hash`;
- who and how: `approver` (the host user), `approvedAt`, `via: desk|freeze`, `base?`;
- what it pins: `descriptorHashes { blockId: hash }` and `children { id: hash }`.

`IRecipeStore` keeps the normalized recipe text by hash. Register, approve and revoke each write a ledger event
(`recipe.registered`, `recipe.approved`, `recipe.revoked`, C26). The store lives in `ledger.db`, so a host handover
carries it (§13, P7).

### 4.2 Approving at the desk

1. `sindri recipe approve <file|id@hash>` asks the server for a **challenge**: a one-time nonce bound to the hash,
   valid for 5 minutes, together with the **preview**.
2. The preview shows:
   - the id, version and hash, and `base` when there is one;
   - a diff against the last approved version of the id;
   - the capability-bearing uses: block, transition, capabilities and parameter values;
   - the effect kinds and their allowlist status;
   - the child recipes with their pinned hashes;
   - the pinned block versions;
   - the eval result.
3. The human types `approve <id> <version>` at an interactive terminal. The CLI sends back the nonce and the typed
   text.
4. The server refuses a request with no valid nonce (`SND-RECIPE-004`). The Avalonia client and the MCP server
   expose no approve route, and an architecture test enforces that (C23).

### 4.3 Rules

| Rule | Checks | When |
|---|---|---|
| `RCP-CAP-001` | A generated recipe's capability-bearing uses (block, `from → to`) and children are a subset of its base's | validate, freeze |
| `RCP-CAP-002` | Capability parameter values equal the base's | validate, freeze |
| `RCP-CAP-003` | No instance starts, and no effect dispatches, from an unapproved or revoked hash, or from one whose pinned descriptor or child hashes no longer match the build (`SND-RECIPE-003`, `005`, `006`) | start, dispatch |
| `RCP-CAP-004` | A hand-written recipe carries no `base` | validate |
| `RCP-CAP-005` | For every capability use kept from the base, the gates and evidence on that transition are a superset of the base's, with thresholds at or above the base's | validate, freeze |
| `RCP-CAP-006` | Budgets and loop caps are at or below the base's | validate, freeze |
| `RCP-CAP-007` | Capability parameters take only literals or `{ profile }`, never `{ fact }`. At run time each value is intersected with the profile's allowlist | validate, run |

The freeze screen shows the generated recipe's diff against its base. Evolve proposals for recipes are always
approval-tier (A 17.19). Revoking a base revokes the generated recipes approved from it.

## 5. The instance and its events

### 5.1 `WorkflowInstance` (Core record)

| Field | |
|---|---|
| `id` | `InstanceId`, from `(recipe id, item, attempt)` for a root, `{parent}/{child recipe}.{parent entry}` for a child |
| `tenant`, `ring` | The A 17.20 seams |
| `recipe` | `id`, `version`, `hash` (frozen at start), plus the pinned child hashes |
| `item`, `attempt`, `parent?` | |
| `state`, `entry` | `entry` rises by 1 each time a state is entered; step-run tokens and derived ids use it |
| `resume` | A stack (at most 2) of `{ state, park? }` |
| `park?` | `{ reason, question, child? }` |
| `loops` | Per loop: `count`, `granted`, `lastSignature { findings, diffHash, failure }` |
| `spent` | Tokens and turns, for the instance and per state |
| `facts` | Per key: `{ ref, sha256, headSha }` |
| `run?` | The open Step run: `token = {entry}.{n}`, the runtime (desk or launcher), and the open task |
| `deadlines` | `{ id = {entry}:{kind}, kind: state\|retry\|takeoverIdle, due?, remaining?, armedAt }` |
| `version` | Rises by 1 per applied transition |

### 5.2 Events

| Event | Payload |
|---|---|
| `workflow.started` | instance, recipe (with pinned children), inputs (scrubbed), the initial post-state |
| `workflow.transition` | `instance`, `recipeHash`, `trigger { kind, id, actor }`, `from`, `to`, `fromVersion → toVersion`, `epoch`, `gates[] { block, rung, score, threshold, verdict, reasons }`, `evidence[]`, `grant?`, `override?`, `outbox[]`, and the **post-state delta** (W21) |
| `workflow.refused` | `trigger`, the matched transition, `gates[]`, and what followed (re-run, park, escalation, or returned to the caller) |
| `recipe.registered`, `recipe.approved`, `recipe.revoked` | §4.1 |

- Events are written as S1 `LedgerEntry`s with the §17.21 envelope.
- `actor` is set by the host (W22). `causation` is the trigger's source (an event, a command or an outbox command).
- Gate reasons and inputs pass through `Aw.Redaction` before they're written (C26).

### 5.3 `Commit`

One transaction on `ledger.db`:
1. **Key:** if the event key `wf:{instance}:{trigger}` exists, roll back and return `AlreadyApplied(sequence)`.
2. **Fence:** refuse with `SND-LEASE-003` unless `LeaseDecision.Holds(row, grant)`.
3. **Version:** run `UPDATE workflow_instances … WHERE version = @fromVersion`. If no row changes, return
   `VersionConflict(current)`.
4. **Write:** the ledger event, then the row (and a new child row), the deadlines, the outbox commands
   (`cmd:{instance}:{entry}:{n}`), the inbox rows, and the gate results (§5.4).

**On `VersionConflict`:**
- For a `system` trigger (an event, a deadline or a step result whose token is still current), the service re-reads
  the instance, re-decides and retries up to 3 times. A recorded gate answer is reused, so a judge question is never
  asked twice for the same trigger.
- A command returns `SND-WF-002` with the current version and a fresh nonce.

### 5.4 Triggers and refusals

| Trigger | Id | Valid when |
|---|---|---|
| Command | A server-issued nonce, bound to the instance version | The nonce is unused and its version is current |
| Step result | The run token `{entry}.{n}` | The token is the open run's; otherwise it is stale (`SND-WF-005`), dropped and logged |
| Event | The source ledger sequence | The instance is in a wait for that kind in the entry the inbox row names |
| Deadline | `{entry}:{kind}` | The deadline is armed and not paused |
| Child event | The child event's key | The instance waits on that child |
| Sleep shift | `shift:{tick}` | §5.5 |

What follows a refusal (W14):
- **From `active` on a Step trigger:**
  - Below the loop's cap, the Step re-runs with the refusal as findings. This is a new run `n+1` and counts a round,
    with no-progress checked.
  - At the cap, the instance parks with `rounds`.
  - With no loop, it parks with `needs-answer`.
- **From a `wait`:** the event is consumed, held resources are released, and the instance parks with `needs-answer`.
- **A `gate.budget.*` refusal:** the instance parks with `budget`.
- **An uncertain or below-threshold top-rung gate:** the instance is escalated.
- **A command:** `SND-WF-004` with the gate results, recorded once under its nonce.

### 5.5 Timers, sleep, takeover, park

- **Built-in services** run without a profile entry: `workflow.timers` (15 s), `workflow.events` (5 s) and
  `workflow.inbox` (5 s). The profile may only change their cadence. This needs S1-D8.
- **Sleep:** each tick records its wall-clock and monotonic times. The unaccounted gap is wall time minus monotonic
  time since the last tick, and at startup it is the time since the last persisted tick. When it is at least 60 s,
  every instance with armed deadlines gets a `shift:{tick}` trigger through `Commit`, so a slow tick shifts
  nothing.
- **Takeover:** every deadline stores `remaining` and clears `due`. A `takeoverIdle` deadline is armed. The resume
  stack is pushed. `TransitionDecision` refuses deadline triggers in `taken-over`.
- **Handback** re-arms the deadlines from `remaining`, clears `takeoverIdle` and pops the stack.
- **`takeoverIdle`** fires a `notify.desk` effect, "hand back {item}?", and changes no state.
- **Park:** deadlines are cleared. The answer pops the stack and re-arms the state's full timeout.

### 5.6 Delivery and children

- **Event router** (`workflow.events`):
  - It reads `ILedgerStore.ReadAfter` from a cursor in `workflow_cursor`.
  - For each event whose kind is registered, it resolves the target instance:
    - through `causation`, from the outbox command it answers (whose id names the instance and entry), or else
    - through `item`, to the instances of the item's latest attempt that wait for that kind.
  - It writes one inbox row per target.
- **Inbox** (`workflow.inbox`): a row applies when the instance is in the wait it names. A row whose instance is
  parked or taken over stays pending. A row whose instance left that state entry is marked `expired`, with a
  `workflow.refused` event.
- **Children:**

| Child change | Parent |
|---|---|
| Created | Inserted in the parent's `Commit` when it enters the `child` wait |
| Terminal (outcome X) | `wait.satisfied` with outcome X |
| Cancelled | `wait.satisfied` with outcome `cancelled` |
| Parked (`onParked: propagate`) | Parks with the child's reason and a reference to the child |
| Resumed from a park | Pops back to its wait |
| Escalated | Escalated, with a reference to the child |
| Parent answered while the park came from a child | The answer is forwarded to the child; the parent resumes when the child does |
| Parent cancelled | Every child is cancelled |
| Parent busy (taken over, or a version conflict) | The row stays pending and applies at handback, or on the next drain |

## 6. CLI and API surface

### 6.1 Process model and addressing

- `sindri recipe validate` and `sindri recipe eval <file>` run in-process: no server, the binary's catalog, and the
  profile from disk if there is one. Without a profile, `RCP-EFFECT-001` reports `skipped (no profile)`.
- Every other verb is a client call to `sindri serve`, which starts if needed (S1's model).
- **Address:** `<item>/<recipe-name>/<attempt>` for a root instance, plus `/<child-recipe-name>` per level, for
  example `T-3/item/1` and `T-3/item/1/step`. A bare `T-3` means the root instance of the item's latest attempt,
  and is `SND-WF-006` when that is ambiguous. `--version` is optional: the CLI fetches the current version and nonce.
- `start` with an existing `(recipe, item, attempt)` returns the existing instance. `--attempt next` starts a new
  attempt.

### 6.2 Verbs

| Verb | Actor | Does |
|---|---|---|
| `recipe validate <file> [--json]` | any | Runs the `RCP-*` rules and prints the hash |
| `recipe eval <file> [--json]` / `recipe eval --record <address> --out <file>` | any / human | Replays the eval logs / writes an instance's history as an eval |
| `recipe register <file>` | human | Validates and stores the recipe; reports `unapproved` |
| `recipe show <id@hash>` | any | The approval preview |
| `recipe approve <file\|id@hash>` | human-terminal | §4.2 |
| `recipe revoke <id@hash>` | human-terminal | Revokes it and the generated recipes approved from it |
| `recipe list [--json]` | any | Prints `no recipes registered` when empty |
| `workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]…` | human | Facts are hashed and stored |
| `workflow status <address> [--json]` | any | §6.3 |
| `workflow list [--needs-me \| --item <id> \| --state <s>] [--json]` | any | `--needs-me` lists parked, escalated, idle taken-over instances and open desk tasks |
| `workflow answer <address> [--text …\|--file …] [--grant n] [--accept\|--reject]` | human (`--grant`, `--accept`: human-terminal) | |
| `workflow complete <address> --outcome <o> [--output <fact>=<file>]…` | human | Reports a desk Step |
| `workflow cancel <address>` | human | |
| `workflow takeover <address>` / `workflow handback <address>` | human | `sindri handback <item>` stays as D §9.3's short form |
| `workflow events <address> [--follow] [--json]` | any | Ledger events for the instance and its children |
| `outbox list [--pending] [--json]` | any | |

- **Exit codes** follow S1: `0` ok; `1` an expected failure (rules failed, refused, not found, conflict); `2` usage,
  or the tool can't work (can't reach `serve`, I/O).
- **Errors:** one stderr line, `error: <message> (<code>)`, with the fix from the catalogue. Every read verb has
  `--json`.
- **`GET /workflow/instances/{address}`** returns the instance, the rendered `next`, `allowedCommands` (each with
  its actor class), the open question or task, the nonce and the children, so the UI holds no logic.
- **Notifications:** entering `parked` or `escalated`, a new desk task, and `takeoverIdle` each emit a `notify.desk`
  outbox command (P6), within the notification budget.

### 6.3 `status` layouts

```
T-3/item/1  workflow.item@3f1c09  v7  running  since 10:42 (1h 12m)
  next      T-3 is running; see its Step: T-3/item/1/step
  waits     child T-3/item/1/step  ->  parked (needs-answer)
  timeout   parks in 1d 22h 48m
  budget    tokens 0 / 400k
  commands  cancel, takeover
```

```
T-3/item/1/step  workflow.step@9ab2d4  v4  produce  desk task open
  next      Produce the Step output (round 1/3), then run:
            sindri workflow complete T-3/item/1/step --outcome done --output fact:output=<file>
  timeout   parks in 3h 51m
  commands  complete, cancel, takeover
```

```
T-1/step/1  workflow.step@9ab2d4  v9  parked: rounds  since 11:54
  question  checks failed 3 times with the same findings (see `workflow events`)
  answer    sindri workflow answer T-1/step/1 --text "..."      (add --grant 1 for one more round)
```

Durations are shown as `1d 22h 48m`. Plain output uses ASCII (`->`). The golden files in `tests/` pin these
layouts.

## 7. The Core/port boundary and S1 changes

### 7.1 Placement

| Layer | Holds |
|---|---|
| `Aw.Sindri.Core/Workflow` | Recipe records and typed ids (`RecipeId`, `BlockId`, `EventKind`, `FactKey`, `InstanceId`, `OutcomeName`); `BlockDescriptor`, `EventDescriptor`, `BlockCatalog`, `IDescribedBlock`; the block kind interfaces; `RecipeHash` (JCS); `RecipeValidator` with `ValidationContext` (the catalog, registered and approved recipe hashes, the effect allowlist, the base, all as data); `TransitionDecision`, `RefusalRules`, `LoopRules`, `BudgetRules`, `TimerDecision`, `ChildPropagation`, `EventFold`; the ports `IWorkflowStore`, `IRecipeStore`, `IRecipeApprovals`; `SND-RECIPE-*` and `SND-WF-*` |
| `Aw.Sindri.Features` | Slices: `ValidateRecipe`, `EvalRecipe`, `RegisterRecipe`, `ShowRecipe`, `ApproveRecipe`, `RevokeRecipe`, `ListRecipes`, `StartInstance`, `ApplyCommand`, `CompleteStep`, `GetInstance`, `ListInstances`, `TickTimers`, `RouteEvents`, `DrainInbox`. Also `BlockRuntime`, the desk runtime and the pure blocks. `ApplyCommand` takes the store, the runtime, the clock and the approvals; validation and telemetry are decorators (≤ 5 dependencies) |
| `Aw.Sindri.Infrastructure` | `YamlRecipeReader`, the SQLite stores and their migrations (`workflow_instances`, `workflow_deadlines`, `workflow_inbox`, `workflow_cursor`, `workflow_gate_results`, `recipes`, `recipe_approvals`), the I/O blocks |
| `Aw.Sindri.Host` | The `recipe`, `workflow` and `outbox` verb groups, each with its own command table file (keeping S1's table within the size and complexity limits), the endpoints, the JSON context entries. Command classes are named by group and verb (`RecipeValidateCommand`), so `/new-slice` generates them |

### 7.2 S1 changes, each owned by an engine phase (W26)

| # | Change | Found on the S1 branch | Owner |
|---|---|---|---|
| S1-D1 | S1 merged | In progress | Every phase merges after it |
| S1-D2 | A ledger insert that joins an outer `SqliteUnitOfWork` without committing | `InsertOnce` commits its own transaction (`SqliteLedger.cs:56`) | P4 |
| S1-D3 | The outbox dispatcher under the lease, with the dispatch-time approval and allowlist recheck | `outbox_commands` exists (S1 migration 3); no dispatcher | P6 |
| S1-D5 | A server-issued challenge for typed confirmations. It also replaces `profile approve`'s body flag; that part is flagged to the S1 owner | `profile approve` trusts a `Confirmed` flag in the body | P2 |
| S1-D6 | An item or instance filter on `/sindri/events`, and `workflow events` | The SSE endpoint exists, with no filter | P4 |
| S1-D7 | `EventFactory.Entry` takes `item` and `causation` | It hardcodes `Item: null` | P4 |
| S1-D8 | Built-in services scheduled without a profile entry | Only profile-named services run | P4 |
| (met) | Cadence below a minute | `Cadence` accepts `5s`–`24h` | — |

Outside S1:
- `ILauncher` replaces the desk runtime when it lands (W20).
- The §7.1 router and the slot allocator (A 17.20 P1) are stubs in P7.
- Judge (#156) and Evidence (#158) come as blocks with the worker recipes, outside these phases.

## 8. Error catalogue (in `docs/csharp/sindri.md`)

| Code | Kind | Meaning | Fix shown |
|---|---|---|---|
| SND-RECIPE-001 | invalid | Validation failed (the `RCP-*` lines follow) | Fix each line |
| SND-RECIPE-002 | notFound | No recipe with that id or hash | `sindri recipe list` |
| SND-RECIPE-003 | conflict | The hash isn't approved | `sindri recipe approve` |
| SND-RECIPE-004 | forbidden | Approving needs the typed challenge at an interactive terminal | Run it in a terminal |
| SND-RECIPE-005 | conflict | The hash was revoked | Approve a new version |
| SND-RECIPE-006 | conflict | A pinned block or child changed since approval | Re-approve the recipe |
| SND-RECIPE-007 | conflict | Same id and version, different content | Raise `version` |
| SND-WF-001 | notFound | No such instance | `sindri workflow list` |
| SND-WF-002 | conflict | The instance changed; current version and nonce attached | Re-read and retry |
| SND-WF-003 | conflict | The trigger isn't allowed in this state | `allowedCommands` in `status` |
| SND-WF-004 | conflict | A gate refused; gate results attached | Act on the reasons |
| SND-WF-005 | conflict | A stale Step result (an earlier run) | None; it was dropped |
| SND-WF-006 | invalid | An ambiguous address | Use the full address shown |
| SND-WF-007 | forbidden | This actor can't fire this trigger (a session, or a grant without a terminal) | Run it yourself at a terminal |
| SND-WF-010 | unavailable | No runtime for a named block | A build with that runtime, or cancel |

## 9. Validator and eval format

### 9.1 Rules mapped to §17.28.5

Each rule has an invalid fixture that fails it (`tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/<rule>.yaml`).
Output is one line per failure, `RCP-…: <what> at <file>:<line>:<col> (<path>); <fix>`. Exit 1 on any failure.

| §17.28.5 bullet | Rules |
|---|---|
| Matches the JSON Schema | `RCP-SCHEMA-001` (shape, types, unknown fields, per-kind fields), `-002` (schema version), `-003` (refused YAML features, size, depth), `RCP-STATE-001`, `RCP-INIT-001`, `RCP-DUR-001` |
| Every block id is registered | `RCP-BLOCK-001` (unknown), `-002` (wrong kind for the slot), `-003` (parameters), `RCP-EVID-001`, `RCP-EVENT-001` (unregistered event kind) |
| Inputs produced upstream | `RCP-WIRE-001` (every path), `-002` (`produces` backed by the source), `RCP-WHEN-001` (outcome not declared by the source), `RCP-OUTCOME-001` (a declared Step, event or child outcome with no transition; `step.failed` defaults to a park and isn't required), `RCP-CHILD-001` (unknown child, or inputs not covered) |
| Capability blocks only in approved recipes | `RCP-CAP-001`…`007` (§4.3) |
| Every state reachable | `RCP-REACH-001` |
| A way out of every non-terminal state | `RCP-EXIT-001` |
| Timeouts on `wait` and `active` | `RCP-TIME-001` |
| Every loop has a cap | `RCP-LOOP-001`, `-002` (not on a cycle), `-003` (empty `noProgress`) |
| Allowlisted effects only | `RCP-EFFECT-001` |
| Hash matches | `RCP-HASH-001` |
| (added) | `RCP-TRIG-001`, `RCP-WHEN-002` (shadowed), `RCP-THRESH-001` (threshold on rung 1), `-002` (none on rung 2 or 3), `RCP-NEXT-001` (variable), `-002` (approval-tier verb), `RCP-CHILD-002` (a cycle in the child graph), `-003` (depth > 4), `RCP-VERSION-001`, `-002` (at register) |

### 9.2 Eval format

`evals/<recipe id>/<case>.jsonl`:
- line 1 is `{ "start": { "inputs": { … } } }`;
- each later line is `{ "trigger": { "kind", "outcome"?, "outputs"? }, "gates"?: { "<block>": "pass|fail|uncertain" },
  "expect": { "state", "park"?, "outcome"? } }`.

`recipe eval` folds the decisions over the triggers, using scripted gate answers, and reports the first line where
the result diverges. `--record` writes a live instance's history in the same format.

## 10. Engine properties mapped to tests

| Property | Test | Lane | Phase |
|---|---|---|---|
| Replaying an event log reproduces the instance | `Folding_the_events_rebuilds_the_row` (`EventFold` over recorded events, on every `WorkflowStoreContract` run); `Eval_cases_reach_their_expected_states` | unit, integration | 3, 4 |
| Applying any trigger twice is a no-op | `Same_trigger_twice_records_one_event` (decision and contract), `Retried_command_returns_AlreadyApplied_not_conflict` | unit, integration | 3, 4 |
| A stale epoch or version is refused | `Commit_refuses_a_stale_epoch` / `_a_stale_version` | unit (fake), integration | 4 |
| A stale Step result changes nothing | `Late_result_from_an_earlier_entry_is_dropped` | unit | 3 |
| A timer during a takeover does nothing | `Deadline_during_takeover_is_refused`, `Tick_skips_paused_deadlines` | unit, integration | 5 |
| Takeover of a parked instance returns to the park | `Handback_pops_to_the_park_with_its_reason` | unit | 3 |
| Child propagation (every row of §5.6) | `Child_<change>_<parent effect>` | unit, integration | 3, 7 |
| A refusal is never a dead end | `Refused_step_reruns_within_the_loop`, `Refused_wait_parks_needs_answer`, `Refused_budget_gate_parks_budget` | unit | 3 |
| Fail closed | `Unavailable_gate_refuses`, `Step_without_runtime_parks` | unit | 3 |
| No-progress, budget, grant | `Same_findings_twice_parks_no_progress`, `Exhausted_budget_parks`, `Grant_adds_n_rounds_and_needs_a_terminal` | unit | 3 |
| Sleep shifts, a slow tick doesn't | `Sleep_gap_shifts_deadlines`, `Slow_tick_shifts_nothing` | unit | 5 |
| Events wait for their instance | `Event_for_a_parked_instance_applies_after_the_answer`, `Event_for_a_left_entry_expires` | unit, integration | 4 |
| Approval re-checked at dispatch | `Revoked_recipe_effect_is_refused_at_dispatch` | integration | 6 |
| Transition, outbox and event commit together | `Failed_outbox_write_rolls_back_the_transition` | integration | 4, 6 |
| No approve route outside the CLI | `Ui_and_mcp_reference_no_approve_endpoint` (architecture) | unit | 2 |

## 11. Test strategy by lane, and telemetry

| Lane | Budget (per package) | Holds |
|---|---|---|
| `unit` | < 10 s | Every `RCP-*` rule (one invalid fixture each, plus valid ones); the hash and normalization; the schema and overlay staleness tests; the YAML features tests (`on` key, duplicates, anchors); every pure decision; the eval cases; a bounded seeded property run; the fake-store contract bases; `BlockContract` per block; architecture tests |
| `integration` | < 60 s | The same contract bases on the SQLite adapters; migrations on a copy of the previous schema; the loopback endpoints; CLI golden files (`recipe validate --json`, `status` layouts, `list` empty states); the timer, event and inbox services against SQLite with `FakeTimeProvider` |
| `heavy` | nightly | S1's AOT smoke, which gains `recipe validate` in P1 |

- No mocking libraries. Fakes and their contract bases arrive in the same PR. The clock and ids are always fakes.
- **Dev loop:** `AW_STATE_DIR=$(mktemp -d)`, then `dotnet run --project src/Sindri/Aw.Sindri.Host -- serve` in one
  terminal and verbs in another. The dev recipes in `recipes/dev/` drive the signals.
- **Telemetry:**
  - An `Aw.Sindri.Workflow` `ActivitySource` gives a span per `Commit` (recipe, `from`, `to`, result).
  - Metrics: transitions, refusals by block, parks by reason, no-progress parks, escalations, version conflicts,
    inbox depth, deadline lag, and desk task age.
  - All of it is scrubbed by `Aw.Redaction`. These metrics feed A §17.2.

## 12. The first recipes

- **`workflow.item`** and **`workflow.step`** (below). They live in `recipes/sindri/`, with their evals in
  `recipes/sindri/evals/`.
- **Dev recipes** for the signals live in `recipes/dev/`:
  - `workflow.dev-timeout` (a desk Step, a `1m` timeout);
  - `workflow.dev-notify` (an `effect.notify.desk` on a transition).
- **Validate-only fixtures** show that v1 expresses the later machines, without new blocks:
  - `workflow.bugfix` (the §17.28.1 example in long form);
  - `workflow.direction` (leg timeouts and a bounded exchange loop).
- **Blocks in these recipes:**

| Block | First runtime |
|---|---|
| `step.produce`, `step.checks`, `step.verify` | Desk (W20) |
| `gate.budget.available` | Real, pure |
| `gate.route.startable` | Stub over the profile mode |
| `effect.claim.ref` | Real (P6). Its existence check treats a ref that names this instance's command id as `created` |
| `effect.notify.desk` | Real (P6) |

- **Events:**

| Event | Outcomes |
|---|---|
| `item.routed` | `startable`, `notStartable` |
| `claim.created` | `created`, `claimExists` |

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
               next: "Waiting for a session slot for {item}." }
  claimed:   { kind: wait, waitFor: { event: claim.created }, timeout: { after: 10m, then: retry },
               next: "Waiting for the claim ref sindri/{user}/{item}." }
  running:   { kind: wait, timeout: { after: 2d, then: park },
               waitFor: { child: { recipe: workflow.step, inputs: { spec: { fact: fact:stepSpec } }, onParked: propagate } },
               next: "Waiting for {child}." }
  done:      { kind: terminal, outcome: resolved }
  satisfied: { kind: terminal, outcome: alreadySatisfied }
  dropped:   { kind: terminal, outcome: unresolved }
transitions:
  - { from: triaged,   to: startable, on: wait.satisfied, when: { outcome: startable },
      gates: [gate.route.startable], produces: [fact:stepSpec] }
  - { from: triaged,   to: dropped,   on: wait.satisfied, when: { outcome: notStartable } }
  - { from: startable, to: claimed,   on: wait.satisfied, gates: [gate.budget.available], effects: [effect.claim.ref] }
  - { from: claimed,   to: running,   on: wait.satisfied, when: { outcome: created } }
  - { from: claimed,   to: dropped,   on: wait.satisfied, when: { outcome: claimExists } }
  - { from: running,   to: done,      on: wait.satisfied, when: { outcome: resolved } }
  - { from: running,   to: satisfied, on: wait.satisfied, when: { outcome: alreadySatisfied } }
  - { from: running,   to: dropped,   on: wait.satisfied, when: { any: [ { outcome: unresolved }, { outcome: failed }, { outcome: cancelled } ] } }
evals: [evals/workflow.item/happy.jsonl, evals/workflow.item/child-parks.jsonl, evals/workflow.item/claim-exists.jsonl]
```

```yaml
schema: 1
id: workflow.step
version: 1
inputs: [fact:spec]
initial: produce
states:
  produce: { kind: active, step: step.produce, loop: revisions, timeout: { after: 4h, then: park },
             budget: { tokens: 200k }, next: "Produce the Step output for {item} (round {loop.revisions}/{loop.revisions.cap})." }
  checks:  { kind: active, step: step.checks, timeout: { after: 30m, then: retry },
             next: "Run the Step's deterministic checks." }
  verify:  { kind: active, step: step.verify, timeout: { after: 1h, then: park },
             next: "Review the output as the verifier." }
  done:    { kind: terminal, outcome: resolved }
transitions:
  - { from: produce, to: checks,  on: step.completed, when: { outcome: done }, produces: [fact:output] }
  - { from: checks,  to: verify,  on: step.completed, when: { outcome: passed } }
  - { from: checks,  to: produce, on: step.completed, when: { outcome: failed },   produces: [fact:findings] }
  - { from: verify,  to: done,    on: step.completed, when: { outcome: accepted } }
  - { from: verify,  to: produce, on: step.completed, when: { outcome: rejected }, produces: [fact:findings] }
loops: { revisions: { cap: 3 } }
budget: { tokens: 400k }
evals: [evals/workflow.step/happy.jsonl, evals/workflow.step/no-progress.jsonl, evals/workflow.step/rounds.jsonl]
```

`step.produce` declares the outcome `done` and the output `fact:output`. `step.checks` declares `passed` and
`failed`, with `fact:findings`. `step.verify` declares `accepted` and `rejected`, with `fact:findings`.

## 13. Review resolutions

/autoplan (2026-10-10) ran five lenses, all NEEDS_WORK, with no CRITICAL findings. The consolidated review is in
`~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-workflow-engine/consolidated-review.md`. Every HIGH and
every MEDIUM is fixed in the doc. The LOWs are answered here.

| Id | Resolution |
|---|---|
| C1 | W12, §5.3, §5.4: trigger-keyed idempotency, key checked first, stale run tokens dropped |
| C2 | W14, §5.4: each retry is a new trigger, and what follows a refusal is defined |
| C3 | W15, §5.6: event router and inbox |
| C4 | §2 `EventDescriptor`, `RCP-EVENT-001`, §12 events table |
| C5 | W16, §5.1 resume stack |
| C6 | W17, §5.6 children table, child ids |
| C7 | W20 desk runtime; every phase signal now reaches an outcome |
| C8 | W6, §3.8 engine-owned states with texts, outcomes and exits |
| C9 | §4.2 preview and challenge, §6.2 answer flow, §6.3 layouts |
| C10 | Phases re-cut: descriptors and recipes in P1, eval presence in P2 and running in P3, dev recipes with real blocks, P7 sized L |
| C11 | §6.1 process model and addressing, S1-D8 built-in services |
| C12 | W1, §2 registration and placement |
| C13 | W11, §4.1 pins, `SND-RECIPE-006` |
| C14 | §8 lives in `docs/csharp/sindri.md`; `docs/sindri/errors.md` (generated from TS) is untouched |
| C15 | W26, §7.2 owners and corrected findings |
| C16 | `RCP-CAP-005`, `006`; W9 base chosen by code; the freeze shows the diff |
| C17 | `RCP-CAP-007`, `{ profile }` references, run-time intersection |
| C18 | W22 actor classes, `SND-WF-007`, grant capped and terminal-only |
| C19 | W18, §5.5 |
| C20 | W21, `EventFold` test |
| C21 | `RCP-WIRE-002`, `RCP-OUTCOME-001`, `RCP-CHILD-002`/`003`, `RCP-THRESH-002`, `RCP-VERSION-001`/`002` |
| C22 | §3.4 `retry` re-emits with derived command ids; §12 claim existence check |
| C23 | §4.2 challenge; architecture test; S1-D5 |
| C24 | W11 cascade; children pinned by hash; `RCP-CHILD-002`/`003` |
| C25 | §3.1 refused YAML features, normalization, JCS |
| C26 | §4.1 registry events; §5.2 scrubbing |
| C27 | §9.2 `eval --record`; §12 validate-only fixtures; `/new-block` deferred (below) |
| C28 | §6.2 `--needs-me`, `--json` everywhere, empty states; §6.2 notifications and `allowedCommands` |
| C29 | §6.2 exit codes; §11 telemetry and dev loop; §3.1 positions; §9.2 eval format; §7.1 command names and ≤ 5 dependencies; docs per phase in the Phases; reserved migration ranges |
| C30 | W19 `takeoverIdle` |
| C31 | §3.8 revoke text; §6.2 one `workflow` group (with D §9.3's `sindri handback` kept as an alias); §3.7 mood rule and `{user}` |

**Written answers (C32 and crafted bypasses):**

- **P7, host move:** approvals live in `ledger.db`, and A 17.19's handover moves the signed ledger snapshot, so
  approvals travel with the host. A different host never approves on another host's behalf.
- **P8, adoption metrics:** §11's metrics, plus the outcome counts per recipe, feed A §17.2's weekly digest.
  Nothing new is needed.
- **D-F11, D-F13, D-F14, D-F15:** durations use one format and plain output uses ASCII (§6.3); rule lines carry
  the fix (§9.1); `--version` is optional (§6.1); `alreadySatisfied` maps to `satisfied` (§12).
- **X LOW:**
  - The redundant `ThresholdApplies` field is removed (W4).
  - The verb groups are unified (§6.2).
  - Stacked worktrees follow the `/specToProvenPR` orchestrator's rebase duty (Phases).
- **A-F20, gate re-run after a conflict:** recorded gate answers are reused per trigger (§5.3), so a judge question
  isn't asked twice.
- **A-F21, outbox ids:** they are derived (`cmd:{instance}:{entry}:{n}`).
- **A-F23, validation depends on the profile:** only `RCP-EFFECT-001` reads it, and it reports `skipped` without one
  (§6.1).
- **A-F24, internal retry policy:** at most 3 re-decisions on a version conflict (§5.3); the outbox uses Kernel's
  `OutboxRetryPolicy`.
- **A-F25, indexes:** each migration in P4–P7 declares the indexes its queries use. They are `workflow_instances
  (item, attempt)`, `workflow_deadlines (due) WHERE due IS NOT NULL`, `workflow_inbox (instance, status)` and
  `outbox_commands (status, next_attempt)`. The migration tests cover them.
- **S-L1, injection through ids:** item ids are typed (`ItemId`: letters, digits, `-`, `_`), and effects build
  refs and argv from typed values, never by interpolating strings.
- **S-L2, alias bombs and eval globs:** aliases are refused (§3.1), and eval paths must stay in the recipe's
  directory (§3.2).
- **S-L3, provenance:** the `actor` on every envelope comes from the host channel: CLI at a TTY, CLI without a TTY,
  MCP, or a service (W22). `register` records the actor, not a claim.
- **S-L5, self-reported cost:** desk Steps report what the human enters. Launcher-backed Steps are metered by the
  model proxy (A 17.19's budgets are enforced there), and the engine's budget rule is a second line of defence.
- **S-L6, `next` hints:** `RCP-NEXT-002`.
- **S-L8, SSRF in future providers:** out of scope. The egress proxy allowlist (D §8.2) bounds it when those blocks
  arrive.
- **Crafted bypasses (S-L4 direct SQLite writes, S-L7 unauthenticated loopback callers and port squatting, a
  hand-built approve POST):** outside the threat model (A 17.28.3; `docs/csharp/bugfix.md` "Threat model"). Gates
  stop honest mistakes. The sandbox, file permissions (the state directory is owner-only, from S1) and the
  loopback-only bind are the boundary. Their containment belongs to the sandbox work, not to this engine.
- **`/new-block` scaffold (C27):** deferred until three real blocks exist, so the generator has real examples. Until
  then `docs/csharp/sindri-workflow.md` holds the hand-written recipe for a block.

## Phases

Each phase is one PR. Phase 1 is stacked on `feat/sindri-s1-app-shell`. Each later phase stacks on the phases it
depends on until S1 merges, then targets `main`. Each phase updates `docs/csharp/sindri-workflow.md` and
`docs/csharp/sindri.md` for what it adds. Sizes are a rough guide: S is a few files, M is one subsystem slice, L is
a store or several slices with migrations.

### Parallel execution with an agent team

`/specToProvenPR` runs these phases with an agent team and keeps every agent busy by working from the graph below.
It doesn't run the phases in number order. Phases are cut so that independent ones run at the same time, and units
inside a phase write to disjoint files.

**Phase graph** (an arrow means "needs"):

```
P1 model + validator ──┬──► P2 registry + approval ───────────────────────────────┐
                       └──► P3 pure engine + evals ──► P4 store, start, desk, ──┬──┼──► P6 effects (S1-D3) ──┐
                                                       events (S1-D2,D6,D7,D8)  │  │                         ├──► P7 children + item
                                                                                └──┴──► P5 timers, takeover ─┘
```

| Wave | Runs in parallel | Starts when |
|---|---|---|
| 1 | P1 | Now (stacked on S1's branch) |
| 2 | P2 ∥ P3 | P1's contract unit (records and catalog) is pushed |
| 3 | P4 (P2 continues if it hasn't finished) | P3's decision signatures are committed, and S1 has merged (P4 changes S1 code) |
| 4 | P5 ∥ P6 | P4's `Commit` and `WorkflowStoreContract` are green. P6 also needs P2 |
| 5 | P7 | P5 and P6 are merged |

**Units inside a phase.** A phase's first unit is the **contract unit**: the records, ports and stubs everything
compiles against, committed red. It runs alone, and the other units fan out after it, one implementer each.

| Phase | Contract unit | Then in parallel |
|---|---|---|
| P1 | Recipe records, typed ids, descriptors, `BlockCatalog`, `ValidationContext`, the validator shell with an `IRecipeRule` list | (a) reader, normalization, JCS hash, `RCP-SCHEMA/STATE/INIT/DUR/HASH`, the generated schema and overlay · (b) graph rules `RCP-REACH/EXIT/LOOP/WHEN-002/CHILD-002/003` · (c) wiring and block rules `RCP-WIRE/BLOCK/EVID/EVENT/WHEN-001/OUTCOME/TRIG/THRESH/NEXT` · (d) `RCP-CAP-001/002/004–007` over a base snapshot · (e) the first-recipe descriptors and event descriptors, `recipes/sindri/*.yaml`, `recipes/dev/*.yaml`, the bugfix and direction fixtures · (f) the CLI verb, golden files, `docs/csharp/sindri-workflow.md` |
| P2 | `IRecipeStore`, `IRecipeApprovals`, fakes, contract bases | (a) SQLite adapters and migrations · (b) register, list, show and preview · (c) approve and revoke with the challenge (S1-D5) and the cascade · (d) pins, `RCP-VERSION-*`, registry events, the architecture test |
| P3 | Instance record, trigger types, decision signatures | (a) `TransitionDecision` and `RefusalRules` · (b) `LoopRules` and `BudgetRules` · (c) resume stack and engine-owned states · (d) `ChildPropagation` decisions · (e) `EventFold` and the in-memory store with `WorkflowStoreContract` · (f) the eval format, `recipe eval`, the eval files, approve running evals |
| P4 | `IWorkflowStore` SQLite skeleton, the S1-D2/D7 changes | (a) `SqliteWorkflowStore`, migrations, the contract binding · (b) `StartInstance`, `ApplyCommand`, nonces · (c) desk runtime and `CompleteStep` · (d) `RouteEvents`, `DrainInbox`, built-in services (S1-D8) · (e) `GetInstance`, `ListInstances`, `workflow` verbs, `events` with the filter (S1-D6), golden layouts |
| P5 | The deadline model | (a) `TickTimers` and the sleep shift · (b) takeover, handback, `takeoverIdle` |
| P6 | The `IEffectKind` runtime and dispatcher shell | (a) outbox writes in `Commit` and `retry` re-emission · (b) the dispatcher, allowlist and approval recheck (S1-D3) · (c) `effect.notify.desk`, the notifications, `outbox list` · (d) `effect.claim.ref` |
| P7 | The child row and inbox kinds | (a) child creation and propagation · (b) router and slot stubs, `gate.route.startable`, `gate.budget.available` · (c) `workflow.item` with its evals, end to end |

**Hotspots.** Units never edit these files. They write fragments, and the phase's integrator merges them:

| Hotspot | Rule |
|---|---|
| `PublicAPI.Unshipped.txt` | Regenerated by the integrator from `RS0016` |
| `SindriErrors.cs`, `docs/csharp/sindri.md` | Reserved ranges: P1 `SND-RECIPE-001`, P2 `002`–`007`, P3 and P4 `SND-WF-001`–`007`, P6 and P7 `SND-WF-010`–`019` |
| `Add…()` methods, `SindriJsonContext` | One private `Add<Unit>()` method or one partial-context file per unit; the integrator adds the calls |
| Migrations | Reserved number ranges: P2 `W0xx`, P4 `W1xx`, P5 `W2xx`, P6 `W3xx`, P7 `W4xx` |
| Command tables | The integrator; units add their command class |

**Roles:**
- **Orchestrator** (the `/specToProvenPR` session): holds the graph and the queue of ready units, dispatches the
  next one as soon as an implementer frees up, and rebases the stacked branches. It writes no product code.
- **Implementer:** one per unit, in its own worktree, red first. It never touches a hotspot file.
- **Integrator:** one per phase. It merges the hotspot fragments, runs the full gate (build with zero warnings, the
  unit and integration lanes within budget, `format`, `check-deps`, `check-rules`), and opens the draft PR.
- **Prover:** one per phase PR. It runs the phase's **Signal** in the running app and records the evidence.
- **Reviewers:** `/review` on each phase PR until no blocking or issue findings remain. Their fixes return to
  implementers as new units.

**Limits:**
- At most two units build or test at a time, because of the heavy-job lock's two slots. The other units keep
  writing code meanwhile.
- The orchestrator respects the session cap.
- A phase's review loop runs while the next wave's contract units are being written. Review is never skipped to
  free a slot.

### Phase 1: the recipe model and validator

- **Goal:** recipes parse, normalize, hash and validate against the catalog, and `sindri recipe validate` reports
  named rules in-process.
- **Files:**
  - `src/Sindri/Aw.Sindri.Core/Workflow/**`;
  - `src/Sindri/Aw.Sindri.Features/ValidateRecipe/**`, `Features/Blocks/**` (descriptors only);
  - `src/Sindri/Aw.Sindri.Infrastructure/Workflow/YamlRecipeReader.cs`;
  - `src/Sindri/Aw.Sindri.Host/Commands/Recipe/**`;
  - `schemas/recipes/*.json`;
  - `recipes/sindri/*.yaml`, `recipes/dev/*.yaml`;
  - `tests/Aw.Sindri.Tests/Workflow/**`, `tests/Aw.Sindri.Testing/Workflow/**`;
  - `docs/csharp/sindri-workflow.md`.
- **Size:** L.
- **S1 dependency:** stacked on S1's branch (S1-D1 to merge).
- **Signal:**
  - `sindri recipe validate tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-REACH-001.yaml` exits 1 with
    `RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a
    transition into it or remove it`.
  - `sindri recipe validate recipes/sindri/workflow.step.yaml` exits 0 and prints its hash.
  - The bugfix and direction fixtures validate.

### Phase 2: registry and approval

- **Goal:** recipes are registered by hash and approved through a previewed, challenged desk action, with pins and
  cascade.
- **Files:**
  - Core ports, slices `Register`, `Show`, `Approve`, `Revoke`, `List`;
  - SQLite adapters and migrations `W0xx`;
  - the challenge (S1-D5);
  - registry events;
  - contract bases and fakes;
  - the architecture test;
  - docs.
- **Size:** M.
- **S1 dependency:** S1-D5. Until P3, approval requires the `evals` files to exist, and from P3 on it also runs
  them.
- **Signal:**
  - `sindri recipe approve recipes/sindri/workflow.step.yaml` prints the preview (capability uses, pinned blocks,
    the diff) and asks for `approve workflow.step 1`. Then `sindri recipe list` shows
    `workflow.step@sha256:… approved (desk)`.
  - `echo | sindri recipe approve …` without a TTY exits 1 with `SND-RECIPE-004`.

### Phase 3: the pure engine and recipe evals

- **Goal:** every decision is pure Core, proven by eval cases and the event fold.
- **Files:**
  - `Core/Workflow/Engine/**`;
  - the in-memory `WorkflowStore` and `WorkflowStoreContract`;
  - `EvalRecipe` and `recipe eval`;
  - `recipes/*/evals/**`;
  - approve running the evals;
  - docs.
- **Size:** L.
- **S1 dependency:** none beyond S1-D1.
- **Signal:**
  - `sindri recipe eval recipes/sindri/workflow.step.yaml` prints `happy pass`, `no-progress pass`, `rounds pass`.
  - Editing an `expect` in a copy makes it exit 1, naming the line that diverged.

### Phase 4: the store, start, desk Steps and event delivery

- **Goal:** instances start and move through fenced, trigger-keyed commits; desk Steps complete them; events reach
  them through the inbox.
- **Files:**
  - `SqliteWorkflowStore`, migrations `W1xx`;
  - slices `StartInstance`, `ApplyCommand`, `CompleteStep`, `GetInstance`, `ListInstances`, `RouteEvents`,
    `DrainInbox`;
  - the desk runtime;
  - the built-in services;
  - the S1-D2, D6, D7 and D8 changes;
  - the `workflow` verbs and golden layouts;
  - docs.
- **Size:** L.
- **S1 dependency:** S1 merged, then this phase makes the D2, D6, D7 and D8 changes itself.
- **Signal:**
  - `sindri workflow start workflow.step --item T-1 --fact fact:spec=spec.md` and `sindri workflow status T-1`
    show the desk task at `produce`.
  - Three `sindri workflow complete` calls (`done`, `passed`, `accepted`) move it to `done (resolved)`, and
    `sindri workflow events T-1` lists every transition.
  - Re-sending a `complete` with its used nonce returns `AlreadyApplied` and records nothing.

### Phase 5: timers, takeover and handback

- **Goal:** timeouts fire as keyed transitions, sleeps shift them, and takeovers pause them.
- **Files:**
  - `TickTimers`, the sleep shift, migrations `W2xx`;
  - takeover, handback and `takeoverIdle`;
  - docs.
- **Size:** M.
- **S1 dependency:** none (the cadence is met).
- **Signal:**
  - `sindri workflow start workflow.dev-timeout --item T-2`, wait a minute, and `status` shows
    `parked: timeout`.
  - A second instance, taken over right after it starts, shows `taken-over` with `timeout paused (0m 40s left)`.
    Nothing fires until `sindri workflow handback`; then it parks a further 40 s later.

### Phase 6: effects through the outbox

- **Goal:** effects are outbox commands in the transition's transaction; the dispatcher re-checks approval and the
  allowlist; parks notify the human.
- **Files:**
  - the `IEffectKind` runtime and outbox writes;
  - the dispatcher (S1-D3);
  - `effect.notify.desk`, `effect.claim.ref`;
  - notifications;
  - `outbox list`;
  - migrations `W3xx`;
  - docs.
- **Size:** M.
- **S1 dependency:** S1-D3, made by this phase, and P2.
- **Signal:**
  - `sindri workflow start workflow.dev-notify --item T-4`: the `workflow.transition` event lists its outbox id,
    `sindri outbox list` shows it dispatched, and a desk notification appears.
  - After `sindri recipe revoke workflow.dev-notify@…`, a second instance's effect shows `refused at dispatch
    (SND-RECIPE-005)`.

### Phase 7: children and the item lifecycle end to end

- **Goal:** `workflow.item` runs with a `workflow.step` child; every child change propagates.
- **Files:**
  - child creation and propagation, migrations `W4xx`;
  - router and slot stubs, `gate.route.startable`, `gate.budget.available`;
  - `recipes/sindri/workflow.item.yaml` with its evals;
  - docs.
- **Size:** L.
- **S1 dependency:** none beyond P4–P6.
- **Signal:**
  - `sindri workflow start workflow.item --item T-3` shows `triaged -> startable -> claimed -> running` in
    `workflow events T-3`, and `git show-ref sindri/<user>/T-3` finds the claim ref.
  - Completing the child's three desk Steps moves the parent to `done (resolved)`.
  - On a second item, `T-5`, completing the child's `checks` as `failed` twice with the same findings parks the
    child with `no-progress`, and the parent shows `parked (no-progress, child T-5/item/1/step)`. Answering the
    parent with `--grant 1` at a terminal is forwarded to the child and resumes both.
