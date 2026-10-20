# Sindri context "explain": the "Why this matters" summary for a map anchor

Status: draft for the owner's read (2026-10-20). Asked by the ideas lane for the F3 context map (the owner approved the
pinned "Why this matters" card; UI spec `~/.agentic-workflow/mockups/f3/specs/context.md` §2 row 2b, §4 "Why this matters
text"). Builds on the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md` §7 trust, §8.9 lenses) and F1
(#494: the map refreshes itself).

## 0. Why

The context map shows what an anchor touches: tickets and decisions above, product in the middle, code below. A person
opening it still has to read the whole canvas to answer the one question they came with: *why does this matter, and what
could go wrong?* The card answers it in at most three plain sentences, each pointing at the nodes it is about, so hovering
a sentence lights up its nodes on the map. Today the card shows "No summary written for this map yet."

## 1. Decisions

| # | Decision | Why |
|---|---|---|
| D1 | A new Context slice, `ExplainAnchor`, writes the summary through `IModelPort` with a new `ModelPurpose.Explain` | the map's cleanup agent runs once per map and decides links; a summary is per anchor and is prose. One purpose, one budget, one switch (as every model use, retrieval spec §3) |
| D2 | **Reads never call the model.** `LensesView` gains an optional `why`, filled only from the stored summary for (anchor, map version); absent when none is stored | the lenses read is stored-graph only and must stay fast and outage-proof (retrieval spec §8.9) |
| D3 | Writing is a **job** (`ExplainAnchor`, key `explain:<sha8>.v<N>`, where `<sha8>` is the first 8 hex of the anchor key's SHA-256: a job key allows only lowercase, digits and `._:/-`, up to 80 characters, and anchor keys can have capitals and run long) started three ways: (a) the app, when the card is shown for an anchor with no summary at the **current** map version and no recorded failure (D9); (b) `sindri context explain <anchor>`; (c) after an F1 refresh promotes a new version, one follow-up job per anchor that had a summary at the previous version (at most `refreshCarry`, most recently written first by `writtenAt`; an anchor missing from the new map is skipped quietly; the carry stops quietly when the day's budget runs out, it never shows 171) | (a) gives the owner the card without a button; (c) keeps the cards people already looked at current; the cap bounds the spend |
| D4 | The input is the anchor's `LensesView` at that version, rendered as fenced, untrusted text (labels, decision text, conflict lines, counts). Record bodies are never sent; only the stored scrubbed snippets the view already holds | the same trust rule as every model prompt (retrieval spec §7); nothing the map doesn't already show leaves the box. The prompt says the fenced text is data, the call has no tools, and the answer is JSON: `{"sentences":[{"text","nodes"}]}` |
| D5 | The output is validated before it is stored: 1 to 3 sentences, each 20 to 280 characters, each citing at least one node key **present in that view** (the keys are the anchor, every band item, conflict nodes and decision nodes the view holds; the keys are in the prompt); unknown cites are dropped and a sentence left with none is dropped; a sentence with a URL, markup, a control character or a fence marker is dropped; zero sentences left is a failed job (`SND-CONTEXT-170`) | the UI highlights the nodes on hover; a cite to a node not on the canvas would point nowhere, an uncited sentence is an unsupported claim, and ticket text could carry a link a person might click |
| D6 | Stored text is marked `trust: "untrusted"` (the only values are `trusted` and `untrusted`; model text is untrusted by the retrieval spec) plus `writtenBy: "model"`, and is rendered as plain text; the card's sub-line says "Written from this map · {date}" (from `writtenAt`) | the reader must know a model wrote it; plain text keeps a hostile label from becoming UI |
| D7 | Storage: one row per (anchor, map version) in `context.db`, keyed to the `map_version` row so retention (20 versions) drops them with no extra code; the text goes through `Aw.Redaction` before it is stored | the pinned read (as of an approval) shows the summary written for that version, or none |
| D8 | Who may start it: a human or a service (the app's call is the human's); a session caller is refused (`SND-CONTEXT-020`), as for build and review | it spends the owner's model budget |
| D9 | A failed job (170, 171, model unavailable) stores a failure marker for (anchor, version). The app never auto-starts again for that pair until the map version changes; the CLI and the card's retry start it anyway. The rule is a pure Core decision (like the sync-gap one) | otherwise every open of a card retries and spends again |
| D10 | `why` is served only to a reader who may see every node it cites: at read time a sentence citing a node outside the caller's scope is dropped, and a reader left with no sentence gets no `why` | the summary is written from the full view but read by scoped sessions too (`MapAccess`) |

## 2. Shape

`LensesView` (schema 1, single anchor) gains:

```json
"why": { "mapVersion": "v13", "writtenAt": "2026-10-20T09:12:40Z", "trust": "untrusted", "writtenBy": "model",
         "sentences": [ { "text": "Tab manager restores panes per window, which ENG-482 and the decision of 3 Oct both depend on.",
                          "nodes": ["feature:tabs.tab-manager", "record:linear:issue/ENG-482"] } ] }
```

Left out when no summary is stored (`JsonIgnore` when null, like `mapCommit`). The multi-anchor read (schema 2), a range read (`LensRange`) and a draft read (`draft=true`) never have one; a pinned read of a retained version has the summary written for that version, or none. It is `nodes`, not `cites`, because `LensesView` already has a `Cites` dictionary of record citations. The F3 UI spec (§4) names the shape `why: [{text, cites}]`; X3 amends it to `why.sentences[{text, nodes}]`.

`ExplainAnchorResult(Anchor, MapVersion, Sentences, Dropped, Tokens)`; summary `"why v13: 3 sentences"`. Ledger:
`context.explain.written` (public) with anchor, version, sentence count and the dropped counts by reason, never the text; the lenses read refreshes through the job events, as it does for every context job. A failed job's error carries counts only, and no answer text goes to a log or telemetry.

Routes and verbs (api lane's surfaces, context lane's handlers): `POST /sindri/context/explain` `{anchor}` → 202 job (an anchor not in the current map is refused before a job is queued);
`sindri context explain <anchor> [--detach] [--json]` (anchor key as `context lenses` takes it; follows the job like `map build` unless `--detach`; Ctrl-C stops following, not the job; exit 0 written, 1 failed, 2 usage, 130 interrupted); a catalog Job row `ExplainAnchor`, label "Write summary". A 409
when the same key is running is quiet for the app. New code per the job pattern: an `EventTypes` constant and payload record, `SindriJsonContext` entries, the event catalog golden, the `ModelPurpose` JSON name, and the `PublicAPI.Unshipped` lines.

## 3. Profile

`context.explain`: `{ enabled: true, maxTokensPerDay: 50000, refreshCarry: 20 }` (X2 adds the keys to the profile doc and its check; out-of-range values are `SND-CONTEXT-001`). `enabled: false` turns the job off
(`SND-CONTEXT-172`; 001 means a bad profile value, and the card keeps its fallback). The day is UTC; spend is a counter in `context.db`, reserved from the estimate before the call and settled after, one writer, and the model port's token count is an estimate, so the cap is approximate. `doctor` shows the day's remaining budget. One summary costs about 3k input and 300 output tokens, so the default
day is about 15 summaries; over it, the job fails with `SND-CONTEXT-171` and the card says "Summary budget for today is
used up." No model on the box (`ModelAnswer` unavailable) fails the job with the model port's code, whose message talks about labeling, so the card uses its own text for it.

## 4. Errors (this spec owns 170–179)

| Code | When | Card says |
|---|---|---|
| `SND-CONTEXT-170` | the answer had no valid sentence after validation | "No summary could be written for this map." |
| `SND-CONTEXT-171` | over `maxTokensPerDay` (the CLI message names `context.explain.maxTokensPerDay`) | "Summary budget for today is used up." |
| `SND-CONTEXT-172` | `context.explain.enabled` is false | "No summary written for this map yet." (no auto-start) |
| existing 020 / model unavailable | a session / no agent CLI | "No summary written for this map yet." for a session (no auto-start); "No model is available to write a summary." |

Other card states: while the job runs, "Writing summary…" with the card height reserved; a pinned (older) version with no summary says "No summary was written for this version." and never auto-starts. Each sentence row is focusable; hover or focus lights its `nodes`, and a row whose nodes are all off the drawn canvas shows no highlight. A screen reader hears "cites N nodes: labels".

## 5. Testing

- Unit (Context): the validator (sentence count, length, cites present in the view, uncited dropped, zero → 170, a URL or markup dropped, an injected label never reaches a stored sentence), the
  prompt builder (fenced, untrusted, no record bodies, golden prompt for a fixture view), the budget, the store contract
  (fake and SQLite: write, read by (anchor, version), retention drop), the daily counter (reserve, settle, UTC rollover), the failure-marker decision.
- Unit (Sindri): job start rules (human ok, session 020, running → 409 quiet, unknown anchor refused), the carry after refresh (anchors with a
  summary at v-1, capped, most recently written first, stops at the budget), catalog pin +1, goldens (`LensesView` with and without `why`, the job row, the
  event).
- Integration: lenses read returns `why` after the job ran on a fake model; a pruned version returns none.
- Hand test (scratch home): build a map, open an anchor's lenses, run `context explain`, read `why`, cross-check every
  cite against the view's node keys; refresh the map (F1) and see the summary carried to the new version.

## 6. Phases and parallel plan

| Phase | Unit | Depends on | Observable |
|---|---|---|---|
| X1 | Context: `ModelPurpose.Explain`, settings, `WhyStore` (fake + SQLite + contract), validator, prompt builder, `ExplainAnchor` slice, facade `IContextMap.Explain`, `LensesView.why` read | none | Context goldens with the fake model |
| X2 | Sindri: the job, route, CLI verb, catalog row, label, event, carry after F1's refresh, goldens; Ui JSON context for `why` | X1 (starts on X1's red contracts) and F1 (#494) merged | **In the app:** `sindri context explain <anchor>` then `context lenses <anchor> --json` shows `why` with valid cites |
| X3 (ui lane) | The card renders `why`, hover-to-cite, starts the job when absent | X2's goldens | the card shows the sentences; hovering one lights its nodes |

X1 ends with a gate: the owner reads `context explain` output for about five real anchors with a real model before X2 and X3 start, since the input is only the stored view and the summary could just restate the map. X2 also covers the docs: the Commands and route tables, the 170–172 range, the catalog, the profile doc, and running X1 and X2 against the fake model in a scratch home.

Two builders: X1 Core/Store and X1 slice in parallel on the red contracts, then X2 while the ui lane builds X3 against
X2's goldens.

## 7. Open questions for the owner (plain words; defaults are what we do if you say nothing)

- **Q1. Write it automatically when the card is shown?** *Default:* yes, the first time you open a map for something
  that has no summary, Sindri writes one in the background (a few seconds) and the card fills in. The alternative is a
  "Write summary" button.
- **Q2. Daily cap.** *Default:* about 15 summaries a day (50,000 tokens). Raise or lower it in the profile.
- **Q3. Keep summaries current after the map refreshes?** *Default:* yes, for up to 20 of the summaries written most
  recently, each time the map rebuilds itself, as far as the day's cap allows (20 would cost about 66,000 tokens, more than the default day, so the rest wait for the next refresh).
- **Q4. Read real summaries before the card is built?** *Default:* yes: after the Context part is done, you read five real summaries and say whether they help before we build the app side. The alternative is to build everything at once.
