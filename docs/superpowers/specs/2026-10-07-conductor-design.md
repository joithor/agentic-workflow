# Conductor — design

Status: draft for review, revision 3 · 2026-10-07
Revision 2 addressed `/autoplan` round 1 (21 HIGH). Revision 3 addresses round 2 (7 HIGH, R1–R7, and M1–M15,
L1–L2 in `plans/conductor/consolidated-review.md`). It also adds hook-enforced checks (§5.3) and code-shape
checks with a code index (§6.2).

## 1. Why

A transcript audit of one month of work (406 sessions, 2,911 human turns; method in the appendix) found the
human's effort goes mostly to the least judgment-heavy parts of agent work:

| Where human turns go | Share |
|---|---|
| Shipping direction (draft PR → review loop → ready → watch reviewers → fix CI/conflicts → "push") | ~20% |
| Product decisions answering agent questions | ~20% |
| Keeping orchestrators on track (handoff prompts, "read this digest", "we crashed, pick up", role corrections) | ~11% |
| Research asks | ~10% |
| Defects the human found by hand | ~5.5% |
| Dispatch (pasting tickets). One turn per ticket, so turn counts undercount its real cost | ~5.5% |
| Mid-flight scope discovery | ~4% |
| QA direction | ~4% |
| Evidence and environment recipes (near-verbatim repeats) | ~3% |

The conductor is a local, always-on system that picks up work, sizes it, starts sessions with a cited
context pack, keeps them on track, ships them through a fixed recipe, and asks the human only when it has to.
It learns from its own steering so recurring corrections become rules.

## 2. Goals, metrics, kill criteria

### Goals
- Remove copy-paste dispatch (manual start in one command, then auto-start for small clear work).
- Remove retyped shipping direction (the ship recipe is data, executed by the conductor).
- Remove orchestration babysitting (cited pack injected at start and re-injected on compaction).
- Every judgment step proves it is complete before handing off.
- Evolve: recurring human steering becomes a proposed, backtested, typed rule change.
- Keep the codebase simple as it grows: deterministic checks for reinvention, second-case
  generalization and size/complexity, judged only on a shortlist (§6.2).
- Generic core; workplace and repo specifics live in a separate private profile repo.

### Non-goals
- Merging. The human always merges.
- Answering product questions. The conductor batches and contextualizes them; it never answers them.
- A web dashboard (CLI, notifications, ledger only).
- Multi-host scheduling in v1.

### Success metrics (baselines and final targets set in rollout step 0; figures below are starting proposals)
| Metric | Source | Proposed target after 4 weeks of auto-start |
|---|---|---|
| Human turns per merged PR | transcripts + GitHub | −40% vs baseline |
| Shipping-direction turns per merged PR | transcript classifier (the audit's patterns) | −75% |
| Continuity turns (handoffs, "pick up", role corrections) per week | transcript classifier | −50% |
| Copy-paste dispatches of auto-start-eligible items | ledger vs transcripts | 0 |
| Interrupting notifications per merged PR | ledger | ≤ 2 |
| Auto-started PRs reworked or reverted | GitHub + ledger | ≤ the rate for human-started PRs |
| Model cost per merged PR | ledger | within `budget.perItem` |
| Human "wrong path" corrections per merged PR | transcript classifier | −50% (direction checks should catch these first) |
| Clone count and duplication ratio on main | code index | flat or falling |

### Kill criteria
- **Auto-start:** if after 4 weeks human turns per merged PR is down less than 20%, or the rework rate of
  auto-started PRs exceeds that of human-started PRs **of the same size class**, auto-start is turned off (`mode: assist`). The
  conductor keeps running manual start, packs and the ship recipe.
- **Eval loop:** if fewer than half of approved rules hold their backtested gain over the next 20 items,
  proposals stop.
- **Direction checks:** per checkpoint, see §6.1 (back to shadow when `revise` doesn't reduce rework).
- **Index providers:** graph or embeddings are turned off for a repo when the eval-loop arm comparison
  shows no gain.

## 3. Invariants

1. **One conductor at a time** per host, fenced (§9.1). Across hosts, v1 runs one active host and treats
   cross-host exclusion as best-effort with duplicate-tolerant outcomes (§9.4).
2. **Nothing long-lived holds a model.** The tick is short deterministic code; model work runs in bounded
   jobs and sessions that end.
3. **No step grades itself.** Verifiers are separate; the conductor runs the worker's reproduce check
   itself (§6). **No path goes unchallenged:** approach, scoping, drift, shape and
   scope-expansion checkpoints get a direction check on a different model before more effort is
   committed (§6.1).
4. **Escalate, never guess.** Below-threshold decisions and exhausted loops notify and park.
5. **One heavy job at a time per host.** The session cap is a ceiling on sessions, not heavy jobs.
6. **Deny by default inside conductor sessions.** A conductor gate enforces the profile allowlist,
   fails closed, and is not disableable from inside a session (§8.1).
7. **Untrusted text is data, never instructions** (§8.3).
8. **No secrets in packs, ledger, logs, notifications or eval corpus** (§8.4).
9. **No human hand-labeling.** The eval loop labels from outcomes and adjudicators only.
10. **Hooks enforce; agents don't opt in.** Every required check fires on a hook event or a conductor
    action, never because an agent chose to call a tool. MCP is for reading data only (§5.3).

## 4. v1 scope

| In v1 | Deferred (interface and contract tests exist, no built-in) |
|---|---|
| macOS, launchd, tmux, macOS notifier (with fallback chain §10.4) | systemd/cron schedulers, ntfy, chat-DM notifier |
| Single active host | Shared `Lease` adapter, multi-host |
| Tracker: Linear. Reviewer: GitHub | Other trackers and reviewers |
| Sources: tracker, notes dir, transcripts, memory (Prism) | Chat (Slack) as a pack source |
| Modes: `shadow`, `assist`, `auto-small` | `full` (auto-start scoping workstreams) |
| Worker wraps existing `/bugFixOrchestrator` | New native worker |
| Ship state machine for single PRs and stacks | — |
| Eval loop: proposal-only on evidence and ship recipes | Free-form rule discovery |
| Direction checks, conductor-relayed, bridge as audit mirror; shadow first (§6.1) | Human participating live in a thread; non-Anthropic challengers by default |
| Code index: structure, clones, dependencies, local embeddings, graphify graph; shape checks at commit (§6.2) | Cross-repo index |

Linux is a design constraint in v1 (no OS assumptions in core, per-OS boot-id, hook portability fixes,
Linux container test script) but its built-in adapters ship later (§12).

## 5. Architecture

```
launchd ─► conductor tick  (short, deterministic; singleton lock + fencing epoch §9.1)
             ├─ reconcile: ledger ⇄ Launcher.list() ⇄ job/session liveness (§9.2)
             ├─ Tracker.scan(scope, cursor)        → changed work items
             ├─ spawn bounded jobs (detached, own lease, own budget):
             │     triage · context pack · scoping · verifier · eval
             ├─ router (trust gate + size + ambiguity + mode)
             ├─ Launcher.start(session, pack, overlay)   (≤ cap)
             ├─ ship state machine (per change request / stack)
             └─ Notifier (taxonomy + budget §10)
```

The tick never waits on a model. It reads job results from the ledger on the next tick.

### 5.1 Core packages (in this repo)
| Path | Responsibility |
|---|---|
| `conductor/` | Tick, queue, cap, fencing, job runner, Step runner, ship state machine, ledger, CLI |
| `conductor/adapters/` | Interfaces, built-ins (§11.2), `adapterContractTests()` |
| `conductor/profile/` | Zod schema, JSON Schema export, validator, migrator, example profile, scaffolder |
| `conductor/gate/` | Conductor-mode PreToolUse gate, outward-write filter, path guard (§8) |
| `conductor/scrub/` | Secret/PHI scrubber used at ingest, pack write, ledger write, eval corpus |
| `judge/src/questions/` | New questions (size, ambiguity, pack-probe, independence, step-complete, direction-verdict), called through the `judge` CLI |
| `conductor/index/` | Code index builders, `Index` adapter, graphify adapter, shape signals (§6.2) |
| `mcp-bridge/` | Audit mirror for direction-check threads. Hardened: token auth, no CORS, Host check, ack-based unread, size caps, retention (§6.1) |
| `config/hooks/` | The conductor-mode hook set in §5.3: injection, gate, checkpoints, shape checks, turn classification, Stop gate, heartbeat |

### 5.2 Storage (M7)
- The conductor owns `$AW_STATE_DIR/conductor/ledger.db` (SQLite, WAL). It is the only writer.
- Hooks never write to the DB. They append events to `$AW_STATE_DIR/conductor/spool/<session>.jsonl`, and
  the tick ingests them.
- Judge decisions are referenced by judge's decision id. Score, threshold and reason code are **copied**
  into the ledger row, because judge prunes decision details after 30 days (L1, L5).
- The schema lives in `planning/ERD.md` with versioned migrations. `doctor` detects schema skew (M19).
- Ledger files and the state dir are mode 0700, outside any session's working tree. Conductor sessions run
  with a gate that denies writes under `$AW_STATE_DIR/conductor/` (STRIDE tampering).

### 5.3 Enforcement model: hooks, not tools
**Principle:** every check that must happen is fired by a hook event or by the conductor. No check
depends on an agent choosing to call a tool. MCP servers are for **reading data** (tracker, docs, code
navigation). They are never the trigger for a check, a gate, or a hand-off. An agent can't skip, delay, or
forget a check, because it never decides whether one runs.

| Hook event | What it enforces in a conductor session | Blocking? |
|---|---|---|
| `SessionStart` (startup) | Inject the pack's Task, Acceptance and Evidence plan; register the session; write the spool heartbeat | — |
| `SessionStart` (compact / resume) | Re-inject Task and Acceptance plus the pack pointer (§7.2) | — |
| `UserPromptSubmit` | Classify the turn by origin: `answer` (replies to a pending question), `takeover` (unsolicited human input), `conductor` (the fixed `continue` token). For `continue`, inject any pending verdict or answer from the session inbox as additional context (§6.1 delivery). A human correction raises a drift signal. | — |
| `PreToolUse` (all tools) | Conductor tool gate: default deny, profile allowlist, argument constraints (§8.1) | yes |
| `PreToolUse` (first `Edit`/`Write` to a non-test source file in a work item) | **Approach checkpoint:** if no `proceed` verdict exists for the current approach, deny with `CND-DIR-010 awaiting direction check <id>`, and the conductor opens the check (§6.1). The session ends its turn; the verdict arrives on the next `continue`. | yes |
| `PreToolUse` (`Bash` matching `git commit`) | **Shape checks** on the staged diff against the code index (§6.2). A signal denies the commit with the evidence and opens a shape direction check. | yes |
| `PostToolUse` | Heartbeat spool; drift signals (test-failure streaks, files outside the predicted set, repeated verifier reasons); scrub-on-read for tool output that goes back into context (§8.4) | — |
| `Stop` | Conductor-mode done-gate: the session may stop only if the ledger shows its Step's completion checks passed, or the item is parked or awaiting a human. It replaces the regex done-gate in conductor sessions (M11). | yes |
| `PreCompact` | Checkpoint the Step state to the ledger spool before compaction | — |

**Hook test rule:** every row has a test that drives the hook with a recorded payload and asserts the
decision. A hook that errors in conductor mode denies (fails closed). Outside conductor sessions, the
existing hooks behave exactly as today.

## 6. The Step contract

```
Step = { inputs, output schema, deterministic checks, verifier, max rounds, budget, escalation }

produce → deterministic checks → verifier → pass ── yes ─► ledger + hand off
              ▲                             │ no (reasons)
              └──── revise with reasons ────┘   rounds or budget exhausted → park + notify (needs-answer)
```

- **Generation** runs in a bounded job (a fresh `claude -p` with a tool allowlist) or a session.
- **Decisions** are judge `QuestionModule`s run through the `judge` CLI, on the chain
  `jev → agent CLI → rules`. A below-threshold result escalates.
- **Verifiers** are judge questions first. When the chain escalates, a separate refutation job runs; the
  producer never verifies itself.
- **Budgets:** each Step has `maxRounds` and a cost cap. Each item has `budget.perItem`, and the host has
  `budget.perDay` with a circuit breaker that pauses auto-start (C8).

| Step | Runs as | Deterministic checks | Verifier |
|---|---|---|---|
| Triage | tool-less job | Schema-valid; every in-scope item has `{size, ambiguity, missingInfo[], sources[]}`; size in the profile vocabulary (L2) | Judge re-sizes **100% of auto-start candidates** and a sample of the rest (M14) |
| Context pack | tool-less job (fetching is done by conductor code through Source adapters) | Every claim cites a source ref; required sections present (§8.3); scrubber passes; size within limit | Judge pack-probe: a fresh job answers N probe questions from the pack alone |
| Scoping | job with read-only code tools | Every surface maps to a workstream; dependency graph acyclic; independence checked against predicted file sets | Adversarial "missing surface" job until no new surface or the round cap |
| Worker | tmux session (`/bugFixOrchestrator` in v1) | **The conductor re-runs the recorded reproduce check itself** in a clean worktree at the PR head: it must fail on the base and pass on the head. The check runs under the same sandbox as sessions (scrubbed env, gate policy, command schema, timeout) (M3). Path guard (§8.5) and shape checks (§6.2) pass. | Judge resolution-check against the original item text |
| Ship | conductor code | Reviewer threads resolved and CI green, read from the Reviewer adapter and CI, never from agent output | — |
| Eval proposal | job | Typed rule schema; forbidden fields untouched (§7.4) | Holdout backtest (directional), then a shadow A/B win before approval (§13 step 6) |

### 6.1 Direction checks ("is this the right path?")
Step verifiers ask *is this output complete?* Direction checks ask a different question: *is this the right
thing to be doing at all?* In the audit only the human asked it: "the ticket description suggests the
fix, why are we not doing it", "that's a messy workaround, not the fix we want", "wait, why are you adding
that branch?". A direction check is a short, structured dialogue between models before more effort is
committed.

**Checkpoints:** each one is fired by a hook (§5.3) or by the conductor. None is fired by the agent.

| Checkpoint | Fired by | Question |
|---|---|---|
| Approach | `PreToolUse` on the first non-test source edit | Does this solve the item as written? Does it match the stated fix? Is there a simpler or more correct path? |
| Scoping | Conductor, before a scoping proposal goes to the human | Are these the right workstreams? Is anything here solving the wrong problem? |
| Drift | Conductor, on any drift signal: 2 failed fix rounds, the same verifier reason twice, a diff outside the predicted file set, a human correction, elapsed time more than 2× estimate | Keep going, change approach, or stop and ask? |
| Shape | `PreToolUse` on `git commit` when a shape signal fires (§6.2) | Reuse, generalize, or keep the new code (with a reason)? |
| Scope expansion | Conductor, when a session's diff or notes propose new items or a widened change | Is this necessary for this item, or a separate item? |

XS-clear items skip Approach unless a drift signal fires.

**Transport: the conductor relays every turn (R1, R2, R4).**
- Participants never talk to each other directly. The conductor runs each turn as a bounded job:
  - **proposer turn:** from the session's own checkpoint output, which the hook spools as `position.json`
  - **challenger turn:** a stdin/stdout job, read-only code tools, no network
- The conductor writes every turn itself, with a fixed role (`proposer | challenger | arbiter`), the check
  id, the epoch, and an HMAC keyed by a conductor secret.
- **The MCP bridge is the record of each dialogue,** not its transport. Every turn is mirrored to a bridge
  conversation (UUID = check id) for audit and for `conductor why`.
- **Bridge hardening, required when the bridge is enabled:**
  - token auth on every route
  - CORS off and a Host-header check
  - `sender` taken from the token, not the request body
  - ack-based unread (no destructive reads)
  - payload size caps
  - retention under §8.4
- No session can read or post to a direction-check thread. If the bridge is down, checks still run, the
  ledger keeps the record, the mirror is back-filled when the bridge returns, and `doctor` warns. The
  bridge is never a hard dependency.

**State machine** (durable in the ledger):
```
requested → position → challenge(n) → reply(n) → verdict → delivered
        └──────────── any state → cancelled | escalated
```
- n ≤ `direction.maxExchanges` (default 2).
- While a check is open, the session is in the known-wait state `awaiting-direction`. The reaper never
  treats it as a stall (§9.2).

**Delivery into the session (R2):**
1. The verdict (or the human's decision) is written to the session's inbox file. The file is owned by the
   conductor and is read-only to the session.
2. The conductor sends the fixed `continue` token.
3. The `UserPromptSubmit` hook injects the inbox contents as additional context.
4. For a `revise` verdict, the text is synthesized by conductor code from the verdict's agreed objections,
   as a structured list. Raw thread text never becomes instructions.

**Decision:** a judge question `direction-verdict` (`proceed | revise | escalate`) reads the HMAC-verified
turns only. Below threshold, the result is `escalate`. Escalation creates a `decide` item (§10.1). The
human runs `conductor decide <item>` to see both positions and choose.

**Providers and egress (R3):**
- The profile's `providers.allowed` defaults to Anthropic only.
- Diversity comes from a **different Claude model** than the proposer's (e.g. proposer Sonnet, challenger
  Opus), plus **Jev** as the arbiter's first classifier (already vendor-reviewed for code in judge).
- Other providers (Codex, Cursor CLI, others) are opt-in per provider. They receive a bounded bundle:
  scrubbed, sensitive paths denied, non-agentic mode. Every provider call writes an audit row.
- If no distinct challenger model is available, the check is skipped and recorded as `skipped:no-diverse-model`.
  It never silently uses the proposer's model.

**Proof before trust (R5):**
- Checks start in `direction.mode: shadow`: they run and record verdicts but don't block. The ledger
  compares them against later human "wrong path" corrections and rework.
- They move to `enforce` per checkpoint once shadow shows precision of at least `direction.minPrecision`
  (default 0.6) over at least 20 checks.
- **Caps:** `direction.maxPerItem` (default 3), `direction.maxPerDay` (default 40), plus each check's cost
  counted against `budget.perItem`.
- **Kill criterion:** a checkpoint whose `revise` verdicts don't reduce rework compared to shadow over 30
  items goes back to shadow.

### 6.2 Code-shape checks and the code index
Four questions keep a codebase healthy as it grows:
- Can this be simpler?
- Can this be more elegant?
- Now that there are two cases, should this be generalized?
- Have we reinvented something that already exists?

In a small codebase a reviewer answers them by eye. At scale they must be **deterministically
evaluable**. Deterministic signals produce evidence, and models judge only the shortlist.

**Code index** (per repo, behind an `Index` adapter, rebuilt incrementally on merges to main, stored in
`$AW_STATE_DIR/conductor/index/<repo>.db`):

| Layer | Content | Built-in |
|---|---|---|
| Structure | Symbols (name, signature, kind, file, exported, callers) | tree-sitter; LSP (Serena) when available |
| Clones | Normalized AST hashes per function and block (identifiers and literals abstracted), and token shingles | core |
| Dependencies | Package manifests, plus internal utility modules marked by profile globs | core |
| Embeddings | Function-level vectors from a **local** model (Ollama, through Prism when available). No code leaves the machine. | core, `index.embeddings: local` |
| Graph | Module and call graph, cross-file relationships, surface clusters | **graphify** adapter (`index.graph: graphify`) |

- **Graphify as a provider:** it is one source of candidates. It feeds the Reinvention and Scoping checks
  (surface discovery) and is evaluated arm-vs-arm by the eval loop (graph on vs off). It stays enabled only
  where it measurably improves outcomes. A prior trial found a code graph did not beat plain search for
  agent *navigation*; here it is used for *candidate recall*, and that use has to earn its place too.
- **Embeddings and graph neighbors** only ever produce candidates. A candidate becomes a signal only when a
  deterministic check confirms it, or when the shape direction check judges it.

**Signals** (computed by the `PreToolUse git commit` hook on the staged diff; deterministic, profile
thresholds):

| Question | Signal | Default threshold |
|---|---|---|
| Reinvented? | A new symbol's AST hash matches an existing one; signature and name similarity ≥ t; an embedding neighbor ≥ e **and** an AST similarity ≥ a; a new dependency overlapping an existing one by purpose tag | t 0.85, e 0.9, a 0.6 |
| Generalize at the second case? | A near-clone between new code and existing code (≥ N tokens, Jaccard ≥ s) | N 60, s 0.8 |
| Simpler? | Changed lines over the size budget (XS 80, S 250, M 600); cyclomatic complexity delta above the limit; new files, exports, interfaces, flags or config keys above the size class's allowance | per size class |
| More elegant? | No direct signal. Covered by the three rows above plus the shape direction check | — |

**Flow:**
1. A signal denies the commit.
2. The session gets the evidence ("`formatShiftWindow` in your diff is an 87% AST match for
   `shared/time/formatWindow.ts:12`").
3. The conductor opens a **shape** direction check (§6.1).
4. Outcomes:
   - `revise`: reuse or generalize.
   - `proceed`: the new code is kept, with a recorded reason. The pair `(new symbol, existing symbol)`
     isn't flagged again.
   - `escalate`: the human decides.

**Repo trend:** the eval loop tracks duplication ratio, clone count, average complexity and dependency
count on main over time. A rising trend becomes a rule proposal (e.g. "reuse `formatWindow` for time
ranges"). It never becomes free-text guidance.

**Index freshness:** `doctor` reports index age. A check against a stale index (older than
`index.maxAgeHours`, default 24) records the staleness on its signal, so stale results are never presented
as current.

## 7. Flows

### 7.1 Intake and routing
1. The tick scans the profile scope with an `updatedSince` cursor, pagination and rate-limit backoff (M6).
2. New or changed items get a triage job.
3. The router decides:

| Condition | Route |
|---|---|
| `mode: shadow` | Record only. No session starts. |
| `mode: assist` | The item is listed in `status` as startable. The human runs `conductor start <item>`. |
| `mode: auto-small`, size ≤ `autoStartMaxSize` (default `XS`), ambiguity `none`, all authors trusted (§8.3), budget available | Auto-start a worker session. |
| Otherwise | Scoping queue. In v1 the scoping output is a proposal the human approves before workstreams start. |

4. Unassigned items are assigned to the human only when a session starts (allowlisted as `assign: self,
   unassigned only`, M15).
5. Questions are batched per item and sent as one needs-answer notification (§10).

### 7.2 Sessions
- `conductor start <item>` (manual) and auto-start both go through the Launcher. A session gets:
  - a conductor-generated name `cdt-<ulid>` (§8.6)
  - its own git worktree on the claim ref `cdt/<user>/<item>` (§9.4), pre-trusted through the settings
    overlay so no folder-trust prompt appears (M5)
  - a settings overlay: normal permission mode (never bypass), the profile tool allowlist, and the
    conductor gate registered (§8.1)
  - a scrubbed environment and scoped tokens (§8.2)
  - its pack path, injected at SessionStart
- **Re-injection (M9):** on SessionStart for `compact` and `resume`, a hook injects the pack's Task and
  Acceptance sections plus a pointer to the full pack. This replaces the context guard's digest nudge for
  conductor sessions.
- Sessions end when their Step completes. A session past `session.maxAgeHours` (default 8) or
  `session.maxContextTokens` is checkpointed and relaunched from pack plus checkpoint.

### 7.3 Ship state machine
The profile names the reviewers (by account id), labels and CI checks.

```
build:   each level opened as a draft; stack complete when the last level's worker step passes
release: for level = bottom … top:
           undraft (needs-approval unless the allowlist includes it; default: needs-approval)
           → wait for reviewer bots → fix rounds (worker session) until threads resolved ∧ CI green
           → re-draft → next level
done:    stack fully reviewed; the human merges
```

### 7.4 Eval loop (daily; proposal-only in v1)
- **Corpus:** human-origin turns only. Provenance comes from `turn-origin.sh` and the
  UserPromptSubmit/`isMeta` markers. Tool results and Source text are excluded (H10). The corpus is
  scrubbed (§8.4).
- **Detection:** the same correction or instruction across ≥ `eval.minSessions` (default 3) distinct
  sessions on ≥ 2 distinct days.
- **Rule schema (typed, not prose):** only these kinds:
  - add or modify an evidence recipe step
  - add a ship recipe step
  - add a reviewer account
  - adjust the size rubric
  - add a pack section requirement
- **Forbidden fields:** allowlist, `trustedAuthors`, hosts, secret pointers, notifier and scheduler
  config, hook config, budgets. Rules can never change these.
- **Backtest (directional only; not counterfactual):** gated on ≥ 30 completed items. Uses an older-70% /
  newer-30% holdout and outcome labels
  (merged without rework, steering turns, reverts, escalations).
- **Approval:** `conductor rules show <id>` displays the semantic diff, the exact source turns and the
  backtest. `approve` writes a signed commit to the profile repo; `reject` records a label the eval loop
  learns from.
- **Auto-revert:** a rule whose metric regresses over the next 20 items is reverted (with notification).

## 8. Safety boundary
Required before `mode: auto-small` can be enabled. `doctor` refuses `auto-small` until every check in
§8.1–8.7 passes.

### 8.1 Conductor tool gate (H1, H6, M12)
- A PreToolUse hook active only when the Launcher-set `AW_CONDUCTOR_SESSION=<cdt-id>` is present. The hook
  reads the session's policy from a conductor-owned file, not from env the session can change.
- **Default deny.** It allows only the profile allowlist, with argument constraints. Examples:
  - comment only on the claimed item, at most one per milestone
  - status/labels only on the claimed item
  - push only to the claimed branch
  - `gh pr create --draft` only
- **MCP tools are allowed by exact name only.** Unknown or new tools are denied (M4).
- **Real controls live server-side:** branch protection and rulesets on the remote (no push to protected
  branches, no force push). The gate is the in-session layer. Because tokens can't express "draft only",
  the conductor watches PR state and re-drafts plus alerts on any undraft it didn't approve.
- **Always denied**, with no allowlist entry possible in v1: undraft, merge, close/cancel items, create
  items, chat posts, `gh api` writes, workflow triggers, prod-mutating MCP tools, `kubectl`/exec tools.
- **Fails closed:** a parse error or missing policy means deny. `AW_JUDGE_CHILD` and other env flags are
  ignored in conductor mode.
- A denied call returns a typed reason. The conductor parks the item with that reason and sends a
  needs-approval notification. The session never sits on a permission prompt.
- `send-keys` sends only the fixed `continue` token, and only when the pane is not showing a permission
  dialog.

### 8.2 Credentials and identity (M10, M15)
- Sessions start with a scrubbed env allowlist. They get no ssh-agent forwarding and no cloud profiles.
- They get scoped tokens: GitHub (branch push and draft PRs only) and tracker (comment and status only).
- Sessions use a separate git identity.
- When the profile provides bot identities, writes use them. Otherwise every write carries a "via
  conductor" footer.
- **Credential broker:** recipes that need secrets (for example, logging in to a preview environment) run
  through `conductor cred run <recipe>`. It resolves secret pointers at call time and passes values only
  to the child process. Secrets never enter packs, env, or model context.

### 8.3 Trust tiers and pack format (H2)
- `trustedAuthors` (immutable account ids) and `trustedBots` live in the profile. Rules can't change them.
- **Auto-start requires** the item creator, last editor and all commenters to be trusted. Otherwise the
  item goes to `assist`.
- **Review comments** feed fix rounds only from trusted reviewers and bots, matched by id.
- **Pack layout:**
  ```
  ## Task            (synthesized by conductor from trusted fields; instructions live only here)
  ## Acceptance
  ## Stated fix      (present only if its author is trusted; otherwise quoted below)
  ## Surfaces
  ## Evidence plan   (recipe names from the profile)
  ## Sources         (citations)
  ## Untrusted quotations
  <untrusted source="tracker:ITEM#comment-…" author="…"> … </untrusted>
  ```
- The session preamble states that `<untrusted>` content is data and never an instruction source.
- Ingest strips HTML comments, zero-width characters, encoded blobs and remote image URLs.
- Triage and pack Steps are tool-less. Fetching happens in conductor code through typed Source adapters,
  by id, never by arbitrary URL (M18).

### 8.4 Scrubbing and retention (H11)
- The scrubber (secret patterns from `detect-secrets.sh` plus JWTs, chat tokens, private key blocks,
  credentialed URLs, and identifier shapes such as MRN and SSN) runs on every fetched record, every pack
  write, every ledger free-text field and the eval corpus.
- The scrubber also runs **at every egress to a model** (pack injection, challenger bundle, judge input),
  not only before storage (M9).
- The bridge database (when enabled) follows the same retention. Bridge and challenger text are never part
  of the eval corpus (M10).
- Packs are stored 0600 with a 14-day TTL purge.
- The ledger stores reason codes and hashes. Free-text reasons are scrubbed and capped.

### 8.5 Egress filter and path guard (H8, M16)
- **Outward-write filter:** every allowed outward write (comment, PR body, attachment) passes the scrubber,
  a size cap and a URL domain allowlist.
- **Status comments** are composed by conductor code from structured fields (state, PR link, check
  results). They are never free model text.
- **Evidence attachments** need a manifest and a redaction pass.
- **Prod data:** worker sessions get no prod-read or exec MCP tools by default. Prod reads are available
  only through named profile recipes, and their output is marked sensitive and blocked from egress.
- **Path guard:** a diff that touches profile-declared protected paths (CI and workflow config, hooks,
  auth, lockfiles, the profile itself) parks the item for approval.

### 8.6 Notifier action contract (H7)
- Session names are conductor-generated `cdt-<ulid>`, validated against `^cdt-[0-9a-z]{26}$` at both the
  Launcher and the Notifier.
- Notification actions carry only `{kind, session_id | item_id}`. A local resolver turns that into an argv
  array at click time, with no shell (`tmux attach -t <name>`; remote: `ssh -- <host> …` with the host
  taken from the profile allowlist).
- Titles and bodies carry the item id plus a scrubbed, length-limited title. No other item text goes into
  actions.

### 8.7 Audit log and profile integrity (M13, M17)
- **Audit log:** an append-only log under `$AW_STATE_DIR/conductor/audit/` records every allowed and
  denied gated call (tool, args hash, item, session, profile hash). There are per-item rate limits, and a
  spike in denied calls raises an alert.
- **Profile integrity:** each session pins the profile commit hash in the ledger. A profile change takes
  effect only after `conductor profile approve <hash>`. Recipes are validated against a command schema.

## 9. Runtime correctness

### 9.1 Singleton tick with fencing (H3)
The conductor implements its own lock in TypeScript. The bash helpers in `config/lib/locks.sh` and
`skills/ui-evidence/scripts/lib/locks.sh` are unchanged, and the heavy-job lock stays canonical there (L1).

- **Acquire:**
  1. Create `lock.tmp-<pid>/owner.json` holding `{pid, pidStartTime, host, bootId, startedAt, epoch}`.
  2. Rename it to `conductor.lock`.
  - The rename is atomic, and it fails if `conductor.lock` exists, because that directory is never empty.
- **Stale takeover:** if the owner is provably dead (same host, same `bootId`, pid not alive, or a
  different `bootId`, or a live pid whose start time differs from `pidStartTime`, meaning the pid was
  reused), rename `conductor.lock` → `conductor.lock.stale-<ulid>`. Only one taker wins that rename. The
  winner re-reads the renamed owner file and confirms it is still the dead owner before acquiring. A
  mismatch puts it back and exits (M2).
- **Fencing:** acquiring increments `epoch` in the ledger in a `BEGIN IMMEDIATE` transaction. Every ledger
  write, job result and Launcher action carries the epoch, and stale epochs are rejected. A paused or
  zombie tick can't act after another tick takes over.
- **Boot id:**
  - macOS: `sysctl -n kern.bootsessionuuid`
  - Linux: `/proc/sys/kernel/random/boot_id`
  - The state dir must be on local disk; `doctor` checks this.
  - Containers must not share a state dir.
- A tick that can't acquire exits 0 and writes `no-op: locked by <host>/<pid> since <t>` to stderr.

### 9.2 Job and session liveness (H4)
- **Alive:** the tmux pane exists and `pane_pid` is alive.
- **Progressing:** the transcript mtime changed or a spooled heartbeat arrived within
  `liveness.stallAfter` (default 20 min). Pane content changes are ignored, because spinners fool them (L2).
- **Known waits are not stalls:** a permission dialog, a question to the human, `awaiting-direction`
  (§6.1), or a wait on the heavy-job lock.
- **Relaunch is kill-before-relaunch:**
  1. Kill the pane.
  2. Confirm it's gone.
  3. Relaunch on the same claim ref and worktree.
  - After 2 relaunches the item is parked.
- **Sleep:** when the tick sees a wall-clock jump larger than its interval, it extends every lease by the
  gap before evaluating them.

### 9.3 Answers, takeover and handback (R6)
The `UserPromptSubmit` hook classifies every human turn in a conductor session:

| Turn | Condition | Effect |
|---|---|---|
| `answer` | A question is pending for this session | Recorded as the answer. The session continues. No takeover. |
| `takeover` | No question is pending (unsolicited input) | Session → `taken-over`: no reaping, no `send-keys`, lease paused, `status` shows `with you`. Also raises a drift signal. |
| `conductor` | The fixed `continue` token from the conductor | Inbox delivery (§6.1) |

- `conductor handback <item>` ends a takeover.
- **Timeout:** after `takeover.idleTimeout` (default 2 h) with no human input, the item gets a needs-answer
  notification: "hand back ITEM?". It is never handed back silently.

### 9.4 Claims (M1)
- v1 has one active host (`hosts.active`). Other hosts refuse to tick.
- **The atomic claim is one deterministic ref**, `cdt/<user>/<item>`. It is created with a create-only ref
  update that fails if the ref exists. A relaunch reuses the same ref, and nothing else creates it. The
  same naming is used everywhere (§7.2, §15).
- Tracker markers are advisory, and are trusted only when written by the conductor's identity.
- Cross-host exclusion is best-effort. The ref makes duplicates fail fast.

## 10. Human-facing surfaces

### 10.1 Notification taxonomy and budget
| Kind | Interrupts | Title pattern | Action (as text) |
|---|---|---|---|
| needs-answer | yes; **never overflows** (queued with a count) | `[repo] ITEM: question (n)` | `conductor attach ITEM` (in-session) or `conductor answer ITEM` (job) |
| decide | yes; never overflows | `[repo] ITEM: direction check needs you` | `conductor decide ITEM` |
| needs-approval | yes | `[repo] ITEM: approve <action>` | `conductor approve call:<id>` / `rule:<id>` / `profile:<hash>` |
| parked | batched hourly | `[repo] ITEM: parked: <code>` | `conductor why ITEM` |
| fyi | no | `[repo] ITEM: <milestone>` | — |
| digest | no; at `notify.digestTimes` | `conductor: N waiting on you` | `conductor status` |

- Notifications carry the item id, a scrubbed short title and the command as text (§8.6). They never carry
  item body text or debate content.
- `notify.maxPerHour` (default 4) caps needs-approval, parked and fyi. Anything over the cap goes to the
  digest.
- Repeats of the same error code are deduplicated with a cool-down.
- Direction-check escalations count toward `direction.maxPerItem`, not the hourly cap.
- Voice: plain, first person ("I parked FRN-123 because the reproduce check passed on the base"), states
  the reason and the next step, never blames.

### 10.2 Question lifecycle
1. `asked`
2. Reminder after `questions.remindAfter` (default 4 h).
3. Auto-park after `questions.parkAfter` (default 24 h).

- `conductor snooze ITEM <dur>` delays a question.
- Questions inside a session are answered in the session. The hook records them as `answer`, not takeover.
- Questions from jobs are answered with `conductor answer ITEM`, which opens `$EDITOR` with the template:
  item, what's already decided, the question, options with a recommended default, sources.

### 10.3 CLI
| Command | Purpose | Empty / error text examples |
|---|---|---|
| `status` | Grouped: waiting on you, with you, parked, running, queued. Columns: item, title, step, round/cap, age, next action | "Nothing is waiting on you. 3 running, 1 queued." |
| `why ITEM` | Triage verdict, router inputs, Step rounds, direction-check threads and verdicts, shape signals with evidence, judge scores and thresholds, denials | `CND-ITEM-404 no such item: ITEM` |
| `ledger [--item --step --since]` | Raw ledger query | — |
| `start ITEM` | Manual dispatch | "Started ITEM in cdt-…; attach with `conductor attach ITEM`" |
| `attach ITEM\|cdt-id [--print]` | Attach, or print the command; prints the tmux detach key | `CND-SESS-404 session ended; see conductor why ITEM` |
| `answer` / `decide` / `approve` / `reject` / `snooze` | Human responses. Ids are typed (`call:`, `rule:`, `profile:`, `dir:`) | "Recorded. ITEM resumes on the next tick." |
| `park` / `unpark ITEM [--hint]` | Park or resume with a hint | — |
| `handback ITEM` | End a takeover | — |
| `pause` / `resume` | Global kill switch (also `AW_CONDUCTOR_DISABLE=1`) | — |
| `ack` | Clear the ATTENTION file after reading it | "Cleared 3 attention entries." |
| `notify --test` | Send a test notification through every configured notifier | — |
| `observe` | Zero-config read-only run on the example profile (M14) | — |
| `doctor` | Health checks (§11.5) | — |
| `tick [--dry-run]` | One tick, or print every action | — |
| `shadow report` | Routing and direction-check agreement, per class | — |
| `rules show\|approve\|reject` | Eval-loop proposals | — |
| `index build\|status\|query` | Code index (§6.2) | — |
| `profile init\|validate\|explain\|migrate\|approve` | Profile tooling. `approve` shows the diff first | — |
| `scheduler install\|uninstall\|status [--dry-run]` | Scheduler | — |

**Output contract:**
- Plain-text state words; state is never shown by color or glyph alone.
- `--json` on every read command.
- `NO_COLOR` and non-TTY output are honored.
- Exit codes: `0` ok, `1` attention needed, `2` error.
- Errors carry stable codes `CND-<AREA>-<NNN>`, with the areas seeded in `docs/conductor/errors.md`: PROFILE,
  LOCK, SESS, ITEM, GATE, DIR, SHAPE, INDEX, BRIDGE, NOTIFY, BUDGET.

### 10.4 Silent-failure floor (R7)
- **Fallback chain:** macOS notification, then the `$AW_STATE_DIR/ATTENTION` file, then a banner in
  `status` output.
- **ATTENTION file lifecycle:**
  - Every interrupting notification also appends a line (timestamp, kind, item, command).
  - `conductor ack` clears it.
  - `status` and `doctor` show the count of unacknowledged entries.
- **Delivery limit:** macOS can't confirm a notification was seen. `doctor` therefore reports the time of
  the last `notify --test` and the oldest unacknowledged ATTENTION entry, and states this limit plainly. It
  doesn't claim delivery.

## 11. Developer contracts

### 11.1 Profile (H16, M20)
- **Format:**
  - `profile.yaml` (workplace) and `repos/<repo>.yaml` (per repo), both carrying `schemaVersion`.
  - Defined in Zod, exported as JSON Schema for editor autocomplete.
  - Example profile at `conductor/profile/examples/generic/`.
- **Tooling:**
  - `conductor profile init` scaffolds a profile in `mode: shadow`.
  - `conductor profile validate [--json]` runs by hand and in the profile repo's pre-commit. Errors give
    file, key path, expected type and a fix hint.
  - `conductor profile migrate --dry-run` handles schema upgrades. Deprecated keys warn for one minor
    version before they halt.
- **Precedence:** repo file > profile file > core default. `conductor profile explain <key>` shows the
  effective value and where it came from.
- **Data only:** the profile contains no code in v1. Adapters live in core.
- **Secret pointers:** schemes are `env:`, `file:`, `keychain:`, `op:`. The validator rejects values that
  look like secrets.
- **Size vocabulary (L2):** `XS | S | M | L | XL`, with the default rubric in core and overrides in the
  profile.

### 11.2 Adapters (H17)
```ts
type AdapterError = { kind: "retryable" | "rate-limited" | "fatal" | "not-found"; code: string; retryAfterMs?: number };
type Result<T> = { ok: true; value: T } | { ok: false; error: AdapterError };

interface Tracker {
  scan(scope: ScopeQuery, cursor?: string): Promise<Result<{ items: WorkItemRef[]; cursor: string }>>;
  read(id: string): Promise<Result<WorkItem>>;                 // includes authors for the trust gate
  comment(id: string, body: TemplatedBody, key: string): Promise<Result<void>>;  // idempotent on key
  setStatus(id: string, status: StatusKind): Promise<Result<void>>;              // idempotent
  assign(id: string, who: "self"): Promise<Result<void>>;                        // idempotent
  attach(id: string, url: string, title: string, key: string): Promise<Result<void>>; // idempotent on key
}
interface Source   { find(item: WorkItem): Promise<Result<SourceRef[]>>; fetch(ref: SourceRef): Promise<Result<SourceRecord>> }
interface Launcher { start(spec: SessionSpec): Promise<Result<SessionId>>; list(): Promise<Result<SessionState[]>>;
                     attachArgv(id: SessionId): string[]; sendContinue(id: SessionId): Promise<Result<void>>; kill(id: SessionId): Promise<Result<void>> }
interface Notifier { notify(n: Notification): Promise<Result<void>>; test(): Promise<Result<void>> }
interface Reviewer { threads(change: ChangeRef): Promise<Result<ReviewThread[]>> }   // author ids for trusted-bot matching
interface Scheduler{ install(dry: boolean): Promise<Result<string>>; uninstall(dry: boolean): Promise<Result<string>>; status(): Promise<Result<string>> }
```
- **Selection:** the profile picks an adapter by `type:` (for example `tracker: { type: linear, ... }`).
- **Registration:** a static registry in core.
- **Contract tests:** `adapterContractTests(name, factory)` is a reusable Vitest helper. Every built-in and
  fake adapter must pass it.

### 11.3 Setup (H18)
- **Scheduler:** `conductor scheduler install [--dry-run]` writes the launchd plist; `--dry-run` prints it.
  Re-running is idempotent.
- **setup.sh:** `./setup.sh --with-conductor` is opt-in. It builds the package and runs the scheduler
  install, and it respects `--dry-run`, so the merge gate's setup dry-run stays clean.
- **Profile root resolution:** `--profile` > `$AW_PROFILE_DIR` > the `$AW_STATE_DIR/profile` symlink.

### 11.4 Migration from existing orchestrators (H19)
- The v1 Worker session runs `/bugFixOrchestrator <item>` unchanged, for items of type bug.
- Evidence keeps using `ui-evidence`. Profile evidence recipes are passed to the session as pack content.
  Adding multi-source evidence to bugFixOrchestrator itself is a follow-up spec.
- The conductor reads `$AW_DIR/bugfix/*/state.json` read-only for reconcile. It skips items that have a
  bugfix state dir it did not create, so manual runs stay supported.
- Cut-over to a native worker is a later spec. `/bugFixOrchestrator` is not deprecated in v1.
- A "When to use which" table (`/bugFixOrchestrator`, `/specToProvenPR`, `/autoplan`, conductor) goes in
  `docs/conductor/README.md` (L3).

### 11.5 Debug surface (H20)
- **Ids:** every ledger row, job and session carries `tickId` and `epoch`.
- **Logs:** `$AW_STATE_DIR/conductor/logs/tick-YYYYMMDD.jsonl`, rotated after 14 days.
- **`doctor` checks:**
  - lock holder and stale locks
  - epoch sanity
  - duplicate scheduler installs
  - profile valid and approved
  - each adapter reachable
  - tmux present
  - secret pointers resolve (without printing them)
  - notifier test send
  - conductor gate registered and failing closed (a deliberate denied probe)
  - state dir on local disk
  - boot-id readable
  - ledger schema version
  - budget headroom
  - every §5.3 hook registered, each answering a recorded probe payload with the expected decision
  - code index age per repo, and which providers are enabled (structure, embeddings, graphify)
  - local embedding model reachable
  - bridge mirror: auth enabled, CORS off, backlog size (only when enabled)
  - unacknowledged ATTENTION entries and the last `notify --test` time
  - direction-check mode per checkpoint, shadow precision, caps used today

## 12. Portability
| Concern | v1 (macOS) | Linux (seams in v1, built-ins later) |
|---|---|---|
| Scheduler | launchd | systemd user timer with `loginctl enable-linger`; cron fallback |
| Launcher | tmux | tmux |
| Notifier | macOS notification → ATTENTION file | ntfy (authenticated, high-entropy topic, M11), chat DM |
| Boot id | `kern.bootsessionuuid` | `/proc/sys/kernel/random/boot_id` |
| Claude auth | interactive login | headless auth runbook |
| Hooks | — | Fix `context-guard.sh` to prefer `stat -c%s` on GNU (detect, don't guess); add hooks to the Linux test script |
| Tests | local | `scripts/test-linux.sh` runs core and hook tests in an Ubuntu container (M22) |

## 13. Rollout
0. **Measure and decide** (M3, M7, M14):
   - Turn the audit extraction into `scripts/transcript-audit/` (L4).
   - Record baselines and **set the §2 targets from them**.
   - Measure the share of the last 60 days of in-scope items that would have been XS, clear and trusted.
     - If it is at least 10%, keep `autoStartMaxSize: XS`.
     - If it is under 10%, pilot `S` with 100% verification, or skip auto-small and stay in `assist`.
1. **Prerequisites:**
   - Fix done-gate bare-word false positives for non-conductor sessions.
   - The conductor-mode Stop hook (§5.3).
   - Profile schema and tooling.
   - Lock and fencing.
   - Scrubber.
   - Bridge hardening, if the bridge mirror is enabled.
2. **Shadow:**
   - `conductor observe` gives zero-config value on day one.
   - Then shadow mode on the real profile: triage and pack jobs run tool-less, the code index is built
     (structure, embeddings, graphify), and no sessions start.
   - Exit when per-class routing agreement is at least 90% over at least 30 items.
3. **Assist:**
   - `conductor start`, packs and re-injection, the ship state machine, notifications.
   - The full §5.3 hook set.
   - Shape checks enforced at commit. They're deterministic and their thresholds are tuned in shadow.
   - Direction checks run in **shadow** for the Approach and Drift checkpoints only.
4. **Auto-small:** enabled when `doctor` passes §8 and the step-2 threshold holds.
   - Direction checks move to `enforce` per checkpoint once they reach the §6.1 precision bar.
5. **Scoping proposals:**
   - Scoping, Scope-expansion and Shape direction checks run, plus graph and embedding candidate recall
     for surface discovery.
   - Piloted on one project the human leads, and backtested on the project's original brief for recall of
     the issues filed later.
6. **Eval loop:** proposal-only, once there are ≥ 30 completed items.
   - At most 2 proposals per week.
   - Each proposal must win a shadow A/B (rule applied in shadow vs not, over at least 10 items) before
     approval is requested (M6).
   - Index providers (graph on/off, embeddings on/off) are evaluated the same way.

## 14. Testing
- **Unit tests** in Vitest with in-memory SQLite and fake adapters: tick, router, Step runner (pass,
  fail-then-pass, exhausted rounds, budget breach, escalation), ship state machine, question lifecycle,
  notification budget and dedup.
- **Singleton and fencing:**
  - two concurrent ticks: one is a no-op
  - stale lock, dead pid: taken over
  - stale lock, other boot id: taken over
  - live owner: respected
  - stale epoch write: rejected
  - non-active host: refuses to tick
- **Liveness:**
  - a stalled pane is relaunched exactly once (kill-before-relaunch)
  - a permission dialog is not a stall
  - a sleep gap extends leases
  - a taken-over session is never reaped
- **Gate:**
  - every "always denied" row is denied
  - malformed policy denies
  - `AW_JUDGE_CHILD` is ignored
  - argument constraints hold (wrong item id, non-draft PR)
- **Injection fixtures:**
  - an untrusted-author item never auto-starts
  - `<untrusted>` fences survive pack synthesis
  - a stated fix from an untrusted author is quoted, not placed in Task
  - hidden content is stripped
- **Notifier:** an item title containing shell metacharacters never reaches argv; session-name validation
  rejects bad names.
- **Scrubber:** fixtures for each secret and identifier pattern, at ingest, pack, ledger and egress.
- **Hooks (§5.3):** every row is driven with a recorded payload and its decision asserted; a hook error in
  conductor mode denies; outside conductor sessions behavior is unchanged.
- **Direction checks:**
  - each checkpoint fires from its hook or conductor trigger, never from an agent tool call
  - forged, unsigned or wrong-role turns are rejected
  - the arbiter reads only HMAC-verified turns
  - verdict delivery through the inbox and `continue`
  - `awaiting-direction` is never reaped
  - same-model fallback is recorded as `skipped:no-diverse-model`
  - providers outside `providers.allowed` are never called
  - per-item and per-day caps
  - shadow mode never blocks
  - bridge down: checks run and the mirror back-fills
- **Shape checks:** fixture repos with an exact clone, a renamed clone, a second-case near-clone, a
  duplicate dependency and an over-budget diff each produce the right signal. Recorded `proceed` pairs are
  not re-flagged. A stale index is labeled stale.
- **Index:** `adapterContractTests` for structure, embeddings and the graphify adapter; incremental rebuild
  matches a full rebuild.
- **Turn classification:** an answer to a pending question isn't takeover; unsolicited input is; the
  takeover timeout notifies.
- **Unattended e2e** (heavy suite): a fixture item goes from shadow through assist start, approach check,
  commit with shape check, PR draft, to Stop gate, with no prompt appearing (M5).
- **Eval loop:** non-human-origin turns are excluded, forbidden-field rules are rejected, the holdout split
  is enforced.
- **Adapters:** `adapterContractTests` for every built-in and fake.
- **Real-process tests** (tmux, macOS notifier, the unattended e2e) are tagged `heavy` and excluded from
  the default `npm test`. They are a merge-gate item, run serially (`npm run test:heavy`), per the
  one-heavy-job rule. `scripts/test-linux.sh` is also a merge-gate item for changes under `conductor/`
  and `config/hooks/` (M15).
- **Merge gate:** add `conductor` to the AGENTS.md typecheck and test lists.

### Docs deliverables (M19)
- `docs/conductor/README.md` (including "when to use which")
- `docs/conductor/profile.md` (reference generated from the schema)
- `docs/conductor/adapters.md`
- `docs/conductor/errors.md`
- `docs/conductor/linux.md` (with the Linux adapters)
- ledger schema in `planning/ERD.md`
- CLI in `planning/API_CONTRACT.md`
- updates to `planning/ARCHITECTURE.md` and `planning/TESTING.md`

## 15. Second user (M4)
- A teammate runs `conductor profile init` from the generic example and gets their own state dir and
  ledger.
- Claims are namespaced by user (`cdt/<user>/<item>`, §9.4), so two humans on the same tracker
  never claim each other's items. Their scopes come from their own profiles.
- Shared recipes can be copied between profile repos. A shared team profile layer is out of scope for v1.

## Appendix: audit method
- Extract every non-machine human turn from `~/.claude/projects/**/*.jsonl` (excluding sidechains, tool
  results, and injected text) with context: active skills, context-guard state, token count, and the
  preceding agent message.
- Classify a stratified sample of 450 turns by reading them.
- Floor counts across all turns using exact-phrase patterns.
- Rollout step 0 moves the scripts into `scripts/transcript-audit/` so the audit can be reproduced.
