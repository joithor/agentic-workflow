# Critique: Sindri UI revision-8 mockups (11 files)

Target: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/` (home, workflows, workflow-project, workflow-incident, item-sheet, recipes, recipe-inspector, agents, context, context-filemap, system). Register: product. Context: `docs/sindri/PRODUCT.md`, `docs/sindri/DESIGN.md`.

Two independent assessments: A, an LLM design review (read every HTML file and every 1280-wide render); B, `impeccable detect` 4.5.2 over the directory (220 findings). Neither saw the other.

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | "● Healthy" beside "index failed" on ten pages, "◐ Degraded" on System for the same state; no loading, success or post-click state drawn anywhere |
| 2 | Match system / real world | 3 | Leaks: "needs your class", "accept at a term.", a literal "(state)" after the budget bar, "signal answer (grant)" |
| 3 | User control and freedom | 2 | No undo for "Mark all seen", "Accept all deterministic" (31 writes) or Cancel on a running agent |
| 4 | Consistency and standards | 2 | Same id, different titles across pages; one glyph set with three legends; five spellings of the protected label; two topbar variants; the Workflows list built twice (table vs div grid) |
| 5 | Error prevention | 2 | Take over pre-focused in the project sheet, Accept pre-focused on Home; Cancel X on an unmodified key on every Agents row with no confirmation |
| 6 | Recognition over recall | 3 | Per-page legends for ◐; the graph/tree toggle is a 10.5 px mini-segment; recipe rows show no open affordance |
| 7 | Flexibility and efficiency | 3 | Chords, ⌘K, J/K, bulk accept; no bulk select on Work or Agents; key hints live in four places |
| 8 | Aesthetic and minimalist | 3 | Stripes, the always-pulsing band on all eleven pages, four buttons on every Agents row, a nested box (sheet, panel, expand) |
| 9 | Error recovery | 2 | System's Impact panel is excellent; no failed-action state exists anywhere |
| 10 | Help and documentation | 2 | Nothing explains "no-progress", "desk", "continue-as-new"; the disabled symbols lens explains itself only to screen readers |
| **Total** | | **25/40** | **Acceptable: significant improvements needed** |

## Anti-Patterns Verdict

**LLM assessment.** Not slop in the product sense: a Linear-fluent engineer would trust the chrome. One sans and one mono, restrained colour, no gradients, glass, hero metrics, card grids or modals; rows say their situation and carry one action. Two shared bans are hit: **side-stripe accents** (3 px orange bars on needs-you, parked, escalated and changed rows, inherited from the base shell; 2 to 3 px lavender selection insets, new, in nine of eleven files) and **em dashes** in every protected-action label and some prose. The one second-order trap touched is the box-drawing tree in context-filemap.html (terminal cosplay).

**Deterministic scan (B).** 220 findings in 11 files. Cramped padding 107 (text flush against container edges: chips, kbd hints, search field, table cells). Undersized functional text 57 (10.5 px labels, concentrated in workflow-project and workflow-incident, 26 each: the states band captions and the facts eyebrow). Tiny body text 29 (11.5 px body on agents, context, system, home). Low contrast 9 (kbd on the primary button 3.6:1; `--ink-tertiary` on surface-1 about 3.3:1). Same text repeated 3 (Attach three times inside one panel on agents; home). Em-dash saturation 3 files (16 in item-sheet). Tight line height 1 (recipe-inspector). "Overused font: Inter" 11 is a false positive: Inter is the spec's substitute for Linear's face and the product register allows it. Where A and B agree: the dashes, the contrast of tertiary ink, the Agents row repetition. B caught what A under-weighted: the type scale has six sizes between 10.5 and 13 px and the smallest carry functional text.

## Overall Impression

The visual system is right and the semantic system is loose. The pages look like one product, but the numbers, titles and glyphs do not agree across them, the riskiest actions sit under the first keystroke, and several panels render on top of themselves. Biggest opportunity: one fixture, one vocabulary, one action-safety rule, applied to all eleven files.

## What's Working

1. **Answer, then act, with the class visible.** Needs-you rows, Agents rows and System's Impact panel state the situation, carry the resolving action, mark protected ones with the lock and the copyable command, and explain consequences in place.
2. **One recipe-driven frame renders three kinds of instance.** States, Facts, Children read identically for a project, an incident and an item; the inspector's numbers reconcile (112 = 81 + 12 + 4 + 6 + 9).
3. **Accessibility is designed in.** aria on rows and graph nodes, sparklines and meters as `role="img"` with words, a reduced-motion rule, lock buttons whose title explains the typed confirmation.

## Priority Issues

- **[P1] Cross-page data drift breaks "Nothing invented".** ITM-01JE7 is "Add needs-you ack route" on Home and "Sync status pill" elsewhere; ITM-01JE9 and ITM-01JC8 likewise; Home says 6 running, Work says 2; the strip says 7 open under a tab that says 12; the health word differs by page. *Why:* the mockups are the contract the implementing agents read. *Fix:* one fixture feeding every page; compute the health word one way. Files: home, agents, workflow-project, item-sheet, workflows, system. **/impeccable polish**
- **[P1] Rendering defects that destroy meaning.** context.html map-review row overlaps its evidence line and splits "map_override row" across lines; system.html Impact panel lines overlap (the grid treats inline code as items); recipe-inspector renders 1289 px wide and clips the OPEN rows under the lock buttons; workflow-project Children titles collapse to "Ba…"; workflow-incident's crumb truncates; item-sheet's page-head counts stack, "Create item" clips, the Step column clips. **/impeccable layout**
- **[P1] The topbar overflows at 1280 two ways.** The base's 220 px search pushes the health pill off the edge (eight files clip "◇ profile changed"); three files shrink the search to 192 px and clip the ⌘K kbd instead. *Fix once in the shell.* **/impeccable layout**
- **[P1] Initial focus and single keys put the riskiest action first.** Take over pre-focused in workflow-project (↵ ↵ from the list is a takeover); Accept pre-focused on Home; Cancel X as a bare ghost button on every Agents row; "Accept all deterministic" commits 31 writes with no undo. *Fix:* focus the read action, move Cancel behind the chevron or a confirm, draw an undo state. **/impeccable harden**
- **[P1] One glyph set, three legends.** ● is done, live, ok and Live; ◐ is in progress, with you, degraded and "2 in progress" (◐◐); ○ is not started and absent; with-you is ◎ on Home and Work and ◐ on Agents. *Fix:* one vocabulary and one legend, colour following the glyph identically. **/impeccable clarify**
- **[P2] Five spellings of the protected label, all with the banned dash.** "Grant rounds — at a terminal", "Grant rounds — terminal", "Grant — terminal", "Approve at a terminal", "accept at a term." as passive text. *Fix:* one pattern without the dash, always a button. **/impeccable clarify**
- **[P2] Side-stripes and the always-pulsing band.** The stripes duplicate the chip and the word; the band pulses forever on all eleven pages, including three where nothing needs anyone. *Fix:* delete the bars, tint the row with `--attn-soft`, make the band static and present only while the needs-you count is non-zero. **/impeccable quieter**
- **[P2] The item sheet is 520 wide while its frame is 760.** Facts wrap to three lines, Children 0 occupies a 164 px column for one sentence, the list behind loses Step, Agent and Since. *Fix:* 760 for every instance sheet; Children 0 as a footer line. **/impeccable layout**
- **[P2] Type and padding floor (from B).** 10.5 px functional labels, 11.5 px body, text flush against chip and cell edges. *Fix:* floor functional text at 11 px and body at 12 px, two sizes below the title instead of six, 2 to 4 px inset on chips and kbd. **/impeccable typeset**

## Cognitive Load

4 of 8 failed (high); two are deliberate density per PRODUCT.md principle 4. Real failures: no single focus on Context (six equal-weight panels); no progressive disclosure (every Agents row shows all four actions, and the sheet repeats them). Decision points over four options: the nav (7, approved), the map-review proposal row (5), the inspector head (5).

## Persona Red Flags

**Alex (power user):** no bulk select on Work or Agents; A and X mean different things per page; two keys for Start; Recipes pre-focuses "Validate file…" while opening a recipe has no key; the sheet tabs sit at the bottom with nothing under them.
**Sam (screen reader, low vision):** `input:focus{outline:none}` on the anchor field with no replacement; `role="row"` divs without a grid ancestor in three files; selection contrast about 1.1:1 plus a stripe; `--ink-tertiary` fails AA on null cells.
**Riley (stress):** "triage.alert 6" header over three rows with no "more" line; no empty states for Needs you 0, Agents 0, Workflows 0; a recipe with more than six states overflows the band; mid-word truncation.
**The owner glancing between sessions:** the health pill contradicts itself; the id in their Warp prompt has a different title on Home; ↵ ↵ from Workflows is a takeover; the band pulses for hours so it never signals change.

## Minor Observations

Home "Show all 7 in Queue" beside Queued 4; the split-button chevron has no key hint; stage headers lowercase mono on Workflows and uppercase sans behind the sheets; "(state)" placeholder in workflow-project; `.expand` is a nested card; the States band wraps "closed" with a dangling link glyph on the incident; the with-you Agents row has no meter track; "Validate file…" is the primary on Recipes; the 30-day selector is a 22 px pill on Recipes and a 28 px button on Agents; ◐◐ invents a glyph; the inspector dims its list and the other sheets don't; Take over offered on model and tool steps where takeover is undefined; "$4.12 today" beside a 30-day window; "Neighbors N" collides with "N start"; "Build draft" primary while 46 proposals wait, with no word on whether it discards them; the cross-band edges and the conflict button are absolute-positioned to this content height; collapsed placeholder bands shipped as product UI in two files; the legend wraps mid-sentence on System; raw engine strings "1 (exit 1)" and "escalate TO fired"; the fixed caption covers content in tall captures; arrows mean three things; lavender used for seven roles where DESIGN.md allows four; `#fff` on the primary button and brand mark; no loading or error state in any file.

## Questions to Consider

1. The chip says "park" and the word says "needs you now"; what is the orange bar for, and why does the same orange pulse on Recipes, Context and System?
2. Every live Agents row carries four actions and the sheet repeats them. Which surface is the control surface? Would one Open per row plus one hovered action row be faster and safer than twenty-eight buttons?
3. "● Healthy" sits beside "index failed" on ten pages and System says "Degraded". Is "healthy" the process or the system? One word computed the same way everywhere, or three verdicts?


---

# Critique run 2: Sindri UI revision-8 mockups, after the fix passes

Target: `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design/mockups/` at b84eddee (11 files, re-captured 1280-wide renders). Register: product. Independent assessment A (did not read run 1, fixes.md or the snapshots) plus the detector (B): 9 files clean, 2 with the em-dash advisory firing only on tabular nulls; "Overused font: Inter" ignored as a false positive.

## Design Health Score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | No loading or refreshing state; the decision row's live-looking Accept beside "acted on once the workflow engine runs" |
| 2 | Match system / real world | 3 | Five phrasings for human-blocked; "open" means three things |
| 3 | User control and freedom | 3 | Undo on bulk accept and Mark all seen; none on a single map-review Accept/Reject, Drop, or the decision Accept |
| 4 | Consistency and standards | 2 | Band on 3 of 11 pages; three behind-sheet treatments; Attach primary in some places, secondary in others; hint placement varies; fixture drift |
| 5 | Error prevention | 3 | Cancel behind a menu plus confirm, read-first focus, budget marks; re-scope Accept and escalation Reject unguarded |
| 6 | Recognition rather than recall | 3 | Legends on three pages only; ◆ ? ◇ ░ ⚠ unexplained elsewhere; two adjacent ▾ on Agents waiting rows |
| 7 | Flexibility and efficiency | 3 | Chords, ⌘K, ⌘N, ⌘Z, bulk accept; no multi-select on Work or Agents |
| 8 | Aesthetic and minimalist design | 3 | "needs you now" repeats the panel title; spec-note parentheticals in Agents group headers; Context's Node panel stretched |
| 9 | Error recovery | 3 | Home's failure row and System's Impact name cause, fix and blast radius; refusal and Reload states exist only in comments |
| 10 | Help and documentation | 2 | "? Shortcuts" on four pages; no help surface, no "what is a park" |
| **Total** | | **28/40** | **Good: address weak areas, solid foundation** |

## Anti-Patterns Verdict

Passes the product slop test: one shell, one button vocabulary, one glyph set with a legend, Restrained palette, no stripes, gradients, hero metrics, card grids or modals; every id, hash and command plausible. The machine shows in uniformity: every section is the same surface-1 panel with an 11 px eyebrow and a 13/12/11 ladder, so hierarchy rides on four greys. The detector agrees: nothing left but the tabular-null em dashes.

## What's Working

- Home passes the five-second test: `5 need you · 4 running · 1 with you · 4 queued · Degraded`, most urgent first, every tinted row carrying its resolving action with the class visible.
- One instance frame for a project, an incident and an item; the wrapped states band keeps links attached to their nodes; the `next` sentence is the best copy in the set.
- The inspector's numbers reconcile; the lens stack's cross-band edges are computed from the node boxes.

## Priority Issues

- **[P1] Attach means three things.** Home says Attach pauses automation; Agents says the first keystroke is the takeover; the sheets make Attach the read action beside a separate Take over. One sentence everywhere. **clarify**
- **[P1] Not-yet-wired actions rendered live.** The decision row's Accept is a brand primary beside "acted on once the workflow engine runs", while Run again on the next row is disabled with a pending chip. One pattern. **harden**
- **[P1] Fixture drift.** `workflow.project` runs three instances but is not among the seven recipes; the engine table lacks ITM-01JD9 and ITM-01JD6 and says "activities 2 running" beside four live sessions; the index failed at 03:00 on Home and 12:21 on System and Context; INC-14's NOW differs behind the sheets. **audit**
- **[P1] Three behind-sheet treatments.** Work reflows live; Workflows is sliced with headers cut mid-word; Recipes dims with a scrim. One rule. **layout**
- **[P2] Human-blocked vocabulary and counts.** "need you", "waiting on you", "running at a desk", "to look at", each count with a different scope. **clarify**
- **[P2] Shell drift.** Band, sheet tab, hint placement and legends differ page to page. **polish**
- **[P2] The protected button has no defined click** on six pages; only Home shows the command line. **harden**
- **[P2] Decision points above four**: two chevrons on Agents waiting rows, five verbs on the Context proposal row, five controls on Home's escalated row. **distill**

## Cognitive Load

3 of 8 failed (moderate): single focus on Context, chunking on the lens row and the Recipes and Agents tables, minimal choices on the three rows above.

## Persona Red Flags

**Alex:** no multi-select; two chevrons on one row; the terminal hop with no command on Recipes, Context, Agents; hints move page to page. **Sam:** `● done` and `● live` differ by colour alone; ◐ is in-progress and degraded in one screen; the red "Cancel item" button is under AA on surface-3; the typed-confirmation note lives in `title` only; fixed px grids will clip at 1.3 text scale. **Riley:** "12 open" includes done rows; the index-failure time differs by page; the bulk-accept button stays enabled above its own toast; `workflow.project` missing from Recipes. **The owner:** Accept on the re-scope does nothing; Attach's semantics unclear at a glance; "import the design output" has no Import action on any screen.

## Minor Observations

"needs you now" repeated under three chips; the failed-service row tinted orange beside a red ✕; two "since" values on the item sheet with no label; the behind-lists kept pre-fix column widths; "Pause in the menu" promised but not drawn; destructive before Keep in confirms; press transform on two pages only; no legend on Recipes; the Node panel stretched with empty surface; "Accept all deterministic" enabled above the toast; "212 queries / 7 d" on the SVG edge; the vertical sheet-tab handle is the one invented affordance; `--ink` and `--ink-muted` near-indistinguishable on dark.

## Questions to Consider

1. If Attach is read-only until a keystroke, why does Home call it a takeover; if it is a takeover, why is it the pre-focused read action? Which sentence should the owner believe at 2 am?
2. Is a `⌂ · terminal` button that does nothing the honest rendering of the protected class, or should it open Warp with the command typed?
3. Has "dense, calm, exact" become "uniform"? Where on each page is the one element allowed to be louder than 13 px?
