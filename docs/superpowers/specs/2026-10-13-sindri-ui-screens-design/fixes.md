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
- Workflows strip: `12 open`, five rows, "… 7 more" as a link; the triage row's [Answer A].
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
