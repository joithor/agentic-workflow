# Sindri spec-to-proven-PR line: the line contract, design, plan and the per-phase child

**Status:** design, revision 1 (draft; not built until every station is specced, LN1) · **Date:** 2026-10-15 ·
**Owner decisions:** 2026-10-15 (§1) · **Builds on:** the scoping station spec
`2026-10-15-sindri-scoping-station-design.md` (SC, #258), rev 3.1, branch `docs/sindri-scoping-station`; the
decision-tree recipes spec `2026-10-15-sindri-decision-tree-recipes-design.md` (DT, #240), rev 4; the parked PR
babysitter and docs sync draft `2026-10-15-sindri-pr-babysitter-docs-sync-design.md` (PB), branch
`docs/sindri-pr-babysitter-docs-sync`; the workflow engine spec `2026-10-10-sindri-workflow-engine-design.md` (WF), rev
8.2, schema 1; `docs/csharp/sindri-workflow.md` (REF); `docs/csharp/packets.md` (PKT); the API surface, UI screens,
context retrieval and session launcher specs (API, UI, CTX, LCH); factory addendum §17.4, §17.5, §17.11, §17.23 (FA);
`plans/sindri-ideas/research/prompting.md` (PR-R) · **Lane:** the ideas lane owns the line, its stations and seeds; the
engine, API, UI, context, packets and launcher lanes take the rows in §12 · **Next:** the owner reviews this doc
together with #258 and the PB draft; nothing is built until all three are approved (§13).

Spec-to-proven-PR is being rebuilt from the ground up as Sindri block recipes, one station at a time. #258 specs the
first station, scoping. This doc specs everything from the frozen scope packet to an open, proven pull request:

- **the line contract** (§3): the packet each station hands the next, how each is hashed and frozen, the evidence it
  carries, the three owner stops, and the meta recipe `workflow.line` that chains the stations;
- **the design station** (§4) and the **house-style pack** it writes in (§5);
- **the plan station** (§6), which ends at the owner's after-design stop with the plan graph shown;
- **the per-phase child** `workflow.phase` (§7): build, prove, review and ship (the PR opens with its evidence pack, no
  stop before it), then the parked draft's docs sync and babysit, whose interface only is defined here (§7.6), ending at
  the owner's ready-to-merge stop;
- **injectable packs** (§5): the design's house style, and the review lenses with their prompts, both chosen per
  workspace or repo through config, never hard-wired;
- how parallel stack phases run on schema 1, which has no fan-out (§8).

The line **replaces** #240's `workflow.spec-to-proven-pr` meta recipe station by station, and **reuses** #240's block
recipes (`workflow.review-loop`, `workflow.hand-test`, `workflow.verify`, the evidence route and its `evidence-ui`,
`-api`, `-db` and `-all` children, `step.judge.choose`). What changes in #240 is listed as rows DTC1–DTC10 (§12.7).
`workflow.bugfix` is untouched.

Recipes stay schema 1, closed templates, camelCase fact keys and outcomes. Nothing here adds a recipe field.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| LN1 | **The line** (owner, 2026-10-15). Spec-to-proven-PR is rebuilt from the ground up as block recipes: scope → design → plan → a per-phase child [build → prove → review → ship → babysit → docs sync]. Nothing on the line is built until every station is specced; #258 stays a draft until then | A station that is wrong poisons every later one, and a packet shape fixed by one station constrains the next. Specs first, all of them, then build front to back |
| LN2 | **Owner stops** (owner, 2026-10-15; corrected the same day): **three** stops. After scoping; after design (the plan graph is shown at this stop; there is no separate plan stop); and **ready-to-merge**, which the line reaches only when the PR is open with its evidence pack, docs sync's stacked docs PR is complete and CI is green. There is **no stop before the PR opens** (§3.4) | The owner decides where a wrong call costs the most, and reviews a finished PR rather than a PR-to-be. Between the stops only the interview asks the owner anything |
| LN3 | **Pain points** (owner, 2026-10-15): incomplete scoping, incomplete behaviour answers, and evidence that must accumulate at every stage. Today the owner runs /autoplan about five times per design, and a project can be 20+ PRs, built in sequence or as a PR stack with some phases in parallel | §4's lens loop replaces the repeated /autoplan runs with a bounded, recorded loop; §3.3 makes every packet carry its parents' evidence; §8 runs 20+ phases from one plan graph |
| LN4 | **Design** (owner, 2026-10-15). The output is a spec doc plus a frozen packet, in an **injectable house style**: a style pack (template, section rules, examples, voice) chosen per repo or profile, never hard-wired, since the owner's personal style differs from their company's. Review is a **lens loop** on a different model than the writer, repeated until 0 CRITICAL and 0 HIGH, at most 4 rounds, then a stop for the owner. Its lenses come from the workspace's injectable lens pack (LN14); the default pack is product, arch, design, devex and security. **One draft**: each open decision gets 2–3 options with trade-offs and a recommendation, asked at the after-design stop through the interview. **Evidence:** every surface covered or excluded with a reason; every acceptance item traced to a phase and a proof signal; each lens round kept; every cross-spec contract edit checked | §4 and §5 |
| LN5 | **Plan** (owner, 2026-10-15). A dependency graph: independent phases become a PR stack built in parallel, dependent phases run in order. Each phase is capped (at most M) and carries its acceptance items, proof signals and allowed files. Say exactly how parallel phases run on schema 1 | §6 and §8; the recommendation is LN11 |
| LN6 | **Build** (owner, 2026-10-15). One TDD builder per phase in its own worktree: a failing test for each acceptance item first, then code, in atomic commits, touching only the files the plan allows (a scope gate). Parallel phases get their own builders under the box's heavy-job cap | §7.2 |
| LN7 | **Prove** (owner, 2026-10-15). Every acceptance item is proven with evidence tied to the commit it ran on, plus a real-app run, before/after for fixes, and **hidden scenarios** the builder never saw (holdout, IDEA-026) | §7.3 and §9 |
| LN8 | **Review and ship** (owner, 2026-10-15; corrected the same day). #240's review loop runs until 0 blocking and 0 issue, capped at 5 iterations, suggestions resolved in writing. When it passes, the PR opens with its evidence pack in the body, with no stop. Then docs sync runs until its stacked docs PR is complete, and babysitting runs until CI is green. Only then does the line stop for the owner, at ready-to-merge: the open PR, its evidence pack, the docs PR and green CI, merged with one click | §7.4, §7.5, §7.6 |
| LN14 | **Review lenses and their prompts are injectable** (owner, 2026-10-15). Each workspace or repo picks its review lenses, and each lens's prompt, through config, the same pattern as the style pack. No lens set is hard-wired. #240's review loop and the design station's lens loop use the same mechanism; the review loop keeps its cap of 5, the design loop its cap of 4 | §5.2. Teams review for different things; a lens is configuration, approved like a seed, not code |
| LN9 | **The after-design stop lives in the plan station** (this doc, §6.4). The design station ends with a hashed candidate packet and no stop; the plan station builds the graph from it and its last states are the stop, which freezes the design, the plan and every phase packet together | LN2 puts one stop after design *and* plan. One recipe that owns both packets at the stop can freeze them atomically and route a revision to the right station |
| LN10 | **Every packet is an `aw-packets` kind** (this doc, §3.2). The plan graph *is* an `aw-packets` task plan (`links.tasks`: phases are tasks, stacks are workstreams), so PKT's existing rules (acyclic graph, write scopes, no overlap between unordered tasks, held-out acceptance) check it with no new code | PKT already checks exactly the plan's invariants. A second plan format would drift from it |
| LN11 | **Parallel phases run as linked instances started one at a time by a dispatcher, up to a cap** (this doc, §8; recommended over a schema-v2 request and over sequential children). `workflow.line` has a pure dispatch Step that starts at most one ready phase per transition through an effect, then waits for phase events; at most `workflow.line.parallel` (default 2, never above the box's heavy-slot cap) run at once | Schema 1 keeps one current state per instance and no fan-out (WF §3.9). A start effect plus an event gives a capped fan-out without a schema change, keeps the child depth inside `RCP-CHILD-003`, and degrades to sequential at cap 1. IDEA-054's schema-v2 fan-out is engine-LATER and would block the line |
| LN12 | **No seed reuses a prompt as-is** (owner, IDEA-098; carried from SC10). Every agent Step's seed on the line, including the #240 blocks the line uses, is written new against PR-R's rubric and evidence loop | §10. It amends DT7's /enhancePrompt porting (DTC7) |
| LN13 | **Packets, not facts, cross stations; facts never do** (carried from SC §3) | A station reads its input packet by hash and verifies it against the freeze record; it never reads another instance's facts |

## 2. Terms

- **Station:** one block recipe on the line. It takes a packet in and leaves a packet out.
- **Packet:** a directory in an instance's run area (W11), hashed by `aw-packets hash` (digest v1). **Candidate:**
  hashed, not frozen. **Frozen:** its hash is recorded by `aw-packets freeze` under
  `$AW_STATE_DIR/packets/<repo-owner>/<repo-name>/<instance-slug>/frozen/` (PKT "Freeze").
- **Owner-frozen / machine-frozen:** frozen by a block on an owner's approve, or by a block when its checks pass with
  no owner act. Both are frozen; the difference is recorded in the freeze record's `by`.
- **Phase:** one node of the plan graph, built as one PR. **Stack:** a chain of phases whose PRs are stacked, each
  based on the previous phase's head branch. **Wave:** the phases the dispatcher may run at once.
- **Proof signal:** an observable statement that proves one acceptance item, with an evidence kind (FA §17.11) and
  where it runs (`head`, or `base` and `head` for a fix). The proof signals of a phase are its verification plan,
  frozen before any code exists.
- **Hidden scenario (holdout):** a scenario written for one phase by a Step the builder never shares a context with,
  stored outside every builder-readable path, and run only by the evaluator (§9).
- **Lens round:** one pass of the five design lenses over the draft, appended as one entry to `lens-rounds.jsonl`.
- **Escape:** a surface or behaviour added after the stop that should have caught it (SC §3's `scoping.escape`,
  generalised in §3.3).

## 3. The line contract

### 3.1 The line

```
              stop 1                               stop 2 (plan graph shown)
input ─► [scoping] ─► scope ─► [design] ─► design ─► [plan] ─► design + plan + phase packets (frozen)
          #258        packet    §4      candidate     §6                     │
                                                                             ▼
   workflow.line: dispatch ─► one workflow.phase per plan node, ≤ parallel at once (§8)
                                                                             │
   workflow.phase:  build ─► prove ─► review ─► ship ─────────► babysit (docs sync inside) ─► merged
                    §7.2     §7.3     §7.4      §7.5             §7.6
                    phase    proof    review    PR opens with    hand-off
                    packet   pack     result    its evidence     packet
                                                pack (no stop)
   babysit: docs sync's stacked docs PR complete + CI green ─► stop 3: ready-to-merge ─► one merge click
```

### 3.2 The packet chain

Every packet is an `aw-packets` kind (LN10; P2), hashed by `aw-packets hash`, and carries `index.json`: its kind, its
parents' hashes, and the hash of the acceptance chain it extends. A reader verifies its input with `aw-packets verify
<dir> --hash <h>` (P3) before it reads anything.

| # | Packet (kind) | Made by | Files | Frozen fields | Hash and freeze | Evidence it adds |
|---|---|---|---|---|---|---|
| 1 | **Scope packet** (`scoping`) | `workflow.scoping` (#258 §8.3) | #258 §8.3 | All files | Owner-frozen at stop 1 by `step.scope.freeze` | #258 SC7: citations, four lens coverage rows, an all-failing acceptance list, a clean challenge |
| 2 | **Design packet** (`design`) | `workflow.design` (§4) | `spec.md`, `sections.json`, `coverage.json`, `decisions.json`, `acceptance.json`, `signals.json`, `contracts.json`, `lens-rounds.jsonl`, `style.json`, `escapes.json`, `runs.json` | All; `decisions.json`'s chosen options are filled at stop 2 | Candidate at `step.design.evidence`; **owner-frozen at stop 2** by `step.plan.freeze`, with the plan | Surface coverage, a proof signal per acceptance item, every lens round, contract-edit checks, style conformance, escapes (§4.5) |
| 3 | **Plan graph** (`plan`) | `workflow.plan` (§6) | `packet.json` with `links.tasks` (PKT's task plan: one task per phase), `graph.json` (stacks, waves, bases), `exec-review.json`, `holdout.json` (set hashes and counts, no text), `answers.jsonl` | All | Candidate at `step.plan.evidence`; owner-frozen at stop 2 by `step.plan.freeze` | Every acceptance item in exactly one phase, PKT's rules, the executability review, sizes within the cap (§6.3) |
| 4 | **Phase packet** (`phase`), one per phase | `step.plan.freeze`, by code from the plan and design | `phase.json` (goal, kind, deps, size, base rule, `allowedFiles`, `testChange`, read seeds, budget), `acceptance.json` (its items), `signals.json` (its verification plan), `holdout.json` (set hash and count), `design.md` (the sections covering its items), `amendments.jsonl` | All except `amendments.jsonl`, which only appends | Owner-frozen at stop 2 with the plan. An amendment (an owner's scope grant, §7.2) is a new hash whose parent is the old one | Nothing new: it is the build's contract |
| 5 | **Proof pack** (`proof`) | `workflow.prove` (§7.3) | `proof.json` (per item: `passing`, evidence refs `{kind, path, sha256, commit, at}`), `red.json`, `app-run/`, `evidence/`, `before-after.json`, `holdout.json` (ids, verdicts, commit, `spent`; no text), `not-proven.md`, `acceptance.json` | All | Machine-frozen by `step.prove.evidence` on `complete` | Every item passing on one commit, the real-app run, fail-before/pass-after for fixes, the hidden scenarios' verdicts (§7.3) |
| 6 | **Review result** (`review`) | `workflow.review-loop` (DT §4.1, DTC4) | `review.json` (rounds, each finding with its class and its resolution, the last run's verdict, the head sha) | All | Machine-frozen by `step.review.record` on `clean` (DTC4) | Zero blocking and zero issue on the reviewed head; every suggestion answered in writing |
| 7 | **PR hand-off** (`handoff`) | `workflow.ship` (§7.5) | `pr.json` (base, head branch, head sha, stack parent, title, labels), `body.md` (the evidence pack as the PR body), `green.json`, `index.json` over packets 1–6 | All | Machine-frozen by `step.ship.freeze` when the local GREEN gate passes and the three shas agree, just before the PR opens (no owner stop) | The GREEN gate's run on the head, and the body the PR opens with |
| 8 | **Merge record** (appended, not a packet) | `workflow.pr-babysitter` and `workflow.docs-sync` (PB, §7.6) | `babysit.jsonl` in the phase's run area: each push, CI run, comment handled, the docs PR and its result, the final snapshot | — | Appended; never edits packets 1–7 | CI green on the head, the docs PR complete, every comment handled: the ready-to-merge stop's preconditions (BD3, BD5) |

### 3.3 Contract rules

| Rule | Says |
|---|---|
| **LC-1 Packets only** | A station's input is a packet hash in a fact. The station verifies it (`aw-packets verify`, P3) and reads the files; it never reads another instance's facts (LN13) |
| **LC-2 Acceptance chain** | `acceptance.json` in every packet has `parent: <hash of the previous acceptance.json>`. Ids and texts never change (IDEA-003, SC §3). Design may append items and mark old ones `superseded`, confirmed at stop 2. Only the proof pack flips `status` to `passing`, each flip with evidence `{kind, path, sha256, commit, at}`. `LC-ACC-001`, run by every station's evidence check, recomputes the chain from the scope packet and refuses any text edit |
| **LC-3 Evidence accumulates** | No packet drops what its parents proved; each adds its own (the right column of §3.2). `index.json` makes the chain walkable, and `sindri line show <address>` (API9) prints it, oldest first |
| **LC-4 Tied to a commit** | From the phase packet on, every artifact names the commit it ran on. The proof pack, the review result and the hand-off must agree on one head sha before the PR opens (`SHIP-SHA-001`); a head that moved sends the phase back to prove (§7.1) |
| **LC-5 One freezer** | Each packet has exactly one freezing block, named in §3.2. Owner-frozen packets are frozen only on a human-only approve (DT §8.5) |
| **LC-6 Trust carries** | Trust labels (SC §11) carry forward. A design or plan item a model wrote is `untrusted` until stop 2's approve; owner answers stay `owner`. Text from earlier packets reaches agents inside the pack fence (W7) |
| **LC-7 Escapes are recorded** | A station that adds a surface or behaviour the previous stop approved without writes a `line.escape` ledger row `{ from, station, kind, id }` (`scoping.escape` is its `from: scoping` form, SC §3). Design writes them for scope (§4.5), plan for design, build and prove for plan (an owner scope grant, a hidden scenario that finds a missing behaviour). API7's `escapes` reads them |
| **LC-8 Purge keeps frozen packets** | A purged instance keeps its frozen packets' directories (W11). A candidate that was never frozen goes with its instance |

### 3.4 The owner stops

All three are human-only (DT §8.5; a session's `complete` gets `SND-AUTH-002`), each with a fixed form (W2). There is
no stop before a PR opens (LN2, corrected): a phase's PR opens as soon as its review passes and its GREEN gate holds.

| Stop | Recipe and state | Shows | Form |
|---|---|---|---|
| **1. After scoping** | `workflow.scoping` · `stop` (#258 §6.1) | #258 §8.4 | approve, revise (notes), drop |
| **2. After design** | `workflow.plan` · `decide` then `stop` (§6.4) | `DesignStopView` (API10): lens findings left, the decisions with options, the spec, the plan graph with stacks and waves, coverage and trace tables, escapes | First the decisions interview (#258 §7's form, FormFrom `fact:questions`); then approve, revise design (notes), revise plan (notes), drop |
| **3. Ready-to-merge**, per phase | `workflow.pr-babysitter` · `ready` + `command.merge` (PB3), entered only when CI is green on the head **and** docs sync has ended (its stacked docs PR opened, or nothing to sync) (BD3, BD5) | `ReadyView` (API11): the open PR and its body (the evidence pack: the acceptance table with evidence and shas, hidden-scenario verdicts, not-proven, review rounds), the docs PR, CI on the head sha, the stack base, the merge record | merge (one click, human caller only), or send it back: `command.rework` with notes ends the babysitter `unresolved` and the phase rebuilds (BD8) |

### 3.5 The meta recipe `workflow.line`

`workflow.line` (W12's name in #258) chains the stations. Its full YAML is Appendix C.7; its shape:

| State | Kind | Does |
|---|---|---|
| `seed` | block | Writes the empty round-1 facts (`fact:designRevision`, `fact:phaseBoard`, `fact:phaseEvent`), as #258's intake does |
| `scoping` | child `workflow.scoping` | Stop 1 inside. `resolved` brings `fact:scopePacket` |
| `design` | child `workflow.design`, loop `redesign` (cap 3) | `resolved` brings the candidate `fact:designPacket` |
| `plan` | child `workflow.plan` | Stop 2 inside. `resolved` brings the frozen `fact:planPacket` and `fact:phasePackets`; `unresolved` brings `fact:designRevision` and goes back to `design`; `failed` is a drop |
| `dispatch` | block `step.line.dispatch`, loop `ticks` (cap 200) | Pure: reads the plan graph and the board, reports `start` (one ready phase, `fact:nextPhase`), `full`, `done` or `blocked` (§8.2) |
| `starting`, `building` | waits on `line.phase-changed` (W15) | `start → starting` carries `effect.line.start-phase`; every phase event goes back to `dispatch` |
| `owner` | human-only `step.line.decide` | A phase ended unresolved: retry it, skip it (and its dependents), or stop the line |
| `done`, `dropped`, `stopped` | terminals | `resolved`, `unresolved`, `failed` |

The line is the **epic layer** DT Q8 left in the skill: the stage map is the plan graph, the autonomy contract is the
three stops, and "start the next stage after a merge" is the dispatcher.
