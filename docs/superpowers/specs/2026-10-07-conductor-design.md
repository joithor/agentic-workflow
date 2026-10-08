# Conductor — design

Status: draft for review, revision 5 · 2026-10-08
Revision 5 addresses `/autoplan` round 4: sessions run in containers (§8.2) with conductor-mediated
push (§8.5); the environment is an adapter; a dashboard and menu-bar badge (§10.5); calibration fixes.

Revision 4 history:
Revision 4 addresses `/autoplan` round 3 (T1–T6, M1–M10): hook semantics grounded in the Claude Code
docs, a sandboxed session user, enforced shape checks with defined outcomes, and an offline index.
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
| Clone count and duplication ratio on files conductor sessions touched | code index | flat or falling |
| Transcript-classifier accuracy | adjudicated sample of 100 turns per month | ≥ 0.85, reported in `shadow report` |

### Kill criteria
- **Auto-start:** if after 4 weeks human turns per merged PR is down less than 20%, or the rework rate of
  auto-started PRs exceeds that of human-started PRs **of the same size class** (minimum 20 auto-started
  PRs before this fires), auto-start is turned off (`mode: assist`). The
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
10. **Hooks enforce; agents don't opt in.** Every required check fires on a hook event, a git hook, or a
    conductor action, never because an agent chose to call a tool. MCP is for reading data only. Hooks
    enforce *patterns*. The *security boundary* is the session OS user, server-side protections, and
    conductor-side re-verification (§5.3).

## 4. v1 scope

| In v1 | Deferred (interface and contract tests exist, no built-in) |
|---|---|
| macOS, launchd, session containers (OrbStack or Docker), tmux inside containers, macOS notifier (with fallback chain §10.4), web dashboard plus SwiftBar badge | systemd/cron schedulers, ntfy, chat-DM notifier |
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

### 5.3 Enforcement model: hooks force patterns, the sandbox enforces security
**Principle:** every check that must happen is fired by a hook event or by the conductor. No check
depends on an agent choosing to call a tool. MCP servers are for **reading data**.

There are two layers, with different jobs:
- **Hooks enforce behavior patterns** on a cooperative agent: checkpoints, shape checks, pack injection,
  turn classification. This is what removes the human steering.
- **The security boundary does not depend on hooks:**
  - a dedicated OS user for sessions
  - conductor state readable only by the conductor user
  - server-side branch protection
  - conductor-side re-verification of the pushed head (§6, §8.5)

  A hook that fails open costs a missed nudge. It never costs a breach.

**Grounded hook semantics** (Claude Code docs, checked 2026-10-07; rollout step 1 confirms them with a live
probe, and the probe suite becomes a `doctor` check):

| Fact | Consequence |
|---|---|
| Only exit 2 or `permissionDecision:"deny"` blocks a tool call. Other non-zero exits, timeouts (default 600 s) and a missing command **fail open** | Every conductor hook runs through `cdt-hook`, a small static wrapper that traps all errors and timeouts (its own budget is shorter than the hook timeout) and emits an explicit deny with `CND-HOOK-9xx`. `doctor` probes every hook. |
| `continue:false` with `stopReason` ends the turn, and Claude sees the reason | Checkpoints end the turn cleanly instead of looping on denials |
| A Stop block (`decision:"block"`) is capped at 8 consecutive continuations; `stop_hook_active` marks a re-entry | Stop is a **pattern** gate only. It blocks when the Step's required artifacts are missing (at most twice per chain). Known waits (`awaiting-direction`, `awaiting-human`) are always allowed to stop. Completion is verified **conductor-side after Stop**, never inside the hook. |
| `UserPromptSubmit` can add `additionalContext` and can block | Inbox delivery and turn classification |
| Managed-settings hooks merge with all other levels and can't be removed below managed | Conductor hooks ship in the **session image's root-owned managed settings**. The session runs as an unprivileged container user and can't change them. The host's own Claude sessions get no conductor hooks. |
| `-p` and SDK sessions skip the trust dialog; interactive sessions need the folder trusted | The image pre-accepts trust for `/workspace/src` in the container user's config at build time |

**Identifying a conductor session:** the container *is* the session. Conductor hooks exist only in the
session image, so there's no identity check to bypass, and no `env -u` path (T2).

**Hook map:**

| Event (matcher) | Enforces | Effect on violation |
|---|---|---|
| `SessionStart` (startup) | Inject the pack's Task, Acceptance and Evidence plan; register; heartbeat | — |
| `SessionStart` (compact, resume, fork) | Re-inject Task and Acceptance plus the pack pointer | — |
| `UserPromptSubmit` | Turn classification (`answer` / `takeover` / `conductor`). On a verified delivery token (§6.1), inject the inbox. A human correction raises a drift signal. | Block a forged delivery token (`CND-DIR-030`) |
| `PreToolUse` (all tools) | Conductor tool gate (§8.1) | Deny `CND-GATE-1xx` |
| `PostToolUse` (all tools) | **Edit detection by working-tree state, not by tool name:** after any tool call, the hook compares `git status --porcelain --untracked-files=all` plus content hashes against the last snapshot, so untracked files count too (M6). The first change to a non-test source file without a `proceed` verdict fires the Approach checkpoint. This catches Edit, Write, `sed -i`, `tee`, scripts and codegen. | `continue:false`, `stopReason: CND-DIR-010 awaiting direction check <id>`. The changes stay in `/workspace/src`; the verdict decides keep or revert. |
| `PreToolUse` (`Bash`) plus a git `pre-commit` hook set via the image's system git config | **Shape checks** (§6.2), as **pattern** enforcement. The git hook catches the common commit forms; `--no-verify` doesn't matter, because the conductor-side verifier re-runs the same checks on the bundle before anything is pushed (§8.5). | Commit refused with evidence and `continue:false` (M6); Shape direction check opened |
| Session export (Stop and commit hooks write `/spool/out.bundle`) | Hand-off for conductor-mediated push (§8.5) | — |
| `PostToolUse` | Heartbeat spool; drift signals; scrub tool output going back into context | — |
| `Stop` | Pattern gate as described above | Block at most twice, then allow and record `CND-HOOK-210` |
| `PreCompact` | Checkpoint Step state to the spool | — |

Session-side git hooks are a convenience for fast feedback, not a control. The authority is the
conductor's verifier container (§8.5).

**Debuggability (T4):**
- Every hook decision is appended to `$AW_STATE_DIR/conductor/hooks/<session>.jsonl` with event, matcher,
  input hash, decision, code and duration.
- `conductor hook log <item>` shows the decisions for an item.
- `conductor hook replay <log-id>` re-runs a recorded decision.
- `conductor hook test <event> <fixture.json>` runs a hook against a fixture.
- Codes separate the cause: `1xx` policy, `2xx` pattern, `9xx` crash or timeout.
- The deny text always includes the code, a one-line reason and `conductor why <item>`.
- **Kill switches:** `hooks.<name>.enabled` per hook in the profile. Changing one requires
  `profile approve` and is shown in `status` as a degraded mode. The gate and the Stop pattern gate can't be
  switched off.

**Coexistence (T1d):**
- The session image includes the existing `config/hooks/*` safety hooks. Hooks merge, and the safety
  hooks only add denials.
- The image omits `done-gate.sh` and `scope-gate.sh`, because the conductor's Stop pattern gate and tool
  gate replace them.
- On the host, nothing changes.

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
| Worker | tmux session (`/bugFixOrchestrator` in v1) | **The conductor re-runs the recorded reproduce check itself** in a fresh **verifier container** from the exported bundle (§8.5): it must fail on the base and pass on the head. Flaky handling: 3 runs, majority wins, and disagreement parks the item (M2). Path guard (§8.5) and shape checks (§6.2) pass. | Judge resolution-check against the original item text |
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
  id, the epoch, and an HMAC keyed by a conductor secret. Each MAC covers the previous turn's MAC, so the
  turns form a chain.
- **Key custody (M3):** the key lives in the human's keychain (macOS) or a 0400 file in the
  conductor-private zone (Linux). It is never mounted into any container. It rotates on every epoch
  change, and the previous key stays valid until every check opened under it has closed (M6).
- **Position authorship (M2):** the proposer's `position.json` is captured by the PostToolUse hook from the
  session's checkpoint output, stamped with the session id and turn id, and signed by the conductor on
  ingest. The session never signs anything.
- **Leg timeouts (M2):** position 10 min, challenge 5 min, reply 10 min, verdict 2 min. A timeout escalates.
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
2. The conductor sends `continue cdt-tok:<nonce>` through `send-keys`. The nonce is single-use, bound to
   the check id and the session, and signed into the inbox file.
3. The `UserPromptSubmit` hook verifies the nonce against the inbox (match, unused, unexpired), injects
   the inbox contents as additional context, marks the nonce used, and writes a delivery ack to the spool.
   A prompt that carries a bad or reused nonce is blocked (`CND-DIR-030`). A prompt with no nonce is
   classified as human (§9.3).
4. For a `revise` verdict, the text is synthesized by conductor code from the verdict's agreed objections,
   as a structured list. Raw thread text never becomes instructions.

**Decision:** a judge question `direction-verdict` (`proceed | revise | escalate`) reads the HMAC-verified
turns only. Below threshold, the result is `escalate`. Escalation creates a `decide` item (§10.1). The
human runs `conductor decide <item>` to see both positions and choose.

**Providers and egress (R3):**
- The profile's `providers.allowed` defaults to Anthropic only.
- Diversity comes from a **different Claude model** than the proposer's (e.g. proposer Sonnet, challenger
  Opus), plus **Jev** as the arbiter's first classifier (already vendor-reviewed for code in judge).
- The arbiter is restricted as well. The conductor calls `judge --providers <profile list>`, a new judge
  CLI flag that limits the chain (M1). Without the flag judge would fall through to any installed agent
  CLI.
- Other providers (Codex, Cursor CLI, others) are opt-in per provider. They receive a bounded bundle:
  scrubbed, sensitive paths denied, non-agentic mode. Every provider call writes an audit row.
- If no distinct challenger model is available, the check is skipped and recorded as `skipped:no-diverse-model`.
  It never silently uses the proposer's model.

**Proof before trust (R5):**
- Checks start in `direction.mode: shadow`: they run and record verdicts but don't block. The ledger
  compares them against later human "wrong path" corrections and rework.
- **Exception:** the **Shape** checkpoint enforces from rollout step 3b, because enforced shape checks
  (§6.2) depend on it to resolve a refused commit.
- Other checkpoints move to `enforce` once shadow shows precision of at least `direction.minPrecision`
  (default 0.6) over at least 20 checks.
- **Caps:** per checkpoint, `direction.maxPerItem.<checkpoint>` (default 3 each; Shape 5), `direction.maxPerDay` (default 40), a per-check budget
  (`direction.maxCostPerCheck` default $0.50, `direction.maxMinutesPerCheck` default 30,
  `direction.deliveryTimeout` default 15 min → escalate), plus `budget.perItem` (M9).
- **Kill criteria** need at least 30 checks per checkpoint before they can fire (M9).
- **Kill criterion:** a checkpoint whose `revise` verdicts don't reduce rework compared to shadow over 30
  items goes back to shadow.

### 6.2 Code-shape checks and the code index
Four questions keep a codebase healthy as it grows:
- Can this be simpler?
- Can this be more elegant?
- Now that there are two cases, should this be generalized?
- Have we reinvented something that already exists?

At scale they must be **deterministically evaluable**: deterministic signals produce evidence, and models
judge only the shortlist. **All index layers enforce from the first enforcing rollout step (3b).** That's
an explicit decision, taken over a shadow-first alternative. Every enforced outcome is defined, so nothing
can deadlock.

**Code index** (per repo, behind an `Index` adapter, stored in `$AW_STATE_DIR/conductor/index/<repo>.db`,
owned by the conductor user):

| Layer | Content | Built with |
|---|---|---|
| Structure | Symbols (name, signature, kind, file, exported, callers) | tree-sitter; LSP (Serena) when available |
| Clones | Normalized AST hashes per function and block (identifiers and literals abstracted), plus MinHash/LSH over token shingles (no pairwise comparison) | core |
| Dependencies | Package manifests, plus internal utility modules marked by profile globs | core |
| Embeddings | Function-level vectors from a **local** model | Ollama directly, or Prism with `cloud_fallback:false` and `route_guard:local` |
| Graph | Module and call graph, cross-file relationships, surface clusters | **graphify** adapter (`index.graph: graphify`) |

**Offline guarantee (T5):**
- Indexing never sends code off the machine. Embedding calls go only to a loopback endpoint.
- The graphify adapter runs with network access denied (sandbox profile, or a network-less namespace on
  Linux) and fails closed if it attempts egress.
- `doctor` verifies all three: the loopback endpoint, the Prism flags, and the graphify sandbox.
- **Inputs:** tracked files only (respecting `.gitignore`), excluding the profile's `index.denyPaths`
  (secrets, fixtures containing PHI, generated code). Symlinks aren't followed. Per-file and total size
  caps apply.

**Freshness and branches (M5):**
- The main index is rebuilt incrementally on merges to main. Builds take the heavy-job lock.
- Each worktree gets a **per-worktree overlay** built from its own diff, so checks see in-flight changes.
- Every derived artifact carries a version stamp (indexer version, model id, graphify version, commit
  SHA). A stamp mismatch triggers a rebuild.

**Graphify as a provider:** it is one source of candidates for the Reinvention and Scoping checks.
- A prior trial found a code graph did not beat plain search for agent *navigation*. Here it is used for
  *candidate recall* and enforces from day one by decision.
- The eval loop still measures it arm-vs-arm and reports whether it earns its cost.
- **Kill criterion:** a layer whose candidates are overturned more than 80% of the time over 30 signals
  is demoted through a rule proposal.
- **Fast circuit breaker (M1):** a single signal type overturned 5 times in a row is paused for that
  repo until reviewed.
- **Single-layer failure:** only that layer's checks take the `index-unavailable` outcome. The other
  layers still enforce.

**Signals** (computed at commit by the git `pre-commit` hook, and re-computed conductor-side on the pushed
range; profile thresholds, seeded by a historical replay over past merged PRs in rollout step 2, then calibrated **per layer** on
real commits in step 3a (Q1, M1):

| Question | Signal | Default threshold |
|---|---|---|
| Reinvented? | A new symbol's AST hash matches an existing one; signature and name similarity ≥ t; an embedding neighbor ≥ e **and** an AST similarity ≥ a; a graph neighbor with an overlapping call set; a new dependency overlapping an existing one by purpose tag | t 0.85, e 0.9, a 0.6 |
| Generalize at the second case? | A near-clone between new code and existing code (≥ N tokens, MinHash Jaccard ≥ s) | N 60, s 0.8 |
| Simpler? | Changed lines over the size budget (XS 80, S 250, M 600); cyclomatic complexity delta above the limit; new files, exports, interfaces, flags or config keys above the size class's allowance | per size class |
| More elegant? | No direct signal. Covered by the three rows above plus the Shape direction check | — |

**Enforced outcomes (T3), every one of which ends:**

| Situation | Commit | Then |
|---|---|---|
| Signal fires, check budget available | Refused with evidence | Shape direction check opens; the session ends its turn (`awaiting-direction`); the verdict is delivered: `revise` (reuse or generalize), `proceed` (keep, reason recorded), or `escalate` (`decide` item) |
| Signal fires, `direction.maxPerItem` exhausted or no diverse model available | Allowed, with a `shape-unresolved` trailer | PR gets the `shape-unresolved` label; needs-approval to the human; the ship state machine won't undraft until it's resolved |
| Index missing, stale beyond `index.maxAgeHours`, or failing | Allowed, with an `index-unavailable` trailer | Conductor rebuilds; the conductor-side re-check runs on the push range once the rebuild finishes; a failure there → needs-approval |
| Check exceeds the 2 s commit budget | Allowed, with a `shape-deferred` trailer | The conductor-side check on push is authoritative and refuses the push if it signals |

The Shape direction check ships in the **same** rollout step as shape enforcement (§13, step 3b).

**Evidence format (M8):**
- Each signal names its type, value and threshold, both locations as `path:line`, the index age and the
  providers involved.
- Repo-derived strings (symbol names, comments, snippets) are shown inside `<untrusted>` fences (M4).
- A `proceed` reason is labeled `agent-written` with the session id, and the daily digest lists every
  `proceed` that let code through.

**Exemptions expire (M4):** a recorded `proceed` exempts the pair `(new symbol, existing symbol)` until
either file changes, or for 30 days, whichever comes first.

**Repo trend:** the eval loop tracks duplication ratio, clone count, average complexity and dependency
count over time on the files conductor sessions touched (M9). A rising trend becomes a typed `reuse-hint`
rule proposal (§7.4).

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
  - its own container (§8.2) with `/workspace/src` cloned from the read-only base, on the local branch
    for the claim ref `cdt/<user>/<item>` (§9.4); trust pre-accepted in the image (M5)
  - an environment from the repo's `Environment` adapter (§11.2), for example a PR preview, shared host
    services or a per-container stack
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
  - add a `reuse-hint` (prefer existing symbol X for purpose Y), from the shape trend (§6.2)
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

### 8.2 Session isolation, credentials and identity (I1–I5)
**Every session runs in its own container.** The container runtime sits behind a `Sandbox` adapter:
- macOS: OrbStack or Docker Desktop, both in one Linux VM.
- Linux: Docker or Podman, natively. This is the same model on a cloud box.

The host never executes code a session wrote.

**Resource ceiling** (this also fixes the host crashes):
- Each container has CPU and memory limits (`session.cpus`, `session.memory`, default 2 CPU / 3 GB).
- On macOS the whole fleet also lives inside one VM with a fixed size (`sandbox.vmMemory`, default 12 GB).
- A runaway session hits its own limit. It can't take down the host, the terminal, or the other sessions.

**Mounts:** reads follow the Serena pattern (read-only mounts, as in `scripts/serena-docker`); writes stay in
the container.

| Host path | Container path | Mode | Purpose |
|---|---|---|---|
| Conductor-owned bare mirror, `$AW_STATE_DIR/conductor/mirrors/<repo>.git` (fetched every tick) | `/mirror` | ro | Object store |
| Conductor-created checkout of the base ref (a `git worktree` of the mirror) | `/workspace/base` | ro | Exact starting tree |
| Package and toolchain caches | `/cache` | ro | Installs become cache copies |
| Code index snapshot | `/index` | ro | Shape checks |
| `$AW_STATE_DIR/conductor/sessions/<id>/ro/` (pack, policy, inbox, delivery nonces) | `/conductor` | ro | The conductor writes it; the session reads it |
| `$AW_STATE_DIR/conductor/sessions/<id>/spool/` | `/spool` | rw | Heartbeats, positions, hook decision log, outgoing `git bundle` (§8.5). The host treats all of it as untrusted data. |
| *(container volume)* | `/workspace/src` | rw | `git clone --shared /workspace/base` at start: seconds, no network, own `.git` |
| *(container volume)* | `node_modules`, build output | rw | Fast IO; never bind-mounted |

- Directory zones (I4):
  - **conductor-private:** HMAC key, ledger, mirrors' config. Never mounted.
  - **shared read-only:** `/conductor`, `/mirror`, `/workspace/base`, `/cache`, `/index`.
  - **session-writable:** `/spool` and the volumes.
- **No shared gitdir (I1):** the session's `.git` lives in its own volume. Nothing the session writes can
  end up in a git dir the human's shell or editor reads.
- **Optional live view:** the conductor rsyncs `/workspace/src` working files, **excluding `.git`** and
  executable git metadata, to `$AW_STATE_DIR/conductor/view/<item>/` for read-only viewing in an editor.

**Credentials (I3):**
- **No write tokens in the container.** The session commits locally. Pushing is conductor-mediated (§8.5).
- **Model access** goes through the conductor's **key proxy** (a loopback HTTP proxy bound into the
  container network). It injects the API key per request, enforces `providers.allowed` and budgets, and
  logs usage. The key never enters the container.
- **Data access:** read-only tracker and docs tokens, scoped per profile. MCP servers are configured
  inside the image with read scopes only.
- **Egress allowlist:** the key proxy, the read-only data endpoints, package registries through the cache
  proxy, and the profile's environment endpoints (e.g. preview hosts). Everything else is denied,
  including the host's loopback services (Prism dashboard, bridge).
- **Secrets for recipes** (e.g. a preview login): `conductor cred run <recipe>` runs host-side and passes a
  short-lived session cookie or token into `/conductor` for that recipe only. Long-lived secrets never
  enter the container.

**Identity:**
- The container is the conductor session. Conductor hooks ship only in the session image's
  **root-owned managed settings** (`/etc/claude-code/managed-settings.json`), and Claude runs as an
  unprivileged user that can't modify them.
- The host's own Claude sessions get no conductor hooks at all, so there's nothing to no-op.
- Writes the conductor makes to trackers and GitHub use bot identities when the profile provides them, or
  carry a "via conductor" footer.

**tmux and attach (M3):**
- tmux runs **inside** each container, with its own socket. Sessions can't reach each other's panes, and
  the host doesn't need tmux.
- `conductor attach ITEM` runs `docker exec -it <ctr> tmux attach` and records the attach.
- Human-origin turns are authenticated by the conductor seeing an attached client it opened. Input
  arriving while no conductor-opened client is attached is not human.
- Answers outside a session go through `conductor answer` / `decide`, which the host OS user
  authenticates.

**Heavy-job lock across containers:**
- The conductor brokers the lock over a narrow unix socket mounted into each container. The socket offers
  `acquire(kind, ttl)` and `release` only.
- Leases expire, so a dead container never holds the lock.
- Index builds and image builds take the same lock.

**Warm start:**
- Images are keyed by `(repo, lockfile hash, toolchain)`. Rebuilding one is a heavy job.
- Startup is: mirror fetch (host), base worktree (host), `clone --shared` and cache-backed install
  (container).
- Target: under 30 s to the first prompt on a warm image.

**Escape surface:** container escape is out of scope for v1. The runtime's defaults apply, plus no
privileged mode, no host network, no Docker socket mount, read-only root filesystem except the volumes,
and dropped capabilities.

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
- **Conductor-mediated push (I2, I3):**
  1. When the session finishes a commit series, the session image's Stop/commit hooks write
     `git bundle create /spool/out.bundle <base>..HEAD`.
  2. The conductor never runs git on the session's tree. It verifies the bundle inside a **fresh
     verifier container**: unprivileged, no network, no credentials. The verifier fetches the bundle into
     a clean clone of the mirror and runs, on that exact head:
     - `git fsck`
     - the reproduce check
     - the shape checks
     - the path guard
     - diff caps

     Every conductor-side git call uses `-c core.hooksPath=/dev/null`, no attributes or filters, and
     `safe.directory` scoped.
  3. Only if the verifier passes does the conductor push the verified SHA from its own mirror, using its
     write token, to `cdt/<user>/<item>`.
- **Server side:** rulesets protect `cdt/*` refs (no force push, no direct push to protected branches),
  and the profile documents them. `doctor` runs a **bypass probe**: from inside a session container, a
  direct push and a direct API write must both fail.
- **CI secrets:** environment-protected so `cdt/*` refs can't use them without approval.
- **Undraft:** the undraft watch re-drafts within one tick interval. The remaining window is documented.

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
- **Alive:** the container is running, and its tmux pane exists with `pane_pid` alive.
- **Progressing:** the transcript mtime changed or a spooled heartbeat arrived within
  `liveness.stallAfter` (default 20 min). Pane content changes are ignored, because spinners fool them (L2).
- **Known waits are not stalls:** a permission dialog, a question to the human, `awaiting-direction`
  (§6.1), or a wait on the heavy-job lock.
- **Relaunch is kill-before-relaunch:**
  1. Kill the pane.
  2. Confirm it's gone.
  3. Relaunch a container from the last exported bundle (or the base, if none exists) on the same claim ref.
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
- A takeover during an open direction check pauses that check (its leg timers stop). Handback resumes it.
- **Timeout:** after `takeover.idleTimeout` (default 2 h) with no human input, the item gets a needs-answer
  notification: "hand back ITEM? (`conductor handback ITEM`)". It is never handed back silently.

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
| needs-approval | yes; **never overflows** | `[repo] ITEM: approve <action>` | `conductor approve call:<id>` / `rule:<id>` / `profile:<hash>` |
| parked | batched hourly | `[repo] ITEM: parked: <code>` | `conductor why ITEM` |
| fyi | no | `[repo] ITEM: <milestone>` | — |
| digest | no; at `notify.digestTimes` | `conductor: N waiting on you` | `conductor status` |

- Notifications carry the item id, a scrubbed short title and the command as text (§8.6). They never carry
  item body text or debate content.
- `notify.maxPerHour` (default 4) caps parked and fyi; anything over goes to the digest.
- needs-answer, decide and needs-approval never overflow. They're bounded by a **global interrupt ceiling**
  `notify.maxInterruptsPerHour` (default 6). Above it, interrupts collapse into one "N items need you"
  notification that lists ids, and the ATTENTION file gets every entry (M7).
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
| `ledger [--item --step --since]` | Raw ledger query | "No ledger rows match." |
| `start ITEM` | Manual dispatch | "Started ITEM in cdt-…; attach with `conductor attach ITEM`" |
| `attach ITEM\|cdt-id [--print]` | Attach, or print the command; prints the tmux detach key | `CND-SESS-404 session ended; see conductor why ITEM` |
| `answer` / `approve` / `reject` / `snooze` | Human responses. Ids are typed (`call:`, `rule:`, `profile:`, `dir:`) | "Recorded. ITEM resumes on the next tick." / `CND-ITEM-409 nothing pending for ITEM` |
| `decide ITEM [--pick a\|b\|other --note "..."]` | Shows the item's Task, the proposer position, the challenger objections and replies (untrusted strings fenced), the arbiter score and threshold, and a recommendation; records the pick | "No direction check is waiting on you for ITEM." |
| `hook log\|replay\|test` | Hook decisions per item; re-run a recorded decision; run a hook on a fixture (§5.3) | "No hook decisions recorded for ITEM." |
| `setup-host [--dry-run]` / `teardown-host` / `repo add <path>` / `index setup` / `image build <repo>` | Host, repo, index and session-image setup (§11.3) | dry-run prints every change; `teardown-host` reverses the manifest |
| `dashboard [--url]` | Start the dashboard, or print its tokened URL (§10.5) | — |
| `open ITEM` / `view ITEM` | Fetch the pushed branch into a host worktree / open the read-only live working files | — |
| `park` / `unpark ITEM [--hint]` | Park or resume with a hint | "Parked ITEM." / `CND-ITEM-409 ITEM is not parked` |
| `handback ITEM` | End a takeover | "ITEM handed back; resuming on the next tick." / `CND-SESS-409 ITEM is not taken over` |
| `pause` / `resume` | Global kill switch (also `AW_CONDUCTOR_DISABLE=1`) | "Paused. Running sessions finish their current Step; nothing new starts." |
| `ack [ITEM]` | Clear one item's ATTENTION entries, or all of them | "Cleared 3 attention entries." / "Nothing to acknowledge." |
| `notify --test` | Send a test notification through every configured notifier | "Sent via macOS; wrote ATTENTION entry. (macOS can't confirm you saw it.)" |
| `observe` | Zero-config read-only run on the example profile (M14) | "Observed N items; would have started M. Nothing was changed." |
| `doctor` | Health checks (§11.5), one line per check: `ok` / `warn` / `fail` + fix command | exit 1 on any warn, 2 on any fail |
| `tick [--dry-run]` | One tick, or print every action | "no-op: <reason>" on stderr when nothing to do |
| `shadow report` | Routing, direction-check and shape agreement per class, plus classifier accuracy | "Not enough shadow data yet (n/30)." |
| `rules show\|approve\|reject` | Eval-loop proposals | "No proposals pending." |
| `index build\|status\|query` | Code index (§6.2) | `CND-INDEX-404 no index for <repo>; run conductor repo add` |
| `profile init\|validate\|explain\|migrate\|approve` | Profile tooling. `approve` shows the diff first | validate: "Profile valid." / errors with file, key path, fix |
| `scheduler install\|uninstall\|status [--dry-run]` | Scheduler | dry-run prints the unit/plist |

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
  - An item's entries clear automatically when that item's pending question, decision or approval is
    resolved. `conductor ack [ITEM]` clears the rest manually.
  - `status` and `doctor` show the count of unacknowledged entries.
- **Delivery limit:** macOS can't confirm a notification was seen. `doctor` therefore reports the time of
  the last `notify --test` and the oldest unacknowledged ATTENTION entry, and states this limit plainly. It
  doesn't claim delivery.

### 10.5 Dashboard and menu-bar badge
**Web dashboard** (`conductor dashboard`):
- Served by the conductor on `127.0.0.1` with a random token in the URL (rotated on restart, printed by
  `conductor dashboard --url`).
- No CORS, Host-header check, CSRF token on actions.
- Works over an ssh tunnel to a cloud box unchanged.
- It's a thin layer over the CLI's `--json` outputs. Actions call the same verbs, so there's no second
  source of truth.

| View | Shows | Actions |
|---|---|---|
| **Sessions** | Per item: title, step, state (`running` / `awaiting-direction` / `with you` / `verifying` / `parked`), round/cap, age, container CPU and memory against its limit, and a **live read-only pane preview** (`tmux capture-pane` snapshot every 5 s, scrubbed, untrusted strings escaped) | Attach (opens Warp via the §8.6 resolver), park, unpark, pause |
| **Waiting on you** | needs-answer, decide and needs-approval items, oldest first | answer, decide (both positions side by side), approve, reject |
| **Item** | The `conductor why` view: triage, pack sources, direction-check threads, shape evidence, verifier rounds, hook decisions, ledger timeline | open the pushed branch, view the live working files (`view/<item>/`) |
| **Fleet** | VM ceiling and use, heavy-job lock holder and queue, index age per repo, environment status per repo | pause / resume all |
| **Today** | Cost against budget, notifications sent and suppressed, ATTENTION entries, direction-check and shape-check counts | ack |

- **Live updates:** the dashboard polls the CLI JSON every 5 s. No websocket server is needed in v1.
- **Accessibility:** states are shown as text, not color only. The previews are text, not images.

**Menu-bar badge** (macOS, `Badge` adapter, built-in: SwiftBar plugin):
- Shows `▶ running · ⚑ waiting on you` counts, refreshed every 30 s from `conductor status --json`.
- The dropdown lists waiting items. Clicking one opens the dashboard at that item.
- No actions run from the menu bar itself.
- Linux has no built-in badge; the dashboard and notifications cover it.

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
interface Sandbox  { buildImage(repo: RepoRef, dry: boolean): Promise<Result<ImageRef>>; run(spec: ContainerSpec): Promise<Result<ContainerId>>;  // limits, mounts (§8.2), egress allowlist
                     exec(id: ContainerId, argv: string[], tty: boolean): Promise<Result<number>>; stats(id: ContainerId): Promise<Result<ResourceUse>>; remove(id: ContainerId): Promise<Result<void>> }
interface Environment { provision(item: WorkItem, change: ChangeRef | null): Promise<Result<EnvHandle>>;  // urls + short-lived creds via the broker
                        status(h: EnvHandle): Promise<Result<"pending" | "ready" | "failed">>; teardown(h: EnvHandle): Promise<Result<void>> }
interface Badge    { publish(summary: StatusSummary): Promise<Result<void>> }
```
- **Selection:** the profile picks an adapter by `type:` (for example `tracker: { type: linear, ... }`).
  **`Environment` is chosen per repo** in `repos/<repo>.yaml`. Built-ins:
  - `none`
  - `preview`: request a preview through profile-named labels or CI, then wait for the deploy URL
  - `shared-host`: host services, with a database and key prefix per session
  - `per-container`: a full stack in the container

  Labels, hosts and wait conditions are profile data; the core contains no workplace specifics.
- **Registration:** a static registry in core.
- **Contract tests:** `adapterContractTests(name, factory)` is a reusable Vitest helper. Every built-in and
  fake adapter must pass it.

### 11.3 Setup (H18)
- **Scheduler:** `conductor scheduler install [--dry-run]` writes the launchd plist; `--dry-run` prints it.
  Re-running is idempotent.
- **setup.sh:** `./setup.sh --with-conductor` is opt-in. It builds the package and runs the scheduler
  install, and it respects `--dry-run`, so the merge gate's setup dry-run stays clean.
- **Profile root resolution:** `--profile` > `$AW_PROFILE_DIR` > the `$AW_STATE_DIR/profile` symlink.
- **Host:** `conductor setup-host [--dry-run]`:
  - checks or installs the container runtime
  - sizes the VM (`sandbox.vmMemory`)
  - creates the conductor state zones
  - starts the key proxy and the heavy-lock broker
  - installs the dashboard and badge
  - writes a **manifest** of every change; `conductor teardown-host` reverses it
  - needs no admin rights and no new OS users (I5)
- **Session images:** `conductor image build <repo>` builds the repo's image. It contains the toolkit,
  skills, the session hook set, the existing safety hooks, Claude Code, tmux and the repo toolchain, and
  it is keyed by lockfile hash. Rebuilding one is a heavy job.
- **Repos:** `conductor repo add <path>` creates the bare mirror, records the repo in the profile, and
  queues the first index and image builds.
- **Index dependencies (M6):** tree-sitter grammars are bundled. Ollama and the embedding model
  (`index.embeddingModel`) are installed by `conductor index setup`. graphify is installed and pinned by
  the same command and run in its network-less sandbox. `doctor` verifies each.

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
  - container runtime reachable, VM headroom, key proxy and lock broker alive
  - the bypass probe (§8.5) fails as it should
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
| Launcher | containers (OrbStack or Docker) with tmux inside | Docker or Podman with tmux inside (no VM) |
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
   - **Hook probe:** a scripted live probe confirms the §5.3 semantics table on the installed Claude Code
     version. It becomes `doctor`'s probe suite, and a version bump re-runs it.
   - **Container spike (go/no-go):** a session image runs interactive Claude Code through the key proxy;
     attach from Warp works; the bundle export and verifier push path works; the warm start is under 30 s.
   - `conductor setup-host`, the key proxy, the lock broker.
   - The judge `--providers` flag (M2).
   - The `cdt-hook` wrapper and the Stop pattern gate.
   - Fix done-gate bare-word false positives for non-conductor sessions.
   - Bridge hardening in `mcp-bridge` code (`origin:true` is in `src/index.ts` today).
   - Profile schema and tooling.
   - Lock and fencing.
   - Scrubber.
   - Bridge hardening, if the bridge mirror is enabled.
2. **Shadow:**
   - `conductor observe` gives zero-config value on day one.
   - **Historical replay** (Q1): the shape signals run over the last 200 merged PRs per repo, to seed
     thresholds and to measure how often reinvention or duplication actually occurred (P1).
   - The dashboard and badge are available from this step.
   - Then shadow mode on the real profile: triage and pack jobs run tool-less, the code index is built
     (structure, embeddings, graphify), and no sessions start.
   - Exit when per-class routing agreement is at least 90% over at least 30 items.
3a. **Assist, patterns:**
   - `conductor start`, packs and re-injection, turn classification, the tool gate, the Stop pattern
     gate, the ship state machine, notifications.
   - Approach and Drift direction checks run in **shadow**.
   - Shape signals are computed and recorded but don't refuse commits yet, for threshold calibration on
     real commits.
3b. **Assist, shape enforcement:** once **each layer** reaches a precision of at least 0.7 over at least
   30 signals on real commits (a layer that hasn't reached it enforces with its threshold raised one
   notch and is flagged in `doctor`) (M1):
   - All index layers enforce (structure, clones, dependencies, embeddings, graphify).
   - The Shape direction check enforces in the same step.
   - The conductor-side verifier is authoritative for every push.
4. **Auto-small:** enabled when `doctor` passes §8 and the step-2 threshold holds.
   - Direction checks move to `enforce` per checkpoint once they reach the §6.1 precision bar.
5. **Scoping proposals:**
   - Scoping and Scope-expansion direction checks run, plus graph and embedding candidate recall
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
- **Hooks (§5.3):**
  - every row is driven with a recorded payload and its decision asserted
  - the `cdt-hook` wrapper turns a crash, a timeout, a non-2 exit, and a missing inner script into an
    explicit deny with a `9xx` code
  - `continue:false` ends the turn
  - the Stop pattern gate blocks at most twice, then allows and records
  - host Claude sessions have no conductor hooks
  - the container user can't modify the managed settings
  - edit detection fires on `sed -i`, `tee` and script writes, not only on Edit/Write
  - the git pre-commit hook catches `commit -a`, `-C` and aliases; `--no-verify` is caught by the verifier
- **Live hook probe** (heavy, also in `doctor`): the §5.3 semantics table against the installed Claude
  Code version.
- **Isolation:**
  - from a session container, all of these fail: reading the HMAC key, the ledger or the API key;
    writing to `/conductor`, `/mirror` or `/workspace/base`; pushing to the remote; writing to the
    tracker; reaching host loopback services; attaching another session's tmux
  - a tampered spool entry is ignored
  - a bundle that fails verifier checks is never pushed, even when session-side checks passed or were
    skipped with `--no-verify`
  - the verifier runs no git hooks, filters or attributes from the bundle
- **Containers:** CPU and memory limits hold; the heavy-lock lease expires for a killed container; the
  warm-start target is met on a warm image; `teardown-host` restores the manifest.
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
- **Shape checks:**
  - fixture repos with an exact clone, a renamed clone, a second-case near-clone, a duplicate dependency, a
    graph-neighbor reinvention and an over-budget diff each produce the right signal
  - each enforced-outcome row in §6.2 ends (cap exhausted, index unavailable, budget exceeded): no
    deadlock
  - an exemption expires on file change and after 30 days
  - evidence fences untrusted strings
- **Offline index:** with the network blocked, embeddings and graphify still build; with an egress attempt
  injected, the graphify adapter fails closed; Prism flags are verified; denied paths and symlinks are
  never read; the per-worktree overlay matches a full rebuild.
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
- `docs/conductor/errors.md`, generated from the code registry. A test fails when a code is used but not
  documented, or documented but unused (M8).
- `docs/conductor/hooks.md`, `docs/conductor/index.md`, `docs/conductor/host-setup.md` (containers,
  key proxy, teardown)
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
