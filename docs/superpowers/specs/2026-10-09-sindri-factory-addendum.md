# Sindri — factory addendum (§17)

Status: draft for review, revision 3 · 2026-10-09 (applies the /autoplan findings C1–C17; resolutions in §17.19)
Revision 2 adds:
- development lenses and workflow generation (§17.7)
- lean session bootstrap (§17.8)
- standing workflows (§17.9)
- the context graph as the part Sindri builds, with pluggable components (§17.3)
- a sandbox policy compiled per task (§17.5)
- the human's answers to Q1, Q3 and Q5, with options for Q2 and Q4 (§17.17)

Amends: `2026-10-07-sindri-design.md` (revision 7) and the Plan 6 dashboard spec. Where they disagree, this
addendum wins for the sections it names. Everything else in the main spec stands.

## Core tenet: Sindri grows Sindri

Everything this addendum defines is a **starting point**, not a finished design. That covers the lenses,
evidence kinds, standing workflows, context-graph components, rules, budgets and surfaces. Sindri improves,
grows, enhances and evolves Sindri: it measures its own parts (§7.4, §7.7), proposes changes to them, **finds new
capabilities worth adding** (§17.18), and builds the approved ones through its own factory (§7.6). The human
approves direction. Sindri does the finding, the building and the proving.

### Architectural principles that follow from the tenet

Sindri can only evolve parts it can isolate, measure and swap, and it can only afford to run them if most decisions
cost no model turn. Three principles follow, and every section below applies them.

1. **Modularity.** Every part is a module with a typed contract, its own eval suite and telemetry, and a
   registry entry. Parts include lenses, evidence providers, standing workflows, context-graph components, rule
   packs, decision questions, prompts and surfaces. A module can be proposed, A/B tested, adopted or reverted
   without touching its neighbours. A part that can't be swapped alone is a design defect.
2. **Context management.** No session gathers its own context. Context is assembled by code into a budgeted,
   cited injection plan (§17.5, §17.8), and measured for size and relevance (§17.2). Modules declare what context
   they need. They never load "everything that might help".
3. **The decision ladder.** Each decision takes the cheapest rung that can make it with enough confidence:

   | Rung | Example | Cost |
   |---|---|---|
   | 1. Deterministic code | Selector matching, schema checks, path guards, routing tables, lens triggers | ~0 |
   | 2. A decision model | A judge question on Jev, with calibrated thresholds (the existing `judge` chain) | A fraction of a cent, milliseconds |
   | 3. A small model, one bounded call | Classification or extraction with no tools | Low |
   | 4. An agent turn or session | Design, implementation, open-ended investigation | High |

   - A decision escalates a rung only when the lower rung is below its threshold (invariant 4).
   - Every decision records which rung made it, the cost, and the later outcome.
   - Self-evolution pushes decisions **down** the ladder. A rung-3 or rung-4 decision that keeps getting the same
     answer becomes a proposed judge question. A judge question that is reliably confident becomes a proposed
     rule or code path.
   - "Agent turns that a cheaper rung could have made" is a tracked metric (§17.2).

## 17.0 Why this addendum

Plans 1–6 built the control room: measurement, the ledger, the code index, scoping, offline self-evolution and
the dashboard. Nothing yet takes a designed project to merged code without the human dispatching it, and every
agent session still starts heavy: memory bootstrap on the first turn, then every skill and subagent definition
loaded whether or not the task needs it. This addendum defines the production line and the context discipline
it depends on.

**The target pipeline:**

```
 scope ──► design studio ──► handoff packet ──► workflow generation ──► factory build ──► review anywhere ──► human merges
(sindri    (human SME + agent,  (frozen, schema-    (deliverable → lenses,   (Sindri supervises;    (phone or desk;      (epic → default
 scope)     at the desk)         validated)          steps, injection plan)   narrow sessions)       signed decisions)    branch)
```

Next to the pipeline, **standing workflows** (§17.9) run on events: PR babysitting, triage, incidents,
documentation, cleanup, digests and performance watching.

**Changed assumptions:**

| Main spec | Changed to | Section |
|---|---|---|
| Fewer human turns per merged PR is the headline metric (§2) | Build-side turns go toward 0. Product and design turns are the human's job and are expected to rise | §17.2 |
| `full` mode is deferred; scoping output goes to the human for approval (§4, §7.1) | Scoping feeds a design studio with the human. The build starts only from a frozen handoff packet, then runs without the human until review | §17.4 |
| The v1 worker wraps `/bugFixOrchestrator` (§11.4) | A workflow generator turns each deliverable into Steps run by **lenses** (QA, UI, UX, architecture, data, language, security, performance and more), handed off automatically | §17.7 |
| Sessions inherit the host's full skill set and memory bootstrap | Sindri sessions load only what the Step's injection plan names | §17.8 |
| The human always merges (§2 non-goals) | Task PRs merge into a stacked epic branch under the deterministic gate; the epic → default-branch PR is always the human's | §17.10 |
| Approvals come only from host CLI verbs (§8.2, §10) | CLI verbs stay authoritative. A tailnet-only surface adds signed phone decisions for non-protected kinds | §17.12 |
| macOS first, Linux built-ins later (§12) | The factory host is swappable between a laptop and an always-on Linux box | §17.13 |

**Rings:** ring 0 is this toolkit; **ring 1** is workplace repos, where the factory is tested and improved
first; **ring 2** is a private product repo, started after ring 1 holds. Ring specifics stay in their private
profiles and repos. This document is generic.

## 17.1 New invariants

12. **The human is the product SME.** Sindri batches, contextualizes and cites product questions. It never
    answers them, and it doesn't count product or design turns as cost.
13. **Agents don't decide their own scope, and models never grant capabilities.** A session's write scope,
    sandbox policy, context, rules, lenses and next handoff come from the frozen packet and its frozen workflow,
    computed by Sindri code. A model may *propose* the Step order, review-only lenses and injection plans at
    planning time (§17.7). It may never propose a **capability field**: tool, command, MCP, network, write-scope
    or budget allowlists. The session policy is always the profile's approved allowlist (the ceiling) intersected
    with the lens and task allowlists, never more. Freezing and re-freezing are desk actions. Widening scope is a
    scope-expansion request (§6.1), never a silent edit.
14. **No build without a frozen packet.** A build Step starts only from a handoff packet that passes
    `sindri packet validate` and is frozen by hash (§17.4).
15. **Context is budgeted.** Every session starts from an injection plan with a token budget per section.
    Anything loaded that the plan didn't name is a defect with a metric.
16. **Nothing here is final.** Every catalog, schema, threshold and surface in §17 is a registered artifact
    that Sindri may propose to change, extend or replace through §7.4, §17.14 and §17.18. A part with no eval
    suite or telemetry is itself a gap that Sindri proposes to fill.

## 17.2 Metrics, revised

These replace the "human turns per merged PR" row of §2 and its use in the auto-start kill criterion.

| Measure | Target | Why |
|---|---|---|
| Shipping direction, continuity, QA direction, recipes, dispatch turns | → 0 per merged PR (step-0 baseline, then −75% after 4 weeks of factory builds) | The factory's job |
| Mid-build product questions (asked after the packet froze) | Tracked. Each one is a **packet miss** fed to scoping backtests and evolve | The question belonged at the desk |
| Product and design turns at the desk | Tracked, no target. Expected to rise | The human's job |
| Tokens in context at a session's first model call | ≤ the injection plan's budget; tracked per role and lens | Invariant 15 |
| Context relevance (share of injected sections the session cited or used) | ≥ 0.6 per lens after calibration | Invariant 15 |
| Out-of-scope attempts per task (denied by the sandbox policy) | Tracked; a rising rate is a workflow-generator defect | Invariant 13 |
| Phone decisions per merged PR, and median decision latency | Tracked | Measures the review surface |
| Decisions by ladder rung, cost per decision, and agent turns a lower rung could have made (judged after the fact) | The share made at rungs 1–2 rises month over month | The decision ladder |

Auto-start's kill criterion keeps the rework comparison and drops the −20% total-turns condition.

## 17.3 Knowledge layer: the context graph

A human engineer brings six kinds of context to a requirement. The factory needs all six, joined:

| Dimension | Examples |
|---|---|
| **Product surfaces and behavior** | Screens and routes, actions, features, entities, flows, the rules attached to them |
| **Codebase** | Symbols, modules, call graph, clones, dependencies, ownership, history |
| **Requirements and conversations** | Tickets, specs, ADRs, chat threads, meeting notes, decisions |
| **User behavior** | Session replays, funnels, journeys, feature usage |
| **Telemetry and operations** | Traces, metrics, logs, errors, incidents, deploys, performance baselines |
| **Design** | Design files, tokens, design-system components, mockups |

**What exists elsewhere, and what doesn't.** Each component is being improved quickly by people focused on it:
connectors, memory, retrieval, storage, and offline consolidation ("dreaming"). Org-knowledge engines join
conversations and docs. Code engines index code. Analytics tools hold behavior and telemetry. **No product joins
all six into one graph keyed by product surface and code symbol.** That join is the part Sindri builds.

**Decision:** Sindri builds the **context graph**: the entity and link model, fusion, the query planner and the
evals. Every other component is a pluggable, pinned, swappable adapter. Each adapter pin is a registered artifact
(§7.4), so evolve proposes upgrades and swaps, and the backtest decides them (see Proof below).

```
 connectors ──► ingest + scrub ──► component stores ──► entity extraction ──► CONTEXT GRAPH (Sindri)
 (adapters)     (§8.3, §8.4)       (memory, vector,     (deterministic first,   entities · typed edges ·
                                    FTS, code index,     model-suggested         provenance · freshness
                                    analytics APIs)      flagged)                       │
                                                                                         ▼
                              query planner: seeds → graph expansion → per-component search → fusion (RRF)
                                                                                         │
                                                                                         ▼
                                                     injection plan sections (§17.8), cited, budgeted
```

**Component interfaces** (all return cited, scrubbed, trust-tagged records with stable refs):

| Component | Interface | Notes |
|---|---|---|
| `Connector` | `sync(cursor)`, `fetch(ref)` | One per source system. Read-only. MCP servers count, through a read-only MCP client with a server and tool allowlist |
| `MemoryStore` | `search(q)`, `write(rec)` (sindri-owned facts only), `consolidate()` | `consolidate` is the "dreaming" pass: dedupe, merge, decay, propose edges. It runs as a heavy job |
| `Retriever` | `search(q, k)` over FTS and vectors | Local by default: embeddings on loopback (§6.2) |
| `CodeGraph` | the existing code index and graphify | Unchanged |
| `ProductGraph` | `nodes(filter)`, `edges(node)` | Sindri-owned. Built from packets, routes, UI code and docs |
| `BehaviorSource` | `journeys(surface)`, `usage(surface)` | Summaries only: structure and counts, never values typed by users |
| `TelemetrySource` | `query(surface, window)` | Errors, latency, traces linked to routes and symbols |

**The context graph itself:**
- **Entities:** product nodes, code symbols and modules, records (tickets, threads, docs), journeys, telemetry
  series, incidents, decisions, rules.
- **Edges:** `implements`, `mentions`, `decides`, `exercises` (a journey through a surface), `observes`
  (telemetry on a route or symbol), `violates`, `supersedes`.
- **Deterministic links first:** explicit ids (ticket keys, PR numbers), paths, symbol names, route patterns,
  flag keys, component names, span names. Model-suggested links carry `provenance: model` and a confidence. They
  are used for recall, never as the only evidence for a surface.
- **Freshness:** every edge stores the source refs and the commit sha it was computed at, and is rebuilt with the
  index (§6.2 freshness rules).

**Query planner:** seeds come from the task or brief text and the packet's product nodes. They are expanded
through the product and code graphs (bounded depth), then sent to each component with the expanded entity set.
Results are fused with reciprocal rank fusion under per-dimension budgets that the injection plan sets.

**Proof:** `sindri scope --backtest` gains one arm per dimension plus `all`. A component or dimension is enabled
for a repo only when its arm improves recall without dropping precision below the §7.5 bar. Swapping an adapter
(a new memory store, say) is an evolve proposal judged on the same arms.

**Separation:** work and personal sources never mix. A profile names its connectors. Ring-1 profiles can't name
personal stores (for example Prism).

**Reuse:** the context graph is a separable package with no Sindri imports, so a ring-2 product with the same
problem can lift it after an ownership check.

## 17.4 Design studio and the handoff packet

**Design studio:** desk sessions where the human and an agent turn a scope map into a design, using the existing
skills (`/officeHours`, brainstorming, `/autoplan`, `/archReview`). The studio adds:
- a running **decision log**: each answered question is a record with its asker, answer and the product nodes it
  affects;
- `/rule` capture: "I always want X" said at the desk becomes a proposed rule (§17.6);
- a local **visual walkthrough** (HTML) generated from the packet. It is never published unless the human
  publishes it.

**Handoff packet:** a directory with a manifest, validated by a typed C# schema (System.Text.Json source-generated
records, with a generated JSON Schema for editors; see the readiness design §4b and §4c). Each schema file carries
`schemaVersion`. A minimal version ships in readiness step R1a so the C# ports themselves run as packets.

| Part | Contents |
|---|---|
| `packet.yaml` | id, repo, base sha, status, hash, links to everything below |
| Design | Design docs, ADRs, product-graph delta, data-model delta |
| Decision log | Answered questions, plus deferred ones with an owner and a "build may assume" default |
| Out of scope | An explicit list. The scope-expansion check reads it |
| Workstreams → tasks | Per task: id, deps, size, **write scope** (globs or "new: path"), **read seeds** (symbols, product nodes, refs), acceptance examples (a share held out), evidence plan (§17.11), rule selectors (§17.6), budget |
| Workflow | The frozen output of workflow generation (§17.7) |
| Plans | Generated plan files, so the plan-file tracker and `observe` pick them up |

**Deterministic checks** (`sindri packet validate`):
- the task graph is acyclic, and every task is in a workstream;
- every write scope exists at the base sha or is declared new;
- concurrent tasks don't have overlapping write scopes unless a dependency orders them;
- every acceptance example maps to at least one evidence kind;
- every open question is answered, or deferred with a default;
- every task fits its size class's budget, or is split;
- the holdout share exists and is stored where the implementer can't read it;
- the workflow passes its coverage checks (§17.7).

**Freeze and amend:** `sindri packet freeze` records the hash in the ledger, and builds run against that hash.
Any later change is an **amendment**: a scope-expansion or packet-miss item goes back to the desk queue, and only
the affected tasks are re-planned and re-frozen.

## 17.5 Narrow scope and sandbox per session

**Task compiler:** for each Step in the frozen workflow, Sindri code (no model) computes a **session spec**:

| Field | Computed from |
|---|---|
| `write` | The task's write scope, plus its tests by the repo's test-path convention |
| `read` | The context-graph query for the task's seeds (§17.3), within the injection plan's budget |
| `rules` | Rule selection (§17.6) for the repo, paths, task kind, lens and stage |
| `skills`, `agents`, `mcp` | The lens definition's allowlist (§17.7) |
| `sandbox` | The sandbox policy below |
| `evidence` | The evidence plan's providers (§17.11) |
| `budget` | Tokens, turns and wall time by size class and lens |
| `next` | The workflow's next Step and its input schema |

**Sandbox policy, compiled from the scope:** every session runs with a policy generated from its session spec.
Out-of-scope actions are refused by the gate (§8.1) and the container, never left to the agent.

| Control | Compiled from | Enforced by |
|---|---|---|
| File writes | `write` globs | `PreToolUse` gate on edit tools and on Bash commands with write targets; the `PostToolUse` working-tree check (§5.3) for scripts and codegen; the sindri-side verifier refuses a bundle with out-of-scope paths (§8.5) |
| File reads | Repo plus `read` refs; secrets and deny paths from the profile | Mounts (§8.2) and the gate |
| Commands | The lens's command allowlist (for example only the data lens may run migrations, only the performance lens may run load tests) | The gate (`SND-GATE-1xx`) |
| Network | Model proxy; package registry only when the task may change dependencies; the environment endpoints the evidence plan needs | The egress proxy (§8.2) |
| MCP servers and tools | The lens's allowlist, read-only by default | The image's managed settings and the gate |
| Skills and subagents | The lens's allowlist | Session image settings (§17.8) |
| Budgets | `budget` | The model proxy and the tick |

A denied action returns `SND-SANDBOX-1xx` (a new error area; `SND-SCOPE` stays with `sindri scope`) with the
rule, the attempted action and the fix, and turns into a scope-expansion request. Three denials
of the same kind in one Step park the Step with a `scope-mismatch` reason, which counts against the workflow
generator (§17.2).

**Context discipline:**
- The pack is assembled in budgeted sections: task and acceptance, rules, write-scope code, read context, refs.
  A section over budget is cut by rank, never by position.
- `session.maxContextTokens` (§7.2) is set per lens. Crossing it checkpoints and relaunches from pack plus
  checkpoint instead of compacting into a mixed context.
- A task whose pack can't fit with at least half the window free is split by the compiler (by write scope) or
  sent back to the desk. A session never finds out mid-task that it needs more context.

## 17.6 Rules and taste: a growing, selectively injected library

**Rule record** (a registered artifact, §7.4):

```yaml
id: taste.ts.no-barrel-files
kind: taste            # taste | engineering | process | product-precedent | security | lens
text: "Import from the module file, not an index barrel."   # one or two imperative sentences
why: "Barrels hide cycles and slow type-checking."
select: { repos: ["*"], languages: [ts], paths: ["**/*.ts"], taskKinds: [feature, refactor], lenses: [implementer, architecture], stages: [build, review] }
check: { type: eslint, rule: "no-restricted-imports" }       # deterministic | judge | none
source: { type: desk, ref: "decision-log#D-12" }             # desk | correction | adr | review | import
status: active          # proposed | active | retired
```

**Layers** (a later layer wins on the same id): core defaults → `rules/` in this repo (the human's personal
taste, safe to be public) → profile overlay (workplace and repo specifics, private) → packet (project-specific).

**Selection is deterministic:** match selectors against the session spec, rank by specificity and recent
violations, then fill the lens's rules budget. A session never carries the whole library.

**Enforcement ladder:** injected text → a judge `rule-check` question in the verifier → a deterministic check.
A rule with a `check` is enforced at commit and by the sindri-side verifier. A rule that keeps being judged
becomes a proposal to write its check.

**Growth** (self-adopt tier, §7.4):
- `correct` gains a `taste` label class next to design and process; each one drafts a rule proposal citing the
  turn.
- Review findings the human accepts or rejects become rule proposals or retirements.
- `/rule` at the desk lands as `proposed`, and the phone inbox approves or rejects proposed rules.
- A rule whose fires are overturned more than half the time over 20 fires is proposed for retirement.

**Product precedents** (`kind: product-precedent`) record how the human answered a recurring product question.
Agents cite them when they ask; they never answer for the human (invariant 12).

## 17.7 Development lenses and workflow generation

**Lenses:** a lens is a development discipline with its own questions, rules, tools and evidence. Each lens is a
registered artifact:

```yaml
id: lens.data
title: Database and data
triggers: { changeClasses: [schema, migration, query], paths: ["**/migrations/**", "**/*.prisma", "**/*.sql"], productTags: [reporting] }
questions: ["Is the migration reversible?", "Do new queries have indexes?", "Is tenancy enforced?"]
rules: { select: { lenses: [data] } }
skills: [archReview]          # only these are loaded
agents: [database-administrator]
mcp: []                       # read-only by default
commands: ["<repo migrate cmd> --dry-run", "<repo test:db cmd>"]
evidence: [migration, test]
model: { tier: standard, challenger: different }
budget: { tokens: 120k }
```

**Initial lens catalog** (an open list; lens definitions can be imported from community subagent catalogs as a
trust-tiered pack under §8.3, then evaluated like any other artifact):

| Lens | Typical triggers | Evidence |
|---|---|---|
| Product / PM (acceptance examples, holdouts) | Every feature task | Acceptance examples |
| System architecture | New modules, cross-package changes, new dependencies | Shape signals, `/archReview` notes |
| Implementer (per language: TS, Swift, Python, SQL, …) | Every code task; language picked from the paths | `test` |
| QA and evaluator | Every task | Held-out examples, the evidence plan |
| UI design | Components, styles, tokens | `design-parity`, `a11y` |
| UX design | Flows, navigation, forms, empty and error states | `ui-browser`, `behavior-sim` |
| Database and data | Schema, migrations, queries | `migration`, `test` |
| Security | Auth, input handling, secrets, dependencies, permissions | `security` |
| Performance | Hot paths, queries, bundles, build time | `perf`, `trace` |
| Infrastructure and deploy | CI, containers, IaC, environments | `cli-transcript`, deploy dry-run |
| Integrations | Third-party APIs, webhooks, connectors | `api-transcript` against mocks |
| Accessibility | UI | `a11y` |
| Documentation | Public APIs, user-facing behavior | `docs` |

**Workflow generation:** a meta-orchestrator Step that runs **once per deliverable** at packet time, not once
per agent turn.
1. **Inputs:** the packet, the context-graph query for its surfaces, and the lens catalog.
2. **Deterministic first pass:** lens triggers are matched against each task's write scope, change classes and
   product nodes. This yields the required lenses per task.
3. **Model pass** (bounded job, read-only): proposes the Step graph. That covers the order of lenses per task,
   which lenses review rather than build, where parallel work is safe, and the injection plan per Step
   (§17.8). It may add **review-only** lenses (they read and comment, and get no extra capabilities), and it never
   removes a required one. It can't write capability fields (invariant 13).
4. **Coverage checks** (deterministic): every required lens appears; every acceptance example has an evaluator
   Step; every Step's injection plan fits its budget; write scopes of parallel Steps don't overlap; every Step
   has a valid next handoff.
5. **Direction check** (§6.1) on a different model: "is this the right workflow for this deliverable?"
6. **Freeze** with the packet. From then on, injection and handoff are deterministic.

**Handoff is an artifact plus a ledger transition.** Each Step ends by writing an output that matches the next
Step's input schema. Sindri validates it, records the transition and starts the next Step. No human paste, and no
agent picks the next step. Verdicts are computed by a script from the evaluator's checks (invariant 3).
Information barriers stay: the implementer can't read held-out examples or evaluator notes.

**Existing skills become Step implementations in sindri mode:** `/specToProvenPR` (feature tasks),
`/bugFixOrchestrator` (bug tasks), `/review` and `/addressReview` (review loop), and the evidence providers. In
sindri mode there is no "ask the user" (`--permission-prompts none`; the preamble's asks become parks), evidence
and review state are keyed by worktree, and outward writes go through the write shim (§11.4). The interactive
mode stays for desk use.

## 17.8 Lean session bootstrap

Today every session runs the memory bootstrap on its first turn, then loads every installed skill's and
subagent's description into context. Sindri sessions start from an **injection plan** instead.

| Today (host sessions) | Sindri sessions |
|---|---|
| Memory bootstrap on the first turn (Prism `session_bootstrap`) | No memory bootstrap. Memory arrives through the context graph as cited pack sections |
| All skills and subagents listed | Only the lens's `skills` and `agents`, set in the session image's managed settings |
| All MCP servers connected | Only the lens's `mcp` allowlist |
| Global rules and CLAUDE.md in full | Selected rules (§17.6) plus the repo's agent file trimmed to the sections the injection plan names |
| Context grows by tool output | Per-section budgets, relaunch on overflow (§17.5) |

**Mechanism:** the session image is built per (repo, toolchain). At launch, Sindri writes a per-session settings
overlay that enables only the planned skills, agents, MCP servers and hooks. The `SessionStart` hook injects the
pack. `doctor` measures first-call context tokens per lens against the plan's budget.

**Host sessions (optional):** a `lean` profile for the human's own sessions applies the same idea at the desk.
Its contents are a minimal index of skills, memory loaded on demand, and lenses invoked by name. It is opt-in
and measured the same way.

## 17.9 Standing workflows

Some work isn't a packet. Standing workflows are workflow templates fired by events, using the same lenses,
sandbox policies, budgets and handoffs. Each one has an **autonomy tier**: `report` (writes a digest or comment
only), `propose` (opens a draft PR or an item for the human), or `act` (performs allowlisted actions).

| Workflow | Fires on | Lenses | Default tier |
|---|---|---|---|
| PR babysitter | A sindri PR's CI, review threads or conflicts change | Implementer, QA | `act` (fix rounds within the ship contract) |
| Triager | A new tracker item, alert or failed run | Product, the matching lens | `propose` (size, route, missing info; never answers product questions) |
| Inspector | A merge to the default branch; nightly | QA, security, performance | `report` → `propose` |
| Incident investigator | An alert or error spike from telemetry | Telemetry, the matching lens | `report` (a cited timeline and hypotheses) |
| Incident resolver | The human accepts an investigation's hypothesis | Implementer, QA | `propose` (fix PR; never deploys) |
| Incident comms manager | Incident state changes | Documentation | `propose` (drafts updates; the human sends) |
| Documentation maintainer | Merges that change public behavior | Documentation | `propose` |
| Codebase cleanup | Weekly; shape-signal trends (§6.2) | Architecture, implementer | `propose` |
| Daily digest | Daily | — | `report` |
| Performance watcher | Deploys; nightly benchmarks | Performance | `report` → `propose` |
| Skill maintainer | Skill-change items (§17.14) | Implementer, QA | `propose` |

A workflow moves up a tier only through an approval-tier profile change, after its `report` output has a measured
precision bar like direction checks (§6.1).

## 17.10 Stacked epics

- Each packet gets an epic branch. Task PRs are opened as a stack against it in dependency order (§7.3's stack
  machine).
- With `epicMerge: auto`, a task PR merges into the epic once the deterministic ship gate passes:
  - CI green on the **verified head SHA**, merged with `--match-head-commit <sha>`;
  - review threads resolved **by a human or a trusted bot** (`trustedBots`, §8.3); a thread resolved by a Sindri
    session or the PR babysitter doesn't count;
  - the evidence plan satisfied, and the review-round cap not exceeded.
- Undrafting a task PR in an epic is an explicit allowlist entry (§8.1), not a general undraft permission.
- Ring-1 profiles default to `epicMerge: off`. It turns on only where the workplace policy allows agent merges into
  a shared branch (§17.19 ring-1 table).
- The epic → default-branch PR is always the human's. It carries the packet's walkthrough and the evidence
  roll-up.
- Protected modules (§7.7) always wait for the human, even inside an epic.

## 17.11 Ship contract and evidence providers

**Ship contract** (`skills/_shared/ship-contract.md`, read by every worker and by Sindri's ship Step):
- `maxReviewRounds` defaults to 3 (repo override in the profile). When it's exhausted, the item parks with the
  open findings.
- The evidence plan must be satisfied: every acceptance example has a passing artifact from a provider.
- Publish rules (PR body sections, attachments, tracker comment) are shared, not per skill.

**Evidence provider contract** (`sindri/evidence/`, also callable from skills through a CLI):

```ts
interface EvidenceProvider {
  kind: string;
  applies(task: SessionSpec, changed: ChangedFiles): boolean;
  plan(task: SessionSpec): Promise<Result<EvidencePlanItem[]>>;
  run(item: EvidencePlanItem, at: "base" | "head"): Promise<Result<EvidenceArtifact>>;
  verify(base: EvidenceArtifact | null, head: EvidenceArtifact): Promise<Result<Verdict>>;  // script first, judge only to explain
  publish(a: EvidenceArtifact): Promise<Result<PublishedRef>>;  // ask-first rules stay per provider
}
```

**Initial kinds** (an open list; a new kind is a registered artifact with its own eval suite):

| Kind | Typical change |
|---|---|
| `ui-browser` (ui-evidence, Playwright) | Web UI flows |
| `behavior-sim` (seeded, human-paced sessions checked against invariants) | Interaction-heavy UI |
| `ios-sim` (XcodeBuildMCP) | iOS |
| `test` (unit, integration, contract) | Logic and APIs |
| `api-transcript` (recorded request and response against a mock or preview) | Endpoints, webhooks, integrations |
| `cli-transcript` | CLIs, scripts, installers, infrastructure |
| `migration` (dry-run, rollback, schema diff) | Database changes |
| `perf` (benchmark, bundle size, type-check time) | Hot paths, build budgets |
| `a11y` (axe scan) | UI |
| `design-parity` (pixel diff against a mockup or design file) | UI with a design source |
| `security` (`/cso` diff mode, dependency audit) | Auth, input handling, dependencies |
| `trace` (log or telemetry query on a preview) | Jobs, async flows |
| `docs` (render and link check) | Docs |
| `recording` (screen or simulator video of the flow) | Any UI, as proof |
| `narrative` (explainer video from a Scene Plan JSON, rendered by a pinned renderer) | Optional and non-gating |

Selection = the packet's evidence plan ∪ providers whose `applies` matches the changed file classes. The
`narrative` kind takes JSON only: agents never write renderer code that runs on the host (§8.2).

## 17.12 Review from anywhere

**Network:** the hosts sit on a Tailscale tailnet. Nothing listens on a public interface. The dashboard is served
with `tailscale serve` (tailnet only), never `funnel`.

**Surfaces, matched to the decision:**

| Surface | Decisions | Where |
|---|---|---|
| **Swipe inbox** (mobile view of the Plan 6 dashboard) | Binary, low-context, **non-protected** only: undraft a PR, accept evidence, approve or reject a **prompt-text** rule or idea card, keep or drop a scope surface, resolve `shape-unresolved`. Never tier raises, capability changes, evolve adoptions or publishes, or packet freezes | Phone |
| **Walkthrough pages** (HTML from the packet, the scope map, or a PR with its evidence) | Read and comment: designs, scope maps, PR walkthroughs, incident timelines | Phone or desk |
| **Desk queue** | Open questions, design revisions, amendments, packet freezes, protected-surface approvals | Desk |
| **Live session** (Warp remote control, or `sindri attach` over Tailscale SSH) | Watching or taking over a running session | Either |

**Authentication:**
- Host CLI verbs (`approve`, `answer`, `decide`, `handback`) stay the authority.
- A phone decision needs the tailnet identity **and** a passkey (WebAuthn) bound to an enrolled device. Each
  decision is a signed ledger record (kind, item, choice, device, time). It goes through the same code path as
  the CLI verb.
- Phone decisions are limited to the non-protected kinds listed above. Protected surfaces (§7.4 approval tier)
  and packet freezes stay at the desk.
- Free-text feedback from the phone is an `answer` record from an authenticated human.
- Typing into a session through Warp remote control is in-pane input: a **takeover** (§8.2 W6), never an
  authenticated decision.

This replaces Plan 6's "no actions from the browser" for the listed kinds only. Plan 6's rule that the browser
can't bypass a CLI gate still holds: the phone path is the same gate with a second authenticator.

## 17.13 Swappable factory host

- The factory host can be the always-on Linux box or the laptop. Neither is the default. The profile lists both,
  and `sindri host handover <to>` moves the active role in either direction.
- **Handover:** running Steps checkpoint at their next safe point (WIP bundle, §8.2). The ledger, mirrors and
  index are synced to the target with a fenced epoch bump (§9.1), and the target resumes. A host that wakes up
  with a stale epoch stays passive (invariant 1).
- Linux built-ins from §12 move from "later" to a factory prerequisite: the systemd scheduler, the Docker sandbox,
  and a notifier the phone can reach (ntfy on the tailnet, or the dashboard's push).
- The desk reads the active host's dashboard over the tailnet, wherever it is.

## 17.14 Factory-mode skill maintenance

A **skill change** (including a lens or workflow template) is a work-item kind. Sources: a one-line human
request (desk or phone), evolve proposals, hook false-positive telemetry, and Steps that park on the same skill
twice.
- **Worker:** skill TDD. Write pressure scenarios that fail on the current text, edit the skill and show they
  pass. Then run the skill's eval suite (replayed sessions where one exists) and the skill lint: preamble,
  frontmatter, size budget, and no interactive asks on sindri-mode paths.
- **Modularity first:** shared contracts (ship contract, evidence providers, autonomy brief, rule selection, lens
  schema) live in `skills/_shared/` and are referenced, not copied.
- **Ship:** a PR to this repo with a human merge (§7.7). The overlay version applies until the merge (§7.4).

## 17.15 Subscription capacity and handoff

- The model proxy (§8.2) keeps a **quota ledger per account** (subscription or API key) and per provider.
- **Routing for a new Step:** first the lens's allowed providers (profile), then the account with the most window
  left that still keeps `quota.reserveForHuman`.
- **On a rate limit,** a running Step checkpoints. It relaunches from pack plus checkpoint on the next allowed
  account, or waits for its window. Today it can only pause.
- **Accounts are tagged `work` or `personal`.** A ring-1 item never runs on a personal account, and the reverse
  also holds.
- Unattended subscription use needs confirmed plan terms (§13 step 4). API-key mode is the fallback.

## 17.16 Rollout changes

**Prerequisite: agent readiness.** Before F0, this repo reaches agent-readiness Level 4 under
`2026-10-09-agent-readiness-design.md`, including the refactoring that design defines. Level 5 is reached
together with the F-steps, because its criteria (self-improving systems, requirements decomposed into
parallel execution) are what the factory delivers.

These steps come after Plan 6 and replace the order of §13 steps 3a–6 for factory work. Every step ends with a
"Turn it on" task (§13.3). Ring 1 (workplace repos) gets each step after ring 0 holds. Ring 2 starts after F3
holds on ring 1.

| Step | Contents | First value |
|---|---|---|
| F0 | Metrics revision (§17.2); packet schema with `validate`, `freeze`, `init --from-scope` and `amend`; rule, lens and workflow schemas and loaders | Packets, rules and lenses are checkable |
| F0.5 | **Thin slice:** one micro-packet → one task → one PR through `/specToProvenPR` in sindri mode, with 2 lenses (implementer, QA) and 2 evidence kinds (`test`, `cli-transcript`) | The whole pipeline works end to end once, before it grows |
| F1 | Lean session bootstrap (§17.8) with first-call token measurement; ship contract; evidence providers `test`, `ui-browser`, `api-transcript` and `cli-transcript`; sindri mode in `/specToProvenPR` and `/bugFixOrchestrator` | Sessions start small; any task kind can be proven |
| F2 | Task compiler and compiled sandbox policy (§17.5); roles and automatic handoff; stacked epics (main spec step 3a, expanded) | A frozen packet builds task by task in scope, with no pasted prompts |
| F3 | Workflow generation and the starting lens catalog: 4 lenses (implementer, QA, architecture, security), 4 evidence kinds, 3 standing workflows (PR babysitter, triager, daily digest). Scouting grows the rest (§17.18) | Any deliverable gets the right lenses, decided once at freeze time |
| F4 | Review anywhere (§17.12) and standing workflows at the `report` tier (§17.9) | Approvals from the phone; digests, triage and inspection running |
| F5 | Context graph with first adapters, the query planner, and backtest arms per dimension (§17.3) | Measured scoping and pack precision per repo |
| F6 | Skill-change items, rule growth into self-adopt, standing workflows promoted by tier | The factory maintains its own skills, taste and lenses |
| F7 | Swappable host handover on a shared lease, and subscription capacity routing | Builds move between hosts safely, and keep running when a window runs out |

F5 can run in parallel with F1–F4, because it doesn't depend on them.

## 17.18 Capability scouting: Sindri finds what to add next

Self-evolution (§7.4) improves what exists. Scouting finds what **doesn't exist yet** and brings it to the human as
ideas to approve. Examples: generating explainer videos from code and evidence, adding narration with a voice
clone, a new evidence kind, a better memory store, a new lens, a standing workflow nobody asked for yet.

**Sources** (read-only; scouting never installs or runs anything it finds):

| Source | Finds |
|---|---|
| **Internal gaps** | Packet misses, parks by reason, repeated scope-expansion requests, lenses with no evidence kind, evidence kinds with low precision, slow decisions, standing-workflow misses, steering turns the audit still sees |
| **Upstream changes** | New releases of pinned dependencies and vendored packs (§16), provider CLI changelogs (new hooks, flags or SDK features), new MCP servers |
| **The software-factory field** | What other agent factories and agentic dev platforms ship and publish: their tenets and frameworks (for example agent-readiness levels and criteria), orchestration and handoff models, review surfaces, evals, case studies and benchmarks. For example Factory (Droids, agent readiness), Warp (agents and remote control), coding-agent vendors' changelogs and engineering blogs. Each card says which of Sindri's principles or metrics the advance bears on, and whether Sindri already does it, does it worse, or lacks it |
| **Ecosystem** | Skill and subagent catalogs, agent-tooling repos, papers and posts on agent harnesses, memory, retrieval and evals |
| **The human's notes** | The notes dir and the desk decision log ("would be nice if…", links saved for later) |

**Scout job:** a weekly bounded job, plus one on demand (`sindri scout [--topic <text>]`). It runs read-only with
network limited to an allowlist of sources, and it writes **idea cards**. Fetched text is untrusted data
(invariant 7): it's summarized and cited, and it never becomes instructions.

**Idea card:**

| Field | Contents |
|---|---|
| What | One sentence, plus where it would plug in (lens, evidence kind, standing workflow, context-graph component, surface, skill) |
| Why | The internal gap or metric it targets (§17.2), with citations |
| Evidence | Links, licence, maturity, maintenance signals, prior art in this repo |
| Cost and risk | Effort size, recurring cost, new egress or data flows, trust tier (§8.3), and whether it touches a protected surface |
| Eval plan | How Sindri would prove it helps: a backtest arm, an A/B, an evidence-precision check, or decision latency |
| Dedupe | Links to earlier cards on the same idea and how they were decided |

**Human decision (swipe inbox, §17.12):** `build` (it becomes a work item that the factory scopes, builds and
proves like any other, with protected surfaces still approval-tier), `later` (resurfaces when its cited gap
grows), or `no` (recorded with an optional reason, so the same idea isn't proposed again unless its evidence
changes materially).

**Ranking:** expected effect on the §17.2 metrics and on the gaps cited, divided by cost. Ideas tied to a
measured gap rank above ideas that are only novel. The inbox shows at most `scout.maxCardsPerWeek` (default 5),
and digests list the rest.

**Guardrails:**
- Scouting proposes. It never adopts. Every `build` passes through the normal Steps, evals and merge rules.
- New external services (for example a voice-cloning API) are new egress and new data flows. Their card must
  say what data leaves the machine. Building one is approval-tier, and the integration runs only on artifacts
  that are safe to send: never workplace code or ring-1 data unless the workplace profile allows that provider.
- Likeness: a voice clone may only be of a person who gave consent, by default only the human's own voice. The
  consent record is a profile entry.
- The scout's own precision is tracked: the share of cards approved, and the share of built ideas that kept
  their eval gain. It's tuned like any other artifact. If fewer than one card in ten is approved over two months,
  the weekly cadence drops to monthly.

**Starting idea backlog** (seed cards, to be re-ranked by the scout once it runs):
- explainer videos generated from code, the packet and evidence (Scene Plan JSON rendered by a pinned renderer;
  the `narrative` evidence kind, §17.11);
- narration for those videos with a consented voice clone of the human;
- a native phone app, if the PWA's decision latency stalls (§17.17 Q4);
- marketplace memory or consolidation stores as context-graph arms (§17.3);
- importing community lens and subagent definitions as trust-tiered packs (§17.7).

## 17.19 Review resolutions (revision 3)

These resolve the /autoplan findings C1–C17. Where a rule here conflicts with an earlier section, this section wins.

### Capabilities, protection and the core tenet (C1, C2, tension 2)
- **Every registered artifact is split in two:** *prompt text* (instructions, questions, rubrics, rule wording) and
  *capability fields* (tool, command, MCP, network, write-scope and budget allowlists; trust tiers; workflow
  templates; security rules).
- **Prompt text** may self-adopt under §7.4. **Capability fields** are always approval-tier, a packet can't
  override them downward, and no model output may set them (invariant 13). Sindri still *proposes* capability
  changes (invariant 16); the human approves them at the desk.
- **Ceiling:** the compiled session policy = the profile's approved allowlist ∩ the lens allowlist ∩ the task spec.
  A lens can only narrow, never widen.
- **Imported lens and subagent packs** enter as prompt text at the lowest trust tier. Their capability fields are
  dropped on import and must be re-declared through approval. Upstream upgrades of imported packs never self-adopt.

### What the sandbox really enforces (C3, C4, C5)
| Control | Pattern (cooperative agent; hooks) | Boundary (holds against a hostile agent) |
|---|---|---|
| Writes | `PreToolUse` path guard; `PostToolUse` tree check | The sindri-side verifier refuses any bundle with out-of-scope paths (§8.5) |
| Commands | Gate on Bash command strings (easily evaded) | Credentials and network: a command that needs a secret or an endpoint the session lacks can't succeed |
| Network | — | The egress proxy allowlist per task (§8.2) |
| Reads | Gate on read tools | Mounts: only the repo, the pack and allowed refs are present |
| Budgets | — | The model proxy and the tick |
| Holdouts | — | Stored in sindri-private state, mounted **only** into evaluator Steps, never committed; a canary example in each set detects leaks |
- The per-session settings overlay lives in the read-only `/sindri` mount. The gate, the Stop gate and the safety
  hooks are in root-owned managed settings that the overlay can't disable.
- Rule checks that run repo-controlled config (for example a repo's eslint or analyzer config) run in the
  **phase-2** verifier, never in the phase-1 verifier, which runs no repo code.

### Hosts and the lease (C6, C17, tension 3)
- One **shared lease** decides the active factory host. It lives on the always-on box (or another shared store),
  and its epoch is checked before every outward action: push, merge, tracker write, notification. A host that
  can't reach the lease is passive and can't act outward. A laptop working offline runs in a degraded local mode
  that can't push or merge.
- Hosts are tagged `work` or `personal`, like accounts. Ring-1 data and sessions never run on a `personal` host.
- `sindri host handover <to> [--dry-run]` signs the transferred ledger snapshot, verifies it on arrival, and keeps
  the previous host's snapshot for `handover --back`.
- The dashboard is reached at one stable tailnet name that follows the lease, so phone passkeys stay valid across
  handovers.

### Context graph boundaries (C7)
- The context graph package defines ports (`Scrubber`, `Sanitizer`, `CodeIndex`, `Store`) that Sindri injects; it
  has no Sindri imports. The existing `Source` adapters are wrapped by a `Source → Connector` shim, not duplicated.
- One store per profile, written only by Sindri. Consolidation ("dreaming") output keeps the **lowest trust** of its
  inputs, so untrusted chat or telemetry text never becomes a trusted fact.
- The graph's evals (backtest arms) stay in Sindri, so the graph can't grade itself.

### Phone decisions (C8, C11, tension 1)
- **Enrollment** happens at the desk (`sindri device enroll`) and creates a passkey for one device and the stable
  tailnet name. `sindri device revoke` takes effect immediately.
- **Each decision signs** `(item id, item version, nonce, choice)`. A decision for a changed item, or a reused
  nonce, is refused with a code, and the item shows its new state.
- **Walkthrough pages** are served from a separate origin with no cookies or decision endpoints, so a generated page
  can't trigger or change a decision.
- **Plan 6 amendment:** the dashboard binds to loopback and is exposed only through `tailscale serve`. The Host and
  Origin checks accept the stable tailnet name, and the login token is replaced by the passkey for tailnet clients.
- **States per surface** (spec'd in Plan 6's amendment): pending, deciding, decided here, decided elsewhere (CLI or
  another device), stale (item changed), offline (queued locally, sent on reconnect, refused if stale), passkey
  failed, revoked device.
- **Not only swipe:** every swipe has visible buttons (WCAG 2.5.1). Swipe right = approve, left = reject, up =
  "queue for desk", and each choice can be undone for 10 seconds. Recordings carry captions or a transcript.
- **Queues:** the swipe inbox holds non-protected binary items; the desk queue holds open questions, amendments,
  freezes and every approval-tier item; the dashboard's "Needs you" lists both with their queue.

### Ring 1 (C10, tension 4)
| Feature | Ring 1 default | Turns on when |
|---|---|---|
| Assist mode, packets, task compiler, evidence, lenses | On | — |
| `epicMerge: auto` | Off | The workplace allows agent merges into a shared branch |
| Phone decisions | Off | The workplace allows decisions from a personal device on a personal tailnet |
| Personal Linux box as host | Never | Ring 1 runs only on `work` hosts |
| Standing workflows above `report` | Off | The workplace allows agents to comment or open PRs on their own |
| Personal accounts | Never | — |

"F3 holds on ring 1" is judged only over the rows that are on there.

### Developer experience (C12, C14, C16)
- `sindri packet init --from-scope <map>`, `packet amend`, and `packet validate --json` (one record per failed
  check with its rule id and fix).
- `sindri session spec <step>` prints the compiled session spec. `sindri why-denied <id>` shows the denial record:
  rule, attempted action, policy source, and how to request expansion.
- Skills detect sindri mode through `SINDRI_MODE=1` and a state-path shim that keeps today's state files
  (`reviews/<pr>.json`, bugfix state) readable from the same paths.
- A skill lint spec: frontmatter, preamble, size budget, no interactive asks on sindri-mode paths, and pressure
  scenarios as `scenarios/*.md` with an expected-behavior block.
- **Micro-packets:** an S-or-smaller tracker item gets an auto-generated packet (one task, write scope from triage,
  default evidence for its kind), frozen without a desk step. It still goes through validation.
- Rule, lens and workflow files carry `schemaVersion` and have generated JSON Schemas. Lens `commands` name repo
  command aliases from `repos/<repo>.yaml`, not raw strings. `sindri rules explain <id>` shows why a rule was
  selected. A glossary in `docs/sindri/glossary.md` fixes "lens", "factory", "profile", "ring" and "packet".

### Human effort (C15)
- A weekly **human-minutes** metric (desk sessions, phone decisions, takeovers) with a ceiling in the profile.
  Crossing it two weeks in a row pauses new auto-started work and raises the top time sinks in the digest.
- A notification budget **per surface**: phone pushes per day, digest items, desk-queue additions. Standing
  workflows and scouting draw from it.

## 17.17 Decisions

**Decided by the human (2026-10-09):**
- **Q1:** stacked PRs into epic branches, with `epicMerge: auto` into the epic only (§17.10).
- **Q3:** the factory host is swappable between the laptop and the Linux box; neither is the default (§17.13).
- **Q5:** ring 1 (workplace repos) comes first, to test and improve the factory. Ring 2 starts after F3 holds on
  ring 1. Ring-1 runs follow the workplace's agent policy: human-gated, `assist` mode, and work accounts only.

**Q2: component options for the context graph.** These are candidates to evaluate as backtest arms, not
decisions. None was re-verified for this revision.

| Component | Candidates |
|---|---|
| Connectors | MCP servers (read-only client); unified-API connector platforms; per-source native adapters (as Linear today); the workplace knowledge engine's connectors (ConnectAI) |
| Memory and consolidation ("dreaming") | Prism (personal only); memory-layer libraries with consolidation passes (for example Mem0, Zep/Graphiti, Letta, Cognee) |
| Retrieval and storage | SQLite FTS5 + sqlite-vec (local, matches the ledger); LanceDB; Postgres + pgvector (for a shared box) |
| Org knowledge (conversations, decisions) | ConnectAI (workplace source of record today); commercial engines with MCP read surfaces (for example Unblocked, Glean, Falconer); notes vault |
| Codebase | The existing index + graphify; Serena (LSP); hosted code engines as an arm only if the profile allows code to leave the machine |
| Product graph | Sindri-owned (no external candidate joins product nodes to code) |
| User behavior | Product analytics APIs (for example PostHog), browser session-replay data, OTel browser spans |
| Telemetry and operations | OTel backends, error trackers, deploy logs, through `TelemetrySource` adapters |
| Design | Design-file APIs, `design-tokens.json`, design-system component inventories |

The recommendation is to build the context graph, query planner and evals first over the sources already
reachable (code index, graphify, Linear, notes, ConnectAI through MCP). Then add one component per arm and keep
it only when the backtest says it helps.

**Q4: the review surface.** Pros and cons:

| Option | Pros | Cons |
|---|---|---|
| **PWA on the tailnet** (the Plan 6 dashboard, made mobile) | One codebase with the dashboard. No app store. Tailnet-only by construction. WebAuthn passkeys work in mobile Safari and Chrome. Ships fastest | Web push on iOS needs the PWA installed to the home screen, and is less reliable than native. No background refresh. Swipe gestures need care to feel native |
| **Native iOS app** | Best swipe feel, reliable push, Face ID, offline queue of decisions | A second codebase, App Store or TestFlight distribution, and the tailnet still has to be on. Slowest to build |
| **Warp remote control only** | Exists today. Full fidelity on live sessions | A terminal on a phone: no structured decisions, and in-pane input is a takeover, not an approval (§8.2). Fine for watching, poor for reviewing |
| **Chat bot** (a DM channel) | Push and replies come free, and it's familiar | Decisions leave the tailnet through a third party. Weak authentication for signed decisions. Rich walkthroughs don't fit |
| **GitHub mobile** | Already installed. Good for reading PR diffs and comments | It sees only GitHub objects: no packets, rules, lenses or evidence roll-ups, and no Sindri decisions |

The recommendation is the PWA first, with Warp remote control for live sessions. Build native only if push
reliability or swipe feel measurably slows decisions (§17.2 decision latency).
