# Fix list from the impeccable critique (2026-10-14)

Owner decisions: one health word (worst verdict wins); stripes removed and the band only while needs-you > 0;
read-first focus and Cancel behind a confirm; 760 for every instance sheet. Source: `impeccable-critique.md`.

## Wave 1: the shared shell (applied identically to all eleven files)

1. **Top bar fits 1280.** Nav of seven with the fixture's counts; the search field `flex: 1 1 160px` with the
   placeholder "Search or run a command" and the ⌘K kbd inside it, never clipped; the health pill
   `◐ Degraded · updated 2s ago · index failed · profile changed` fully visible at 1280 with 16 px to the edge.
2. **The attention band** is static (no animation) and present only on pages where the needs-you count is non-zero
   (Home, Work, Agents' waiting group); absent on Workflows, Recipes, Context, Context file map, System, and the
   recipe inspector. A `prefers-reduced-motion` rule is unnecessary once nothing moves; keep the comment that it
   pulses once on change in the real app.
3. **No side stripes.** Remove every `.bar`, `border-left > 1px` accent and inset box-shadow selection stripe. Needs-you,
   parked, escalated and changed rows get a full-row `--attn-soft` tint plus their chip and word; selected rows get
   `--s3` background and `aria-selected`, no stripe.
4. **Glyph vocabulary** from `fixture.md`: one `.g` class set (`.g-done`, `.g-live`, `.g-prog`, `.g-todo`,
   `.g-you`, `.g-park`, `.g-esc`, `.g-model`, `.h-ok`, `.h-deg`, `.h-fail`, `.h-abs`) with the same glyph and colour
   everywhere; one legend text, rendered by the same partial on every page that has a legend.
5. **The protected-action label**: one pattern (`⌂ Grant rounds · terminal`), always a `<button class="btn term">`,
   including Workflows' NOW cell for pagerduty P-2211 and Recipes' re-approve.
6. **No em dashes anywhere** in copy or labels; the tabular null stays `—` only in numeric cells (keep it, it is a
   value, not punctuation). Replace prose dashes with a middle dot, a colon or a sentence break.
7. **Type floor.** Functional text (labels, kbd, captions, eyebrows, cells) at 11 px minimum; body 12 px minimum; the
   page has at most three sizes below the title: 13 body, 12 meta, 11 eyebrow and kbd. Line height 1.4 on multi-line
   text. Chips and kbd get 2 px vertical and 6 px horizontal padding; cells 8 px.
8. **Contrast**: `--ink-tertiary` only on disabled controls; null cells and separators use `--ink-subtle`; the kbd
   inside the primary button uses `--on-primary` at 85 % opacity on a 12 % darker pill.
9. **Lavender roles**: primary button, focus ring, links, the current-nav indicator. Not for the selection stripe (gone),
   not for the ▣ glyph (ink), not for the anchor node stroke (use `--line3` plus the ▣ glyph).
10. **Caption**: `position: absolute; bottom: 12px; right: 16px` of the page, not fixed, so captures never cover content.
11. **Fixture**: every id, title, count and word from `fixture.md`; the health word Degraded everywhere; nav counts
    Workflows 12 · Work 12 · Recipes 7 · Agents 4.

## Wave 2: per page (the refine, enhance and fix passes)

Each page agent applies, in this order, the relevant references: layout, harden, clarify, onboard, typeset, quieter,
distill, colorize (check only), animate (hover and press only, 120 ms; no page-load motion), delight (one moment
per page at most, in copy, never decoration), adapt (the 900 × 600 minimum: the page scrolls, the sheet goes
full width under 1100 px, note it in a comment), optimize (no fonts beyond the two; no layout-animated properties).

### home.html
- Workflows strip: `8 open` (open = not terminal, owner 2026-10-14), five rows, "… 3 more" as a link, a collapsed "4 done today" line; the triage row's [Answer A].
- Running panel: with-you row first, then the four running rows; "Show all 5 in Work".
- Initial focus on the park row's "Open in Claude Code" (read action), not Accept.
- Draw the empty state of Needs you as a comment block (`<!-- empty: "Nothing needs you. Last cleared 11:12." -->`).

### workflows.html
- The pagerduty NOW cell's protected action becomes the locked button; "needs your class" → "needs your severity".
- triage.alert group: three rows plus "… 3 more done" line; group header count 6 stays.
- One key per action: drop "N start" from the legend, keep "Start… ⌘N"; A answer, B hand back, X cancel (with confirm).
- Stage headers: lowercase mono everywhere (the sheets' lists behind must match).
- Initial focus on the first row, not on "Start…".

### workflow-project.html, workflow-incident.html
- Sheet 760 (already); the list behind matches workflows.html's table markup and headers (one implementation; copy it).
- Initial focus: Attach (incident) and "Open in Context" or the Timeline tab (project); Take over is never pre-focused.
- Cancel X moves into a chevron menu on the subject line with a one-line confirm state drawn once (`<!-- confirm -->`
  block visible on the incident: "Cancel INC-14? The mitigation agent is stopped and the comms child closes. [Cancel instance] [Keep]").
- Children titles never truncate below the whole first word plus the id; the status column shrinks first.
- Remove the literal "(state)"; the budget line reads `budget 1.2M / 3M this state`.
- The crumb title never truncates: the meta wraps under it.
- States band: a long recipe wraps to a second row with the link glyph on the row that continues, never dangling.
- Tabs row: a key hint `1 2 3 4` and one line of content under the active tab (Timeline's first two events).
- Facts eyebrows and band captions at 11 px (no 10.5).

### item-sheet.html
- Sheet 760; Facts and Children side by side with Children 0 as a footer line under Facts.
- Page head counts on one line; "Create item" never clips; the Step column shows whole words.
- Subject and facts from `fixture.md` (ENG-511 intake, no project).
- Initial focus on Attach.

### recipes.html, recipe-inspector.html
- Opening a recipe: the row is the affordance (title as a link, `↵ Open` in the key line); "Validate file…" is secondary.
- The 30-day selector is the same `.btn` control as Agents' (28 px).
- Inspector width exactly 760 and the page never wider than 1280; the States band wraps rather than overflows.
- OPEN rows: the park/escalation words never clip under the lock button (button goes to its own line if needed).
- OUTCOMES bars render as bars; "◐◐" becomes "◐ 2"; the blocks/definition line is visible at 800.
- A recipe with ten states: comment how the band wraps.

### agents.html
- Each live row: Attach (primary read action) and a chevron menu holding Take over, File a request, Cancel (with
  confirm). Tool and model steps: Open instance and the menu. With you: Attach, Hand back. Waiting: the answer, Open in
  agent (split), menu.
- "$4.12 today" and the 30-day control are two frames: label the table "BY AGENT · 30 days" and the budget "BUDGET · today" and drop "30 days ▾" from the page head.
- The with-you row gets a meter track like the others.
- Budget legend on one line; the soft and hard marks explained in words.
- Empty state comment for 0 live.

### context.html, context-filemap.html
- Map-review proposal row: the kind chip, the key, the confidence and the evidence line never overlap; actions on their own line under the row at this width.
- "Build draft" is secondary; the focused primary is "Accept all deterministic"? No: the focused element is the anchor search (read-first). Say under Build draft what it does to the 46 proposals ("keeps pending proposals").
- The graph | tree toggle becomes a visible segmented control beside the lenses, 13 px.
- The anchor field keeps a focus ring.
- File map: replace the box-drawing characters with indentation and a disclosure glyph (▸ ▾) in a proportional-safe layout; keep the columns.
- The "bands unchanged" placeholder becomes the two bands collapsed to real header lines with their counts.
- The conflict ▸ button and cross-band edges are positioned relative to their nodes (SVG or flex), not absolute pixels.
- Node card: "Neighbors N" loses its N hint (N is Start elsewhere); use `→` to open.

### system.html
- Impact panel: each consequence is one block-level line; nothing overlaps.
- Legend on one line; the deps.yaml mention in prose, not mono mid-sentence.
- "1 (exit 1)" → "1 · exit 1"; "escalate TO fired" → "escalate timeout fired".
- The services placeholder becomes the real services table header line with "5/6 ok · 1 failed (index)".
- Initial focus on "Run again" stays (it is the fix).

## Wave 3: the second critique's P1s (owner, 2026-10-14)

Source: `impeccable-critique.md`, run 2. Apply in every file where the element appears.

1. **Attach means one thing.** Under every Attach button the fixture sentence; Home's "Attach pauses automation (a
   takeover)" and Agents' legend line are replaced by it. Take over keeps its own button where the action set has it.
2. **Not-yet-wired actions use one pattern.** The decision row's sentence becomes the fixture's; Accept stays live.
   Any other control that waits on an API phase is disabled with the dashed pending chip (as Run again is).
3. **Fixture drift.** Recipes gains `workflow.project` as the first row (nav count Recipes 8 in all eleven files, page
   title and totals per fixture.md); the inspector's list behind gains the same row; the engine table has the eight
   rows and `activities 4 running`; `code index ✕ 03:00` on Context, the file map and System's graph and impact panel;
   INC-14's NOW and `PRD-exports.md` per fixture.md in the lists behind both workflow sheets.
4. **One behind-sheet rule.** Workflows (behind workflow-project and workflow-incident), Work (behind item-sheet) and
   Recipes (behind recipe-inspector) all reflow to the narrow live list: id · title · state, full-width headers, no
   scrim, no `inert`, no sliced columns.

## Wave 4: the calm pass (owner, 2026-10-14: "too busy compared to Superplane or Human Layer")

Reference patterns: Human Layer's task view (a small uppercase status chip, the title with at most one tag, when,
one action; no borders inside the panel; the Linear key as the only id); Superplane's cards (one title, one chip,
one age). Eight rules, applied to every page. Where a rule and an earlier wave disagree, this wave wins.

1. **Sections by surface, not cards.** Pages have no bordered or rounded panels. A section is a heading at 15 px / 500
   with 32 px above it; consecutive sections alternate bands (canvas, then surface-1, full width of the page column);
   hairlines only between table rows. The sheet keeps its card (the one lifted thing); menus and the palette keep theirs.
2. **Ids recede.** Titles lead every row. `ITM-…`, `REQ-…`, `PRJ-…` and `snd-…` ids are hidden by default
   (`.id` visible on row hover and focus-within, always in the sheet header, always inside copyable commands). Linear
   keys (`ENG-511`, `APP-212`) stay visible where the row has one. The engine table and the history keep their ids
   (they are the subject there).
3. **One inline action per row**: the resolving action, then a ▾ holding the rest. No second button, no kbd hint in
   rows (hints live in `?` and appear on focus). Copyable command lines move into the ▾ and the sheet; the protected
   button still reveals its line in place when pressed.
4. **Three or four columns per list**: status chip · title (+ at most one tag) · when · action. Workflows' stage
   columns become one slim segmented progress bar per row (`▰▰▰▱▱▱`, one segment per state, the current one named
   beside it); Agents rows: chip · session's item title · when · action, the meter only on hover/focus or in the sheet;
   Recipes keeps its numbers but drops OPEN and TREND into the inspector; System's engine table keeps its columns.
5. **A real type scale**: page title 20 / 600; section heading 15 / 500; row title 14 / 500; body 13; meta 12; status
   chips 11 / 500 uppercase. Nothing else at 11. Hierarchy by size and weight.
6. **Colour groups, chips state.** The attention tint (`--attn-soft`) sits behind the whole Needs you section once,
   never per row. State is an uppercase chip in tinted ink: PARKED, ESCALATED, DECISION, FAILED, PROFILE, WITH YOU,
   LIVE, RUNNING, QUEUED, DONE, DEGRADED, OK (chip background `--s2`, text `--ink-muted`; attention chips text
   `--attn`; failed `--error`; live and ok `--success`). The glyph-plus-word pairs and the legends go. The accent
   stays on the one primary per page and the focus ring.
7. **Nothing repeated.** Repo tags only when a row's repo differs from its section's majority; "needs you now" and
   the per-row motion words go; the glyph legend partial goes; the health strip becomes one sentence.
8. **Density by rhythm.** 40 px rows, 16 px gutters, five rows per Home section then "all N →"; 24 px between a
   heading and its rows.

Also in this wave: Home's tool-step rows (ITM-01JE9, ITM-01JD6) get `Open instance ▾`, not Attach, as Agents has.

### Wave 4a: the shell (one script, all eleven files)
The base CSS gains the type scale (rule 5), the `.chip` set (rule 6), `.section` and `.band` (rule 1), `.id` hover
rule (rule 2), 40 px rows (rule 8); the panel border and radius rules are removed from page CSS where they apply to
page sections (not the sheet, menus, palette). The legend partial and `.motion` words are removed everywhere. The
health strip becomes the one sentence. Nav unchanged.

### Wave 4b: per page
- home.html: the approved drawing below (Needs you band with five chip rows and one action each; Workflows with
  progress bars; Running and Queued as two chip lists; the one-line health sentence).

```
Home        5 need you · 4 running · 1 with you · 4 queued

Needs you                                                                                        Mark all seen
▒▒▒ attention band behind the whole section ▒▒▒
  PARKED      Retry storm in outbox dispatcher                 checks failed twice      12m   Grant rounds · terminal   ▾
  DECISION    Re-scope: drop the iOS head from this item        filed by a session       26m   Accept   Decline        ▾
  ESCALATED   Context map review for tabs.layout                judge 0.62, below 0.80   2h    Accept · terminal        ▾
  FAILED      Nightly index build  SND-INDEX-012                graphify timed out       9h    Run again
  PROFILE     Profile changed since approval · 2 keys                                    2h    Approve · terminal
▒▒▒

Workflows    8 open                                                                                all 8 →
  Outbox retry storm        incident      ▰▰▰▱▱▱  mitigate 1/3     agent rolling back · comms posting      1h 24m
  sentry #8812 TypeError    triage        ▰▱▱▱    classify         needs your severity                     4m   Answer
  Offline sync              project       ▰▰▰▰▰▱▱ build 4/9        1 running · 1 parked · 3 queued          3h
  Billing exports           project       ▰▰▰▰▱▱▱ design           with you: import the design output      20h
  Tab manager v2            project       ▰▰▰▱▱▱▱ scoping 2/3      challenger round 2                       46m

Running                                                      Queued
  WITH YOU   Onyx adapter secret pointers    25m  Hand back     Stream limits 429 fallback          1h    Open ▾
  LIVE       Add needs-you ack route         6m   Attach ▾      doctor views line                   52m   Open ▾
  RUNNING    Tester: decision race test      2m   Open ▾        Tray badge on Linux                 40m   Open ▾
  RUNNING    Judge outbox drain              1m   Attach ▾      Receipt verify route                12m   Open ▾
  RUNNING    Services page job panel         0m   Open ▾

Sindri   Running · pid 48211 · epoch 7 · API v1 · views 2s ago · 5 ok, 1 failed (index) · profile changed since approval
```
  The progress bar is a slim segmented bar (CSS, not glyphs): one segment per recipe state, filled up to the current
  one, the current stage named beside it. The health line may read as wave 4a wrote it ("Sindri is degraded: the code
  index failed at the 03:00 build and the profile changed since approval. Updated 2s ago.").
- workflows.html: chip · subject title · progress bar with the stage name · NOW · when · action; the recipe group headings
  as section headings, not table captions.
- workflow-project.html, workflow-incident.html, item-sheet.html: the frames keep the sheet card; inside it, FACTS and
  CHILDREN become sections (rule 1) and children rows follow rule 3 and 4; the list behind follows the page rules.
- recipes.html, recipe-inspector.html: chip · recipe · runs · done without you % · median · last change · action;
  the inspector's small panels become sections in one band.
- agents.html: per rule 4; the group headings as section headings; BY AGENT and BUDGET as two sections side by side
  without borders.
- context.html, context-filemap.html: the three bands stay (they are the design) but lose their panel borders: the
  band backgrounds alternate instead; node card, map review and sources become sections.
- system.html: the graph and impact lose their panel borders (alternating bands); engine table keeps its columns.

## Wave 5: consistency (the third critique's P1s and P2s; owner rule 2026-10-14: one label-to-colour mapping)

1. **Chips from the table.** Every `.chip` on every page uses fixture.md "State chips" verbatim (word and class);
   `LIVE` becomes `RUNNING`; behind-sheet rows keep the page's chip and carry the stage as a tag; Home's Workflows
   strip rows get the chip column; the health pill and Home's health sentence use the same colours.
2. **Ids at rest.** Ids inside prose are never hidden. Rows on Work, Agents, Workflows and the engine table draw the
   id in a fixed-width mono slot in `--ink-tertiary` at rest, `--ink-subtle` on hover and focus, so nothing shifts.
   Ids stay hidden only on Home's needs-you band and Workflows strip, where titles are unique. One mechanism (`.id`
   with `.at-rest`), no `.key` exception.
3. **Run again** is the pending pattern (disabled, dashed "Available with API P6", the copyable command) on both Home
   and System until API P6; System's initial focus moves to Sync now.
4. **Recipes** gets Failed, Tokens/run (p50) and a four-week spark back as mono columns; the action column is `auto`;
   the footnote matches the columns.
5. **Counts**: the inspector's band and Open list include every workflow.item instance in the fixture (JE7, JD9,
   JA1 with you, JE9, JC8, JB4; "◐ 2" at produce); System's history heading reads "last 5 of 88" and the page line
   "12 instances open · 4 activities running".
6. **Agents** shows the token meter at rest in `--ink-tertiary`, brighter on hover.
7. **Workflows**: action column `auto`; Start… carries ⌘N; "Other recipes" is its own heading with two rows; one order
   rule everywhere (most urgent first, then oldest), written here: INC-14, sentry #8812, PRJ-sync, PRJ-exports,
   PRJ-tabs for the strip; within a Workflows group, most urgent first.
8. **Words with every glyph**: `✕ 03:00 failed` on Context and System; `checks ◐ r1 failed` not `●`.

## Wave 6: the fourth critique's P1s (owner, 2026-10-14: "Docs then Wave 6")

1. **One definition of the human queue.** fixture.md defines: *needs you* = the union of parked, escalated, decision,
   failed-service and profile rows (Home's count); *waiting on you* is retired everywhere; *with you* = taken-over
   instances, a neutral chip, never counted as needs-you. Every page-head count is derived from the rows under it:
   Workflows "3 need you" becomes the number of attn-chip rows on the page; Work's and Agents' "waiting on you"
   headings become "Needs you" with the same union rule (Agents: parked + escalated = 2; Work: escalated + decision +
   parked = 3, the parked section folds into it).
2. **Recipes and the inspector from one source.** fixture.md gains a per-recipe table (runs, done, done without you,
   failed, cancelled, human rate = runs with a park answered, an escalation decided or a takeover ÷ runs, median,
   tokens p50, four weekly buckets); Recipes' "done without you %" = done-without-you ÷ runs; the page total is the
   run-weighted mean; the inspector's OUTCOMES, HUMAN TIME and per-state numbers reconcile with the same rows.
3. **The band and the health sentence.** The attention band is removed from every page (the pill carries the state;
   R8-13's band rule is withdrawn, noted in the spec). One health sentence generated from the pill's reasons,
   verbatim on Home and System: "Sindri is degraded: the code index failed at the 03:00 build, the profile changed
   since approval, and Onyx is 2h behind. Updated 2s ago."
4. **The Attach note under every Attach primary**: one `.attach-note` line with the fixture sentence directly under
   the control row of every sheet that offers Attach (item sheet, incident) and under every Running group that offers
   Attach (Home, Agents); the menu copy stays.
Also, cheap and from the same critique: Home's queued-row tag no longer shrinks (`flex:none`, the title truncates);
Timeline panels show "… 39 more" under their two events; Context draws one state (the toast state wins: 15 pending,
the row reads "0 proposals · 31 accepted 12:26", header 15); Agents draws the open menu only (the confirm moves to
a comment); needs-you ids on Home use `.id.at-rest` like the Running rows; "soft 80%" loses its attention colour
until the fill passes the marker; Agents' chip words capitalised in source; the inspector's "human rate" matches
the fixture's formula.
