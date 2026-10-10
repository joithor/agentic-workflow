# Sindri — factory addendum (§17)

Status: draft for review, revision 5 · 2026-10-10 (applies /autoplan round 1 C1–C17 in §17.19 and round 2 D1–D12 in §17.22)
Revision 5 adds the factory field review (§17.23): review back-pressure, self-proving tests, read-only existing tests,
consequence tiers, diff guardrails, closed outcomes with a Not proven section, the merge-confidence card, gate
removal criteria, escaped-defect tracing, comprehension drift, a PR lease, signed run receipts, no-progress
detection, the implementer's plan review, feedback-loop classes and factory/human cohorts; invariants 17–19.
It also adds a second field pass (§17.24: failure ownership, migration oracles, the critic lens, context assertions,
executor escalation, `spec-conflict`, DORA keys and postmortems, sealed artifacts, one live variant per artifact) and
field harvest (§17.25), which makes finding and building other factories' practices part of Sindri grows Sindri.
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
capabilities worth adding** (§17.18), **harvests what other software factories already do and builds the practices
it lacks** (§17.25), and builds the approved ones through its own factory (§7.6). The human
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
17. **Sessions don't edit the tests that judge them** (§17.23.3).
18. **Production stops when review is full** (§17.23.1).
19. **Every verdict says what it didn't prove** (§17.23.6).

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

Auto-start's kill criterion keeps the rework comparison and drops the −20% total-turns condition. §17.23.17 adds
rows for back-pressure, test proof, triage accuracy, escaped defects, no-progress parks, the merge card and cohorts.

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
| `write` | The task's write scope, plus **new** test files by the repo's test-path convention. Existing tests only when the packet names them (§17.23.3) |
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
| **The software-factory field** | What other agent factories and agentic dev platforms ship and publish: their tenets and frameworks (for example agent-readiness levels and criteria), orchestration and handoff models, review surfaces, evals, case studies and benchmarks. For example Factory (Droids, agent readiness), Warp (agents and remote control), coding-agent vendors' changelogs and engineering blogs. Each card says which of Sindri's principles or metrics the advance bears on, and whether Sindri already does it, does it worse, or lacks it. Factory repos and topics run through the dedicated field-harvest loop (§17.25) |
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
- `sindri host handover <to> [--dry-run]` signs the transferred ledger snapshot and verifies it on arrival.
  `--back` is a new handover in the other direction, never a restore of an old snapshot (D2).
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
- **Walkthrough pages** are served from a **separate tailnet hostname** with no cookies and no decision endpoints, so
  they share no passkey RP ID or cookies with the dashboard (D3). Comments link to the dashboard origin.
- **Plan 6 amendment:** the dashboard binds to loopback and is exposed only through `tailscale serve`. The Host and
  Origin checks accept the stable tailnet name, and the login token is replaced by the passkey for tailnet clients.
- **States per surface** (spec'd in Plan 6's amendment): pending, deciding, decided here, decided elsewhere (CLI or
  another device), stale (item changed), offline (queued locally, sent on reconnect, refused if stale), passkey
  failed, revoked device.
- **Not only swipe:** every swipe has visible buttons (WCAG 2.5.1). Swipe right = approve, left = reject, up =
  "queue for desk". Undo works because every outward command a decision causes waits **10 seconds in the outbox**
  before dispatch; undo cancels the queued command, never an action already sent (D3). Recordings carry captions or a transcript.
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
- **Micro-packets (D1):** an S-or-smaller tracker item that passes the trust gate (§8.3) gets a generated packet.
  Its write scope is **computed by code**: paths linked in the item, plus code-index neighbours of symbols the item
  names. If no scope can be computed, there is no micro-packet and the item goes to the desk. A micro-packet is frozen
  by a **one-tap human decision** (phone allowed in ring 0, desk only in ring 1). It still goes through validation.
- Rule, lens and workflow files carry `schemaVersion` and have generated JSON Schemas. Lens `commands` name repo
  command aliases from `repos/<repo>.yaml`, not raw strings. `sindri rules explain <id>` shows why a rule was
  selected. A glossary in `docs/sindri/glossary.md` fixes "lens", "factory", "profile", "ring" and "packet".

### Human effort (C15)
- A weekly **human-minutes** metric (desk sessions, phone decisions, takeovers) with a ceiling in the profile.
  Crossing it two weeks in a row pauses new auto-started work and raises the top time sinks in the digest.
- A notification budget **per surface**: phone pushes per day, digest items, desk-queue additions. Standing
  workflows and scouting draw from it.

## 17.20 Parallelism and distribution

**Today:** one active host (invariant 1), a session cap, and one heavy job at a time per host (invariant 5). Inside
a packet, tasks with non-overlapping write scopes can run in parallel (§17.4, §17.7). Nothing yet schedules
**several packets and standing workflows at once** fairly, and nothing runs Sindri across machines. The target is a
ladder, and each level reuses the one below:

| Level | Shape | What it adds |
|---|---|---|
| **P1 · many workstreams, one host** | One Sindri, many packets, epics and standing workflows | A **scheduler** with a WIP limit per packet and per ring, priorities, and fair sharing. **Conflict-aware dispatch:** tasks whose write scopes or `deps.yaml` neighbours overlap are serialized, and others run together. **Resource classes** replace the single heavy-job lock: CPU, memory, browser, container builds, index builds and model quota each have their own slots. A capacity model sets the session cap from the smallest of quota windows, host resources and the human-minutes budget (§17.19) |
| **P2 · one coordinator, many workers** | One coordinator holds the shared lease (§17.19), the ledger, and every decision and outward action. Worker hosts only run containers | **Worker hosts** (laptop, Linux box, cloud boxes) register over the tailnet, advertise resource classes, and pull Steps under leases with heartbeats. They hold no tokens beyond their session proxies, and their spool ships back to the coordinator. A dead worker's Step returns to the queue from its last checkpoint |
| **P3 · federation** | Several independent Sindri instances (for example one per ring) | Instances never share state or credentials. They share **prompt text only** (lens wording, rules, rubrics) and idea cards, at the lowest trust tier. Evidence providers (code) and workflow templates or any capability field never cross; each instance re-implements or re-approves them (D5). Direction defaults to **home → work** |
| **P4 · team mode** | One Sindri serving a team at work | A shared database store instead of per-host SQLite, a user per human with roles, per-user and per-team budgets, tenant-keyed ledger rows, audit per user, and the second-user rules of main spec §15. Every decision still has a named human approver |

**Limits that don't go away with more machines:**
- **Model quota** is usually the first ceiling. The capacity router (§17.15) spreads Steps across allowed accounts, but
  never across the work/personal line.
- **Human review bandwidth** is the real ceiling. The human-minutes budget caps how much work Sindri starts, however
  many workers exist.
- **Merge contention:** parallel tasks on one epic rebase onto each other. Conflict-aware dispatch and small write
  scopes keep it low, and a task that conflicts twice is serialized.

**Seams built now, so the ladder needs no rewrite** (in the C# port, readiness §4b):
- The ledger behind a `LedgerStore` port, with SQLite (today) and a server database (P4) as implementations,
  sharing one contract-test suite.
- Every ledger event is idempotent, keyed by `(kind, item, attempt, sha)`, so retries and a second worker can't
  apply anything twice.
- `Lease`, `Launcher` and `Scheduler` are ports. `Launcher` gains a remote implementation in P2.
- Every row carries a tenant and ring key from day one, set to a single value until P4.

**Rollout:** P1 joins F2 (the task compiler needs it to run a packet's tasks in parallel). P2 follows F7 (it reuses
the shared lease). P3 starts when ring 1 begins, as the safe way to share improvements between home and work. P4
starts only when a team adopts it, and needs the workplace's approval.

## 17.21 Multiplayer foundations

§17.20 scales Sindri across machines. Serving several **people** (team mode, P4) needs more. Most of it is
missing today, because the design so far assumes one human, one SQLite writer, file logs and polling. The table
lists what exists and what is needed, in the order it's built.

| Piece | Today | Needed for multiplayer |
|---|---|---|
| **Event log and bus** | The ledger is the record. Hooks append to spool files. The dashboard and the bridge poll | Every state change is an **event** with one envelope: `id`, `type`, `version`, `tenant`, `ring`, `actor`, `item`, `causation`, `correlation`, `trace`, `time`. Events go into an append-only log in the ledger's transaction (**transactional outbox**). Consumers (dashboard, phone, notifier, workers, digests, other instances) read with durable cursors. Transport is a `Bus` port: in-process → database polling or `LISTEN/NOTIFY` → a broker (for example NATS) at P2+. The bridge becomes one consumer, not a parallel channel |
| **Exactly-once outward actions** | Writes run directly from the ship Step, with idempotency keys on some adapters | Every push, merge, comment, label and notification is an outbox **command** with an idempotency key, run by one dispatcher under the lease. Retries and a second worker can't repeat it |
| **Identity** | One OS user; host CLI verbs are the authority | People, devices and **service identities** (coordinator, each worker, each agent session, each standing workflow). Every record and event carries an `actor`. Agents never act as a person |
| **Authorization** | Implicit: the host user can do everything | Roles per tenant: *owner* (protected surfaces, capability approvals), *approver* (decisions in a ring), *contributor* (desk sessions, packets), *viewer*. Every decision kind declares the role it needs. Two-person approval is available per kind (for example capability changes at work) |
| **Tenancy** | A tenant and ring key on rows (§17.20 seam), single-valued | Isolation per tenant: data, secrets, budgets and quota, rule and lens overlays, notification routing and retention. A query without a tenant fails. No cross-tenant reads, except published packs (P3). Per-tenant fair scheduling, so one team can't starve another |
| **Concurrency between humans** | One human, so no conflicts | Optimistic concurrency on every item (the item version from §17.19). Claims and assignment ("who owns this decision"), visible presence, and a defined rule for two people deciding at once: the first signed decision wins and the other sees `decided elsewhere` |
| **Collaboration surfaces** | Decisions only | Comments on items, packets and walkthroughs; @-mentions; handing an item to another person; a shared desk queue with filters per person; notification preferences per user |
| **Observability** | JSONL tick and hook logs; scorer reports; OTel planned for bridge and sindri (readiness §5) | **One trace per work item:** tick → job → session → tool calls → outward command, with W3C trace context passed into containers and model-proxy calls. Metrics: queue depth, lease age, Step latency by lens, decision latency by person, quota by account, cost by item, person and team, fail-open counts. Logs are structured and correlated by trace id, scrubbed by the logging processor. Every metric and log carries the tenant |
| **SLOs and alerting** | `doctor` and the silent-failure floor (§10.4) | SLOs for the factory: time from freeze to first PR, decision latency, stall rate, and the coordinator's availability. Alerts route to the on-call person of the tenant, with the main spec's notification budget per person |
| **Audit** | Ledger rows and the profile's audit log (§8.7) | An append-only audit stream per tenant with a **hash chain** (each entry covers the previous one), exportable. It records every decision, approval, capability change, override and outward action, with actor and device |
| **Secrets** | Keychain or 0600 files, per host | A secrets port with per-tenant vault backends. Short-lived credentials are issued to workers per Step, and rotation doesn't need a restart |
| **Config and policy as code** | Profile files with `profile approve` | Versioned profiles per tenant, changed by pull request with the required approvers, and a history of who changed which policy and when |
| **Data lifecycle** | Retention in §8.4 | Retention, export and deletion per tenant. Backups and restore drills for the coordinator store |
| **Operations** | One process on one host | Rolling upgrades with version skew rules: workers are at most one minor version behind the coordinator, and events are versioned and upcast. Zero-downtime migrations for the shared store. A health endpoint per component |
| **API** | CLI verbs; the dashboard's local HTTP | One versioned API (HTTP + event stream) for the CLI, dashboard, phone and other instances, plus webhooks. **Approval-tier and protected decisions stay terminal-bound** (a typed confirmation on an interactive TTY, or an enrolled-device passkey); an API token alone can never make them (D6) |

**Order of work:**
- **During the C# port (cheap seams):** the event envelope and transactional outbox, outward actions as outbox
  commands, `actor`/`tenant`/`ring`/trace fields on every record, the `Bus` and secrets ports, and OpenTelemetry with
  trace propagation. They make P1 simpler as well.
- **With P2:** the bus transport between hosts, service identities for workers, and trace propagation across
  machines.
- **With P4:** people and roles, tenant isolation, collaboration surfaces, hash-chained audit, the shared store,
  SLOs, and per-tenant secrets and policy.

**Not a goal:** Sindri doesn't become a hosted SaaS. Team mode runs on infrastructure the team controls, under
its own policy.

## 17.22 Round-2 resolutions (D1–D12)

Where this conflicts with earlier text, this section wins. §17.5 and §17.13 are read through it.

**The lease, from F0.5 (D2).**
- Store: one row in a small SQLite file on the always-on host, served to other hosts over the tailnet by the
  coordinator process (later the P4 shared store). Fields: holder host id, fencing token (monotonic), expiry.
  TTL 60 s, renewed every 20 s.
- **Required from F0.5**, when the first push happens; a single host holds it trivially. Today's per-host ledger
  epoch (`ledger/db.ts`) and the profile's `hosts.active` (`observe.ts`) are replaced by the lease's fencing token.
- Every outward action (outbox dispatch) and every **desk command** (`approve`, `answer`, `decide`, `freeze`) checks
  the token. A host without the lease forwards the command to the holder, or refuses with `SND-LEASE-0xx`. It never
  writes to its local ledger.

**Phone authority (D3).**
- Nonces are **issued by the server** per item version. An offline decision can only use a nonce received while
  online for that version, and is refused if the item changed.
- Undo is the 10-second outbox delay above. The dashboard origin carries comments; walkthroughs link to it.

**Workers (D4).**
- Workers are enrolled at the desk (`sindri worker enroll`), each with its own key and a class (`work` or
  `personal`, resources). Steps are matched to worker class, and ring-1 Steps only go to `work` workers.
- The model proxy and the real tokens stay on the coordinator. Workers get per-Step proxy credentials.
- **Verification never runs on the worker that produced the work.** Phase 1 and phase 2 run on the coordinator or a
  different worker.

**Outbox correctness (D9).**
- Before retrying a command, the dispatcher checks whether its effect already exists (the PR, comment, label or
  merge). Idempotency keys include the event's sequence number. Requeued Steps carry fenced attempt tokens.
- Rings are a dimension inside a tenant. Per-ring WIP limits (P1) apply within each tenant.

**Multiplayer security (D10).**
- The gate re-checks every outbox command at dispatch time.
- Tenancy is enforced in the store with database-level filters, and every cache key includes the tenant.
- The audit chain's head is anchored outside its store once a day (a signed commit to a private audit repo).
- Policy may require a second approver when an owner approves their own request.
- Instances authenticate each other with mutual TLS or signed requests.
- ntfy publishing is authenticated. Tailnet identity headers are trusted only from the local `tailscale serve`
  socket. Adapter pins are approval-tier. The graph store follows §8.4 retention.

**Desk and status surfaces (D11, D12).**
- A standing-workflow panel shows each workflow's tier, last run, precision and a **pause** control.
- A "blocked on you" alert fires when the factory has no runnable work without a human, within the notification
  budget.
- Phone accessibility: every swipe action has a screen-reader action, and focus moves to the next item after a
  decision.
- Generated walkthroughs follow a semantics checklist (landmarks, headings, alt text, captions).
- `sindri queue why <step>` explains why a Step is waiting. The error catalogue gains the `SANDBOX`, `LEASE`,
  `WORKER`, `OUTBOX` and `PHONE` areas.
- Docs deliverables per F-step are listed in the plan that builds it. Evidence publish asks first unless the repo
  profile says otherwise.

**Sizes, exit bars and ring-1 rows (D8).**

| Step | Size | Exit bar |
|---|---|---|
| F0 | S | Schemas, `validate`, `freeze`, `init --from-scope`, `amend` with golden tests |
| F0.5 | S | One micro-packet → one merged PR with evidence. **It runs on one of the C# port packets** (readiness R2), so the first factory-built PR lands during the port |
| F1 | M | 10 packets built with lean bootstrap; first-call tokens within budget |
| F2 | L | 3 packets with parallel tasks merged into epics, rework ≤ the step-0 baseline |
| F3 | M | Workflow generation used on 5 packets; zero `scope-mismatch` parks from missing lenses |
| F4 | M | 2 weeks of phone decisions; median decision latency below the desk's |
| F5 | L | One context-graph arm adopted per repo by backtest |
| F6 | M | 3 self-adopted prompt changes held for 20 items |
| F7 | M | One handover each way and one rate-limit failover in production |

"F3 holds" means 5 packets merged through workflow generation with rework at or below the step-0 baseline. Human
minutes are compared with the step-0 baseline in the weekly digest.

Ring-1 rows added to §17.19's table: containers on the work host (on, if the work host can run them), auto-start
(off until ring-1 policy allows), P3 federation (on, home → work only), workplace connectors (only those the
workplace approves).

## 17.23 Factory field review (revision 5)

A read of other software factories (2026-10-10) found sixteen practices this design didn't have or only had in
part. This section adds them. Where it conflicts with earlier text, this section wins. §17.5, §17.10, §17.11 and
§17.12 are read through it.

**Sources.** Only ideas are taken; no code is copied, so licences don't constrain this section. Each source is
also a seed for the scout's software-factory field (§17.18).

| Source | Licence | Ideas taken |
|---|---|---|
| addyosmani/factory | MIT | Review back-pressure, a test that proves itself, read-only existing tests, consequence tiers, the tune loop, comprehension drift, "never loosen" |
| superplanehq/superplane (factory PRD and design) | Apache-2.0 | The merge-confidence card, one PR writer per head SHA, factory and human cohorts |
| asklokesh/loki-mode, chainloop-dev/chainloop | not stated; Apache-2.0 | Closed outcomes with a NOT PROVEN list, signed run receipts |
| robi42/factory | MIT | Diff guardrails, the implementer reviewing the plan it will execute |
| inkatze/planwright | MIT | No-progress detection |
| actual-software/software-factory-intensive | none stated | Removal criteria on human gates, the feedback-loop taxonomy, measurable acceptance criteria |
| mmaripanguec/dev-sdd-kit | MIT | Untouchable tests, derived artifacts that say "no data" before they guess |

**New invariants** (added to §17.1):

17. **Sessions don't edit the tests that judge them.** Existing tests are read-only to a session unless the frozen
    packet names the change. A test change always gets a human read (§17.23.3).
18. **Production stops when review is full.** No new Step whose result needs a human decision starts while the
    pending-decision queue is at its limit (§17.23.1).
19. **Every verdict says what it didn't prove.** An item's outcome lists every planned check that didn't run or
    couldn't decide. Silence is never read as a pass (§17.23.6).

### 17.23.1 Review back-pressure

The human-minutes ceiling (§17.19 C15) is a lagging, weekly brake. This adds an instant one, read on every tick.

- **Pending decisions** are counted per ring: PRs waiting for a human read or merge, desk-queue questions and
  amendments, needs-approval items, and open swipe cards other than `fyi`.
- `review.maxPending` (profile, approval tier) defaults to 3 per ring.
- At or above the limit, the router starts no new Step whose result needs a human decision. That covers auto-start,
  a packet's next task, and standing workflows at the `propose` tier. Each such item waits with the reason
  `held: review-backpressure`, which `status` and the dashboard show.
- Running Steps finish. Fix rounds on PRs already in the queue still run, because they shrink it. `report`-tier
  standing workflows still run.
- A queue that stays full for 24 hours raises one `blocks-factory` digest entry, not a notification per item.
- Metrics (§17.2): queue depth over time, and hours items were held.

### 17.23.2 Every new test proves itself

The fail-on-base, pass-on-head check (§6) covers bug reproduce checks only. This extends it to every task whose
evidence plan includes `test`.

- In the verifier container (§8.5), a script splits the bundle's diff into test paths (by the repo's test-path
  convention) and non-test paths. It reverse-applies the non-test hunks and runs the task's new tests. Each test
  that backs an acceptance example must fail. Then it restores the tree. No model is involved.
- When test and implementation share a file (inline tests, for example) and can't be split, the verdict is
  `could-not-determine`. It goes into the Not proven section (§17.23.6) and is never counted as a pass.
- **New evidence kind `mutation`:** mutation testing scoped to the changed lines, through the repo's command alias
  (for example Stryker or mutmut). The share of surviving mutants on changed lines must be at or below the
  profile's bar. It's required at the `production` consequence tier (§17.23.4) and on protected paths. Elsewhere it
  runs on the inspector's nightly cadence (§17.9), not per task, to keep verification inside budget.

### 17.23.3 Existing tests are read-only to sessions

This amends §17.5's `write` field ("plus its tests by the repo's test-path convention").

- The compiled write scope includes **new** test files by convention. It includes an **existing** test file only
  when the packet task names it with the reason `test-change`, decided at the desk.
- Snapshot and golden files count as tests. Deleting a test, skipping it, or loosening an assertion counts as
  changing it.
- The `PreToolUse` gate denies the edit (`SND-SANDBOX-110`), and the sindri-side verifier refuses a bundle that
  changes an existing test outside scope.
- A PR with any existing-test change carries the `test-change` label and waits for a human read. `epicMerge: auto`
  doesn't apply to it (§17.10).
- Metric: existing-test changes per merged PR, and the share of them that a desk decision approved.

### 17.23.4 Consequence tiers

Auto-start was gated on size and ambiguity only (§7.1). Autonomy should follow who gets hurt when the work is
wrong, not how hard it is.

- The profile gives every repo a `consequence` tier. `consequence.paths` (for example auth, payments, migrations, CI
  and agent config) raise the tier for any task that touches them.
- A task's tier is the highest tier among its repo and the paths in its write scope. Ring-1 repos default to
  `production`. Changing a tier is approval tier.

| Tier | Meaning | Auto-start | `epicMerge: auto` | Evidence depth | Human read |
|---|---|---|---|---|---|
| `sandbox` | Unlaunched; nobody depends on it | ≤ S | allowed | fast | samples |
| `personal` | The human's own tools; nobody else depends on them yet | ≤ XS | allowed | full | samples, plus every protected path |
| `shared` | Other people depend on it | off by default | off | full | every PR |
| `production` | Someone's business depends on it | off | off | deep (including `mutation`) | every diff |

Evidence depth sets which evidence kinds are required: `fast` is types and lint, `full` adds `test`, and `deep` adds
`security`, `mutation` and the architecture checks. Protected modules (§7.7) wait for the human at every tier.

### 17.23.5 Diff guardrails

These deterministic checks are added to the shape checks (§6.2). They run at commit and in the verifier, on added
lines only, and a hit is refused like any other shape check.

- **New suppressions:** `eslint-disable`, `@ts-ignore`, `@ts-expect-error` with no reason, `# type: ignore`,
  `#[allow(…)]`, `//nolint`, `#pragma warning disable`, `[SuppressMessage]`.
- **New skipped or focused tests:** `.skip`, `.only`, `xit`, `@Ignore`, `[Fact(Skip=…)]`, `pytest.mark.skip`.
- **Swallowed errors:** an empty `catch`, or `except: pass`.
- **Build output and generated files** outside the repo's allowed paths, and lockfile changes in a task that may
  not change dependencies.
- **A source change with no test change,** unless the task kind is `docs` or `config`, or the packet says
  `test: none` with a reason.

An exception needs an inline `sindri-allow: <check-id> <reason>` marker. The verifier records each one and lists it
under Not proven (§17.23.6). The pattern list is a rule pack (§17.6), so evolve can grow it per language.

### 17.23.6 Closed outcomes and the Not proven section

Every item ends with exactly one outcome:

| Outcome | Means | What happens |
|---|---|---|
| `verified` | Every required evidence item passed | The PR proceeds under the ship contract |
| `already-satisfied` | The reproduce or acceptance check passes on the base | The item closes with that evidence; no PR |
| `blocked` | It needs an answer | It parks as needs-answer (§10.2) |
| `unverified` | It was built, but a required evidence item couldn't run or couldn't decide | The PR stays draft and the gap is named |
| `failed` | Gates are red after the round cap, or no progress (§17.23.13) | It parks with the findings |

**Not proven** is a PR-body section that a script generates from the evidence plan (ship contract, §17.11). It lists:
- every planned item without a passing artifact;
- every `could-not-determine` (§17.23.2);
- every `sindri-allow` exception (§17.23.5);
- every merge-card dimension marked "not assessed" (§17.23.7).

When nothing was skipped, the section says so in one line instead of being left out. Swipe cards show its count.

### 17.23.7 Merge-confidence card

Every task PR and epic PR gets a card with five dimensions, each scored 1 to 5:

| Dimension | Better | Source |
|---|---|---|
| Blast radius | lower | Deterministic: fan-in of changed symbols (code index), product nodes touched, the consequence tier of the paths |
| Reversibility | higher | Deterministic first: a migration with no down step, data deletion, a published API or package contract, or an external side effect lowers it. A judge explains |
| Drift from the packet | lower | Judge: the diff against the task's acceptance examples and the packet's out-of-scope list |
| Security | higher | The security lens's verdict when it ran; otherwise "not assessed" |
| Performance | higher | The performance lens and `perf` evidence when they ran; otherwise "not assessed" |

- Dimension names, directions and the caution and critical thresholds live in Sindri code as a registered artifact.
  Changing them is approval tier. For judged dimensions, a model supplies only a score and a one-line summary
  (invariant 13).
- A critical dimension turns the card red and moves the decision from the phone to the desk queue.
- Each card is stored with the PR's later outcome (merged clean, reworked, reverted, escaped defect), so evolve
  calibrates the thresholds the same way as direction checks.

### 17.23.8 Every human gate has removal criteria

Every human gate and approval-tier rule gets a **gate record** (a registered artifact):

| Field | Contents |
|---|---|
| Why | The incident or risk it exists for |
| Worst case if skipped | What would ship, and what recovery would cost |
| Removal criteria | Measurable, for example "20 consecutive items of class X with no escaped defect and no human fix" |
| Evidence so far | Kept up to date from the ledger |

- Evolve may propose loosening a gate only once its removal criteria hold, and the proposal cites the run of
  evidence. Loosening is always approval tier, never self-adopt.
- Tightening is immediate when an escaped defect traces to that gate (§17.23.9).
- **Never loosened** (structural; no removal criteria):
  - the human merges to the default branch;
  - existing tests are read-only to sessions (invariant 17);
  - the producer never verifies its own work (invariant 3, §17.22 D4);
  - protected modules need approval;
  - models never grant capabilities (invariant 13).

### 17.23.9 Escaped defects, triage accuracy and the tune report

- **Escaped defect:** within 30 days of merge, a fix, revert or incident on lines a Sindri item merged, linked by
  blame and PR refs. Each one starts a bounded trace job that lists the gates, evidence kinds, lenses and checks that
  ran on the original item, and names the one that should have caught it. The output is either a tighten proposal
  for that gate or, when no gate covered the case, a new deterministic check proposal.
- **Repeated catches become checks:** when a verifier or judge rejects the same class of problem 5 times in 30 days,
  Sindri proposes a deterministic check. This extends the enforcement ladder in §17.6 from rules to verifier
  rejections.
- **Triage accuracy** has two parts, both judged after the fact:
  - the share of items routed to auto-start or a micro-packet that later needed the desk (an amendment, a scope
    expansion, or a product question after freeze);
  - the share of items sent to the desk that a micro-packet could have handled.
- **Monthly tune report** (in the digest):
  - merged items;
  - human-minutes;
  - rework;
  - escaped defects and which gates they traced to;
  - triage accuracy;
  - queue depth;
  - proposed tightenings and loosenings.

  It ends with one question, answered with those numbers: *is the factory producing work worth the review attention
  it consumes?*

### 17.23.10 Comprehension drift

The inspector (§17.9) gains one signal: files that Sindri items changed 5 or more times in 30 days with no update to
any doc, ADR, rule or decision-log entry linked to them in the context graph (§17.3). These are the places where the
code has moved and the written understanding hasn't. Each finding becomes a `propose` item for the documentation
maintainer. Like every standing workflow, the inspector reports what it checked and found clean (§10.4).

### 17.23.11 One writer per PR, bound to a head SHA

Claims (§9.4) are per item. A PR also needs a writer lock, because the PR babysitter, a fix-round Step, a rebase and
the human can all push to it.

- Every actor that changes a PR takes an exclusive **PR lease** in the ledger, keyed by the PR and bound to the
  head SHA it started from. It is fenced by the host lease's token (§17.22 D2).
- Waiting for CI or review bots holds no lease.
- A push is made with `--force-with-lease=<bound sha>`. If the head moved, the push is refused, the activity's
  output is discarded, and the work is re-planned on the new head.
- A human push to a Sindri PR is a takeover of that PR (§9.3) until `handback`.
- A check handler starts at most one fix Step per failed head SHA.

### 17.23.12 Signed run receipts

When an item ends, Sindri writes a **receipt** containing:
- the item id, packet hash and workflow hash;
- the base and head SHAs and the bundle hash;
- each evidence artifact's hash and verdict;
- each judge decision id and score;
- the outcome and the Not proven list;
- token and compute totals;
- the hash chain of the item's ledger events.

The receipt holds hashes and ids only, never code or ring-1 text, so it is safe to share.

- It's signed with the host's Ed25519 key, which lives in the host's secret store. The public key is in the profile.
- `sindri verify <item | receipt>` re-hashes the receipt and checks the signature and the event chain. It prints one
  of `verified`, `tampered`, `unsigned` or `unchecked`.
- The receipt hash goes in the PR body. `epicMerge: auto` requires a verified receipt for the exact head SHA. The
  epic → default-branch PR carries the roll-up of its tasks' receipts.
- The daily audit-chain anchor (§17.22 D10) includes the day's receipt hashes.

### 17.23.13 No-progress detection

A fix loop (review rounds, CI fix rounds, verifier rejections) parks early, with the reason `no-progress` and the
repeated findings, when round *n* makes no progress over round *n−1*. That means any of:
- its open findings equal round *n−1*'s or are a superset of them, keyed by file, check and normalized message;
- the round produced an empty diff;
- the same check failed twice with the same failure signature.

It doesn't spend its remaining rounds. Metric: `no-progress` parks per lens.

### 17.23.14 The implementer reviews the plan; acceptance examples are measurable

- **Executability review.** In workflow generation (§17.7), between the model pass and the direction check, a
  review-only implementer-lens Step reads each task: its write scope, read seeds, acceptance examples and evidence
  plan. For each task it answers four questions:
  - Is it executable as written?
  - Is any context missing?
  - Is the write scope enough?
  - Can every acceptance example be checked by a command or an artifact?

  Objections revise the workflow within a round cap, or go to the desk. The evaluator Step later receives the
  packet's design rationale, so the build is reviewed against intent as well as the diff.
- **Measurable acceptance.** `sindri packet validate` (§17.4) rejects an acceptance example that uses an
  unmeasurable term ("fast", "clean", "nice", "intuitive", "simple", "good", "obvious") without a number or a
  reference artifact. Example: "returns in under 200 ms on the 10k fixture" passes; "fast" fails. The term list is
  a rule pack.

### 17.23.15 Feedback loops, including external ones

Every feedback loop is a record (a registered artifact) with: signal, trigger, target, action, verification and
history. There are three classes:

| Class | Fires on | Produces |
|---|---|---|
| Aggregate | A repeated pattern: the §7.4 steering sources, repeated verifier catches (§17.23.9) | A rule, prompt or check proposal |
| Reactive | One incident: an escaped defect, a revert, an incident | An immediate tighten proposal (§17.23.8) |
| External | A signal from outside the factory: a support ticket, a customer report, an app-store review, an error-tracker spike, through a `Connector` (§17.3) | A tracker item labelled `external`, with a triage SLA from the profile. Never a direct prompt edit |

- When the root cause of an external item turns out to be an agent artifact, a reactive loop is opened and linked
  to it.
- The digest lists external items past their SLA.
- External text is untrusted data (invariant 7). It is scrubbed, and values typed by users aren't kept beyond the
  item (§17.3).

### 17.23.16 Factory and human cohorts

The dashboard (Plan 6) gains a **Velocity** view: the last 14 days by default, filtered by repo.

- It shows the same indicators for three cohorts: all work, human-authored PRs, and factory-authored PRs (PRs
  opened for a Sindri item).
- Indicators:
  - merged PRs;
  - cycle time (item start to merge);
  - rework and revert rate within 30 days;
  - escaped defects;
  - tracked cost (model tokens and compute).
- Human time is shown as human-minutes, never as a dollar figure. The view has no per-person breakdown, and it
  presents the cohorts side by side, not as a contest.
- Per workflow and per standing workflow, it also shows the success rate (items closed `verified` and merged ÷ items
  closed) and the cost per merged item.

### 17.23.17 Metrics added to §17.2

| Measure | Target | Why |
|---|---|---|
| Pending-decision queue depth, and hours held by back-pressure | Depth ≤ `review.maxPending`; hours held tracked | Invariant 18 |
| Share of acceptance-example tests proven to fail without the change | ≥ 0.9 of tasks with `test` evidence | §17.23.2 |
| Existing-test changes per merged PR | Tracked; each one desk-approved | Invariant 17 |
| Triage accuracy (both directions) | Rises month over month | §17.23.9 |
| Escaped defects per merged PR, and the share traced to a gate | Falling; traced share ≥ 0.8 | §17.23.9 |
| `no-progress` parks per lens | Tracked | §17.23.13 |
| Merge-card calibration (red cards later reworked vs green cards later reworked) | Red rework rate clearly above green | §17.23.7 |
| Factory vs human cycle time and rework, same size class | Factory rework ≤ human rework | §17.23.16; matches the auto-start kill criterion |

### 17.23.18 Rollout

| Step | Adds |
|---|---|
| F0 | Consequence tiers in the profile schema; gate records; the outcome enum and the Not proven section in the ship contract schema; the measurable-acceptance check in `packet validate` |
| F0.5 | The thin slice runs with read-only existing tests, the prove-the-test script and the diff guardrails from its first PR |
| F1 | Not proven in PR bodies; no-progress detection; the `mutation` evidence kind |
| F2 | Review back-pressure; the PR lease; signed receipts, before `epicMerge: auto` is allowed anywhere |
| F3 | The implementer executability review; the merge-confidence card |
| F4 | The card on the swipe inbox; comprehension drift in the inspector; the Velocity view |
| F6 | Escaped-defect tracing, triage accuracy and the tune report; evolve proposals against gate records; feedback-loop records, with external loops once F5's connectors exist |

## 17.24 Factory field review, second pass

A second, deeper read of the same sources (2026-10-10) found nine more practices. Where this section conflicts
with earlier text, it wins. §17.23.6 and §17.23.9 are read through it.

### 17.24.1 Failure ownership (loki-mode)

Every failed check, Step and item records an **owner**:

| Owner | Examples |
|---|---|
| `code` | A test fails on the head, or a lint, type or shape check fails on code the session wrote |
| `harness` | A Sindri bug, a hook crash (`SND-HOOK-9xx`), a bad handoff |
| `environment` | A CI runner or preview environment fails, a dependency registry is down, a known flake |
| `provider` | A model error, a rate limit, a quota limit, a timeout |

- Ownership comes from code, not a model. Exit sources, error codes, the flake register and a rerun on a clean
  runner decide it. Ownership that can't be decided is recorded as `unknown`, and it never counts as `code`.
- **Only `code`-owned failures count** toward:
  - rework and revert rates;
  - escalation (§17.24.5);
  - the kill criteria (§2);
  - consequence-tier and gate-record evidence (§17.23.4, §17.23.8);
  - evolve's outcome labels (invariant 9).
- Every other failure goes into Not proven (§17.23.6) and into `doctor`'s health report. Repeats of the same
  non-code cause become a harness or environment work item, not a prompt proposal.
- Metric: failures by owner per week. A rising `harness` share is a Sindri defect.

### 17.24.2 Name the oracle before a migration (addyosmani)

A task with change class `migration`, `port`, `upgrade` or `large-refactor` must name its **oracle**: what proves
behaviour didn't change.

- `sindri packet validate` refuses such a task when it has no oracle.
- When existing coverage is thin, workflow generation (§17.7) puts the oracle-building tasks first:
  1. build on the current stack;
  2. typecheck;
  3. pin current behaviour with characterization or golden tests.

  Only then come the migration tasks, checked against those tests.
- The pinning tests are frozen with the packet. Under invariant 17, the migration tasks can't edit them.
- Sindri's own C# port (readiness §4c) applies the rule narrowly. It is a clean cutover with no TS behaviour parity,
  so its oracle covers only the frozen hook-facing CLI and file surface, checked by running the real hook scripts
  against the C# binaries.

### 17.24.3 Critic lens (addyosmani)

A review-only lens that argues **against** the change. Every other Step leans toward shipping, so this one doesn't.

| Looks for | Example |
|---|---|
| Assumptions that spread unstated | A default chosen silently that three callers now depend on |
| Abstraction with one caller | An interface, factory or config option used once |
| Behaviour change behind a green suite | A case the tests never covered now behaves differently |
| Dead code and orphans | Exports, branches or config keys left behind by an earlier approach |
| Six-month comprehensibility | Would someone who wasn't in the session understand why it's shaped this way? |
| Unconsidered blast radius | Other readers of this data, callers of this function |

- It's required at the `production` consequence tier and on protected paths. Elsewhere it's optional.
- It runs on a different model from the implementer. Its findings go into the review round like any reviewer's.
- A critic finding that repeats becomes a shape-check or rule proposal (§17.23.9).

### 17.24.4 Executable assertions on context (dev-sdd-kit)

Context-graph facts and pack sections may carry a cheap **assertion** that is checked before injection. Examples:
- a symbol exists;
- a path or route exists;
- a grep matches;
- a read-only query returns rows;
- a config key has a value.

Rules:
- Assertions run in the task compiler (§17.5) with read-only tools and a time budget.
- A failing assertion drops the section, or marks it `stale` when the injection plan requires it, and records a
  freshness miss against the component that produced it (§17.3).
- A section or fact without an assertion falls back to commit-SHA freshness.
- Metric: freshness misses per component. It feeds that component's backtest arm.

### 17.24.5 Executor escalation on evidence (loki-mode)

The decision ladder governs decisions. This extends it to the model that runs a build Step.

- Each lens names a starting executor tier (§17.7 `model.tier`). A Step moves up a tier only after a
  **`code`-owned** failure (§17.24.1): first a retry at the same tier, then the next tier up. Escalation never
  moves down within an item.
- Sindri remembers outcomes per **repo shape** (workspace kind plus test runners, for example
  `multi-root:dotnet+vitest`). A shape with 2 `code`-owned losses in its last 3 runs at a tier starts at the next tier.
- Non-code failures cause no transition. Each escalation is recorded on the receipt (§17.23.12).
- Evolve may propose changing a lens's starting tier from the shape history. That's a budget field, so it's
  approval tier.

### 17.24.6 `spec-conflict` outcome (loki-mode)

This adds a sixth outcome to §17.23.6:

| Outcome | Means | What happens |
|---|---|---|
| `spec-conflict` | The task contradicts the packet, the code at the base SHA, or another frozen task | Parks to the desk as a packet miss (§17.2), citing both sides. Not counted as a failure |

### 17.24.7 Delivery metrics and blameless postmortems (dev-sdd-kit)

- The Velocity view (§17.23.16) adds DORA's four keys per repo and cohort: deployment frequency, lead time for
  changes, change failure rate and time to restore. They're computed from deploys, merges and incidents through
  `TelemetrySource` (§17.3). A repo with no deploy signal shows "no data" rather than a guess.
- The incident resolver's standing workflow (§17.9) ends with a **blameless postmortem** draft from a template:
  timeline, contributing factors, what detected it, what would have caught it earlier, and actions. The human
  edits and publishes it. Its "what would have caught it" line opens a reactive feedback loop (§17.23.15).

### 17.24.8 Sealed derived artifacts (dev-sdd-kit)

Anything Sindri generates for people to read carries a **seal**: the generator id and version plus hashes of its
sources. That covers walkthroughs, metric reports, the digest, generated docs, the Velocity numbers and scope maps.

- `sindri seal check` (and `doctor`) fails when a sealed artifact's sources changed or it was hand-edited. The fix
  is to regenerate it, the same pattern as `sync-rules.sh --check`.
- A generator writes "no data" for any value it can't derive. It never interpolates or estimates silently.
- Hand-written docs aren't sealed. Comprehension drift (§17.23.10) covers them.

### 17.24.9 One live variant per artifact (Actual AI workshop)

Evolve runs at most **one** variant per artifact at a time: in offline comparison, shadow, or canary. A second
proposal for the same artifact waits until the first is adopted, reverted or rejected. Changes to different
artifacts that share an eval suite are also serialized. This keeps a change in outcome attributable to one cause.

### 17.24.10 Rollout

| Step | Adds |
|---|---|
| F0 | Failure ownership in the ledger schema; the oracle field and its check in `packet validate`; the `spec-conflict` outcome |
| F1 | Ownership on every check and park; sealed digests and reports |
| F3 | The critic lens; executor escalation and the repo-shape history |
| F4 | DORA keys in the Velocity view; postmortem drafts in the incident resolver |
| F5 | Context assertions in the task compiler and the context graph |
| F6 | One live variant per artifact in evolve's scheduler |

## 17.25 Field harvest: Sindri finds what other factories do and builds it in

Sections 17.23 and 17.24 came from a person and an agent reading other software factories by hand. Under the core
tenet, this becomes **Sindri's own job**: it watches the field, finds practices it lacks, proves which ones are
worth having, and builds them through its own factory. §17.18's scout finds ideas. Field harvest is the
specialised, continuous loop for one source class: other software factories, agent harnesses and coding-agent
platforms.

```
 field registry ──► harvest job ──► practice cards ──► gap check vs Sindri ──► rank ──► human: build/later/no
 (repos, topics,    (read-only,      (mechanism, cited    (have / partial /                     │
  blogs, changelogs) sandboxed)       file + line, licence) missing, cited §)                   ▼
                                                                    spec section + guide + work items
                                                                    built and proven by the factory
```

**Field registry** (a registered artifact; the human adds or removes entries from the desk or phone):
- named repos, starting with every source in §17.23 and §17.24;
- GitHub topic queries (for example `topic:software-factory`, `topic:agent-harness`, `topic:coding-agent`), sorted
  by stars and recent activity, with a minimum-signal filter;
- coding-agent vendor changelogs and engineering blogs (§17.18's field row);
- for each entry, the paths that usually hold the design: README, `docs/`, specs, PRDs, design docs, agent
  definitions, skills, hooks, gate scripts and the CI workflows.

**Harvest job** (weekly, plus `sindri harvest [--repo <url>] [--topic <q>]`):
- A bounded, heavy job under the scout's guardrails (§17.18): network limited to the registry's hosts.
- It shallow-clones each source into a scratch directory **outside any worktree**. It never runs, builds or
  installs anything it fetched, and all fetched text is untrusted data (invariant 7).
- It reads only what changed since the last harvest (a commit-SHA cursor per source), so the weekly cost tracks the
  field's activity, not its size.
- It stores no code from a source. It keeps summaries with citations: repo, commit SHA, path and line range.

**Practice card** (extends the idea card, §17.18):

| Field | Contents |
|---|---|
| Practice | One sentence, plus the mechanism: what runs, when, and what it decides |
| Citation | Source repo, commit SHA, file and line range for every claim |
| Licence | The source's licence. MIT, Apache-2.0 or BSD allow porting code with attribution after an ownership check. Anything else, or none, allows ideas only |
| Gap verdict | `have`, `partial` or `missing`. Each verdict cites the Sindri spec sections and code it was checked against, and quotes the line that covers it or says what is absent |
| Bears on | The §17.2 metrics and invariants it affects |
| Fit | Where it plugs in: lens, evidence kind, check, outcome, metric, surface, rule pack, standing workflow, context-graph component |
| Cost and risk | Size, recurring cost, new egress, protected surfaces touched |
| Eval plan | How Sindri would prove it helps (§17.18) |

**Gap check, not trust.** The gap verdict is made by a model **with read access only to Sindri's spec, guide and
code index**. A second model, which doesn't see the first one's verdict, must agree before a `missing` or `partial`
card is shown to the human (invariant 3). A `have` verdict that a later item contradicts is an eval miss for the
gap checker. This is how the "already covered" list in §17.23 was made, now done by Sindri.

**Human decision.** Cards appear in the swipe inbox under `scout.maxCardsPerWeek`, ranked by their effect on cited
gaps ÷ cost. Cards that only add novelty rank below cards tied to a measured gap. Decisions are `build`, `later` or
`no`, as in §17.18.

**What `build` does.** It opens one work item that the factory runs like any other:
1. drafts the spec section (an addendum entry in this document's format, with sources, mechanism, rollout row and
   metric) for the human to approve at the desk;
2. updates the HTML guide (`docs/sindri.html`) and its catalog rows;
3. scopes, builds and proves the mechanism through the normal Steps, evals and merge rules (§17.10). Protected
   surfaces stay approval tier.

The spec edit and the build are separate PRs, so the design can land before the code.

**Guardrails:**
- Harvest proposes and never adopts (§17.18). It can't edit the registry, its own gap-check prompts or eval suite
  (invariant 11).
- A card citing code it can't port (licence) describes the mechanism in Sindri's own words. The build never copies
  that source.
- Sources from a workplace (ring 1) are never added to the public registry. A ring-1 profile may have its own
  private registry.
- If fewer than one harvested card in five reaches `build` over two months, harvest drops from weekly to monthly.

**Metrics** (added to §17.2):
- practices harvested;
- the share judged `missing` that the human agreed with;
- cards built;
- the share of built practices still holding their eval gain after 20 items;
- median time from a practice appearing upstream to it being live in Sindri.

**Seed run.** §17.23 and §17.24 are the first harvest, done by hand on 2026-10-10. Their source list is the
registry's starting content, and their "already covered" findings are the gap checker's first labelled examples.
F6 adds the harvest job; until then, `sindri scout --topic software-factory` (§17.18) runs it on demand.

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
