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
