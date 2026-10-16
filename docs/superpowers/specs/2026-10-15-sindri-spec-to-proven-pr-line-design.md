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

## 4. The design station: `workflow.design`

### 4.1 What it replaces, and its shape

It does what /officeHours (the spec draft), /autoplan and its lenses (productReview, archReview, planDesignReview,
planDevexReview, cso in plan mode) do today, as one bounded, recorded loop: one draft, written in the repo's style pack
(§5.1), reviewed by the repo's design lens pack (§5.2) on another model, revised until no CRITICAL or HIGH finding is
left. It reads the frozen scope packet and ends with a hashed candidate design packet. It has no stop of its own except
the lens-cap desk; the owner reads the design at stop 2, in the plan station (LN9).

```
intake ─┬─ fresh ──► draft ─► check ─┬─ gaps ─► draft (loop rounds, cap 5)
        └─ revise ─► apply ──────────┘ pass
                                     ▼
       ┌──► round ─► lens ─► lens-log ─┬─ next ─► lens        (one lens per run, from the pack)
       │   (loop lens, cap 4)          ├─ clean ─► evidence ─┬─ complete ─► designed
       │                               │                     └─ incomplete ─► draft
       └── check ◄─ revise ◄─ findings (below cap)
                                       └─ findings at cap ─► lens-desk (owner: accept, revise, drop)
```

| State | Block | Runtime | Does |
|---|---|---|---|
| `intake` | `step.design.intake` | block, I/O | Verifies `fact:scopePacket` against its freeze record (P3); loads the scope map, acceptance list, open questions and answer log into `fact:scope`; resolves the style pack and the design lens pack (§5) and pins their hashes in `fact:packs`; writes the round-1 seeds. `fresh`, or `revise` when `fact:designRevision` is non-empty |
| `draft` | `step.design.draft` | agent, writer model, read-only (W9) plus its Step directory | Writes `spec.md` in the style pack's template and `fact:design` (sections, coverage, decisions, signals, contracts) (A.1) |
| `apply` | `step.design.apply` | agent, writer model | Revise mode: folds stop 2's answers and notes into the sections they affect, and lists them in `fact:changedSections` (A.2) |
| `check` | `step.design.check` | block, I/O | `DesignPacketChecks` at stage `draft` (§4.3). `pass`, or `gaps` with `fact:findings` |
| `round` | `step.lens.round` | block, pure | Starts a lens round: the pack's lenses that apply (§5.2 `when`), in pack order, as `fact:lensQueue`; `ready` |
| `lens` | `step.lens.run` (`with: { subject: design }`) | agent, **lens model ≠ writer model** (W8), read-only | Runs the queue's next lens: the shared frame (A.3) with that lens's prompt from the pack; reports `fact:lensReport` |
| `lens-log` | `step.lens.log` | block, pure | Appends the report to `fact:lensRounds` (append-only, as #258's challenge-log); at the round's end reports `clean` (0 CRITICAL, 0 HIGH), else `findings` with them as `fact:findings`; `next` while the queue isn't empty |
| `revise` | `step.design.revise` | agent, writer model | Fixes every CRITICAL and HIGH; answers each MEDIUM and LOW in writing in `fact:design.resolutions` (A.2) |
| `lens-desk` | `step.design.lens-desk` | desk, human-only, fixed form | At the round cap with findings left: **accept** (each left finding recorded as an accepted risk, shown first at stop 2), **revise** (notes; another round needs `--grant`), **drop** |
| `evidence` | `step.design.evidence` | block, I/O | `DesignPacketChecks` at stage `evidence`, assembles the packet in the run area, hashes it (`fact:designPacket`), writes `line.escape` rows. `complete` or `incomplete` |

Caps: `rounds` 5 (draft), `lens` 4 (owner, LN4), `lensRuns` 40 (at most 10 lenses a round, §5.2), budget 1500k. Every
timeout parks. Revise mode is the same graph entered through `apply`: the lens frame gets `fact:changedSections`, and a
lens raises findings only inside them or on what they break (A.3), so a decision change costs one narrow round, not a
fresh review.

### 4.2 One draft, and the open decisions

There is one draft (LN4). Where the scope leaves a real choice, the writer does not ask; it records an **open
decision** and writes the spec as if the recommended option were chosen, marking each affected section
`assumes: D3.a`:

```json
{ "id": "D3", "question": "Where does link expiry run: on read, or in the nightly job?",
  "affects": ["sec-4.2", "sec-6"], "cites": ["S2", "AC-4", "L-code-7"],
  "options": [
    { "id": "D3.a", "answer": "Checked on read, in the public route.", "tradeoffs": "Exact to the second; one indexed lookup per read.", "recommended": true },
    { "id": "D3.b", "answer": "Swept by the nightly job.", "tradeoffs": "No read cost; a link lives up to a day too long, which AC-4 forbids." } ],
  "status": "open" }
```

- Two or three options, each with trade-offs and citations; exactly one `recommended` (`DSN-DEC-001`).
- Stop 2 asks them through #258's interview form (§6.4). A pick that equals the assumption changes nothing. Any other
  pick sends the line back to `design` in revise mode, with the answers in `fact:designRevision`, and `apply` rewrites
  only the sections listed in `affects`.
- Scope's open questions (#258 §7.6) must each end answered by a section, carried as an open decision, or deferred
  with an owner and a `buildMayAssume` default (PKT-DEC-001's shape) (`DSN-COV-002`).

### 4.3 The checks

One pure function, `DesignPacketChecks` (`Aw.Sindri.Features/Design/`), runs at `check` (stage `draft`), `evidence`,
in stop 2's read model, and in `step.plan.freeze`. It has #258's findings shape and its `--facts` desk mode: `sindri
design check (<address> | --facts <dir>) [--stage draft|evidence] [--json]` and `sindri design show` (API3's pattern;
exit 0 pass, 1 findings, 2 usage, tool error or not running).

| Rule | Stage | Check |
|---|---|---|
| `DSN-COV-001` | draft | Every scope surface (scope packet `scope-map.json`) is covered by at least one section (`covers: [S-ids]`) or listed in `coverage.json` as excluded, with a one-line reason |
| `DSN-COV-002` | draft | Every scope open question is answered by a section, carried as an open decision, or deferred with an owner and a `buildMayAssume` default |
| `DSN-ACC-001` | draft | Every live acceptance item has at least one proof signal: an evidence kind from FA §17.11's list, an observable statement, and where it runs (`head`, or `base+head` for a fix) |
| `DSN-ACC-002` | draft | Signals are measurable: FA §17.23.14's unmeasurable terms ("fast", "clean", "intuitive" …) without a number or a reference artifact are refused. The term list is a rule pack |
| `DSN-ACC-003` | evidence | `LC-ACC-001`: scope's ids and texts unchanged; every added item cites a section or decision; every `superseded` item names its successor and is flagged for stop 2 |
| `DSN-DEC-001` | draft | Every open decision has 2–3 options with trade-offs and citations, exactly one recommended, a non-empty `affects`, and each affected section carries its `assumes:` marker |
| `DSN-CITE-001` | draft | Every section cites scope ids (`S-*`, `AC-*`, `A*`, `R*`, `L-*`) or a decision; an uncited section is flagged as in #258 |
| `DSN-XSPEC-001` | draft | Every cross-spec contract edit in `contracts.json` names a target file and heading that exist at the scope packet's base sha |
| `DSN-XSPEC-002` | draft | Each edit quotes the target's current text, and the quote is a substring of the target at that sha, so an edit can't be made against text that isn't there |
| `DSN-XSPEC-003` | draft | Each edit is filed as a numbered row in a "Rows to other lanes" section of `spec.md`, one lane per row, and no two edits change one quoted span |
| `DSN-STYLE-001` | draft | `spec.md` follows the style pack's section rules: required sections in order, each within its word limit, the pack's forbidden phrases absent (§5.1) |
| `DSN-LENS-001` | evidence | The last round in `lens-rounds.jsonl` has 0 CRITICAL and 0 HIGH, or every one left is an owner-accepted risk from `lens-desk`; every round ran every lens its `when` selected |
| `DSN-LENS-002` | evidence | The runtime records (W8) show the lens model differs from the writer's, and the vendor too when the profile allows more than one; at the desk it is a warning shown first at stop 2 (as `SCP-CHAL-002`) |
| `DSN-LENS-003` | evidence | Every MEDIUM and LOW of every round has a written resolution in `fact:design.resolutions` |
| `DSN-ESC-001` | evidence (warn) | Surfaces and acceptance items not in the scope packet are listed in `escapes.json`; `evidence` writes one `line.escape` row each (LC-7) |

### 4.4 The packet and its evidence

`coverage.json` is the surface table (surface → sections, or excluded with a reason); `signals.json` the trace table
(item → signals). The *phase* half of LN4's trace (item → phase) is added by the plan station and checked at stop 2
(`PLN-ACC-001`). `lens-rounds.jsonl` keeps every round: each lens's id, pack hash, model, findings with severities and
the resolution of each. `style.json` names the style and lens packs with their hashes.

### 4.5 Failures and parks

| What | Then |
|---|---|
| The scope packet's hash doesn't match its freeze record | `intake` is `step.failed` (`SND-LINE-001`); the instance parks naming both hashes |
| No style pack or lens pack resolves, or a pin doesn't match | `step.failed` `SND-PACK-001`/`-002` (§5.3); parks |
| `check` reports the same findings twice | Parks `rounds` no-progress (WF §3.7) |
| Lens round cap with CRITICAL or HIGH left | `lens-desk`, human-only |
| A lens run fails (provider down, schema invalid) | `step.failed`; the state parks; `answer --resume` re-runs that lens only |
| Budget spent | Parks `budget` (the intended owner check, as in #258) |

## 5. Injectable packs: house style and lenses

A **pack** is a small, versioned directory of prompt-facing text, chosen per workspace (the profile) or per repo, and
pinned by hash in the approved profile, since it steers a model the way a seed does. Two kinds: **style** (§5.1) and
**lenses** (§5.2), one mechanism (LN4, LN14).

### 5.1 The style pack

```
<pack root>/style/<id>/
  pack.json      { kind: "style", id, version, title,
                   sections: [ { id, heading, required, guidance, maxWords? } ],
                   rules: { forbidden: ["…"], maxLines?, tables: "prefer" | "allow", voice: "voice.md" },
                   template: "template.md", examples: ["examples/*.md"] }
  template.md    the skeleton the writer fills, headings in order
  voice.md       tone and wording rules, written as instructions
  examples/      2–4 excerpts in the house style, one a hard case
  evals/         cases for the writer under this pack (optional; §10)
```

- **Where packs live,** first match wins: the repo (`.agentic-workflow/packs/<kind>/<id>/`), the machine
  (`$AW_STATE_DIR/packs/<kind>/<id>/`), built in (`config/packs/<kind>/<id>/`). The built-in style pack `plain` is the
  toolkit's own house style; the owner's personal and company packs live outside the toolkit (app-agnostic rule).
- **Which pack:** profile `workflow.packs.style: { default: <id>, repos: { <repo>: <id> } }`. A repo entry wins.
- **Pinning:** `workflow.packs.pins: { "style/<id>": "sha256:…" }` in the profile. A pack whose `aw-packets hash`
  differs from its pin is refused (`SND-PACK-002`), so a style change is a profile approval (protected), like a seed
  edit (DT §7 "Pinning").
- **How it reaches the writer:** `intake` copies the resolved pack into the writer's Step directory as `style/` (W19),
  and records `{ id, hash }` in `fact:packs` and the run record. The seed (A.1) is fixed and says only "write
  `spec.md` from `style/template.md`, in `style/voice.md`'s voice, following `style/pack.json`'s section rules; the
  examples are examples". The pack is never pasted into the seed, so the seed's hash and evals stay stable while the
  style changes, and `DSN-STYLE-001` checks the result against the same `pack.json`.

### 5.2 The lens pack

```
<pack root>/lenses/<id>/
  pack.json      { kind: "lenses", id, version, applies: "design" | "review",
                   severities: ["CRITICAL","HIGH","MEDIUM","LOW"] | ["blocking","issue","suggestion","nit"],
                   lenses: [ { id, title, prompt: "lenses/<id>.md", reference?, examples?,
                               when: "always" | { surfaceKinds: [...] } | { paths: [...] },
                               model?: "<profile role>" } ] }
  lenses/<id>.md the lens's prompt: what to look for and what each severity means for it
```

- **Which pack:** `workflow.packs.lenses: { design: { default, repos }, review: { default, repos } }`, pinned as style
  packs are. Built in: `design-five` (product, architecture, design, developer experience, security: what the
  /autoplan lenses cover) and `code-review` (the domains /review spawns today). Neither is hard-wired: a repo that
  names another pack gets only that pack's lenses (LN14).
- **At most 10 lenses** a pack (`SND-PACK-003`), so `lensRuns` bounds a run. `when` selects lenses per round from the
  subject (the scope's surface kinds for design, the changed paths for review); a lens not selected is recorded
  `not-applicable` with the reason, never silently skipped.
- **The frame stays ours.** `step.lens.run`'s seed (A.3) holds the output contract (the findings schema, the pack's
  severity scale, citations, fencing, read-only scope, the tool bound, "done when"). The lens prompt fills only its
  **Lens** section. Every lens of every pack is graded by the same frame graders, so a team's lens can't break the
  loop's contract.
- **Model:** the frame runs on `workflow.design.models.lens` (or `workflow.review.models.lens`), which must differ from
  the writer's (or the builder's); a lens may name another role.
- **The review loop uses the same packs** (DTC4): one lens per run, a round per review iteration, cap 5.

### 5.3 Checking a pack

`sindri pack check <dir> [--json]` (`local`, API12): the schema, every referenced file present, at most 10 lenses,
sections and template headings agree, no file outside the pack, and its hash. With `--evals` it runs the pack's eval
cases through SC-E's `recipe prompt-eval` (a heavy job; model calls leave the box). Errors: `SND-PACK-001` (not
found), `-002` (pin mismatch), `-003` (schema or limit), `-004` (a pack of the wrong kind or `applies`).

## 6. The plan station: `workflow.plan`

### 6.1 What it replaces, and its shape

It replaces #240's `workflow.plan` (a per-stage plan plus a verification plan, DTC2) and /specToProvenPR's stage map
(step 0.5). Its output is one graph for the whole change, plus a phase packet per node whose proof signals are the
verification plan, frozen before any code exists, which keeps /specToProvenPR's "plan before code" gate.

```
intake ─► graph ─► check ─► exec-review ─► holdout ─► evidence ─► questions ─┬─ asked ─► decide ─► record ─► settle
           ▲   (loop rounds)    │ objections                                  └─ none ─────────────────────► stop
           └─────── gaps ───────┴──────────────── incomplete                         settle: assumed ─► stop
                                                                                     settle: changed ─► redesign (unresolved)
  stop: approve ─► freeze ─► approved          revise design ─► record ─► settle (changed) ─► redesign
        revise plan ─► record ─► settle (replan) ─► graph          drop ─► dropped (failed)
```

| State | Block | Runtime | Does |
|---|---|---|---|
| `intake` | `step.plan.intake` | block, I/O | Verifies `fact:designPacket` (a candidate: recomputes its hash in the design instance's run area) and `fact:scopePacket`; seeds |
| `graph` | `step.plan.graph` | agent, planner model, read-only plus `sindri plan check --facts` | Writes `fact:plan`: the phases and their edges (§6.2) (A.4) |
| `check` | `step.plan.check` | block, I/O | `PlanChecks` stage `draft` plus `aw-packets validate` on the plan packet (§6.3) |
| `exec-review` | `step.plan.exec-review` | agent, **builder model**, read-only | FA §17.23.14's executability review: for each phase, is it executable as written, is context missing, are the allowed files enough, can every signal be checked by a command or artifact. `executable`, or `objections` as `fact:findings` (A.5) |
| `holdout` | `step.plan.holdout` | agent, evaluator model, capability `holdout-author` (W18) | Writes each phase's hidden scenarios into the holdout store; reports only ids and set hashes as `fact:holdoutSets` (§9) (A.6) |
| `evidence` | `step.plan.evidence` | block, I/O | `PlanChecks` stage `evidence` and `DesignPacketChecks` again; assembles the plan packet (candidate), the phase packets (unfrozen) and `fact:planPacket` |
| `questions` | `step.plan.questions` | block, pure | Turns the design's open decisions (and any `PLN-` question the graph raised) into #258's question form, recommended options marked; `asked` or `none` |
| `decide` | `step.interview.answer` (#258) | desk, human-only, `FormFrom: fact:questions` | Stop 2, part one: the decisions |
| `record` | `step.interview.record` (#258) | block, pure | Appends the batch to `fact:answerLog` |
| `settle` | `step.plan.settle` | block, pure | Compares the latest batch with the assumptions: `assumed` (every pick equals the recommended option), `changed` (a pick differs, or a `Q-revise-design` note: writes `fact:designRevision`), `replan` (a `Q-revise-plan` note) |
| `stop` | `step.plan.review` | desk, human-only, fixed form (W2) | Stop 2, part two: approve, revise design (notes), revise plan (notes), drop |
| `freeze` | `step.plan.freeze` | block, I/O | Re-runs every check; freezes, in order, the design packet, the plan packet and each phase packet (`aw-packets freeze`); seals the holdout sets; writes `design.approved` and `plan.approved` ledger rows (ids and counts only) |

Terminals: `approved` (`resolved`: `fact:designPacket`, `fact:planPacket`, `fact:phasePackets`), `redesign`
(`unresolved`: `fact:designRevision`, produced by `settle` on every path to it), `dropped` (`failed`). Loops: `rounds`
5 (`graph`), `refreeze` 2, as in #258. Every timeout parks; `decide` and `stop` park after 2 and 7 days.

### 6.2 The graph

`fact:plan` is PKT's task plan, one task per phase:

| Field | PKT field | Meaning |
|---|---|---|
| `id`, `title` | `id`, `title` | `P1`, `P2` … stable across rounds |
| `workstream` | `workstream` | The stack the phase belongs to; independent stacks are separate workstreams |
| `deps` | `deps` | Phases this one builds on |
| `size` | `size` | `XS`, `S` or `M`; `L` and `XL` are refused (LN5, `PLN-SIZE-001`) |
| `allowedFiles` | `writeScope` | Globs and `new: <path>`; new test files by the repo's test-path convention are always allowed (FA §17.23.3) |
| `testChange` | `rules` entry `test-change:<path>:<reason>` | Existing test files the phase may change, each with a reason; shown at stop 2 |
| `readSeeds` | `readSeeds` | Product nodes, files and refs the builder starts from |
| `acceptance` | `acceptance[]` (`id`, `text`, `evidence[]`, `holdout`) | The phase's items with their signals' evidence kinds; one entry per hidden scenario with `holdout: true` and no text (PKT-HOLD-002) |
| `kind` | `rules` entry `kind:<k>` | `feature`, `fix`, `refactor`, `docs`, `config`; a `fix` phase's items need `base+head` signals |
| `budget` | `budget` | Tokens, turns, wall minutes by size (profile `workflow.plan.sizes`) |

`graph.json` is derived by code: each phase's **base rule** (§8.3), its stack, and the waves at the profile's
`workflow.line.parallel`.

### 6.3 The checks

`PlanChecks` (`Aw.Sindri.Features/Plan/`), same shape and verbs as §4.3 (`sindri plan check|show`).

| Rule | Stage | Check |
|---|---|---|
| `PLN-PKT-001` | draft | `aw-packets validate` passes the plan packet: PKT-ID, WS, GRAPH (acyclic), BASE, SCOPE-001 (allowed files exist or are new), SCOPE-002 (two phases no dependency orders never write the same path), ACC-001, EVID-001, DEC-001, HOLD-001/002 |
| `PLN-ACC-001` | draft | Every live acceptance item of the design packet is in exactly one phase (LN4's item → phase trace); together with `DSN-ACC-001` every item has a phase and a signal |
| `PLN-SIZE-001` | draft | Every phase is at most `workflow.plan.maxPhaseSize` (default `M`); a size's limits (files, items, budget) are profile values (`workflow.plan.sizes`; P4 defines PKT-SIZE-001 from them) |
| `PLN-SIG-001` | draft | Every item of a `fix` phase has a `base+head` signal; every phase has at least one signal of a real-app kind (`ui-browser`, `api-transcript`, `cli-transcript`, `ios-sim`) unless its kind is `docs` or `config`, with the reason |
| `PLN-STACK-001` | draft | A phase with more than one unmerged dependency at start is marked `waitsForMerge` (§8.3); a stack is at most `workflow.line.maxStack` deep (default 4) |
| `PLN-EXEC-001` | evidence | The last executability review reports `executable` for every phase |
| `PLN-HOLD-003` | evidence | Every phase has a sealed-to-be holdout set with at least `workflow.prove.holdoutMin` scenarios (default 2) and one canary, by hash (§9) |

### 6.4 Stop 2

`DesignStopView` (API10) is the owner's single read of the design and the plan, ordered so behaviour comes before ticks:

1. Lens findings left (owner-accepted risks), lens and writer models, warnings (`DSN-LENS-002`).
2. The open decisions, each with its options, trade-offs and recommendation (the `decide` form when it is open).
3. Superseded and added acceptance items, and escapes.
4. The plan graph: stacks as columns, waves as rows, each phase with its size, items, allowed files, test changes
   and base rule; the critical path; the PR count.
5. The trace table (item → phase → signals) and the coverage table (surface → sections or excluded).
6. `spec.md`, rendered, with `assumes:` markers linked to their decisions.
7. Contract edits and their checks; the lens-round timeline, collapsed.

Hidden scenarios are shown by count only, even to the owner (§9.4).

## 7. The per-phase child: `workflow.phase`

### 7.1 Shape

`workflow.phase` is started per plan node by the line (§8), or by hand with a frozen phase packet: `sindri workflow
start workflow.phase --item <item> --fact fact:phasePacket=<hash>`. Its YAML is Appendix C.6.

```
seed ─► build ─► prove ─► review ─► drift ─┬─ same ─► ship ─► babysit ─► merged
         ▲  (loop stage, cap 3)            └─ moved ─► prove (loop reprove, cap 2)
         ├── prove: unresolved (findings) ──────────────┘
         ├── review: failed (behaviour wrong)
         ├── ship: unresolved (GREEN red, head stale)
         └── babysit: failed (owner's rework at ready-to-merge)
   build/review/babysit unresolved ─► owner (human-only: rebuild, stop)
```

| State | Kind | Does |
|---|---|---|
| `seed` | block `step.phase.seed` | Seeds `fact:buildNotes`; transition to `build` carries `effect.line.report` (`building`) |
| `build` | child `workflow.build` (§7.2), loop `stage` cap 3 | `resolved` brings `fact:built`, `fact:workspace`, `fact:amendments` |
| `prove` | child `workflow.prove` (§7.3), loop `reprove` cap 2 | `resolved` brings `fact:proofPack`; `unresolved` brings `fact:findings` as `fact:buildNotes` |
| `review` | child `workflow.review-loop` (DT §4.1, DTC4) | Input `fact:change` (branch, base, head); `resolved` brings `fact:review` (the review result packet) |
| `drift` | block `step.phase.drift` | `same` when the review's head equals the proof's; `moved` when review fixes moved it (back to `prove`, which re-proves on the new head) |
| `ship` | child `workflow.ship` (§7.5) | `resolved` brings `fact:pr` and `fact:handoff`; transition to `babysit` reports `shipped` to the line (with the head branch, for stacking) |
| `babysit` | child `workflow.pr-babysitter` (PB, §7.6) | `resolved` (merged) brings `fact:mergedSha`; reports `merged` |
| `owner` | human-only `step.phase.decide` | rebuild (notes), stop |
| `merged`, `dropped` | terminals | `resolved`, `unresolved` |

The three gates DT10 keeps are states on every path to `merged`: PROOF is `prove`, REVIEW=0 is `review`, GREEN is
`ship`'s `green` (local) and the babysitter's CI-green `ready` (remote). `GateDominanceTests` (DT-M) walk
`workflow.phase` (DTC1). Child depth: a phase is a linked root (§8), so its deepest chain is phase → prove → evidence →
evidence-all → evidence-ui, 4 levels (`RCP-CHILD-003`).

### 7.2 Build: `workflow.build`

One TDD builder per phase, in the phase's own worktree (W16), under the heavy-job cap (W17).

| State | Block | Runtime | Does |
|---|---|---|---|
| `workspace` | `step.build.workspace` | block, I/O | Creates or reuses the phase's branch `sindri/<line>/<phase>` and its worktree at the base the line chose (§8.3), recorded as `fact:workspace`; seeds |
| `slot` | wait `{ resource: heavy }` (W17) | — | One of the box's heavy slots; held until the build ends or parks |
| `red` | `step.build.red` | agent, builder model, capability `build` (W20) | One failing test per visible acceptance item whose signal kind is `test` or that a test can express, committed as `test: …`; `fact:redTests` maps item → tests (A.7). `written` |
| `red-check` | `step.build.red-check` | block, long (W21) | Runs the red tests on the red commit: every one must fail (`BLD-RED-001`); one that passes is `vacuous` and goes back to `red` with findings |
| `green` | `step.build.green`, loop `revisions` cap 4 | agent, builder model, capability `build` | Implements in atomic commits, one logical change each, conventional messages, only inside `allowedFiles` (A.8). `built`, or `blocked` with a request (`fact:findings`) |
| `checks` | `step.build.checks` | block, long | The scope gate and the build rules below, plus the repo's commands (`workflow.build.commands`: typecheck, the unit lane). `pass`, `failed` (findings → `green`), `outOfScope` (→ `scope-desk`) |
| `scope-desk` | `step.build.scope-desk` | desk, human-only, fixed form | A change outside the allowed files, or a `blocked` request: **allow** (paths and reason: `step.build.amend` amends the phase packet, a new hash, LC-7 escape), **revert** (back to `green` with the out-of-scope hunks named), **stop** |

| Rule | Check |
|---|---|
| `BLD-SCOPE-001` | Every changed path in `base..head` matches `allowedFiles`, a new test file by convention, or an amendment |
| `BLD-TEST-001` | No existing test file changed, skipped or loosened unless `testChange` names it (FA §17.23.3) |
| `BLD-RED-001` | Each item's red tests failed on the red commit, before any non-test change; the failing run is in `red.json` with its commit |
| `BLD-RED-002` | The same tests pass on the head |
| `BLD-COMMIT-001` | Commits are atomic and conventional: `type: description`, no merge commits, test commits before the code they cover |
| `BLD-GUARD-001` | FA §17.23.5's diff guardrails on added lines (new suppressions, skipped or focused tests, swallowed errors, a source change with no test change) |

Terminals: `built` (`resolved`), `stopped` (`unresolved`). A builder never pushes, never runs `gh`, and holds no forge
credential (PB10); its Step directory and the worktree are its only writable paths, and the holdout store is denied
(L-18).

### 7.3 Prove: `workflow.prove`

Every acceptance item is proven with evidence tied to the commit it ran on (LN7). It reuses #240's blocks and adds the
before/after and holdout runs.

| State | Kind | Does |
|---|---|---|
| `intake` | block | Seeds; loads the phase's signals |
| `hand-test` | child `workflow.hand-test` (DT) | The real-app run, by hand: each signal walked in the running app (DTC6) |
| `verify` | child `workflow.verify` (DT) | Always runs: the journey over the phase's signals, the evidence pack |
| `evidence` | child `workflow.evidence` (DT) | The route: `ui`, `api`, `db`, `all`, `none`, `other`; every artifact records its commit (DTC9) |
| `before-after` | block `step.prove.before-after`, long | For each item with a `base+head` signal: its recorded script at the phase's base in a scratch worktree must fail, and at head pass (`PRV-BA-001`); `pass` or `fail` |
| `holdout` | agent `step.prove.holdout`, evaluator model, capability `evaluator` (W18) | Runs the phase's hidden scenarios against the head in the running app (§9) |
| `holdout-log` | block `step.prove.holdout-log` | Records verdicts, marks failed scenarios `spent`, writes their redacted findings, checks the canary. `passed`, `failed`, `leaked` |
| `evidence-pack` | block `step.prove.evidence` | `ProofChecks`; assembles and machine-freezes the proof pack. `complete`, `incomplete` |
| `fail-log` | block `step.prove.fail-log` | Every failure path passes here: writes `fact:findings` from whatever failed |

Terminals: `proven` (`resolved`, `fact:proofPack`), `failed` (`unresolved`, `fact:findings`).

| Rule | Check |
|---|---|
| `PRV-ACC-001` | Every visible item of the phase has at least one passing artifact whose `commit` is the head |
| `PRV-SHA-001` | Every artifact in the pack names the same head sha; artifacts from an earlier head are kept, marked `superseded` |
| `PRV-APP-001` | The hand test and the verify journey both ran on the head in the running app, unless the phase kind is `docs` or `config` |
| `PRV-BA-001` | Every `base+head` item failed at base and passed at head, by the same script |
| `PRV-RED-001` | `red.json` is present and `BLD-RED-001` held (FA §17.23.2's "every new test proves itself") |
| `PRV-HOLD-001` | Every hidden scenario of the phase ran on the head and passed |
| `PRV-HOLD-002` | At least `workflow.prove.holdoutMin` of them were never `spent` (§9.3) |
| `PRV-HOLD-003` | The canary's token is in no commit, diff, fact or builder output (§9.3) |
| `PRV-ACC-002` | `LC-ACC-001`: the pack's `acceptance.json` only flips statuses, each with evidence |
| `PRV-NP-001` | `not-proven.md` lists every planned signal without a passing artifact and every `could-not-determine` (FA §17.23.6); "nothing skipped" in one line when empty |

### 7.4 Review

#240's `workflow.review-loop`, changed by DTC4: it reviews the phase's change (branch and head; no PR exists yet), with
the repo's review lens pack (§5.2), one lens per run and one round per iteration, until 0 blocking and 0 issue, capped
at 5 iterations, then one narrow re-check (DT §4.1). Suggestions and nits are answered in writing and never trigger a
round. `clean` ends through `step.review.record`, which machine-freezes the review result packet. `failed` (behaviour
wrong) sends the phase back to build. Its fixes are commits in the phase's worktree under the same scope gate (DTC4).

### 7.5 Ship: `workflow.ship`

No owner stop (LN2): when review passes and the GREEN gate holds, the PR opens.

| State | Kind | Does |
|---|---|---|
| `slot` | wait `{ resource: heavy }` | The full suite takes a heavy slot |
| `green` | block `step.ship.green`, long (W21) | GREEN, local: the full suite, typecheck and lint on the head (DTC5). `green` or `red` |
| `pack` | block `step.ship.pack` | Assembles the hand-off: `body.md` generated by code from the proof pack, the review result and the phase packet (the evidence pack as the body: summary, acceptance table with evidence links and shas, hidden-scenario counts and verdicts, not-proven, review rounds, the stack base, a `test-change` label when `testChange` is set) |
| `freeze` | block `step.ship.freeze` | `SHIP-SHA-001`: proof, review and hand-off agree on the head; machine-freezes the hand-off. `frozen`, or `stale` (the head moved) |
| `opening` | wait `pr.opened` | Entered with `effect.pr.open` (BD1): pushes the branch with a lease on the frozen head and opens the PR, or updates the phase's open PR after a rework, through `IForge` (PB9). Draft or ready by `workflow.ship.draft` (default ready) |
| `return` | block `step.ship.return` | `red` and `stale` pass here: writes `fact:returnNotes` |

Terminals: `shipped` (`resolved`: `fact:pr`, `fact:handoff`), `returned` (`unresolved`: `fact:returnNotes`, back to
build), `refused` (`failed`: the forge refused the PR, parked at the phase's owner desk).

### 7.6 Babysit and docs sync: the interface

The parked PB draft owns both recipes' internals. The line needs these from them, filed as BD-rows (§12.8):

| Hand-over | From → to | Facts |
|---|---|---|
| Start | `workflow.phase` → `workflow.pr-babysitter` | `fact:pr` (link), `fact:handoff` (hash), `fact:phasePacket` (hash: the allowed files for PB's diff-scope gate), `fact:lineContext` (line, phase, stack parent) |
| Docs | babysitter → `workflow.docs-sync` (PB5, once, at the first CI-green snapshot) | `fact:pr`, plus `fact:designPacket` so docs sync knows which docs the design says change (BD5) |
| Ready-to-merge (stop 3) | babysitter `ready` | Entered only when **CI is green on the head and docs sync has ended** (`opened` or `not-needed`); `ready`'s needs-you row is the line's stop 3, shown by `ReadyView` (API11) |
| End | babysitter → `workflow.phase` | `merged` (`resolved`: `fact:mergedSha`), `closed` or `stopped` (`unresolved`), `rework` (`failed`: the owner's `command.rework` notes as `fact:reworkNotes`, BD8) |

Babysitter fixes after the PR opens stay inside the phase's allowed files (BD2) and mark the proof stale when they
touch non-test source; before `ready` it re-runs a narrow `workflow.prove-recheck` (the verify journey and the recorded
evidence scripts on the new head, no route) and appends the result to the merge record (BD4).

## 8. Parallel phases on schema 1

### 8.1 Options

| Option | For | Against |
|---|---|---|
| **A. Sequential children**: `workflow.line` waits on one `workflow.phase` child at a time | Needs only P7; simplest | No parallelism: 20+ PRs in series, against LN5. The child chain goes 5 deep (line → phase → prove → evidence → evidence-all → evidence-ui), past `RCP-CHILD-003` |
| **B. Linked instances started one at a time by a dispatcher, capped (recommended)** | Real parallelism in schema 1: one start per transition, a cap, events back. A phase is a linked root, so depth stays at 4. At cap 1 it is A | Needs W15 (a start effect and a lifecycle event to the parent) |
| **C. Schema-v2 fan-out (IDEA-054)** | The general answer | Engine-LATER (triage, 2026-10-15); the line would wait on schema v2 |

**B is recommended** (LN11). If W15 slips, the line ships first as A with `RCP-CHILD-003` raised to 5 (W22), and
moves to B without changing `workflow.phase`.

### 8.2 The dispatcher

`step.line.dispatch` is a pure block over `fact:planPacket`, `fact:phaseBoard` and the last `fact:phaseEvent`:

1. Fold the event into the board: a phase is `waiting`, `running`, `shipped` (its PR open, babysitting), `merged`,
   `failed` or `skipped`.
2. If every phase is `merged` or `skipped`: `done`.
3. If a phase is `failed`: `blocked` (the owner desk; nothing new starts until it is answered).
4. Count running phases (`running` or `shipped` and not yet merged). At `workflow.line.parallel` (default 2; never
   above the box's heavy-slot cap): `full`.
5. Pick the first **ready** phase in plan order (§8.3): `start`, with `fact:nextPhase` `{ phase, packet, base,
   stackParent }`. None ready: `full`.

`start → starting` carries `effect.line.start-phase`, which starts `workflow.phase` as a linked instance with id
`{line}/phase.<id>` (idempotent by that id). `starting` waits for its `started` event, then `dispatch` runs again, so
the line starts phases one at a time until it reports `full`, then waits in `building` for any phase event.

### 8.3 Readiness and stacking

| Phase's dependencies | Ready when | Base |
|---|---|---|
| None | Always | The default branch head |
| All merged | Always | The default branch head |
| Exactly one not merged, and it is `shipped` | Its PR is open | That dependency's head branch: the PR is stacked on it |
| More than one not merged | When at most one is left unmerged and it is `shipped` (`waitsForMerge`, `PLN-STACK-001`) | As above |

- Parallel phases touch disjoint files by construction (`PKT-SCOPE-002`), and each builds in its own worktree.
- A stacked PR can't merge before its base: the babysitter refuses `command.merge` while the PR's base isn't the
  default branch, and restacks it when its base merges (BD4). Stop 3 therefore comes in stack order.
- A dependency sent back to build after its dependents started (a rework at ready-to-merge) moves its head; the
  dependents' babysitters restack on the `pr.changed` event (BD4).

### 8.4 Heavy slots and budgets

Builders, the GREEN gate, before/after and evidence runs take a heavy slot (W17), so `parallel: 2` never runs more
than the box's cap of heavy jobs, whatever the dispatcher started. Each phase has its own budget (§6.2); the line's
budget is the sum of the plan's, checked at `freeze` (`PLN-SIZE-001`).

## 9. Hidden scenarios: kept from the builder

IDEA-026 ("scenarios the worker cannot read"), FA §17.4 ("stored where the implementer can't read it") and PKT-HOLD-002
("held-out text lives in sindri-private state, mounted only into evaluator Steps").

### 9.1 Where they live

The **holdout store**, `$AW_SINDRI_HOME/holdout/<instance-slug of the line>/<phase>/` (0700, files 0600), is outside
every repo, every worktree and every run area (W11, W18). It holds one set per phase: `set.json` (scenarios, each `{
id, item, steps, expected, kind }`, plus one canary scenario carrying a random token phrase) and `seal.json` (the
set's hash and the plan packet's). Packets hold ids, evidence kinds, counts and the set hash only.

### 9.2 Who can read them

| Actor | Reads the store | How enforced |
|---|---|---|
| `step.plan.holdout` (author) | Writes its own phase sets; reads the design packet, not the plan agent's session | Capability `holdout-author` (W18): its Step directory is inside the store, not the run area; it shares no session with `graph` |
| Planner, builders, reviewers, ship | Never | The store path is denied in their L-17 policy (L-18); facts and packets carry no text (PKT-HOLD-002); the store isn't an MCP resource |
| `step.prove.holdout` (evaluator) | Its phase's set, read-only | Capability `evaluator` (W18, L-19): the set mounted into its Step directory, the worktree read-only, the app runnable through the evidence CLIs |
| The owner | Counts and verdicts in every view; the text only through `sindri line holdout show <phase>` (protected, typed confirm), which marks every scenario it shows `spent` | API13 |

### 9.3 Results without leaking the scenario

- The evaluator's report (`fact:holdoutResult`) carries, per scenario, the verdict, the item id, a category and an
  **observed** line: what the app did, at most 200 characters, never what the scenario expected or its steps.
  `holdout-log` refuses a report whose observed line shares a run of 8 or more words with the scenario's text (a
  deterministic overlap check) and parks the Step.
- A failed scenario becomes a finding for the builder in terms of its visible item ("AC-4: an expired link opened
  from an export answered 200"), which is an ordinary bug report. That scenario is then `spent`: it still runs as a
  regression check, but no longer counts as hidden (`PRV-HOLD-002`).
- **Canary:** each set has one scenario whose token phrase must never appear outside the store. `holdout-log` scans
  the phase's diff, commit messages and every fact of the phase's instances; a hit is `leaked`, the phase fails with
  `PRV-HOLD-003`, and the owner desk offers a fresh set.
- When too many are spent, `prove` fails `PRV-HOLD-002` and the phase's owner desk offers `sindri line holdout add
  <phase>` (protected), which re-runs the author for that phase and re-seals it (Q4).
