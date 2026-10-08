# Conductor — design

Status: draft for review, revision 2 · 2026-10-07
Revision 2 addresses `/autoplan` round 1 (consolidated verdict NEEDS_WORK, 21 HIGH). Finding ids
(H1–H21, M1–M22, L1–L6) refer to `~/.agentic-workflow/<repo-slug>/plans/conductor/consolidated-review.md`.

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
- Generic core; workplace and repo specifics live in a separate private profile repo.

### Non-goals
- Merging. The human always merges.
- Answering product questions. The conductor batches and contextualizes them; it never answers them.
- A web dashboard (CLI, notifications, ledger only).
- Multi-host scheduling in v1.

### Success metrics (baselines measured in rollout step 0)
| Metric | Source | Target after 4 weeks of auto-start |
|---|---|---|
| Human turns per merged PR | transcripts + GitHub | −40% vs baseline |
| Shipping-direction turns per merged PR | transcript classifier (the audit's patterns) | −75% |
| Continuity turns (handoffs, "pick up", role corrections) per week | transcript classifier | −50% |
| Copy-paste dispatches of auto-start-eligible items | ledger vs transcripts | 0 |
| Interrupting notifications per merged PR | ledger | ≤ 2 |
| Auto-started PRs reworked or reverted | GitHub + ledger | ≤ the rate for human-started PRs |
| Model cost per merged PR | ledger | within `budget.perItem` |
| Human "wrong path" corrections per merged PR | transcript classifier | −50% (direction checks should catch these first) |

### Kill criteria
- **Auto-start:** if after 4 weeks human turns per merged PR is down less than 20%, or the rework rate of
  auto-started PRs exceeds that of human-started PRs, auto-start is turned off (`mode: assist`). The
  conductor keeps running manual start, packs and the ship recipe.
- **Eval loop:** if fewer than half of approved rules hold their backtested gain over the next 20 items,
  proposals stop.

## 3. Invariants

1. **One conductor at a time** per host, fenced (§9.1). Across hosts, v1 runs one active host and treats
   cross-host exclusion as best-effort with duplicate-tolerant outcomes (§9.4).
2. **Nothing long-lived holds a model.** The tick is short deterministic code; model work runs in bounded
   jobs and sessions that end.
3. **No step grades itself.** Verifiers are separate; the conductor runs the worker's reproduce check
   itself (§6). **No path goes unchallenged:** approach, scoping and drift checkpoints get a
   cross-model direction check before more effort is committed (§6.1).
4. **Escalate, never guess.** Below-threshold decisions and exhausted loops notify and park.
5. **One heavy job at a time per host.** The session cap is a ceiling on sessions, not heavy jobs.
6. **Deny by default inside conductor sessions.** A conductor gate enforces the profile allowlist,
   fails closed, and is not disableable from inside a session (§8.1).
7. **Untrusted text is data, never instructions** (§8.3).
8. **No secrets in packs, ledger, logs, notifications or eval corpus** (§8.4).
9. **No human hand-labeling.** The eval loop labels from outcomes and adjudicators only.

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
| Direction checks over the MCP bridge (approach, scoping, drift, agent-initiated) | Human participating live in a bridge thread |

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
| `mcp-bridge/` | Transport for direction checks (existing conversations/messages/unread API); managed as a service by the scheduler |
| `config/hooks/` | SessionStart pack injection, compaction re-injection, takeover detection, heartbeat spool |

### 5.2 Storage (M7)
- The conductor owns `$AW_STATE_DIR/conductor/ledger.db` (SQLite, WAL). It is the only writer.
- Hooks never write to the DB. They append events to `$AW_STATE_DIR/conductor/spool/<session>.jsonl`, and
  the tick ingests them.
- Judge decisions are referenced by judge's decision id, not copied. Ledger rows record judge score and
  threshold (L5).
- The schema lives in `planning/ERD.md` with versioned migrations. `doctor` detects schema skew (M19).
- Ledger files and the state dir are mode 0700, outside any session's working tree. Conductor sessions run
  with a gate that denies writes under `$AW_STATE_DIR/conductor/` (STRIDE tampering).

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
| Worker | tmux session (`/bugFixOrchestrator` in v1) | **The conductor re-runs the recorded reproduce check itself** in a clean worktree at the PR head: it must fail on the base and pass on the head. Path guard (§8.5) passes. | Judge resolution-check against the original item text |
| Ship | conductor code | Reviewer threads resolved and CI green, read from the Reviewer adapter and CI, never from agent output | — |
| Eval proposal | job | Typed rule schema; forbidden fields untouched (§7.4) | Holdout backtest |

### 6.1 Direction checks ("is this the right path?")
Step verifiers ask *is this output complete?* Direction checks ask a different question: *is this the right
thing to be doing at all?* The audit found this was asked only by the human: "the ticket description
suggests the fix, why are we not doing it", "that's a messy workaround, not the fix we want", "wait, why
are you adding that branch?". A direction check is a short, structured dialogue between models before
effort is committed. The MCP bridge carries it.

**Checkpoints** (where a check runs):

| Checkpoint | When | Typical question |
|---|---|---|
| Approach | A worker has a root cause and plan, before it writes the fix | Does this solve the item as written? Does it match the stated fix? Is there a simpler or more correct path? |
| Scoping | Before a scoping proposal goes to the human | Are these the right workstreams? Is anything here the wrong problem? |
| Drift signal | Any of: 2 failed fix rounds; the same verifier reason twice; diff outside the predicted file set; a human correction in the session; elapsed time > 2× estimate | Should we keep going, change approach, or stop and ask? |
| Scope expansion | A session proposes new work items or widening the change | Is this expansion necessary for this item, or a separate item? |
| Agent-initiated | A session asks for one itself via `mcp: agentic-bridge/send_context` to recipient `conductor` with `kind: direction-check` | Whatever the session is unsure about |

XS-clear items skip the Approach checkpoint unless a drift signal fires.

**Protocol** (one bridge conversation per check):
1. The conductor opens a conversation (UUID) and records it in the ledger.
2. **Position:** the proposer posts:
   - the pack's Task and Stated fix, verbatim
   - its intended approach and the alternatives it rejected, with reasons
   - evidence refs
3. **Challenge:** 1–2 challengers each post objections, tied to the item's intent, the stated fix,
   simplicity and risk. At least one challenger runs on a **different model or provider** from the
   proposer, using judge's chain (`jev` → `codex` / `cursor` / other agent CLIs → `claude`), so errors are
   less correlated. Challengers are read-only (code-read tools only, no writes, no network) and get the
   same trusted/untrusted pack split (§8.3).
4. **Reply:** the proposer answers each objection: concede, rebut with evidence, or revise.
5. Steps 3–4 repeat at most `direction.maxExchanges` times (default 2).
6. **Decision:** a judge question `direction-verdict` with outputs `proceed | revise | escalate` reads the
   thread. Below its threshold, the result is `escalate`.
   - `proceed`: work continues.
   - `revise`: the proposer gets the agreed objections as its next instructions.
   - `escalate`: a needs-answer notification with both positions in one screen ("Worker wants A because…;
     challenger argues B because…; recommended: …"). The human picks. This is a product decision, which
     the conductor never makes.

**Records and limits:**
- Every exchange is scrubbed and stored in the ledger by conversation id. `conductor why <item>` shows the
  thread.
- Outcome labels for the eval loop include whether a `revise` led to a merged PR without rework, and
  whether `proceed` items later got a human "wrong path" correction.
- Checks count against `budget.perItem`. On budget breach the check escalates rather than skipping.
- **Transport:** the bridge runs as a scheduler-managed service (launchd KeepAlive in v1). `doctor`
  checks it. If the bridge is down, the check doesn't run silently in some other way: the checkpoint
  escalates with the code `CND-BRIDGE-001`.

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
  - its own git worktree and branch (the branch is the claim, §9.4)
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
- **Backtest:** gated on ≥ 30 completed items. Uses an older-70% / newer-30% holdout and outcome labels
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
  1. Create `lock.tmp-<pid>/owner.json` holding `{pid, host, bootId, startedAt, epoch}`.
  2. Rename it to `conductor.lock`.
  - The rename is atomic, and it fails if `conductor.lock` exists, because that directory is never empty.
- **Stale takeover:** if the owner is provably dead (same host, same `bootId`, pid not alive, or a
  different `bootId`), rename `conductor.lock` → `conductor.lock.stale-<ulid>`. Only one taker wins that
  rename. Then acquire.
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
- **Progressing:** the transcript mtime changed, the pane content hash changed, or a spooled heartbeat
  arrived within `liveness.stallAfter` (default 20 min).
- **Known waits are not stalls:** a permission dialog, a question to the human, or a wait on the heavy-job
  lock.
- **Relaunch is kill-before-relaunch:**
  1. Kill the pane.
  2. Confirm it's gone.
  3. Relaunch on the same branch and worktree. The branch claim prevents a second session on it.
  - After 2 relaunches the item is parked.
- **Sleep:** when the tick sees a wall-clock jump larger than its interval, it extends every lease by the
  gap before evaluating them.

### 9.3 Human takeover (design H14)
- Takeover is detected from human keystrokes in a conductor pane: a UserPromptSubmit event from that
  session whose origin is human. The session moves to `taken-over`.
- While taken over: no reaping, no `send-keys`, the lease is paused, and the item shows as `with you` in
  `status`.
- `conductor handback <item>` returns the session to the conductor.

### 9.4 Claims across hosts (H9)
- v1 has one active host (`hosts.active`), and other hosts refuse to tick.
- The **atomic claim** is the branch: the Launcher creates `cdt/<item>/<host>-<ulid>` with a create-ref
  that fails if any `cdt/<item>/*` branch exists.
- Tracker markers are advisory, and are trusted only when written by the conductor's identity.
- Cross-host exclusion is best-effort. Outcomes are duplicate-tolerant: a second branch for the same item
  parks itself.

## 10. Human-facing surfaces (H13–H15)

### 10.1 Notification taxonomy and budget
| Kind | Interrupts | Title pattern | Body | Action |
|---|---|---|---|---|
| needs-answer | yes | `[repo] ITEM: question (n)` | First question with a recommended default | attach, or `conductor answer ITEM` |
| needs-approval | yes | `[repo] ITEM: approve <action>` | What, why, and the denied call or rule id | `conductor approve <id>` |
| parked | yes, batched hourly | `[repo] ITEM: parked: <reason code>` | Reason, last-round output path | `conductor why ITEM` |
| fyi | no | `[repo] ITEM: <milestone>` | One line | — |
| digest | no; at `notify.digestTimes` | `conductor: N waiting on you` | Grouped list | `conductor status` |

- Interrupting notifications are capped at `notify.maxPerHour` (default 4); overflow goes to the digest.
- The same error code is deduplicated with a cool-down (M21).
- Every notification includes the error or item id **and the command as text** (accessible, and works
  from any channel).
- Voice: plain, first person ("I parked FRN-123 because the reproduce check passed on the base"), states
  the reason and the next step, never blames.

### 10.2 Question lifecycle
1. `asked`
2. Reminder after `questions.remindAfter` (default 4 h, in the digest).
3. Auto-park after `questions.parkAfter` (default 24 h).

Answers: questions raised inside a session are answered in the session by attaching. Questions from jobs
(triage, scoping) are answered with `conductor answer <item>`, which opens `$EDITOR` with the question
template and records the answer as human-origin. `conductor snooze <item> <dur>` delays one.

### 10.3 CLI
| Command | Purpose |
|---|---|
| `conductor status` | Grouped: waiting on you, with you, parked, running, queued. Columns: item, title, step, round/cap, age, next action |
| `conductor why <item>` | Triage verdict, router inputs, every Step round with verifier reasons, direction-check threads and verdicts, judge scores and thresholds, denials |
| `conductor ledger [--item --step --since]` | Raw ledger query |
| `conductor start <item>` | Manual dispatch (replaces copy-paste in `assist` mode) |
| `conductor attach <item\|cdt-id> [--print]` | Attach to a session, or print the command. Prints the tmux detach key (L6) |
| `conductor answer \| approve \| reject \| snooze` | Human responses |
| `conductor park \| unpark <item> [--hint "..."]` | Park or resume with a hint |
| `conductor handback <item>` | End a takeover |
| `conductor pause \| resume` | Global kill switch (also `AW_CONDUCTOR_DISABLE=1`) |
| `conductor doctor` | Checks listed in §11.5 |
| `conductor tick [--dry-run]` | One tick, or print every action without side effects |
| `conductor shadow report` | Routing agreement vs what the human actually did |
| `conductor rules show \| approve \| reject` | Eval-loop proposals |
| `conductor profile init \| validate \| explain \| migrate \| approve` | Profile tooling (§11.1) |
| `conductor scheduler install \| uninstall \| status [--dry-run]` | Scheduler (§11.3) |

**Output contract:**
- Plain-text state words; state is never shown by color or glyph alone.
- `--json` on every read command.
- `NO_COLOR` and non-TTY output are honored.
- Exit codes: `0` ok, `1` attention needed, `2` error.
- Errors carry stable codes (`CND-<AREA>-<NNN>`) that link to `docs/conductor/errors.md`.
- Empty states print explicit text ("Nothing is waiting on you. 3 running, 1 queued.").

### 10.4 Silent-failure floor
- Notifier fallback chain: macOS notification → `$AW_STATE_DIR/ATTENTION` file plus a banner in `status`
  output. ntfy and chat are added when their adapters exist.
- `doctor` checks the bridge is reachable (direction checks depend on it).
- `doctor` sends a test notification and flags a notifier that hasn't delivered within 24 h.

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
0. **Measure.** Turn the audit extraction into `scripts/transcript-audit/` (L4). Record the baselines for
   §2, and the share of the last 60 days of in-scope items that would have been XS, clear and trusted
   (M3).
1. **Prerequisites:**
   - Fix done-gate bare-word false positives, and make done-gate conductor-aware (M8).
   - Profile schema and tooling.
   - Lock and fencing.
   - Scrubber.
2. **Shadow mode.** Triage and pack jobs run tool-less. No sessions start. Lasts until
   `conductor shadow report` shows ≥ 90% routing agreement over ≥ 30 items (M2).
3. **Assist mode:**
   - `conductor start <item>`, packs and re-injection, the ship state machine, notifications, and
     direction checks at the Approach and drift checkpoints.
   - This removes copy-paste dispatch, retyped shipping direction and handoffs while every start stays
     human-initiated.
4. **Auto-small:** enabled when `doctor` passes §8 and the shadow threshold is met.
5. **Scoping proposals:**
   - Piloted on one project the human leads.
   - Backtest: run scoping on the project's original brief and measure recall of the issues filed later.
6. **Eval loop:** proposal-only, once the ledger holds ≥ 30 completed items.

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
- **Direction checks:**
  - every checkpoint trigger fires
  - a challenger on a different provider is selected when one is available
  - the exchange cap is enforced
  - `escalate` produces one needs-answer notification
  - bridge down → `CND-BRIDGE-001` escalation, never a silent skip
  - an agent-initiated request via the bridge is picked up from unread
- **Eval loop:** non-human-origin turns are excluded, forbidden-field rules are rejected, the holdout split
  is enforced.
- **Adapters:** `adapterContractTests` for every built-in and fake.
- **Real-process tests** (tmux, macOS notifier) are tagged `heavy` and excluded from the default
  `npm test`, per the one-heavy-job rule.
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
- Claims and branches are namespaced by user (`cdt/<user>/<item>/…`), so two humans on the same tracker
  never claim each other's items. Their scopes come from their own profiles.
- Shared recipes can be copied between profile repos. A shared team profile layer is out of scope for v1.

## Appendix: audit method
- Extract every non-machine human turn from `~/.claude/projects/**/*.jsonl` (excluding sidechains, tool
  results, and injected text) with context: active skills, context-guard state, token count, and the
  preceding agent message.
- Classify a stratified sample of 450 turns by reading them.
- Floor counts across all turns using exact-phrase patterns.
- Rollout step 0 moves the scripts into `scripts/transcript-audit/` so the audit can be reproduced.
