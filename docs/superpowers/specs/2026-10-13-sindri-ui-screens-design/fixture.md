# The mockup fixture (one scenario across every screen)

Every mockup in `mockups/` renders this one state. A number, id, title or word that differs from this file is a
defect. Times are on 2026-10-12; "now" is 12:26.

## Health (one word, worst verdict wins)

`health.word` = **Degraded** on every page: the `index` service failed and the profile changed since approval.
The pill reads `◐ Degraded · updated 2s ago · index failed · profile changed` everywhere (System's own status line
repeats it). "Healthy" appears nowhere in this fixture.

## Navigation counts

Home · Workflows **12** · Work **12** · Recipes **7** · Context · Agents **4** · System

## Items (Work = 12 items)

| Id | Title | Repo | Source | State | Step | Agent | Since |
|---|---|---|---|---|---|---|---|
| ITM-01JB4 | Context map review for tabs.layout | agentic-workflow | Linear ENG-490 | ▲ escalated · gate (gate.verify.judge 0.62 < 0.80) | gate.verify.judge | — | 09:10 (2h 56m) |
| ITM-01JD2 | Re-scope requested: drop the iOS head from this item | sindri-app | request REQ-01JD4 from a session | ? needs decision | — | — | 11:40 (26m) |
| ITM-01JA1 | Onyx adapter secret pointers | sindri-app | Linear APP-212 | ◎ with you · taken over 12:01 | produce | Claude Code (attached in Warp) | 12:01 (25m) |
| ITM-01JC8 | Retry storm in outbox dispatcher | agentic-workflow | Linear ENG-503 | ▌ parked · no-progress (checks failed twice with the same findings) | step.2 · round 3/3 | — | 11:54 (12m) |
| ITM-01JE7 | Add needs-you ack route | agentic-workflow | Linear ENG-511 | ● running · live | produce · round 2/3 | Claude Code in Warp · snd-01JE7.2 | 12:20 (6m) |
| ITM-01JE9 | Tester: decision race integration test | agentic-workflow | Linear ENG-514 | ● running | checks · round 1/1 | Codex · snd-01JE9.1 | 12:24 (2m) |
| ITM-01JD9 | Judge outbox drain on shutdown | agentic-workflow | Linear ENG-508 | ● running | produce · round 1/3 | Claude Code in Warp · snd-01JD9.1 | 12:25 (1m) |
| ITM-01JD6 | Services page job panel | sindri-app | packet task t4 of PRJ-sync (ws-2) | ● running | checks | factory worker · snd-01JD6.1 (tool Step, no terminal) | 12:26 (0m) |
| ITM-01JF0 | Stream limits 429 fallback | agentic-workflow | triage sentry #8809 | ○ queued | — | — | queued 1h |
| ITM-01JF3 | doctor views line | sindri-app | packet task t6 of PRJ-sync (ws-3) | ○ queued | — | — | queued 52m |
| ITM-01JF4 | Tray badge on Linux | sindri-app | packet task t7 of PRJ-sync (ws-3) | ○ queued | — | — | queued 40m |
| ITM-01JF8 | Receipt verify route | sindri-app | packet task t8 of PRJ-sync (ws-4) | ○ queued | — | — | queued 12m |

Summary lines: Home `5 need you · 4 running · 1 with you · 4 queued`; Work `12 items · 2 waiting on you · 1 with
you · 1 parked · 4 running · 4 queued`. Home's Running panel shows the four running rows and the with-you row
(with-you first), then "Show all 5 in Work". Home's Queued panel shows the four queued rows.

Needs you (5, most urgent first): ITM-01JC8 park · REQ-01JD4 decision (re-scope ITM-01JD2) · ITM-01JB4 escalated ·
service `index` failure · profile changed (2 keys: `services.index.every`, `context.maxBudget`).

ITM-01JE7's item sheet: subject `Linear ENG-511 · repo agentic-workflow · no project · feature:needs-you.ack`;
facts `fact:item` (ENG-511 intake record), `fact:spec` (spec.md sha256:1c0e…), `fact:output` (round 1: 4 files,
+182 −9; round 2 running), `fact:findings` (round 1: 2 findings: second ack returns 500; route missing from the
catalog), `fact:evidence` (after checks pass). Children: none.

## Root workflows (Workflows = 12 open)

| Address | Recipe | Subject | State | Since | Now |
|---|---|---|---|---|---|
| INC-14 | incident.response@5d90e1b2 v4 | PagerDuty P-2211 · Outbox retry storm | mitigate r1/3 ⇄ verify; comms child running | 11:02 (1h 24m) | agent rolling back in ITM-01JG2 · comms posting |
| sentry #8812 | triage.alert@d41f2a07 v2 | sentry #8812 TypeError in tabs.layout | classify · desk | 12:22 (4m) | needs your severity (desk answer) |
| sentry #8809 | triage.alert | sentry #8809 429s on stream | done 12m → ITM-01JF0 queued | 11:58 | done |
| pagerduty P-2211 | triage.alert | PagerDuty P-2211 | ▲ escalated: gate.severity 0.91 → INC-14 opened | 11:02 | accept at a terminal |
| sentry #8801, #8797, #8790 | triage.alert | three done rows (collapsed as "… 3 more") | done | — | — |
| PRJ-sync | workflow.project@a8c21e7f v12 | Linear APP · Offline sync · PRD-sync.md | build 4/9 (design done, packet 3f1c09 frozen) | 09:14 (3h 12m) | ITM-01JD6 checks · 4 done · 1 running · 1 parked · 3 queued |
| PRJ-exports | workflow.project | Linear ENG · Billing exports · PRD-exports.md | design · desk (awaiting import) | 10-11 16:02 (20h) | with you: import the design output |
| PRJ-tabs | workflow.project | Linear ENG · Tab manager v2 · PRD-tabs.md | scoping r2/3 (challenge) | 11:40 (46m) | challenger round 2 · 84k / 600k |
| LIN-ENG, LIN-APP | intake.linear | the two Linear teams | polling every 15m | — | last 12:15 · 2 new items |

PRJ-sync's nine children (packet 3f1c09, four workstreams): ws-1 UI: ITM-01JC3 Sync status pill (● done, PR #241
merged), ITM-01JC5 Conflict resolver dialog (● done, PR #243 merged); ws-2 Store: ITM-01JC1 PUT /sync/changes
(● done, PR #239), ITM-01JC2 Outbox queue (● done, PR #240), ITM-01JD6 Services page job panel (● running checks);
ws-3 Mobile: ITM-01JD7 Background sync (iOS) (▌ parked no-progress, mirrored to the parent),
ITM-01JF3 doctor views line (○ queued), ITM-01JF4 Tray badge on Linux (○ queued); ws-4 Rollout: ITM-01JF8 Receipt
verify route (○ queued). 4 done · 1 running · 1 parked · 3 queued = 9.
(ITM-01JD7 is a child only; it is not in Work's 12 because PRJ-sync's parked child is mirrored on the parent row.)

Home's Workflows strip: `WORKFLOWS 12 open`, rows INC-14, sentry #8812, PRJ-sync, PRJ-exports, PRJ-tabs, then
"… 7 more".

## Agents (4 live · 1 with you · 2 waiting on you)

| Session | Agent | Item · step | Round | Tokens | Since | State |
|---|---|---|---|---|---|---|
| snd-01JE7.2 | Claude Code | ITM-01JE7 · produce | 2/3 | 84k / 200k | 6m | ● live, nobody attached |
| snd-01JD9.1 | Claude Code | ITM-01JD9 · produce | 1/3 | 22k / 200k | 1m | ● live |
| snd-01JA1.3 | Claude Code | ITM-01JA1 · produce | — | 41k / 200k | with you 25m | ◎ with you |
| snd-01JE9.1 | Codex | ITM-01JE9 · checks | 1/1 | 12k / 200k | 2m | ● live (tool Step, no terminal) |
| snd-01JD6.1 | factory worker | ITM-01JD6 · checks | — | 3k / 200k | 0m | ● live (tool Step, no terminal) |
| (model run) | challenger | PRJ-tabs · scoping / challenge | 2/3 | 84k / 600k | 46m | ◌ model call, no session |

"4 live" counts the four tmux sessions (JE7, JD9, JE9, JD6); the with-you session and the model run are listed
separately. Waiting on you: ITM-01JC8 (parked), ITM-01JB4 (escalated). Budget today: Claude Code $3.10 / $8.00,
Codex $1.02 / $4.00, Cursor $0.00 / $2.00 (cursor not found on this host), total $4.12 / $14.00.

## Recipes (7)

As drawn in the spec §4.3; `scoping.project` is "changed since approval" (pinned block step.scope.draft v2 → v3).

## Context

Map v13 current, draft v14 (46 proposals: 31 deterministic, 15 model; 3 orphaned links). Anchor
`feature:tabs.tab-manager`. Sources: linear ● synced 2m ago; linear:doc ● 2m ago; slack ◐ 2h behind; onyx ◐ 2h
behind; git ● 12:21 (code index ✕ nightly failed SND-INDEX-012; symbols lens "index not built until CTX phase 9").

## System

Degraded: code index ✕ (nightly 03:00, SND-INDEX-012), Onyx ◐ 2h behind, cursor ○ not found. Engine: 4 open on
Temporal (ITM-01JE7, ITM-01JE9, ITM-01JC8, ITM-01JB4) plus the roots PRJ-sync, PRJ-tabs, PRJ-exports, INC-14 and
the triage instances; the engine table lists the six rows drawn in §4.6 with ITM-01JD9 and ITM-01JD6 added.

## Glyph vocabulary (one legend, every page)

- Work state: `●` done or live · `◐` in progress · `○` not started or queued · `◎` with you · `▌` parked ·
  `▲` escalated · `◌` model call, no session · `⇄` loop.
- Health: `✓` ok · `◐` degraded · `✕` failed · `○` absent.
- Colour follows the glyph identically everywhere: `▌` and `▲` and the needs-you chip in `--attn`; `✕` in
  `--error`; live `●` and `✓` in `--success`; every other glyph in ink shades. Always with a word.

## The protected-action label (one pattern)

A locked secondary button: lock glyph, the verb, a middle dot, "terminal": `⌂ Grant rounds · terminal`,
`⌂ Accept · terminal`, `⌂ Approve · terminal`, `⌂ Re-approve · terminal`, `⌂ Promote · terminal`. Its `title` is
"Needs a typed confirmation at a terminal"; the copyable `$ sindri …` line sits under it where the row has room.
Never prose, never a dash.
