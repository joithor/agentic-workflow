# Sindri workflow engine: recipes, blocks, validator and engine

**Status:** design, revision 5 (epic reconciliation with the API surface and context retrieval specs, 2026-10-11), awaiting approval · **Date:** 2026-10-10 · **Builds:**
factory addendum §17.28 (one workflow state machine) and §17.29.4 step 2 · **Next:** `/specToProvenPR` takes the
Phases at the end to PRs.

This doc makes §17.28 concrete. It doesn't reopen §17.28 or §17.29, which are settled (decisions Q6 and Q7). It
settles the block contract, the recipe schema, approval and invariant 13, the instance and its events, the CLI and
API surface, the boundary with S1's app shell, and the first recipes. §13 maps every review finding to its fix:
C1–C32 from pass 1, E1–E24 from pass 2 and G1–G18 from pass 3. Revision 5 applies the epic reconciliation
(`docs/sindri/README.md`, X1–X17): callers and action classes from the API spec (§6.2), commands behind its chain
(§5.3), one needs-you list and one notifier (§6.3), `$AW_SINDRI_HOME` (§6.1), the `heavy` allocator (§3.4), the
`StepInput` block kind (§2), and `ILauncher` in API P10 (§7.2). The second epic batch (X19–X33) adds the agent
runtime (§6.5, P8), drops `workflow.request`, makes P4 depend on API P3 for items, and moves every proof to
`sindri-prove.sh`. The third batch (X35–X42) adds the route table (§6.3), puts the agent runtime on API §8.1's
launch contract (§6.5), and adds P8 and the API edges to the phase graph.

References:
- the factory addendum `2026-10-09-sindri-factory-addendum.md` (A) and the core design `2026-10-07-sindri-design.md` (D);
- `docs/csharp/bugfix.md`, the reference for gates and state;
- `.agents/rules/csharp.md` and `csharp-testing.md`;
- S1, the app shell on `feat/sindri-s1-app-shell`, whose C# catalogue, verbs and exit codes are in
  `docs/csharp/sindri.md`. Its types are named as they are on that branch: `ILedgerStore`, `ILease`, `LeaseGrant`,
  `LeaseDecision.Holds`, `IScheduledService`, `EventFactory`, `EventScope`, `Output`, `SindriErrors` and
  `LedgerDatabase`.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| W1 | **A block is a DI-registered class that implements its kind interface (`IGate`, `IEvidenceProvider`, `IStep`, `IEffectKind`) and exposes a static `BlockDescriptor`**, registered generically (`services.AddGate<T>()`). `BlockCatalog` (descriptors) and `BlockRuntime` (from `IEnumerable<…>`) are built from those registrations | The validator stays pure, registration is AOT-safe, and there's no service locator (`ARCH-DI-001`) |
| W2 | **Wiring is checked on every path** | A fact produced on only some paths is a latent refusal at run time |
| W3 | **Recipe schema v1 is closed** (§3). The reader normalizes short forms to the long form, and the schema and the hash describe the long form | Generated recipes can't slip logic in, and no union converters are needed |
| W4 | **The rung comes from the block's descriptor.** Rung-2 and rung-3 gates must have a `threshold` | A recipe can't promote a judge question to deterministic |
| W5 | **Fail closed.** An unknown block is a validation error. An unavailable gate or evidence runtime refuses. A Step with no runtime parks with `SND-WF-010`, unless its descriptor declares `deskCapable`, in which case it runs on the desk runtime (W20) | Nothing passes silently, and the desk fallback is opt-in per block |
| W6 | **Outcomes are a closed set:** `resolved`, `unresolved`, `alreadySatisfied` and `failed` for recipe terminals, plus the engine's `cancelled` | Receipts count outcomes |
| W7 | **Grants are explicit and need a terminal.** `--grant n` (1 ≤ n ≤ the loop's cap) answers a `rounds` or `no-progress` park. `--grant-tokens n` (at most the original budget, once per park) answers a `budget` park | Every park has a useful answer (E10). A session can never grant itself anything |
| W8 | **Every recipe needs a human approval to run.** Approvals are stored keyed by `(id, version, hash)`, never in the file | §17.19: workflow templates are approval-tier |
| W9 | **Hand-written recipes are approved with `sindri recipe approve`**, through a server-issued challenge. **Generated recipes are approved by the packet freeze**, from a `base` that Sindri code picks | Invariant 13 |
| W10 | **`RCP-CAP-001`–`007` keep a generated recipe at or below its base** (§4.3), comparing guards by path and loops by state | C16, C17, E19 |
| W11 | **Approval and pins are checked on every `Commit` and at dispatch.** Approval pins the descriptor hashes of the blocks the recipe names and the hashes of its children. A revoke cascades to the generated recipes approved from it, and wakes running instances so they park at once. `cancel`, `takeover`, `handback`, `takeoverIdle` and engine-owned notifications are exempt from the check. Drift is fixed by re-approving the same hash with the new pins | Drift and revokes take effect immediately, and every instance can still be cancelled or resumed (C13, E15, G2, G7) |
| W12 | **A transition is keyed by its trigger:** `wf:{instance}:{trigger}`. `Commit` checks, in order: the actor, the key (a replay returns the recorded result), the nonce, the fence, the approval, the version | Replays and redeliveries are no-ops, and a replayed refusal reports the refusal (C1, G6) |
| W13 | **One `Commit` per trigger**, in one SQLite transaction, with one event per instance it changes (the instance's own event, plus `workflow.started` for a child it creates): the event(s), the version bump, the instance row (and a new child row), deadlines, outbox commands with their id map, inbox rows and consumed nonces. Every change to an instance row goes through it | A 17.28.1; E13 |
| W14 | **Each refusal consumes its trigger, and what follows is defined** (§5.5) | C2 |
| W15 | **External events reach instances through the engine inbox** (§5.7) | C3 |
| W16 | **Engine-owned states are frames on a stack**, at most two deep. A park, an escalation or a takeover suspends the current frame (`entry`, run, child, deadlines) together with its **resume action**, and the answer that pops it runs that action (§5.8). Deadline ids carry an arm sequence | Inbox rows, run tokens and children survive a park or takeover; an answer always does something (E1, G1) |
| W17 | **Children are created in the parent's `Commit`** and propagate both ways through the inbox (§5.7), keeping the actor of a forwarded command | C6, E14 |
| W18 | **Timers are deadlines applied by built-in services.** Sleep and downtime count as time when nothing ran. They're detected through an `IMonotonicClock` port alongside `TimeProvider`, and applied per instance as keyed `shift` triggers | Replayable and versioned (C19) |
| W19 | **A takeover pauses deadlines; a park stops them** (its answer re-arms them fresh). Each takeover arms a `takeoverIdle` deadline that notifies the human once and clears | D §9.3 |
| W20 | **The desk runtime runs `deskCapable` Steps until the agent runtime (§6.5, P8) runs them.** The state stays `active` with an open task. Its timeout is at least the profile's `workflow.deskTimeout` (default 2d) and always ends in `park`. The human reports with `sindri workflow complete --run <token>`. Reports are validated synchronously. Outcomes are labeled `via: desk` | Every phase reaches a real outcome without letting the agent-sized timeouts park the human's own work (C7, E11) |
| W21 | **Every event carries the full post-state delta** | Folding the log rebuilds the row (C20) |
| W22 | **Callers and permissions follow the API surface spec** (API §4, §6): a caller class (`human`, `session`, `service`) with a terminal flag, and an action class per command or variant. Every engine command runs through API's command chain (gate, audit, authorization). A `protected` action also needs the server challenge (§4.2) typed at a TTY (§6.2) | One permission model for every surface; text typed into a session is never a decision (E2) |
| W23 | **The engine lives in `Aw.Sindri.Core/Workflow` and in Sindri's Features, Infrastructure and Host. Phase 1 is stacked on `feat/sindri-s1-app-shell`.** Stacked phases add files and append registrations; changes to S1's behaviour wait until S1 merges | A 17.28.3; the S1 session's work isn't disturbed |
| W24 | **The first recipes are `workflow.item` and `workflow.step`** (§12). Dev recipes in `recipes/dev/` use real, harmless blocks for the phase signals | No fixture code ships in the binary |
| W25 | **The JSON Schema is generated from the long-form records**, with a hand-written editor overlay. Staleness tests guard both | The `aw-packets` precedent |
| W26 | **Each S1 change is owned by one engine phase** (§7.2) | No dependency without an owner |
| W27 | **Exit codes:** the checker verbs (`recipe validate`, `recipe eval`) return 0 for pass, 1 for fail and 2 for usage or tool errors, like `aw-packets`. Every other verb uses S1's `Output.Error`: 1 when Sindri isn't running, 2 for any other error, with the line `CODE message (fix: …)` | Consistent with the repo's checkers and with S1 (E8) |

## 2. The block contract

```csharp
public enum BlockKind { Gate, EvidenceProvider, Step, Effect, StepInput }

[Flags]
public enum Capability { None = 0, Tools = 1, Effects = 2, AutonomyTier = 4, WriteScope = 8, Network = 16, Budget = 32 }

public enum Rung { Deterministic = 1, JudgeQuestion = 2, BoundedModel = 3 }

public sealed record BlockDescriptor(
    BlockId Id, BlockKind Kind, int Version,
    IReadOnlyList<FactKey> Inputs,
    IReadOnlyList<OutputSpec> Outputs,            // (fact, requiredOn: outcomes); checked against every result and desk report
    IReadOnlyList<OutcomeName> Outcomes,
    Capability Capabilities,
    IReadOnlyList<ParameterSpec> Parameters,
    Rung? Rung,                                   // gates only
    bool DeskCapable);                            // Steps only: may run on the desk runtime (W5, W20)

public sealed record ParameterSpec(string Name, ParameterType Type, bool Required, bool IsCapabilityField);

public sealed record EventDescriptor(
    EventKind Kind,                               // claim.created, item.routed
    string Producer,                              // the actor that may write it (a service or adapter id)
    IReadOnlyList<OutcomeName> Outcomes,          // read from the event's `outcome` field
    IReadOnlyList<FactKey> Facts);                // facts the event records

public interface IDescribedBlock { public static abstract BlockDescriptor Descriptor { get; } }
```

- **Capability-bearing:** `Capabilities != None`, a parameter with `IsCapabilityField`, or (in a generated recipe) a
  child.
- **Runtime interfaces:**
  - `IGate.Evaluate(GateContext) → GateResult(pass|fail|uncertain, score?, reasons)`;
  - `IEvidenceProvider`, per A 17.11;
  - `IStep.Start(StepContext)` and `IStep.Complete(StepRun, StepReport) → StepResult`;
  - `IEffectKind.Build(EffectContext) → payload`;
  - `IStepInput.Fill(StepInputContext) → InjectionSections`: fills sections of a Step's injection plan (A §17.8) before
    `Start`, for example the context engine's `input.context.retrieve` (context spec Phase 10). Declared in P1; the
    first runtime comes with that phase.
- **`Start` runs after the `Commit`** that opened the run. The engine's post-commit loop calls it, keyed by the run
  token, so a re-run after a crash is a no-op.
- **Registration and placement:**
  - `services.AddGate<T>() where T : class, IGate, IDescribedBlock`, and likewise `AddStep`, `AddEffect`,
    `AddEvidence` and `AddEvent(descriptor)`. Blocks are scoped; the catalog is a singleton.
  - Pure blocks live in `Features/Blocks/<Kind>/`, I/O blocks in `Infrastructure/Blocks/<Kind>/`.
  - A block over a lower-tier package (Judge, Evidence, Packets, BugFix's rules) lives in Sindri and reaches that
    package through its `Core` or CLI, under a declared `deps.yaml` edge.
- **Descriptor hash:** SHA-256 over the RFC 8785 canonical JSON of the descriptor. A behaviour change to a block
  bumps `Version` (reviewed; §13 S2-L-c).
- **Tests:** `BlockContract` in `Aw.Sindri.Testing` is the base each block's contract tests derive from. It runs once
  per block.
- **Guide:** `docs/csharp/sindri-workflow.md` (P1) holds the hand-written recipe for a block and the recipe
  reference.

## 3. Recipe schema v1

### 3.1 Reading

- **Refused YAML** (`RCP-SCHEMA-003`): duplicate keys, anchors, aliases, merge keys, explicit tags, more than 256 KiB,
  or nesting deeper than 32.
- **Scalars:** the YAML 1.2 core schema, so `on` stays a string.
- **Errors:** every node keeps its line and column, and every schema error is reported, not just the first.
- **Normalization:** short forms become long forms, then typed binding runs with
  `JsonUnmappedMemberHandling.Disallow`.
- **Hash:** `sha256:` over RFC 8785 (JCS) of the normalized document without `hash`, so a short form and its long
  form hash the same. Hashes are shown as 12 hex characters, and input accepts a prefix of 8 or more.

### 3.2 Top level

| Field | Req. | Rule |
|---|---|---|
| `schema` | yes | `1` |
| `id` | yes | `workflow.<name>` |
| `version` | yes | `RCP-VERSION-001` (same id and version, different hash); `-002` (not above the highest registered) |
| `description` | no | |
| `base` | generated only | Written by Sindri's generator. A hand-written recipe with `base` is `RCP-CAP-004` |
| `inputs` | no | Fact keys. The long form is `{ key, label?, type?: text \| file \| item }`; `recipe show` and `GET /workflow/recipes` return them so the app can render a start form |
| `initial` | yes | An `active` or `wait` state |
| `states` | yes | At least one non-terminal and one terminal state; reserved names refused |
| `transitions` | yes | |
| `loops`, `budget` | no | |
| `evals` | to approve | Paths under `evals/<id>/`, inside the recipe's directory (§9.2) |
| `hash` | no | Checked when present |

Reserved state names: `cancelled`, `escalated`, `parked`, `taken-over`.

### 3.3 Value grammars

| Grammar | Form |
|---|---|
| Fact key | `fact:<name>` or `evidence:<kind>@<base\|head>` |
| Block id | `<gate\|step\|effect\|evidence\|input>.<area>.<name>` |
| Event kind | `<area>.<name>`, registered as an `EventDescriptor` |
| Duration | `<n>m`, `<n>h` or `<n>d`, at least `1m` |
| Tokens | int, or with a `k`/`M` suffix |
| `with:` value | A literal (string, number, bool or a list of these), `{ fact }` or `{ profile }`. Capability parameters take only literals or `{ profile }` (`RCP-CAP-007`) |

### 3.4 States

Every state has `kind: active | wait | terminal`.

| Field | `active` | `wait` | `terminal` |
|---|---|---|---|
| `step` | **required** `{ use, with?, inputs? }`; `inputs` lists `{ use: input.…, with? }` blocks run before the Step starts | — | — |
| `waitFor` | — | **required** | — |
| `timeout` | **required** `{ after, then: park\|escalate\|retry }` | **required** | — |
| `loop` | optional | optional | — |
| `budget` | optional | — | — |
| `next` | **required** | **required** | optional |
| `outcome` | — | — | **required** |

`waitFor` takes one of four forms:
- `{ event: <kind> }`;
- `{ answer: { id, question } }`, satisfied by `workflow answer` with outcome `answered`, which records `fact:answer`;
- `{ resource: heavy | slot }`. `slot` comes from the P7 stub. `heavy` is allocated by Sindri's heavy-job lock port, the same
  port the context engine's `IHeavyJobLock` adapts (context spec §4). Until that port lands it parks with `SND-WF-010`,
  and the validator reports that;
- `{ child: { recipe, inputs, onParked: propagate|ignore } }`. The child's `escalated` and `cancelled` always
  propagate.

`retry` stays in the **same frame entry**, so inbox rows and the run token stay valid. It re-arms the deadline
(`arm + 1`) and re-emits the effects of the transition that entered the state. Each re-emitted effect gets a new
command id but keeps its stable effect identity (§5.4), and the effect block's existence check makes the repeat
harmless. A second timeout parks with `timeout`. In desk mode `retry` is replaced by `park` (W20). `retry` isn't
allowed on a `child` wait (`RCP-TIME-002`).

### 3.5 Transitions

| Field | Req. | Rule |
|---|---|---|
| `from` | yes | A non-terminal state, or a list of them |
| `to` | yes | A recipe state |
| `on` | yes | `step.completed` or `step.failed` from `active`; `wait.satisfied` from `wait`; `command.<verb>` from any non-terminal state (class `desk`, fired with `sindri workflow command <address> <verb>`); engine verbs (`answer`, `accept`, `reject`, `resume`, `grant`, `cancel`, `takeover`, `handback`, `complete`, `start`) are reserved (`RCP-TRIG-002`). `RCP-TRIG-001` |
| `when` | no | §3.6 |
| `gates` | no | `{ use, threshold?, with? }`, run in order |
| `evidence` | no | `{ kind, at, expect: pass\|fail }` |
| `effects` | no | `{ use, with? }` |
| `produces` | no | Each must be an output or fact of the trigger's source (`RCP-WIRE-002`) |

The first transition whose `when` holds is the matched one. If its gates refuse, that is a refusal (§5.5), not a
fall-through. A `step.failed` with no transition parks with `needs-answer`.

### 3.6 `when` (closed)

`{ outcome }`, `{ fact, is: present|absent }`, `{ loop, below: cap }`, `{ all }`, `{ any }` and `{ not }`. Outcome
names come from the trigger's source: a Step, an event, or a child's terminal outcomes plus `cancelled`.

### 3.7 Loops, budgets, `next`

- **Loops:** `{ cap ≥ 1, noProgress }`. `noProgress` defaults to `[findings, emptyDiff, sameFailure]` and can't be
  empty; the rule is A §17.23.13. Popping a frame never counts a round.
- **Budgets:** exhausting one is `→ parked(budget)`.
- **Recipe `next` variables:** `{item}`, `{instance}`, `{state}`, `{user}`, `{child}`, `{loop.<n>}`,
  `{loop.<n>.cap}`, `{budget.tokens.left}`.
- **Engine-only variables** (in §3.8 texts): `{reason}`, `{question}`, `{gate}`, `{score}`, `{threshold}`,
  `{since}`, `{run}`, `{after}`, `{to}`, `{effects}`, `{block}`, `{marker}`.
- **Voice:** waits are descriptions ("Waiting for …"); human-action states are instructions.
- **No approval-tier verbs** may appear in `next` (`RCP-NEXT-002`).

### 3.8 Engine-owned states

| State | Entered by | `next` | Ways out (resume action, §5.8) |
|---|---|---|---|
| `parked` (`rounds`, `no-progress`) | A loop cap, or no progress | "Parked ({reason}): {question}. Grant more rounds at a terminal, or cancel." | `answer --grant n` (protected): re-runs the Step. `answer --text` on `no-progress`: re-runs it once. `takeover`, `cancel` |
| `parked` (`budget`) | A budget, or a `gate.budget.*` refusal | "Parked (budget): {question}. Grant tokens at a terminal, or cancel." | `answer --grant-tokens n` (protected): re-runs the Step, or re-evaluates the withheld transition. `takeover`, `cancel` |
| `parked` (`needs-answer`) | A refusal without a loop, `step.failed`, a wait refusal | "Parked (needs-answer): {question}. Answer with --text." | `answer --text`: re-runs the Step with `fact:answer`, or re-evaluates the withheld transition with the stored trigger. `takeover`, `cancel` |
| `parked` (`timeout`) | `then: park` | "Parked (timeout): {state} ran past {after}. Resume to give it the full time again, or cancel." | `answer --resume`: back to the frame (the same run token) with a fresh deadline. `takeover`, `cancel` |
| `parked` (`spec-conflict`: revoked) | `recipe revoke` | "This recipe was revoked. Cancel, then start a new attempt from an approved version." | `cancel` |
| `parked` (`spec-conflict`: drift) | A pinned block or child changed in this build | "{block} changed since this recipe was approved. Re-approve it (`sindri recipe approve {recipe}`), then resume." | After the re-approval, `answer --resume` re-evaluates the trigger. `cancel` |
| `parked` (`spec-conflict`: dispatch) | `outbox.refused` | "{effect} was refused at dispatch ({code}). Fix the approval or the allowlist, then resume to send it again." | `answer --resume`: re-emits the effect. `cancel` |
| `parked` (`SND-WF-010`) | A Step or resource with no runtime | "No runtime for {block} in this build. Cancel, or resume in a build that has it." | `answer --resume`, `cancel` |
| `escalated` (gate) | A top-rung gate below threshold or uncertain | "Escalated: {gate} scored {score}, below {threshold}. Accept to fire {to} with {effects}, or reject." | `--accept` (protected) overrides only that gate, then evaluates the remaining gates and evidence at the current head. `--reject` is a refusal. `cancel` |
| `escalated` (timeout) | `then: escalate` | "Escalated: {state} ran past {after}. Resume to give it the full time again, or cancel." | `--resume`, `cancel` |
| `escalated` (child) | The child escalated | "Escalated in {child}: {question}. Answer here; it goes to {child}." | The child's own answers, forwarded. `cancel` |
| `taken-over` | `takeover` (allowed on a parked or escalated instance as well) | "With you since {since}. Run `sindri workflow handback {instance}` when you're done." | `handback` (pops back to whatever it covered), `cancel` |
| `cancelled` | `cancel` from any state, the tracker, a cancelled parent | (terminal) | outcome `cancelled` |

### 3.9 Left out of v1

- **Fan-out children** come in schema v2, with the Step graph and stacks (A 17.28.6 steps 3 and 6).
- **Per-transition timeouts** come only if a recipe needs one.
- **A free expression language:** never.
- **Variables and includes:** not planned.

## 4. Approval and invariant 13

### 4.1 Store and events

`IRecipeApprovals` records:
- the recipe: `id`, `version`, `hash`;
- who and how: `approver`, `approvedAt`, `via: desk|freeze`, `base?`;
- what it pins: `descriptorHashes` and `children { id: hash }`.

`IRecipeStore` keeps the normalized text by hash. Register, approve and revoke write `recipe.registered`,
`recipe.approved` and `recipe.revoked` events. Approvals live in `ledger.db` and travel with a host handover.
`recipe list --stale` lists approved recipes whose pins no longer match the build. Re-approving the **same hash**
records the new pins (a drift approval), and the preview shows which blocks changed.

### 4.2 The challenge (shared by every `protected` command)

1. The CLI asks the server for a challenge for one action, such as `approve workflow.step 1`,
   `revoke workflow.step@…`, `grant 1 T-1/step/1` or `accept T-1/step/1`. The server returns a one-time **challenge**, valid for 5 minutes and bound to the subject (the recipe hash and
   version, or the instance version), together with the preview. If the subject changes before the answer, the
   challenge is refused.
2. The human types the action text at a TTY, for example `grant-tokens 100k T-1/step/1`. The CLI returns it with
   the challenge.
3. The server refuses a request without a valid challenge:
   - `SND-AUTH-002` (API §4.2) when the caller isn't `human` with a terminal, before any challenge is checked. From a
     session, the message names the agent variable or parent process it saw;
   - `SND-RECIPE-009` when the challenge has expired. The fix text says "run it again".

**Previews:**
- **`approve`** shows:
  - the id, version, hash and base;
  - a diff against the last approved version;
  - the capability uses, with block, transition, capabilities and parameters;
  - the effect kinds and their allowlist status;
  - the pinned children and blocks;
  - the eval result (from P4 on).

  Failing or missing evals refuse the approval with `SND-RECIPE-008`.
- **`revoke`** shows the running instances that will park, and the generated recipes the cascade revokes.
- **`grant`** and **`accept`** show the park or escalation, and what will run next.

An architecture test checks that the Avalonia client and the MCP server reference none of these routes.

### 4.3 Rules

| Rule | Checks | When |
|---|---|---|
| `RCP-CAP-001` | A generated recipe's capability uses (block plus `from → to`) and children are a subset of its base's | validate, freeze |
| `RCP-CAP-002` | Capability parameter values equal the base's | validate, freeze |
| `RCP-CAP-003` | The hash is approved and not revoked, and its pins match the build (`SND-RECIPE-003`, `005`, `006`) | every `Commit`, dispatch |
| `RCP-CAP-004` | A hand-written recipe has no `base` | validate |
| `RCP-CAP-005` | For each kept capability use, the guards that **dominate** it (the gates and evidence on every path from `initial` to it, plus its own) are a superset of the base's: each gate with the same `with:` and a threshold at or above the base's, and each evidence entry with the same `at` and `expect` | validate, freeze |
| `RCP-CAP-006` | Each loop, matched by the state it's on, has a cap at or below the base's. Each budget the base sets is present and at or below it | validate, freeze |
| `RCP-CAP-007` | Capability parameters are literals or `{ profile }` only, and are intersected with the profile at run time | validate, run |

The freeze screen shows the diff against the base. Recipe changes are always approval-tier. Timeouts aren't
capability fields (§13).

## 5. The instance and its events

### 5.1 `WorkflowInstance`

| Field | |
|---|---|
| `id` | A root is `(recipe id, item, attempt)`. A child is `{parent}/{child recipe}.{parent entry}` |
| `tenant`, `ring`, `recipe` | The recipe is `id`, `version`, `hash`, plus the pinned children |
| `item`, `attempt`, `parent?` | |
| `frame` | `{ state, entry, run?, child?, deadlines[], since }`. `entry` rises on each real state entry |
| `suspended` | A stack of at most 2 suspended frames, each with what suspended it (park, escalation or takeover, with its reason and question) |
| `loops`, `spent`, `facts` | `facts` hold `{ ref, sha256, headSha }` |
| `run?` | `{ token: {entry}.{n}, runtime: desk\|agent, launch?, task }`. The token is a public counter, never a credential |
| `deadlines` | `{ id: {entry}:{kind}:{arm}, kind: state\|retry\|takeoverIdle, due?, remaining? }` |
| `version` | Rises on every `Commit` |

**Push and pop (W16):**
- A park, an escalation or a takeover pushes the current frame and starts the engine-state frame:
  - a takeover pauses the deadlines (keeping `remaining`);
  - a park or an escalation clears them.
- A pop resumes the frame exactly: the same `entry`, run token, child and pending inbox rows. Then it runs the
  frame's resume action (§5.8).
- Deadlines are re-armed with `arm + 1`, the arm being counted per frame entry and deadline kind. They come from
  `remaining` after a takeover, or with the full timeout after an answer.
- A pop counts no round. A re-run that the resume action starts counts one, except after a grant.
- **Stack rules:**
  - A park or an escalation sits on the base frame.
  - A takeover may sit on a park or an escalation, so the stack is at most two deep.
  - A `spec-conflict` park from a revoke **replaces** the stack, ending any takeover.
  - Frames that mirror a child carry the child's id, and are popped by that id rather than from the top.

### 5.2 Events

Each `Commit` writes one event per instance it changes, which is one event except when it also creates a child:

| Event | When |
|---|---|
| `workflow.started` | A root, or a child in its parent's `Commit` (the parent's `workflow.transition` and the child's `workflow.started` share one transaction) |
| `workflow.transition` | Any state or frame change, including the engine-owned states |
| `workflow.refused` | A refusal: gate results plus what followed (a re-run, park or escalation) |
| `workflow.shifted` | A sleep or downtime shift |
| `workflow.notified` | `takeoverIdle` fired |
| `workflow.expired` | An inbox row expired |
| `recipe.*` | Registry actions |

Every event carries:
- the trigger: `kind`, `id` and `actor`;
- `fromVersion → toVersion` and the epoch;
- any gate, evidence, grant or override data;
- the outbox ids;
- the post-state delta (W21).

The ledger writer runs the whole payload through `Aw.Redaction` (E20). `causation` is the trigger's source. S1's
`EventFactory` takes the `item`, the `causation` and the actor of the request (S1-D7).

### 5.3 `Commit`

One transaction:
1. **Caller:** a command has already passed API's chain (`CommandGate`, audit, `AuthorizingService` with `SND-AUTH-002`;
   API §2.1). System triggers carry `service:<name>` and don't take `CommandGate`; step 6 serializes them against
   commands.
2. **Key:** if the key exists, return the **recorded result**. An applied trigger prints `already applied` and exits 0.
   A refused one prints the recorded refusal and exits 2.
3. **Nonce:** a command's nonce must be unused, unexpired and for the current version (`SND-WF-009`). It is
   consumed in this transaction.
4. **Fence:** `LeaseDecision.Holds`, otherwise `SND-LEASE-003`.
5. **Approval:** check the recipe's approval and pins (`RCP-CAP-003`) against a snapshot invalidated by `recipe.*`
   events. `cancel`, `takeover`, `handback`, `takeoverIdle` and engine-owned commands are exempt. A mismatch on any
   other system trigger turns the commit into a `spec-conflict` park.
6. **Version:** `UPDATE … WHERE version = @fromVersion`, otherwise `VersionConflict`.
7. **Write:** the event(s), the row(s), the deadlines, the outbox commands and their `workflow_commands` map rows,
   and the inbox rows.

Gate answers are saved in their own transaction before the `Commit` (`workflow_gate_results`, keyed by trigger and
block). **A command never evaluates a gate itself** (API §2.1: handlers do no slow work). When an answer's resume
action needs gates or evidence (`--accept`, `reevaluate`), the command records the answer and the post-commit loop
evaluates the withheld transition as a system trigger. A system trigger that hits a version conflict re-decides, reusing those answers, up to 3 times. A command
gets `SND-WF-002`.

### 5.4 Triggers, nonces, outbox ids

| Trigger | Id | Valid when |
|---|---|---|
| Command | A nonce from `POST /workflow/instances/{a}/nonce` | Unused, unexpired, and for the current version |
| Step result or desk report | `run:{entry}.{n}`; the token is given as `--run`, and a desk report also carries a nonce | The token is the open run's; otherwise it is stale (`SND-WF-005`). Replaying the same report hits the key and is `already applied` |
| Event | The source ledger sequence | The instance waits for that kind in the frame the inbox row names |
| Deadline | `{entry}:{kind}:{arm}` | Armed, and not paused |
| Child event | The child event's key | The instance waits on that child |
| Shift | `shift:{tick}` | §5.6 |
| Wake after a revoke or drift | `recipe.revoked:{seq}` or `recipe.drifted:{seq}` (written at startup when the build's descriptor hashes differ from an approval's pins) | Always (it parks) |

- **Nonces** are 128-bit random values, stored in `workflow_nonces (nonce, instance, version, expiresAt)`. They live
  for 10 minutes and are consumed in the `Commit` whether the command applies or is refused. The timers service
  deletes expired ones. Reads never mint a nonce.
- **Outbox command ids** are UUIDv8 (RFC 9562) built from SHA-256 over `{trigger key}:{effect index}`, so each
  `Commit` gets ids of its own. SHA-1-based UUIDv5 would trip CA5350. `workflow_commands` maps each command id back
  to its instance and entry.
- **Effect identity:** separately from the command id, each effect carries a stable identity in its payload,
  `{instance}:{entry}:{effect index}`. `effect.claim.ref` uses the **item** as its identity, so a later attempt of
  the same item finds its own claim and gets `created`. The effect's existence check uses this identity, so a
  `retry` or a re-emission is harmless.
- **Engine-owned commands** (`notify.desk` for parks, escalations, desk tasks and `takeoverIdle`) are exempt from
  the recipe approval check at dispatch.
- **Dispatcher:** it honours `notBefore = enqueuedAt + workflow.dispatchDelay` (default 10 s, A 17.22 D3's undo
  window). A refusal at dispatch marks the command dead with Kernel's new `IOutbox.MarkRefused` (K-D1, owned by
  P6), so it isn't retried, and writes `outbox.refused` (producer `system:dispatcher`). The router delivers it, and
  the instance parks with `spec-conflict: dispatch`.

### 5.5 Refusals (W14)

Precedence runs top to bottom:
1. A refusal by a `gate.budget.*` gate parks with `budget`.
2. An uncertain or below-threshold top-rung gate escalates.
3. From `active` on a Step trigger:
   - below the loop's cap, the Step re-runs with the refusal as findings (run `n+1`, one round, no-progress
     checked);
   - at the cap, the instance parks with `rounds`;
   - with no loop, it parks with `needs-answer`.
4. From a wait: the event is stored on the suspended frame as the withheld trigger, held resources (such as a
   session slot) are released, and the instance parks with `needs-answer`.
5. A command gets `SND-WF-004`, recorded once under its nonce.

A **desk report** that is malformed (an undeclared outcome, a missing required output, an undeclared fact) is
rejected before any decision with `SND-WF-008`. No round is used.

### 5.6 Timers, sleep, takeover

- **Built-in services:** `workflow.timers` (15 s), `workflow.events` (5 s) and `workflow.inbox` (5 s) run without a
  profile entry; the profile only sets their cadence (S1-D8). Each runs on its own loop, so a slow gate in the inbox
  doesn't delay the timers (S1-D10).
- **Sleep and downtime:** the unaccounted gap since the last tick is wall-clock time minus `IMonotonicClock` time.
  At startup it is the time since the last persisted tick. When it is at least 60 s, every instance with armed
  deadlines gets a `shift:{tick}` trigger.
- **`takeoverIdle`** (2 h) emits `notify.desk` ("hand back {item}?") and a `workflow.notified` event, then clears.
  It never hands back.

### 5.7 Delivery and children

- **Router** (`workflow.events`):
  - It reads the ledger from its cursor in `workflow_cursor`.
  - It accepts an event of a registered kind only when the envelope's actor is the descriptor's `producer`.
  - It resolves the target through `causation`, via `workflow_commands`, or else through `item` and attempt. Parked
    and taken-over instances are valid targets.
  - It writes inbox rows.
- **Inbox** (`workflow.inbox`):
  - A row applies when its instance is in the matching wait, in the frame that row names, or, for a row written
    before that wait began, in the next matching wait.
  - A row aimed at a **live frame** (the current one, or a suspended one) never expires: it applies when that frame
    is current again.
  - Only an **early** row, written before any matching wait, is held, for up to `workflow.eventHold` (default
    24 h).
  - A row whose frame has ended, or whose hold has run out, expires, with a `workflow.expired` event.
- **Children:**

| Child change | Parent |
|---|---|
| Created | A row inserted in the parent's `Commit` when it enters the `child` wait |
| Terminal (X) | `wait.satisfied` with outcome X |
| Cancelled | `wait.satisfied` with outcome `cancelled` |
| Parked (`propagate`) | Pushes a park mirroring the child's reason and question |
| Resumed | Pops back to its wait (the same frame and child) |
| Escalated | Pushes an `escalated (child)` frame |
| A command to the parent while the frame mirrors a child | Forwarded to the child as a system trigger carrying `onBehalfOf` (the original actor and the consumed challenge's id). The child accepts that as proof; the parent follows the child |
| Parent cancelled | Every child is cancelled, whatever its state |
| Parent busy | The row stays pending until the parent can take it |

### 5.8 Answers and resumes

Each suspended frame stores a **resume action**. Its answer, once accepted, pops the frame and runs it:

| Park or escalation | Resume action |
|---|---|
| `rounds`, `no-progress` | `rerun`: a new run `n+1` of the frame's Step, with the findings and any `fact:answer` |
| `budget` from a Step | `rerun`, under the raised allowance |
| `budget` from a `gate.budget.*` refusal | `reevaluate`: the withheld transition with its stored trigger, under the raised allowance |
| `needs-answer` from a Step (refusal without a loop, `step.failed`) | `rerun`, with `fact:answer` |
| `needs-answer` from a wait refusal | `reevaluate` the withheld transition with its stored trigger, and `fact:answer` |
| `timeout`, `escalated (timeout)` | `rearm`: back to the frame (the same run, the same desk task) with a fresh deadline |
| `spec-conflict: drift`, `SND-WF-010` | `reevaluate` the trigger that parked it |
| `spec-conflict: dispatch` | `reemit`: the refused effect, as a new command with the same effect identity |
| `escalated (gate)` | `--accept`: the withheld transition, with that gate overridden. `--reject`: the §5.5 refusal path |
| A mirrored child frame | Forwarded (§5.7) |

`fact:answer` holds the latest answer text, scrubbed, at most 1 MiB, and visible to the next Step. An answer that
doesn't fit the reason, such as `--text` on a `rounds` park, is `SND-WF-003`, and its fix names the right answer.

## 6. CLI and API surface

### 6.1 Process model, addressing, dev loop

- `recipe validate` and `recipe eval <file>` run in-process (the binary's catalog, plus the profile when there is
  one). Every other verb is a client of `sindri serve`.
- **Addresses:**
  - `<item>` is an `ItemId` (`ITM-…`, API §3) or one of its tracker-key aliases; examples use `T-3` for short.
  - A root is `<item>/<recipe-name>/<attempt>`.
  - A child adds `/<child-recipe>.<entry>`, for example `T-3/item/1/step.4`. `T-3/item/1/step` is accepted while only
    one live child of that recipe exists.
  - A bare `T-3` means the root of the item's latest attempt. When that is ambiguous, `SND-WF-006` lists the
    candidates.
- **`start`** runs the latest approved version of the id, unless it is given `id@hash`. An existing
  `(recipe, item, attempt)` returns that instance. `--attempt next` starts a new attempt.
- **Files:** the CLI sends file contents for `--fact` and `--output`, at most 1 MiB each, and the server never opens
  a client path. Artifacts are stored owner-only (`0600`).
- **Dev loop:**

  ```
  export AW_SINDRI_HOME=$(mktemp -d)      # never the TS sindri's $AW_STATE_DIR/sindri
  alias sindri='scripts/sindri-prove.sh'  # API §12: guards the home, runs the C# host as the agent (a session)
  # owner verbs (profile approve, recipe approve, grants): sindri human <verb>
  ```

  Then copy `recipes/dev/profile.yaml` to S1's profile location (`docs/csharp/sindri.md`), run
  `sindri profile approve <hash>` (the hash `sindri profile validate` prints), and run `sindri serve` in one
  terminal and the verbs in another. Each phase that adds a `workflow:` profile key (S1-D11) adds it to the dev
  profile in the same PR, so the dev profile always matches the schema of its phase.

### 6.2 Callers and action classes (W22)

The engine adopts the API surface spec's model unchanged (API §4.1, §4.2, §6). How a caller is classified:

| Channel | Caller |
|---|---|
| The CLI at a terminal (stdin and stdout), with no agent variable set and no agent-harness parent process | `human` + terminal |
| The Sindri app (Avalonia) | `human`, no terminal |
| The CLI with an agent variable or harness parent, or without a terminal; the MCP endpoint; any HTTP call without `X-Sindri-Caller: human` | `session` |
| Engine services, the router, the dispatcher and adapters, in-process only | `service:<name>` |

Action classes of the engine's commands (API §4.1), with the fail-closed `IClassifiedCommand` rule (API §2.1):

| Class | Engine commands |
|---|---|
| `local` | `recipe validate`, `recipe eval` (in-process, never routes) |
| query | `recipe show`, `recipe list`, `workflow status`, `workflow list`, `workflow events`, `outbox list` |
| `pause` | `ApplyCommand:takeover`. A session may take over (the attach hook runs inside the session); handing back is `desk` |
| `desk` | `recipe register`, `StartInstance` (start, including scoping and any other workflow), `ApplyCommand:answer` (text, resume, reject), `ApplyCommand:handback`, `ApplyCommand:cancel`, `ApplyCommand:command-<verb>`, `CompleteStep` |
| `protected` | `recipe approve`, `recipe revoke`, `ApplyCommand:answer-grant`, `ApplyCommand:answer-grant-tokens`, `ApplyCommand:answer-accept`. Each also needs the §4.2 challenge |

So the app is a full desk client: it starts workflows (scoping included), answers parks, takes over, hands back,
cancels, completes desk tasks and hands work off to a session (API P10, `OpenItem`). `protected` actions show the
CLI to copy until passkeys exist.

**The agent variables are probed, not guessed.** P2 records what each harness actually sets (Claude Code, Codex in
each sandbox mode, Cursor's agent and its integrated terminal) and pins the result in a golden test. That list is
the one API §6 reads; a variable that humans also see, such as one set in an IDE's own terminal, is excluded, and the
parent-process check covers it instead. A refusal names the variable or process it found, so a human in an IDE
terminal knows why. The table is honest-mistake grade (A 17.28.3).

### 6.3 Verbs

| Verb | Class | Prints |
|---|---|---|
| `recipe validate <file> [--json]` | local | Rule lines, then the hash |
| `recipe eval <file> [--json]` · `--scaffold <file>` · `--record <address> --out <file>` | local · local · query | Case results · a generated happy-path eval (first matching transitions, first declared outcome) · an instance's history as an eval |
| `recipe register <file>` | desk | `registered workflow.step@9ab2d40c1e77 (unapproved)` |
| `recipe show <id@hash>` | query | The preview |
| `recipe approve <file\|id@hash>` | protected | The preview, the challenge, then `approved …` |
| `recipe revoke <id@hash>` | protected | The preview, the challenge, then `revoked …; N instances parked` |
| `recipe list [--stale] [--json]` | query | One line per recipe, or `no recipes registered` |
| `workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]…` | desk | The status header. A finished attempt adds `start a new one with --attempt next` |
| `workflow status <address> [--json]` | query | §6.4 |
| `workflow list [--needs-me \| --item \| --state] [--json]` | query | One status header per line. `--needs-me` reads API's `GET /needs-you` (one list for every surface) and shows two sections, **Decisions** (parked, escalated, idle takeovers) and then **Tasks** (open desk tasks). Empty: `nothing needs you` |
| `workflow answer <address> [--text …\|--file …] [--grant n \| --grant-tokens n \| --accept \| --reject \| --resume]` | desk (grants, accept: protected) | The new status header and the resume action it ran |
| `workflow command <address> <verb>` | desk | Fires a recipe `command.<verb>` trigger |
| `workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]…` | desk | The new status header and what happened (`-> checks`, `refused: re-running produce (round 2/3)`, `parked: rounds`) |
| `workflow cancel <address>` · `takeover` · `handback` | desk · pause · desk | The new status header. `sindri handback <item>` stays as D §9.3's short form |
| `workflow events <address> [--follow] [--json]` | query | One line per event: the instance-level filter of the item timeline (API `GET /items/{itemId}/timeline`) |
| `outbox list [--pending \| --dispatched] [--json]` | query | One line per command |

- Every write verb takes `--nonce <n>`, for scripts and tests. Without it, the CLI asks the server for one and
  prints it with `--verbose`. `--json` output always includes the nonce it used.
- `GET /workflow/instances/{address}` returns the instance, the rendered `next`, `allowedCommands` (each with its
  actor class), the open question or task (with its input facts), the run token, the children, and what `--accept`
  would fire.
- **Notifications** go out as `notify.desk` outbox commands, each written to `$AW_SINDRI_HOME/notifications.jsonl` and shown
  as a macOS notification when one is available:
  - parks, escalations and `takeoverIdle` interrupt, within D §10.1's budget;
  - an instance's **first** desk task interrupts once, so starting `workflow.item` gives a cue;
  - later desk tasks are non-interrupting `info`, collected into a daily digest.
- From P6, `notify.desk` is the **only** source of OS notifications. The app shows the needs-you badge and pulse from
  the event stream; until P6 ships it also raises an OS notification for each new `pulse` row, and P6 turns that off,
  so nothing fires twice (API §5.1).
- **`--needs-me`** lists a parent and its mirrored child once, under the child.
- **Empty states:** `nothing needs you` (`--needs-me`), `no instances match` (other filters), `no events`, `no
  commands`, and `no evals declared` (eval).

**Routes** (under S1's `/sindri` group; each is one slice through API's chain, with a catalog row):

| Route | Slice | Class |
|---|---|---|
| `GET /workflow/recipes` · `GET /workflow/recipes/{id}@{hash}` | `ListRecipes` (with each recipe's `inputs`, for the app's start form) · `ShowRecipe` (the preview) | query |
| `POST /workflow/recipes` | `RegisterRecipe` (body: the recipe text) | desk |
| `POST /workflow/recipes/{id}@{hash}/approve` · `/revoke` | `ApproveRecipe` · `RevokeRecipe` (body: the challenge and its typed text) | protected |
| `POST /workflow/challenges` | `IssueChallenge` (body: the action and its subject) | issue |
| `GET /workflow/instances?address=&item=&state=&needsMe=` | `ListInstances`; `address` resolves a CLI address (`T-3/item/1`) to an instance id | query |
| `POST /workflow/instances` | `StartInstance` (body: `recipe`, `item`, `attempt?`, `facts { key: { name, contentBase64 } }`, each ≤ 1 MiB) | desk |
| `GET /workflow/instances/{instanceId}` | `GetInstance`: the instance, rendered `next`, `allowedCommands`, the open question or desk task **with the Step's declared outcomes and outputs**, children | query |
| `GET /workflow/instances/{instanceId}/events?after=` | `GetInstanceEvents` (the item timeline filtered to the instance) | query |
| `POST /workflow/instances/{instanceId}/nonce` | `IssueNonce` | issue |
| `POST /workflow/instances/{instanceId}/commands` | `ApplyCommand` (body: `verb`, `nonce`, `text?`, `fileBase64?`, `grant?`, `grantTokens?`, `accept?`, `reject?`, `resume?`, `challenge?`) | by variant (§6.2) |
| `POST /workflow/instances/{instanceId}/runs/{run}/complete` | `CompleteStep` (body: `outcome`, `outputs { fact: contentBase64 }`, `nonce`) | desk; `step-report` with `X-Sindri-Launch-Secret` |
| `GET /outbox?state=` | `ListOutbox` | query |

Instance ids are opaque (`WFI-…`, a typed id); the CLI's `/`-separated addresses travel only as the `address` query.

### 6.4 `status` layouts

The header is always
`<address>  <recipe>@<hash12>  v<n>  <state>[: <detail>]  since <hh:mm> (<age>)`. Then come labelled lines; the
order is fixed and empty lines are left out.

```
T-3/item/1  workflow.item@3f1c09a2b4d1  v7  parked: no-progress (child)  since 11:54 (12m)
  next      Parked (no-progress) in T-3/item/1/step.4: checks failed twice with the same findings.
            Grant more rounds at a terminal, or cancel.
  child     T-3/item/1/step.4  parked: no-progress
  answer    sindri workflow answer T-3/item/1 --grant 1      (at a terminal; forwarded to the child)
  commands  answer (forwarded), cancel, takeover
```

```
T-3/item/1/step.4  workflow.step@9ab2d40c1e77  v4  produce: desk task  since 10:42 (1h 12m)
  next      Produce the Step output for T-3 (round 1/3), then run:
            sindri workflow complete T-3/item/1/step.4 --run 1.1 --outcome done --output fact:output=<file>
  inputs    fact:spec  spec.md  sha256:1c0e…
  timeout   parks in 1d 22h 48m (desk)
  budget    tokens 0 / 200k (state), 0 / 400k (instance)
  commands  complete, cancel, takeover
```

```
T-1/step/1  workflow.step@9ab2d40c1e77  v9  parked: budget  since 11:54 (3m)
  next      Parked (budget): produce used 200k of 200k tokens.
  answer    sindri workflow answer T-1/step/1 --grant-tokens 100k     (at a terminal; once per park)
  commands  answer, takeover, cancel
```

```
T-6/step/1  workflow.step@9ab2d40c1e77  v5  escalated: gate  since 09:10 (2h)
  next      Escalated: gate.verify.judge scored 0.62, below 0.8. Accept to fire verify -> done with no effects, or reject.
  commands  answer --accept, answer --reject, cancel
```

```
T-2/dev-timeout/1  workflow.dev-timeout@77aa01c3d9e2  v3  taken-over  since 12:01 (5m)
  next      With you since 12:01. Run `sindri workflow handback T-2/dev-timeout/1` when you're done.
  paused    timeout 0m 40s left
  commands  handback, cancel
```

```
T-3/item/1  workflow.item@3f1c09a2b4d1  v12  done: resolved  since 14:20 (1m)
```

The answer line depends on the park reason:
- `timeout` and `SND-WF-010` suggest `--resume`;
- `spec-conflict` names its kind and next step (§3.8);
- `rounds` and `no-progress` suggest `--grant 1`;
- `budget` suggests `--grant-tokens`;
- `needs-answer` suggests `--text`;
- a park mirroring a child names the child.

Durations are shown as `1d 22h 48m`. Plain output is ASCII. Golden files pin every layout.

### 6.5 The agent runtime (owner decision, 2026-10-11)

The runtime that runs a Step in an agent session and takes its result back, so the factory line can run unattended.
It uses API §8.1's launch contract; nothing here launches anything itself.
- **Launch.** For a Step whose descriptor names an agent runtime, the post-commit loop asks `ILauncher` for a
  `process` launch (the always-on `--headless` box has no Warp), with only ids: the instance, the run token, the repo,
  and a fresh per-run worktree (`<repo>/.claude/worktrees/sindri-<instance>-<run>`). The target and its permission
  mode come from the profile (`workflow.agent.target`: Claude Code, Codex or Cursor; `workflow.agent.permissionMode`).
  The session pulls its injection plan, context and packet through the MCP endpoint. `run.runtime` is `agent` and
  `run.launch` names the launch.
- **Report.** The launched session, and only it, reports with `sindri workflow complete --run <token>`. The CLI reads
  `AW_SINDRI_LAUNCH_SECRET` from its environment (set by `session wrap`) and sends it as `X-Sindri-Launch-Secret`;
  the server accepts `step-report` only when the secret matches the open run's launch and the instance isn't taken
  over (`SND-AUTH-003` otherwise). The run token stays a public counter. The report goes through the same validation
  as a desk report (§5.5). The session reports; the engine decides through the recipe's gates.
- **Tokens.** Usage is read by the target's usage reader when the session ends (API §8.1) and recorded on
  `session.ended`; budgets (§3.7) count it, and an over-budget run parks with `budget`. Sessions never report their
  own usage.
- **Ends.** `session.ended` without a report ends the run as `step.failed` (reason `exit` or `lost`), at most 150 s
  after a crash. The Step's `timeout` still applies. A human may take the instance over at any time (`OpenItem`), which
  invalidates the launch secret for `step-report`.
- **Desk Steps stay.** A `deskCapable` Step whose descriptor names no agent runtime keeps running on the desk runtime
  (W20).

## 7. Placement and S1 changes

### 7.1 Placement

| Layer | Holds |
|---|---|
| `Aw.Sindri.Core/Workflow` | Recipe records and typed ids; descriptors, `BlockCatalog`, `IDescribedBlock`, the kind interfaces; `RecipeHash`; `RecipeValidator` with `ValidationContext` (as data); `TransitionDecision`, `RefusalRules`, `FrameRules`, `LoopRules`, `BudgetRules`, `TimerDecision`, `ChildPropagation`, `EventFold`; the ports `IWorkflowStore`, `IRecipeStore`, `IRecipeApprovals`, `IMonotonicClock`; `WorkflowErrors` (a second catalogue class beside S1's `SindriErrors`, validated together) |
| `Aw.Sindri.Features` | Slices: `ValidateRecipe`, `EvalRecipe`, `RegisterRecipe`, `ShowRecipe`, `ApproveRecipe`, `RevokeRecipe`, `ListRecipes`, `IssueChallenge`, `StartInstance`, `ApplyCommand`, `CompleteStep`, `IssueNonce`, `GetInstance`, `ListInstances`, `TickTimers`, `RouteEvents`, `DrainInbox`, `RunPostCommit`. Also `BlockRuntime`, the desk runtime and the pure blocks. Each service takes at most 5 dependencies; validation and telemetry are decorators |
| `Aw.Sindri.Infrastructure` | `YamlRecipeReader`; the SQLite stores and migrations (`workflow_instances`, `workflow_deadlines`, `workflow_inbox`, `workflow_cursor`, `workflow_gate_results`, `workflow_nonces`, `workflow_commands`, `recipes`, `recipe_approvals`); the I/O blocks; the dispatcher |
| `Aw.Sindri.Host` | The `recipe`, `workflow` and `outbox` verb groups, each with its own group command table; endpoints; the agent-variable probe; JSON context entries. The command classes, the endpoints and the group tables are written by hand, following the slice recipe. Slices are scaffolded with `/new-slice --slice-only` (API P0), then wired at the anchors API P1 adds |

### 7.2 S1 changes and their owners (W26)

| # | Change | Found on S1's branch | Owner |
|---|---|---|---|
| S1-D1 | S1 merged | in progress | P2 and later start after it |
| S1-D2 | A ledger insert inside an outer `SqliteUnitOfWork`, without committing | `InsertOnce` commits | P4 |
| S1-D3 | The outbox dispatcher (with `notBefore`, the recheck and `outbox.refused`) | `outbox_commands` exists; no dispatcher | P6 |
| S1-D5 | The server challenge. P2 builds it for the `recipe` and `workflow` verbs, and it replaces `profile approve`'s `confirmed` body flag too, so every `protected` action confirms the same way (API §3.2) | `profile approve` trusts a `Confirmed` flag | P2 |
| S1-D6 | An item or instance filter on `/sindri/events` | no filter | P4 |
| S1-D7 | `EventFactory` takes item, causation and the request's actor (not the process-wide `EventScope` actor) | `Item: null`, one process-wide actor | API P1 (its C6); P4 uses it |
| S1-D8 | Built-in services without a profile entry | only profile-named services run | P4 |
| S1-D9 | Pin the outbox migration at version 3: list it explicitly after S1's two files, so Sindri's later files are 4 and up | appended as `own.Count + 1`, so the next migration would renumber it | P2 (the first phase with a migration) |
| S1-D10 | One loop per service | services run one after another | P4 |
| S1-D4 | Cadence below a minute | Met: `Cadence` accepts `5s`–`24h` | — |
| K-D1 | Kernel `IOutbox.MarkRefused`: mark a command dead without retrying it | Missing in `Aw.Kernel` | P6 |
| S1-D11 | Profile keys under `workflow:`: `deskTimeout` and `eventHold` (P4), `effects` and `dispatchDelay` (P6), `route` (P7, §12). Until `workflow.effects` exists, `RCP-EFFECT-001` reports `skipped`, and effects are refused at run time | S1's profile schema is closed | P4, P6, P7 |

Outside S1:
- `ILauncher` belongs to the API surface spec (P10, `OpenItem`), with its launch targets (Warp plus Claude Code, Codex
  or Cursor) set by the UI design spec. The agent runtime (§6.5) runs Steps that name one; the desk runtime (W20) keeps the rest.
- The API chain (API P1) and the challenge are prerequisites of P2: P2 starts once API P1 has merged.
- The router and slot stubs come in P7.
- Judge (#156) and Evidence (#158) become blocks with the worker recipes.

## 8. Error catalogue (in `docs/csharp/sindri.md`, class `WorkflowErrors`)

| Code | Kind | Meaning | Fix |
|---|---|---|---|
| SND-RECIPE-001 | invalid | Validation failed | Fix each `RCP-*` line |
| SND-RECIPE-002 | notFound | No recipe with that id or hash | `sindri recipe list` |
| SND-RECIPE-003 | conflict | Not approved | `sindri recipe approve` |
| SND-RECIPE-004 | — | Retired: a caller without a terminal gets API's `SND-AUTH-002` | — |
| SND-RECIPE-005 | conflict | Revoked | Approve a new version |
| SND-RECIPE-006 | conflict | A pinned block or child changed since approval | Re-approve (`recipe list --stale`) |
| SND-RECIPE-007 | conflict | Same id and version, other content | Raise `version` |
| SND-RECIPE-008 | conflict | Evals missing or failing | `sindri recipe eval <file>` |
| SND-RECIPE-009 | conflict | The challenge expired, or its subject changed since the preview | Run it again and re-read the preview |
| SND-RECIPE-010 | notFound | The recipe file can't be read | Check the path |
| SND-WF-001 | notFound | No such instance | `sindri workflow list` |
| SND-WF-002 | conflict | The instance changed | Re-read and retry |
| SND-WF-003 | conflict | The trigger isn't allowed here | `allowedCommands` in `status` |
| SND-WF-004 | conflict | A gate refused | Act on the reasons |
| SND-WF-005 | conflict | A stale run token | Use the token in `status` |
| SND-WF-006 | invalid | An ambiguous address | Use a listed address |
| SND-WF-007 | — | Retired: authorization is API's `SND-AUTH-002`, which names the agent variable or process found | — |
| SND-WF-008 | invalid | A malformed desk report | Use a declared outcome and the required outputs |
| SND-WF-009 | conflict | The command's nonce was used, expired or is for another version | Run the command again (the CLI fetches a new nonce) |
| SND-WF-010 | unavailable | No runtime for a named block or resource | Cancel, or a build with it |

## 9. Validator and eval format

### 9.1 Rules mapped to §17.28.5

Line format: `RCP-…: <what> at <file>:<line>:<col> (<path>); <fix>`. Each rule has an invalid fixture.
In-process validation resolves a `child` recipe from the registered recipes when served, or from the same
directory when not (so `workflow.item.yaml` finds `workflow.step.yaml`).

| §17.28.5 | Rules |
|---|---|
| Schema | `RCP-SCHEMA-001`–`003`, `RCP-STATE-001`, `RCP-INIT-001`, `RCP-DUR-001` |
| Blocks registered | `RCP-BLOCK-001`–`003`, `RCP-EVID-001`, `RCP-EVENT-001` |
| Inputs upstream | `RCP-WIRE-001`, `-002`, `RCP-WHEN-001`, `RCP-OUTCOME-001`, `RCP-CHILD-001` |
| Capability | `RCP-CAP-001`–`007` |
| Reachable | `RCP-REACH-001` |
| A way out | `RCP-EXIT-001` |
| Timeouts | `RCP-TIME-001` |
| Loop caps | `RCP-LOOP-001`–`003` |
| Allowlisted effects | `RCP-EFFECT-001` (`skipped` without the profile key) |
| Hash | `RCP-HASH-001` |
| Added | `RCP-TRIG-001`, `RCP-WHEN-002`, `RCP-THRESH-001`, `-002`, `RCP-NEXT-001`, `-002`, `RCP-CHILD-002` (cycle), `-003` (depth > 4), `RCP-VERSION-001`, `-002`, `RCP-TRIG-002` (reserved verb), `RCP-TIME-002` (`retry` on a child wait), `RCP-RES-001` (information: a resource with no allocator in this build) |

### 9.2 Eval format

`evals/<recipe id>/<case>.jsonl`:
- line 1: `{ "start": { "inputs": { … } } }`;
- each later line: `{ "trigger": { "kind", "outcome"?, "outputs"? }, "gates"?: { "<block>": "pass|fail|uncertain" },
  "expect": { "state", "park"?, "outcome"? } }`.

`recipe eval` folds the decisions over the triggers and reports the first line that diverges. `--scaffold`
generates a happy-path case for a new recipe, so the first eval isn't hand-written. `--record` writes a live
instance's history in this format.

## 10. Engine properties mapped to tests

| Property | Test | Lane | Phase |
|---|---|---|---|
| Replay reproduces the instance | `Folding_the_events_rebuilds_the_row` (on every store contract run), `Eval_cases_reach_their_expected_states` | unit, integration | 3, 4 |
| A trigger applied twice is a no-op | `Same_trigger_twice_records_one_event`, `Used_nonce_returns_already_applied` | unit, integration | 3, 4 |
| A stale epoch or version is refused | `Commit_refuses_a_stale_epoch` / `_version` | unit, integration | 4 |
| A stale run token changes nothing | `Late_result_from_an_earlier_entry_is_dropped` | unit | 3 |
| Frames survive push and pop | `Takeover_of_a_parked_instance_returns_to_the_park`, `Pop_keeps_entry_run_child_and_inbox`, `Rearmed_deadline_has_a_new_arm` | unit | 3 |
| A timer during a takeover does nothing | `Deadline_during_takeover_is_refused`, `Tick_skips_paused_deadlines` | unit, integration | 5 |
| Child propagation (§5.7) | `Child_<change>_<parent effect>`, `Forwarded_grant_keeps_the_terminal_actor` | unit, integration | 3, 7 |
| A refusal is never a dead end | `Refused_step_reruns_within_the_loop`, `Refused_wait_parks_needs_answer`, `Budget_refusal_parks_budget` | unit | 3 |
| Fail closed | `Unavailable_gate_refuses`, `Step_without_runtime_and_not_desk_capable_parks` | unit | 3 |
| Desk reports | `Malformed_report_is_rejected_without_a_round`, `Desk_timeout_never_retries` | unit | 4 |
| Grants | `Grant_adds_n_rounds_and_needs_a_terminal`, `Token_grant_once_per_park` | unit | 3 |
| Sleep shifts, a slow tick doesn't | `Sleep_gap_shifts_deadlines`, `Slow_tick_shifts_nothing` (fake `IMonotonicClock`) | unit | 5 |
| Events wait for their instance | `Event_for_a_parked_instance_applies_after_the_answer`, `Early_event_applies_on_entering_the_wait`, `Held_event_expires` | unit, integration | 4 |
| Revoke and drift take effect at once | `Revoke_parks_running_instances`, `Pin_drift_parks_on_next_commit`, `Revoked_effect_is_refused_at_dispatch` | integration | 2, 6 |
| One transaction | `Failed_outbox_write_rolls_back_the_transition` | integration | 4, 6 |
| Callers and classes | `Session_cannot_complete_grant_or_accept` (`SND-AUTH-002`), `Session_may_take_over_but_not_hand_back`, `App_runs_desk_but_not_protected`, `Every_engine_variant_has_a_catalog_row`, `Agent_variables_match_the_probe` (golden) | unit | 2, 4 |
| Every answer does something | `Answer_<reason>_runs_<resume action>` for each §5.8 row | unit | 3 |
| Exits work under a revoke | `Cancel_is_exempt_from_the_approval_check`, `Parent_cancel_cancels_a_drifted_child` | unit, integration | 3, 4 |
| Outbox ids are unique per commit | `Two_parks_in_one_entry_get_distinct_command_ids` | unit | 6 |
| Live frames keep their rows | `Child_result_survives_a_long_parent_park` | integration | 7 |

## 11. Test strategy by lane, and telemetry

| Lane | Budget (per package) | Holds |
|---|---|---|
| `unit` | < 10 s | The rules and fixtures; normalization, hash and schema staleness; the YAML features; every pure decision; the eval cases; a bounded seeded property run; the fake-store contract bases; `BlockContract`; architecture tests |
| `integration` | < 60 s | The contract bases on SQLite; migrations on a copy of the previous schema; loopback endpoints; CLI golden files (every §6.4 layout, empty states); the services against SQLite with `FakeTimeProvider` and a fake `IMonotonicClock` |
| `heavy` | nightly | S1's AOT smoke, which gains `recipe validate` |

**Telemetry:**
- spans per `Commit` (`Aw.Sindri.Workflow`);
- metrics:
  - transitions;
  - refusals by block;
  - parks by reason;
  - escalations and overrides;
  - session refusals;
  - version conflicts;
  - inbox depth;
  - deadline lag;
  - desk task age;
  - **park-to-answer latency**;
  - **time to outcome per recipe**.

These feed A §17.2, and all of it is scrubbed.

## 12. The first recipes

- **Real recipes:** `recipes/sindri/workflow.item.yaml` and `workflow.step.yaml`, with evals in
  `recipes/sindri/evals/`.
- **Dev recipes:** `recipes/dev/`, with `profile.yaml`:
  - `workflow.dev-timeout`: a desk Step and a `1m` timeout (desk floor off in the dev profile:
    `workflow.deskTimeout: 1m`);
  - `workflow.dev-notify`: `ask` (a desk Step), then `ask -> sent` with `effect.notify.desk`, then `sent` waits on
    `notify.delivered` (producer `system:dispatcher`) and goes to `done`. A dispatch refusal arrives while it is in
    `sent`, so it can park.
- **Validate-only fixtures** (tests, with a test catalog):
  - `workflow.bugfix`: modes A and C, with block ids in the `step.bugfix.*` grammar;
  - `workflow.direction`.
- **Blocks:**

| Block | Runtime |
|---|---|
| `step.produce`, `step.checks`, `step.verify` (`deskCapable`) | Desk |
| `gate.budget.available` | Real, pure |
| `gate.route.startable` | A stub over `workflow.route` (P7) |
| `effect.claim.ref` | Real (P6), in the item's repo. A ref that already names this instance's effect identity counts as `created` |
| `effect.notify.desk` | Real (P6) |

- **Events:**

| Event | Producer | Outcomes | Facts |
|---|---|---|---|
| `item.routed` | `router.stub` | `startable`, `notStartable` | `fact:stepSpec` |
| `claim.created` | `system:dispatcher` | `created`, `claimExists` | — |
| `notify.delivered` | `system:dispatcher` | `delivered` | — |

- **`fact:item`** is `{ id, repo }`, where `id` is the `ItemId` (API §3) and `repo` is a key of the profile's `repos`. `effect.claim.ref` creates
  `sindri/{user}/{item}` in that repo.
- **`workflow.route`** (P7) is `{ startable: all | none }`. The `router.stub` service fires on each
  `workflow.started` of `workflow.item` and emits `item.routed` with `outcome` taken from that key, and with
  `fact:stepSpec` copied from `fact:item`'s spec.

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
  running:   { kind: wait, timeout: { after: 14d, then: park },
               waitFor: { child: { recipe: workflow.step, inputs: { spec: { fact: fact:stepSpec } }, onParked: propagate } },
               next: "Waiting for {child}." }
  done:      { kind: terminal, outcome: resolved }
  dropped:   { kind: terminal, outcome: unresolved }
transitions:
  - { from: triaged,   to: startable, on: wait.satisfied, when: { outcome: startable },
      gates: [gate.route.startable], produces: [fact:stepSpec] }
  - { from: triaged,   to: dropped,   on: wait.satisfied, when: { outcome: notStartable } }
  - { from: startable, to: claimed,   on: wait.satisfied, gates: [gate.budget.available], effects: [effect.claim.ref] }
  - { from: claimed,   to: running,   on: wait.satisfied, when: { outcome: created } }
  - { from: claimed,   to: dropped,   on: wait.satisfied, when: { outcome: claimExists } }
  - { from: running,   to: done,      on: wait.satisfied, when: { outcome: resolved } }
  - { from: running,   to: dropped,   on: wait.satisfied, when: { outcome: cancelled } }
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

**Step descriptors:**
- `step.produce`: outcome `done`, required output `fact:output`;
- `step.checks`: outcomes `passed` and `failed`, output `fact:findings` with `requiredOn: [failed]`;
- `step.verify`: outcomes `accepted` and `rejected`, output `fact:findings` with `requiredOn: [rejected]`.
- The P6 and P7 blocks (`effect.*`, `gate.route.startable`) ship in P1 as descriptors with unavailable runtimes,
  so P1 can validate both recipes.

The child's outcomes are `resolved` and `cancelled`, and `workflow.item` covers both.

## 13. Review resolutions

/autoplan ran three passes (2026-10-10), each over five lenses. No pass found a CRITICAL. Passes 1 and 2 came back
NEEDS_WORK. In pass 3, security passed and the other four lenses still had HIGHs, which revision 4 fixes. The consolidated reviews are in `~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-workflow-engine/`
(`consolidated-review.md`, `-r2.md`, `-r3.md`). The owner capped the loop at three passes, so revision 4 isn't
reviewed again. Its fixes are listed below for the owner's review.

### Pass 1 (C1–C32)

| Id | Fixed in |
|---|---|
| C1 | W12, §5.3, §5.4 |
| C2 | W14, §5.5 |
| C3 | W15, §5.7 |
| C4 | §2 `EventDescriptor`, `RCP-EVENT-001`, §12 |
| C5 | W16, §5.1 frames |
| C6 | W17, §5.7 |
| C7 | W20 |
| C8 | §3.8 |
| C9 | §4.2, §6.3, §6.4 |
| C10 | Phases |
| C11 | §6.1, S1-D8 |
| C12 | W1, §2 |
| C13 | W11, §4.1 |
| C14 | §8, which lives in `docs/csharp/sindri.md`, never in the generated TS `docs/sindri/errors.md` |
| C15 | W26, §7.2 |
| C16 | `RCP-CAP-005`/`006`, W9 |
| C17 | `RCP-CAP-007` |
| C18 | W22, §6.2 |
| C19 | W18, §5.6 |
| C20 | W21 |
| C21 | §9.1 |
| C22 | §3.4, §5.4 |
| C23 | §4.2 |
| C24 | W11, §9.1 |
| C25 | §3.1 |
| C26 | §4.1, §5.2 |
| C27 | §9.2 |
| C28 | §6.3 |
| C29 | W27, §6.1, §7.1, §9.2, §11 |
| C30 | W19 |
| C31 | §3.7, §3.8, §6.3 |
| C32 | Below |

### Pass 2 (E1–E24)

| Id | Fixed in |
|---|---|
| E1 | W16, §5.1 |
| E2 | W22, §6.2, §4.2 |
| E3 | Phase graph (P4 needs P2; evals ship in P1; P4 wires approve to run them), `--nonce`, the P6 order |
| E4 | §12 (child outcomes, `item.routed` facts) |
| E5 | S1-D9; migrations are numbered in sequence, and the integrator of the phase that merges second renumbers its files at rebase |
| E6 | S1-D11 |
| E7 | Hotspot rules (Phases) |
| E8 | W27 |
| E9 | §3.8 escalated rows |
| E10 | W7 `--grant-tokens` |
| E11 | W20; §6.3 notification classes; §5.5 desk reports; §2 `Start` after the commit |
| E12 | §5.4 outbox ids |
| E13 | W13, §5.2 |
| E14 | §5.7 forwarding, cancel in every state |
| E15 | W11, §5.4 `outbox.refused` |
| E16 | §5.4 nonces |
| E17 | §3.8, §6.4 |
| E18 | §6.3 `--run`, §5.5, W20 `via: desk` |
| E19 | `RCP-CAP-005`/`006` |
| E20 | §5.2, §10 |
| E21 | §3.4 answer waits, §6.3, §6.4, §8, §4.2 |
| E22 | W5 `deskCapable`, `recipe list --stale`, `eval --scaffold` |
| E23 | §7.1, W23, §6.1 |
| E24 | Below |

### Pass 3 (G1–G18)

| Id | Fixed in |
|---|---|
| G1 | W16, §5.8 resume actions |
| G2 | W11, §5.3 step 5 exemptions |
| G3 | §5.4 UUIDv8 ids, effect identity |
| G4 | §5.7 live-frame rows, §12 `running` 14d |
| G5 | Phases: signal steps marked **(agent)** or **(owner)**, and the owner runbook |
| G6 | §5.3 order, §5.4 report key, §8 `SND-WF-009` |
| G7 | §3.8 drift row, §4.1 same-hash re-approval, §5.4 claim identity by item |
| G8 | §3.8 `timeout` and `dispatch` rows |
| G9 | §4.2 challenge bound to its subject |
| G10 | `RCP-CAP-005` |
| G11 | `RCP-TRIG-002`, `workflow command` |
| G12 | §6.2 probed markers, parent process, named refusals |
| G13 | §5.1 stack rules |
| G14 | §5.4 `MarkRefused` (K-D1), engine-owned commands exempt |
| G15 | §5.7 `onBehalfOf`, §3.4 `retry` keeps its entry, §12 `dev-notify` |
| G16 | §2 `requiredOn`, §9.1 child resolution, §6.1 dev profiles, §12 route and repo, P2 owns the actor table, §7.1 `/new-slice`, the P3 wave |
| G17 | §6.4 layout 1, §3.8 texts, §4.2 and §8 naming, `step.checks` (below) |
| G18 | Below |

### Written answers

- **P7 (host move):** approvals live in `ledger.db` and move with the A 17.19 handover snapshot.
- **P8 / product L9 (metrics):** §11.
- **Design LOWs:** one duration format and ASCII (§6.4); the fix appears on rule lines (§9.1); `--version` is gone
  in favour of nonces (§5.4); short hashes (§3.1); `takeover` from a park (§3.8); engine variables (§3.7); the
  `SND-WF-010` text (§3.8); empty states (§6.3).
- **Arch R11, R12, R14, R16:**
  - Early events are held (§5.7).
  - Gate answers are saved in their own transaction (§5.3).
  - Downtime counts as sleep **on purpose**: a laptop shut down for three days hasn't run the work, so its timeouts
    shouldn't fire. This reverses pass-1 F9(c) deliberately. `IMonotonicClock` makes it testable.
  - Each service runs on its own loop (S1-D10).
- **A-F20–F25:** gate answers are reused (§5.3); outbox ids are derived (§5.4); only `RCP-EFFECT-001` reads the
  profile; at most 3 re-decisions on a conflict, and Kernel's `OutboxRetryPolicy` for dispatch; each migration
  declares its indexes, covered by migration tests.
- **Product (pass 2):**
  - **`ILauncher`** gets its own design next, because it's the session-launch half of §17.29.3 and needs the
    container and attach work.
  - **Bugfix mode B** (two candidates in parallel) needs schema v2's fan-out. Until then the worker recipe runs
    modes A and C, and mode B stays in `bugfix-state`.
  - **The `/new-block` scaffold** is filed by the P7 integrator as an issue, once three real blocks exist.
  - **Pin drift** parks instances with `SND-RECIPE-006`. `recipe list --stale` names what to re-approve, and each
    re-approval is typed on purpose.
- **Security LOWs:**
  - **S2-L-a (nonce lifetime):** 10 minutes, cleaned up by the timers service.
  - **S2-L-b (who may write an event):** `producer` on the event descriptor.
  - **S2-L-c (behaviour changes):** a descriptor hash pins the declared contract, not the behaviour. A behaviour
    change bumps `Version` by review rule, and `BlockContract` tests pin the behaviour.
  - **S2-L-d (file paths):** the CLI sends content.
  - **S2-L-e (metrics):** session refusals and overrides are counted (§11).
  - **S-L1:** typed item ids.
  - **S-L2:** aliases are refused, and eval paths stay inside the recipe directory.
  - **S-L3:** the actor table (§6.2).
  - **S-L5:** the model proxy is the budget backstop.
  - **S-L6:** `RCP-NEXT-002`.
  - **S-L8:** the egress proxy.
- **Timeouts aren't capability fields** (`RCP-CAP-006` doesn't compare them): a shorter timeout only parks sooner,
  and a longer one only waits longer. Neither grants anything.
- **Pass 3 answers:**
  - **`step.checks` with a real runtime** (product M3): a runtime that runs the check command is the `run-test`
    evidence block lifted from `bugfix-state` (A 17.28.2). It belongs to the worker-recipes design together with
    Judge and Evidence. Until then the desk outcome is labeled `via: desk` in receipts.
  - **Scaffolded evals** (product L4): a scaffold proves only the happy path, and the approve preview marks
    `scaffold only` so the human sees how thin it is.
  - **Budget park on `workflow.item`** (arch N7): `gate.budget.available` reads the profile budget, and
    `--grant-tokens` raises the instance's allowance against it. The slot is released at the park (§5.5).
  - **Arch L1–L8:** the nonce step is in `Commit` (§5.3); UUIDv8 (§5.4); the arm counter's scope (§5.1); child
    start events (§5.2); the drift source (§5.4); the run token in the layout (§6.4); `RCP-TIME-002`; the
    dispatcher actor `system:dispatcher`.
  - **Design G7 and G8:** the start hint, the engine variables, empty states, `takeover` from escalated, the
    grant-tokens text and the `--needs-me` dedupe are in §3.7, §3.8, §4.2 and §6.3.
  - **Devex LOWs:**
    - the notifications path is `$AW_SINDRI_HOME/notifications.jsonl` (rev 5; rev 4 named the TS folder);
    - child inputs use `{ fact: <key> }`;
    - P7 passes `fact:item`;
    - the P2 signal shows `<hash>`, not a fixed value;
    - the `workflow` group table is P4's contract unit;
    - `approve <file>` registers first when needed;
    - S1-D4 has a row;
    - the MCP architecture test applies once an MCP project exists;
    - `RCP-EFFECT-001` reads the approved profile.
  - **Security L-1 and L-2:** the 1 MiB caps and `0600` (§6.1); the `ui` class (§6.2).
- **Crafted bypasses** are outside the threat model (A 17.28.3; `docs/csharp/bugfix.md` "Threat model"):
  - direct SQLite writes;
  - unauthenticated loopback callers and port squatting;
  - hand-built challenge requests;
  - unsetting the harness markers.

  Gates stop honest mistakes. The sandbox, the owner-only state directory and the loopback bind are the boundary.

## Phases

Each phase is one PR. The order across all three specs is the one table in `docs/sindri/README.md` ("Build order");
the dependencies below follow it. Signals write `T-n` for the `ITM-…` id that `sindri item add "<title>"` printed:
every signal creates its item first (API P3). Every **(owner)** step runs through `scripts/sindri-prove.sh human` (API
§12), so the Prover runs both halves and the ledger records `human:prover`.
P1 and P3 are stacked on `feat/sindri-s1-app-shell` and add files only. P2 and every later
phase start once S1 has merged, target `main`, and stack on the phases they need. Every phase updates `docs/csharp/sindri-workflow.md` and
`docs/csharp/sindri.md`. Sizes: S is a few files, M one subsystem slice, L a store or several slices with
migrations.

### Parallel execution with an agent team

`/specToProvenPR` runs the phases with an agent team, working from this graph and keeping every agent busy:

```
P1 model + validator ──┬──► P2 registry + approval (S1, API P1) ──┐
                       └──► P3 pure engine + evals ──────────────────┴──► P4 store, start, desk, events (API P3) ──┬──► P5 timers, takeover ─┐
                                                                                                                  └──► P6 effects ─────────┴──► P7 children + item ──► P8 agent runtime (API P10, Judge #156)
```

| Wave | Parallel | Starts when |
|---|---|---|
| 1 | P1 | Now (stacked) |
| 2 | P2 ∥ P3 | P3: P1's contract unit is pushed (stacked; doesn't wait for S1). P2: also S1 merged |
| 3 | P4 | P2 and P3 merged |
| 4 | P5 ∥ P6 | P4 merged |
| 5 | P7 | P5 and P6 merged |
| 6 | P8 | P7, API P10 and Judge (#156) merged |

These engine-local waves sit inside the cross-spec order in `docs/sindri/README.md` ("Build order"), which is the one
that decides what starts when.

**Units:** each phase starts with one contract unit (records, ports, stubs, committed red). The other units then
fan out, one implementer each:

| Phase | Contract unit | Parallel units |
|---|---|---|
| P1 | Records, ids, descriptors, catalog, `ValidationContext`, the validator shell, `WorkflowErrors`, the `recipe` group table | (a) reader, normalization, hash, schema and overlay, `RCP-SCHEMA/STATE/INIT/DUR/HASH` · (b) graph rules · (c) wiring, block and event rules · (d) `RCP-CAP-001/002/004–007` · (e) descriptors, `recipes/**` including the evals and dev profile, the fixtures · (f) CLI verb, golden files, the guide |
| P2 | Ports, fakes, contract bases, the challenge shell, the engine's catalog rows on API P1's chain, and the agent-variable probe | (a) SQLite adapters, S1-D9 · (b) register, show, list, `--stale` · (c) approve (including drift re-approval), revoke with previews, cascade, wake · (d) pins, `RCP-VERSION-*`, registry events, the architecture test · (e) the harness probe and its golden test |
| P3 | Instance, frames, triggers, decision signatures | (a) `TransitionDecision`, `RefusalRules` · (b) `FrameRules` and the engine-owned states · (c) `LoopRules`, `BudgetRules`, grants · (d) `ChildPropagation` · (e) `EventFold`, the in-memory store, `WorkflowStoreContract` · (f) `recipe eval`, `--scaffold`, `--record` format |
| P4 | `SqliteWorkflowStore` skeleton, S1-D2/D7, the `workflow` group table | (a) store, migrations, contract binding · (b) start, commands, nonces, the §5.8 resume actions · (c) desk runtime, `complete`, `RunPostCommit` · (d) router, inbox, S1-D8/D10 · (e) status, list, events (S1-D6), golden layouts, approve running evals, S1-D11 (`deskTimeout`, `eventHold`) |
| P5 | The deadline model, `IMonotonicClock` | (a) `TickTimers`, shift · (b) takeover, handback, `takeoverIdle` |
| P6 | `IEffectKind` runtime, the dispatcher shell, K-D1 | (a) outbox writes, `workflow_commands`, UUIDv8 ids, effect identity, `retry` re-emission · (b) the dispatcher, `notBefore`, recheck, `outbox.refused` (S1-D3) · (c) `notify.desk`, notifications, `outbox list` · (d) `effect.claim.ref`, S1-D11 (`effects`, `dispatchDelay`) |
| P7 | Child rows, inbox kinds | (a) child creation and propagation · (b) router and slot stubs, `route` key, both gates · (c) `workflow.item` end to end, filing the `/new-block` issue |

**Hotspots** (`PublicAPI.Unshipped.txt`, `WorkflowErrors`, `Add…()` methods, the group command tables, the JSON
context): units **append** their own lines, so every unit commit builds. The integrator resolves the unions when it
merges the units. Migrations are numbered in sequence. Within a phase the integrator assigns the numbers, and across
parallel phases the one that merges second renumbers at rebase.

**Roles:**
- **Orchestrator** (the `/specToProvenPR` session): owns the graph and the ready queue, dispatches the next unit the
  moment an implementer frees up, rebases stacked branches, and writes no product code.
- **Implementer:** one per unit, in its own worktree, red first.
- **Integrator:** one per phase. It merges the units, runs the full gate (zero warnings, unit and integration lanes
  in budget, `format`, `check-deps`, `check-rules`), and opens the draft PR.
- **Prover:** runs the **(agent)** steps with `sindri-prove.sh <verb>` (a `session`) and the **(owner)** steps with
  `sindri-prove.sh human <verb>` (API §12), and records the evidence. It checks both against the expected lines and
  the ledger (`workflow events`). A phase isn't proven until both halves match.
- **Reviewers:** run `/review` until no blocking or issue findings remain. Fixes go back to implementers as units.

**Limits:**
- At most two units build or test at once (the heavy-job lock's two slots); the other units keep writing code.
- The session cap is respected.
- Reviews overlap the next wave's contract units and are never skipped.

### Phase 1: the recipe model and validator

- **Goal:** recipes parse, normalize, hash and validate in-process.
- **Files:**
  - `src/Sindri/Aw.Sindri.Core/Workflow/**`;
  - `Features/{ValidateRecipe,EvalRecipe(shell),Blocks}/**` (descriptors);
  - `Infrastructure/Workflow/YamlRecipeReader.cs`;
  - `Host/Commands/Recipe/**`;
  - `schemas/recipes/*.json`;
  - `recipes/**`;
  - `tests/Aw.Sindri.{Tests,Testing}/Workflow/**`;
  - `docs/csharp/sindri-workflow.md`.
- **Size:** L.
- **S1 dependency:** stacked on S1's branch (adds files and registrations only).
- **Signal** (all **(agent)**; these are `local` verbs):
  - `sindri recipe validate tests/Aw.Sindri.Testing/Workflow/Recipes/invalid/RCP-REACH-001.yaml` exits 1 and prints
    `RCP-REACH-001: state 'orphan' is unreachable from 'produce' at RCP-REACH-001.yaml:14:3 (states.orphan); add a
    transition into it or remove it`.
  - `sindri recipe validate recipes/sindri/workflow.item.yaml` and `workflow.step.yaml` exit 0 and print their hashes.

### Phase 2: registry and approval

- **Goal:** recipes are registered by hash and approved or revoked through a previewed, challenged desk action, with
  pins, cascade and wake.
- **Files:**
  - ports and slices;
  - SQLite adapters and migrations (with S1-D9);
  - the challenge;
  - registry events;
  - contract bases;
  - the architecture test;
  - docs.
- **Size:** M.
- **S1 dependency:** S1 merged, and API P1 (the command chain) merged; S1-D5, S1-D9.
- **Signal:**
  - **(owner)** `sindri recipe approve recipes/sindri/workflow.step.yaml` prints the preview (capability uses,
    pinned blocks, the diff against nothing) and asks for `approve workflow.step 1`. Then `sindri recipe list` shows
    `workflow.step@<hash> approved (desk)`.
  - **(agent)** `sindri recipe approve recipes/sindri/workflow.step.yaml` exits 2 with `SND-AUTH-002` naming the
    agent variable. **(owner)** the same with `< /dev/null` (no terminal) exits 2 with `SND-AUTH-002`.
  - At this phase, approval requires the eval files to exist; P4 makes it run them.

### Phase 3: the pure engine and recipe evals

- **Goal:** every decision is pure Core, proven by the eval cases and the event fold.
- **Files:**
  - `Core/Workflow/Engine/**`;
  - the in-memory store and `WorkflowStoreContract`;
  - `EvalRecipe`, `--scaffold`;
  - docs.
- **Size:** L.
- **S1 dependency:** none.
- **Signal** (all **(agent)**):
  - `sindri recipe eval recipes/sindri/workflow.step.yaml` prints `happy pass`, `no-progress pass`, `rounds pass`
    and exits 0.
  - A copy with one edited `expect` exits 1 and names the line that diverged.
  - `sindri recipe eval --scaffold recipes/dev/workflow.dev-timeout.yaml` prints a case that then passes.

### Phase 4: the store, start, desk Steps and event delivery

- **Goal:** instances start and move through fenced, trigger-keyed commits; desk Steps complete them; events reach
  them through the inbox.
- **Files:**
  - `SqliteWorkflowStore` and migrations;
  - the slices in §7.1 for start, command, complete, nonce, get, list, route, inbox and post-commit;
  - the desk runtime;
  - the actor table;
  - S1-D2, D6, D7, D8, D10 and D11 (`deskTimeout`, `eventHold`);
  - golden layouts;
  - approve running the evals;
  - docs.
- **Size:** L.
- **S1 dependency:** S1 merged, API P3 (items) merged; this phase makes its S1 changes.
- **Signal:**
  1. **(owner)** With `workflow.step` approved (P2), `sindri workflow start workflow.step --item T-1 --fact fact:spec=spec.md`,
     then `sindri workflow status T-1/step/1`, shows the desk-task layout with `--run 1.1`.
  2. `sindri workflow complete T-1/step/1 --run 1.1 --outcome done --output fact:output=out.md` prints `-> checks`.
     Two more completes (`passed`, `accepted`) print `done: resolved`. `sindri workflow events T-1/step/1` lists
     every event.
  3. **(owner)** Re-running the first `complete` exactly (the same `--run 1.1`) exits 0 with `already applied`, and
     no new event appears. Re-running it with `--nonce <the nonce from --json>` and a new `--run` exits 2 with
     `SND-WF-009`.
  4. **(agent)** The same `complete`, from the agent session, exits 2 with `SND-AUTH-002` (authorization runs before
     the key). `sindri workflow events T-1/step/1 --json` matches the owner transcript.

### Phase 5: timers, takeover and handback

- **Goal:** timeouts fire as keyed transitions, sleeps shift them, and takeovers pause them.
- **Files:**
  - `TickTimers`, the shift;
  - `IMonotonicClock` and its adapter;
  - takeover, handback, `takeoverIdle`;
  - migrations;
  - docs.
- **Size:** M.
- **S1 dependency:** none.
- **Signal** (dev profile, `workflow.deskTimeout: 1m`; **(owner)**, then the Prover checks `workflow events`):
  - `sindri workflow start workflow.dev-timeout --item T-2`, then wait at least 75 s (the timeout plus a 15 s tick),
    and `status` shows `parked: timeout`.
  - A second instance, taken over within 20 s of starting, shows `taken-over` and `paused timeout 0m 40s left` (±15 s).
    Nothing fires until `sindri workflow handback`, and it parks after roughly the remaining time plus one tick.
  - `sindri workflow answer T-2 --resume` puts the first instance back in its desk task, with the same run token.

### Phase 6: effects through the outbox

- **Goal:** effects are outbox commands in the transition's transaction; the dispatcher re-checks approval and the
  allowlist after a delay; refusals reach the instance; parks notify.
- **Files:**
  - the effect runtime, outbox writes, `workflow_commands`;
  - the dispatcher;
  - `notify.desk`, `claim.ref`;
  - notifications;
  - `outbox list`;
  - S1-D11 (`effects`, `dispatchDelay`);
  - migrations;
  - docs.
- **Size:** M.
- **S1 dependency:** S1-D3 (made here).
- **Signal** (dev profile with `notify.desk` allowlisted and `dispatchDelay: 60s`; **(owner)**, then the Prover
  checks `outbox list --json` and the events):
  1. `sindri workflow start workflow.dev-notify --item T-4`, then `complete` its desk Step. The transition event
     lists an outbox id. After the delay, `sindri outbox list --dispatched` shows it, the notifications file has the
     line, and the instance reaches `done` through `notify.delivered`.
  2. On `T-5`: `complete`, then within the 60 s window run `sindri recipe revoke workflow.dev-notify@<hash>` and type
     its challenge. `outbox list` shows the command `refused (SND-RECIPE-005)`, and `sindri workflow status T-5`
     shows `parked: spec-conflict` (revoked; only `cancel` is offered), and `cancel` works.

### Phase 7: children and the item lifecycle end to end

- **Goal:** `workflow.item` runs with a `workflow.step` child; every child change propagates.
- **Files:**
  - child creation and propagation;
  - the router and slot stubs, the `route` key;
  - `gate.route.startable`, `gate.budget.available`;
  - `recipes/sindri/workflow.item.yaml` evals;
  - migrations;
  - docs.
- **Size:** L.
- **S1 dependency:** none beyond P4–P6.
- **Signal** (dev profile naming a temp repo, with `workflow.route: { startable: all }`; **(owner)**, then the Prover
  checks the events):
  - `sindri workflow start workflow.item --item T-3 --fact fact:item=item.json` shows `triaged -> startable -> claimed -> running` in
    `sindri workflow events T-3`, and `git -C <repo> show-ref sindri/<user>/T-3` finds the claim ref.
  - Completing the child's three desk Steps moves the parent to `done: resolved`.
  - On `T-7`, completing `checks` as `failed` twice with the same findings parks the child (`no-progress`), and the
    parent shows `parked: no-progress (child)`. `sindri workflow answer T-7 --grant 1`, with its challenge typed, is
    forwarded with `onBehalfOf`, re-runs the child's `produce` (§5.8), and resumes both.

### Phase 8: the agent runtime

- **Goal:** a Step runs in a launched agent session that reports its own result; the factory line runs unattended.
- **Files:** the agent runtime and its post-commit launch; the `step-report` token check; `session.launched` and
  `session.ended`; token usage from the provider adapter; the `workflow.agent.target` profile key; docs.
- **Size:** M.
- **Depends on:** P7, API P10 (`ILauncher` and §8.1), Judge's provider port (#156). This phase adds Sindri's
  `deps.yaml` edge to Judge with a recorded fan-out exception (Sindri is at 4), reviewed like Context's.
- **Signal:** with `workflow.agent.target: claude`, starting `workflow.step` on a new item launches a Warp tab running
  Claude Code. The session's `complete --run` moves it to `checks`, and the event records `via: agent` and the token
  usage. The same `complete` from another session exits 2 with `SND-AUTH-002`. Killing the session parks the Step
  through `step.failed`.
