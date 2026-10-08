# Conductor — design

Status: draft for review · 2026-10-07

## 1. Why

A transcript audit of one month of work (406 sessions, 2,911 human turns; method and numbers in the
appendix) found the human spends most of their effort on the parts of agent work that are least about judgment:

- **Dispatch.** Copy-pasting simple tickets into a session is the main manual cost. It shows up as one
  turn per ticket, so turn counts undercount it.
- **Shipping direction (~20% of turns).** The same "raise draft PR → review loop → ready → watch reviewers
  → fix CI/conflicts → push" recipe is retyped, often verbatim.
- **Keeping orchestrators on track (~11%).** Handoff prompts, "read this digest", "we crashed, pick up",
  "you were supposed to dispatch a teammate". Orchestrators lose the original direction, including a
  ticket's own stated fix.
- **QA the agent didn't do (~5.5%).** Defects the human found by hand, mostly on surfaces nobody listed.
- **Evidence and environment recipes.** The same "how to log in to a preview, which creds, attach where"
  instruction, repeated across sessions. The existing bug-fix orchestrator can only produce local-stack UI
  evidence or test output.
- **Scoping.** Surfaces discovered mid-project instead of at project creation.

The conductor is a local, always-on system that picks up work, sizes it, starts sessions with the
right context, keeps them on track, and asks the human only when it has to. It learns from its own
steering so recurring corrections become rules.

## 2. Goals and non-goals

Goals
- Auto-start unambiguous small work to a draft PR with no human dispatch.
- Scope and order larger work into workstreams; auto-start the independent ones.
- Every session starts from a synthesized, cited context pack, and keeps it through long runs.
- Every judgment step proves it is complete before handing off.
- Evolve: recurring human steering becomes a proposed, backtested rule change.
- Run on macOS and Linux (including headless cloud boxes).
- Generic core; workplace and repo specifics live in a separate profile repo.

Non-goals
- Merging. The human always merges.
- Replacing the human's product decisions. The conductor batches and contextualizes questions; it
  does not answer them.
- A web dashboard (v1 uses the ledger, `conductor status`, and notifications).

## 3. Invariants

1. **One conductor at a time** (§7.1). Never two ticks running concurrently on a host, and never two
   hosts claiming the same work item.
2. **Nothing long-lived holds a model.** The always-on part is deterministic code. Every model call
   lives in a bounded Step or a session that ends.
3. **No step grades itself.** Every judgment step has a separate verifier (§5).
4. **Escalate, never guess.** Below-threshold decisions and exhausted loops notify the human.
5. **One heavy job at a time per host**, through the existing heavy-job lock. The session cap (default 4)
   is a ceiling on sessions, not on heavy jobs.
6. **Outward writes follow the profile's allowlist** (§8). Anything else asks.
7. **No human hand-labeling.** The eval loop labels from outcomes and adjudicators only.

## 4. Architecture

```
scheduler (launchd | systemd timer | cron)
   └─► conductor tick  (deterministic; singleton lock §7.1)
         ├─ Tracker.scan(scope)            → work items
         ├─ Step: triage                   → {size, ambiguity, missing info, sources}
         ├─ router: small ∧ clear → worker queue · else → scoping queue
         ├─ Step: scoping & ordering       → surfaces, workstreams, dependency graph
         ├─ Step: context pack             → Source adapters → cited pack file
         ├─ Launcher.start(session, pack)  → tmux session (≤ cap; leases §7.2)
         ├─ Step: worker (inside session)  → proven change + evidence
         ├─ ship state machine             → stack: drafts → bottom-up undraft/resolve/redraft
         ├─ Notifier.notify(question, attach action)
         └─ eval loop (daily)              → ledger + transcripts → rule diff → profile repo
```

### 4.1 Core packages (in this repo)

| Package / file | Responsibility |
|---|---|
| `conductor/` | Tick loop, queue, cap, leases, Step runner, ship state machine, ledger (SQLite, WAL), CLI (`tick`, `status`, `attach`, `park`, `--dry-run`) |
| `conductor/adapters/` | Interfaces plus built-ins: `Scheduler`, `Tracker`, `Source`, `Launcher`, `Notifier`, `Reviewer`, `Lease` |
| `conductor/profile/` | Profile schema (Zod), validator, `conductor profile init` scaffolder |
| `judge/questions/` | New judge questions used by Step verifiers and the router |
| `config/hooks/` | Pack injection at SessionStart; context guard re-injects the pack; lease heartbeat |

### 4.2 Adapter interfaces

| Interface | Methods | Built-ins (v1) |
|---|---|---|
| `Scheduler` | install, uninstall, status | launchd, systemd-user, cron |
| `Tracker` | scan(scope), read(id), comment, attach, setStatus, assign | Linear |
| `Source` | find(item) → refs, fetch(ref) → record | tracker, chat (Slack), notes dir (markdown vault), memory (Prism), transcripts |
| `Launcher` | start, attach-command, list, send-keys, reap | tmux |
| `Notifier` | notify(title, body, action) | macOS notification (click runs an attach command in the configured terminal), ntfy, chat DM |
| `Reviewer` | comments(change), resolved(change) | GitHub review threads (bot logins from profile) |
| `Lease` | acquire, renew, release, holder | local lockdir, shared (see §7.1) |

Adding a tool is a new adapter, not a core change.

### 4.3 Profile repo (private, outside this repo)

The core defines the layout; the profile fills it. Validated on every tick (invalid profile → no tick,
one notification).

```
<profile-root>/
  profile.yaml            # workplace: tracker + scope query, chat, notes dir, memory, size rubric,
                          # write allowlist, session cap, notifier, scheduler, hosts
  repos/<repo>.yaml       # per repo: path, evidence recipes, environment recipes, ship recipe,
                          # reviewers, heavy-job commands
  rules/                  # learned rules (eval-loop output, human-approved)
  prompts/                # optional overrides for Step prompts
```

Secrets are never stored in the profile, only pointers to them (e.g. "creds come from secret store X, key Y").

## 5. The Step contract

Every judgment step has the same shape:

```
Step = { inputs, output schema, deterministic checks, verifier, max rounds, escalation }

produce → deterministic checks → verifier → pass ── yes ─► ledger + hand off
              ▲                             │ no (reasons)
              └──── revise with reasons ────┘   rounds exhausted → notify, park
```

- **Generation** (packs, scoping maps, fixes) runs in fresh Claude sessions.
- **Decisions** (yes/no, pick one) are judge `QuestionModule`s on the existing chain
  `jev → agent CLI → rules`, with thresholds. Below threshold escalates instead of guessing.
- **The verifier** is mostly judge questions. When the chain escalates, the fallback is a separate
  refutation session, never the producer.
- Every round is a ledger row: step, item, round, verdict, reasons, provider, cost.

| Step | Complete when |
|---|---|
| Triage | Every in-scope item has `{size, ambiguity, missing-info list, sources}`. The verifier re-sizes a sample; disagreement → re-triage. Existing triage verdicts on the item (from other bots) are inputs. |
| Context pack | Contains the item's intent, **its own stated fix verbatim if any**, acceptance checks, surfaces, and a citation per claim. Verifier: a fresh agent answers probe questions from the pack alone. |
| Scoping & ordering | Every surface maps to a workstream; dependency graph is acyclic; independence claims are checked against expected file overlap; an adversarial "what surface is missing?" pass repeats until no new surface (round cap). |
| Worker | The reproduce check fails before and passes after; evidence from **every applicable evidence recipe** in the repo profile; judge resolution-check against the original item text. |
| Ship | From reviewer/CI state, not the agent's report: CI green and every reviewer thread resolved at each stack level. |
| Eval-loop proposal | The rule change is backtested on ledger history and shows improvement before it reaches the human. |

## 6. Flows

### 6.1 Intake and routing
1. Tick scans the profile's scope (e.g. "assigned to me" ∪ "unassigned in team triage" ∪ "projects I lead").
2. New or changed items go to triage.
3. Router: `size ≤ profile.autoStartMaxSize` (default `XS`) `∧ ambiguity = none` → worker queue. Else → scoping queue.
   Unassigned items are assigned to the human when a session starts on them.
4. Scoping produces workstreams. Independent workstreams are queued; dependent ones wait on their parents.
5. Questions from any step are batched per item into one notification.

### 6.2 Sessions
- Started in tmux with the pack path injected at SessionStart.
- The context guard re-injects the pack (not only a digest nudge) when context crosses its threshold
  and after compaction.
- Sessions end when their Step completes. A session past `session.maxAgeHours` (default 8) or `session.maxContextTokens` (default the context-guard threshold) is checkpointed,
  reaped, and relaunched from pack + checkpoint.

### 6.3 Ship state machine (generic)
`build drafts (stacked) → stack complete → for each level bottom-up: undraft → resolve reviewers + CI → re-draft → next`.
The profile names the reviewers, labels, and CI. Undrafting requires approval unless the profile's
allowlist includes it (default: not included).

### 6.4 Eval loop (daily)
1. Read the ledger plus transcripts of conductor sessions.
2. Detect recurring human steering (same correction or instruction across ≥ `eval.minSessions` sessions, default 3), the way the
   original audit found the evidence and ship recipes.
3. Draft a rule change for the profile (`rules/` or a recipe).
4. Backtest it on ledger history using outcome labels: merged without rework, steering-turn count,
   reverts, escalations.
5. Send the diff to the human as one notification; nothing changes until they approve.

## 7. Concurrency and failure handling

### 7.1 Singleton conductor
- **Per host:** each tick takes an exclusive lock before doing anything:
  - It's a lock directory under the state dir, holding an owner file `{pid, host, boot-id, started}`.
    This extends the `mkdir` mutex in `config/lib/locks.sh`.
  - Acquire succeeds only if the directory is absent, or its owner is provably dead: same host and
    boot-id, and the pid is not alive. The second case is taken over atomically.
  - A tick that can't get the lock exits 0 immediately and does nothing. Scheduler overlaps, manual
    `tick`, and two schedulers installed by mistake are all no-ops.
- **Per host, second guard:** the SQLite ledger holds a `conductor_lease` row, updated in a
  `BEGIN IMMEDIATE` transaction at tick start. A second tick that somehow got past the lock fails here.
- **Across hosts:** v1 allows one active host per profile. `profile.yaml: hosts.active` names it, and
  a tick on any other host refuses to run.
  - Before any session starts on an item, the work-item claim is also written to the tracker (assign
    plus a claim marker), then re-read. If the re-read shows another host's marker, the claim is
    abandoned.
  - Multi-host scheduling (a shared `Lease` adapter) is out of scope for v1. The interface exists so
    it can be added later.
- `conductor status` shows the lock holder; `conductor doctor` reports stale locks and duplicate
  scheduler installs.

### 7.2 Session leases
Each running session holds a lease renewed by a hook heartbeat. Lease expiry → relaunch from pack +
checkpoint; after 2 relaunches → notify and park.

### 7.3 Other failures
| Failure | Handling |
|---|---|
| Conductor crash / sleep / reboot | Idempotent tick; next tick reconciles the ledger with `Launcher.list()`. |
| API error or sleep mid-response | Detect the stall; judge wake-gate decides whether to send `continue` via `send-keys`; escalation notifies. |
| Resource contention | Session cap from profile (default 4); heavy work queues on the host's heavy-job lock. |
| Memory growth | Sessions end with their Step; age/token budget reaping (§6.2). |
| Step loop exhausted | One notification with the verifier's open reasons; item parked. |
| Invalid profile | No tick; one notification naming the validation errors. |
| Hook false positives | Prerequisite: fix `config/hooks/done-gate.sh` bare-word matching (it fires on questions and negations). Audit other Stop hooks for the same issue. |

## 8. Outward writes

Default allowlist (the profile can narrow or widen it):
- **Pre-approved:** push branches, open draft change requests, attach change requests and reviewed evidence to the item,
  one status comment per milestone, move the item to in-progress or in-review, apply profile-named labels.
- **Always ask:** undraft, close or cancel items, create new items, any chat message, anything touching
  production data.

## 9. Portability

| Concern | macOS | Linux / cloud box |
|---|---|---|
| Scheduler | launchd | systemd user timer (cron fallback) |
| Launcher | tmux | tmux |
| Notifier | native notification; click opens the configured terminal with the attach command | ntfy or chat DM carrying `ssh <host> -t tmux attach -t <session>` |
| Paths | `$AW_STATE_DIR` (default `~/.agentic-workflow`), no hard-coded home paths | same |
| CI | — | the conductor test suite runs on Ubuntu as well |

## 10. Rollout

1. **Prerequisites:** fix done-gate false positives; profile schema and validator; singleton lock.
2. **Shadow mode (about a week):** triage and routing run, no sessions start. The eval loop scores
   routing against what the human actually did. Outcome labels only.
3. **Auto-start small work** once shadow agreement passes the profile's threshold.
4. **Scoping pilot** on one led project. Backtest: run scoping on the project's original brief and
   measure recall of the issues filed later.
5. **Eval loop** proposals enabled.

## 11. Testing

- Vitest with in-memory SQLite and fake adapters for the tick loop, router, Step runner (pass,
  fail-then-pass, exhausted rounds, escalation), ship state machine, and lease expiry.
- Singleton tests: two concurrent ticks (one exits as a no-op), a stale lock with a dead pid (taken
  over), a stale lock from another boot-id (taken over), a live owner (respected), a non-active host
  (refuses).
- Real-process tests for the tmux launcher and each notifier, run on macOS and Ubuntu.
- `conductor tick --dry-run` prints every action without side effects.
- Profile validator tests on fixture profiles (valid, missing fields, secret-looking values rejected).
- Judge questions use judge's existing eval harness.
- Merge gate: add `conductor` to the typecheck and test lists in AGENTS.md.

## Appendix: audit method

- Extract every non-machine human turn from `~/.claude/projects/**/*.jsonl` (excluding sidechains, tool
  results, and injected text) with context: active skills, context-guard state, token count, and the
  preceding agent message.
- Classify a stratified sample of 450 turns by reading them.
- Floor counts across all turns using exact-phrase patterns.

Results: ship direction ~20%, product decisions ~20%, orchestration continuity ~11%, research ~10%,
human-found defects ~5.5%, dispatch ~5.5%, mid-flight scope ~4%, QA direction ~4%, evidence and
environment recipes ~3% (near-verbatim repeats).
