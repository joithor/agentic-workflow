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
| D3 | Writing is a **job** (`ExplainAnchor`, key `explain:<anchor>@v<N>`) started three ways: (a) the app, when the card is shown for an anchor with no summary at the current map; (b) `sindri context explain <anchor>`; (c) after an F1 refresh promotes a new version, for the anchors that had a summary at the previous version (at most 20, newest first) | (a) gives the owner the card without a button; (c) keeps the cards people already looked at current; the cap bounds the spend |
| D4 | The input is the anchor's `LensesView` at that version, rendered as fenced, untrusted text (labels, decision text, conflict lines, counts). Record bodies are never sent; only the stored scrubbed snippets the view already holds | the same trust rule as every model prompt (retrieval spec §7); nothing the map doesn't already show leaves the box |
| D5 | The output is validated before it is stored: 1 to 3 sentences, each 20 to 280 characters, each citing at least one node key **present in that view**; unknown cites are dropped and a sentence left with none is dropped; zero sentences left is a failed job (`SND-CONTEXT-170`) | the UI highlights cites on hover; a cite to a node not on the canvas would point nowhere, and an uncited sentence is an unsupported claim |
| D6 | Stored text is marked `trust: "model"` and rendered as plain text (no markup); the card's sub-line says "Written from this map" | the reader must know a model wrote it; plain text keeps a hostile label from becoming UI |
| D7 | Storage: one row per (anchor, map version) in `context.db`; rows follow map retention (a pruned version drops its summaries) | the pinned read (as of an approval) shows the summary written for that version, or none |
| D8 | Who may start it: a human or a service (the app's call is the human's); a session caller is refused (`SND-CONTEXT-020`), as for build and review | it spends the owner's model budget |

## 2. Shape

`LensesView` (schema 1, single anchor) gains:

```json
"why": { "mapVersion": "v13", "writtenAt": "2026-10-20T09:12:40Z", "trust": "model",
         "sentences": [ { "text": "Tab manager restores panes per window, which ENG-482 and the decision of 3 Oct both depend on.",
                          "cites": ["feature:tabs.tab-manager", "record:linear:issue/ENG-482", "decision:…"] } ] }
```

Left out when no summary is stored (`JsonIgnore` when null, like `mapCommit`). The multi-anchor read (schema 2) leaves it
out in version 1. A draft read (`draft=true`) never has one.

`ExplainAnchorResult(Anchor, MapVersion, Sentences, Dropped, Tokens)`; summary `"why v13: 3 sentences"`. Ledger:
`context.explain.written` (public, invalidates no view; the lenses read is not a view) with anchor, version and sentence
count, never the text.

Routes and verbs (api lane's surfaces, context lane's handlers): `POST /sindri/context/explain` `{anchor}` → 202 job;
`sindri context explain <anchor> [--detach] [--json]`; a catalog Job row `ExplainAnchor`, label "Write summary". A 409
when the same key is running is quiet for the app.

## 3. Profile

`context.explain`: `{ enabled: true, maxTokensPerDay: 50000, refreshCarry: 20 }`. `enabled: false` turns the job off
(`SND-CONTEXT-001`, the card keeps its fallback). One summary costs about 3k input and 300 output tokens, so the default
day is about 15 summaries; over it, the job fails with `SND-CONTEXT-171` and the card says "Summary budget for today is
used up." No model on the box (`ModelAnswer` unavailable) fails the job with the model port's code; the card keeps its
fallback.

## 4. Errors (this spec owns 170–179)

| Code | When | Card says |
|---|---|---|
| `SND-CONTEXT-170` | the answer had no valid sentence after validation | "No summary could be written for this map." |
| `SND-CONTEXT-171` | over `maxTokensPerDay` | "Summary budget for today is used up." |
| existing 001 / 020 / model unavailable | off / a session / no agent CLI | the fallback line |

## 5. Testing

- Unit (Context): the validator (sentence count, length, cites present in the view, uncited dropped, zero → 140), the
  prompt builder (fenced, untrusted, no record bodies, golden prompt for a fixture view), the budget, the store contract
  (fake and SQLite: write, read by (anchor, version), retention drop).
- Unit (Sindri): job start rules (human ok, session 020, running → 409 quiet), the carry after refresh (anchors with a
  summary at v-1, capped, newest first), catalog pin +1, goldens (`LensesView` with and without `why`, the job row, the
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

Two builders: X1 Core/Store and X1 slice in parallel on the red contracts, then X2 while the ui lane builds X3 against
X2's goldens.

## 7. Open questions for the owner (plain words; defaults are what we do if you say nothing)

- **Q1. Write it automatically when the card is shown?** *Default:* yes, the first time you open a map for something
  that has no summary, Sindri writes one in the background (a few seconds) and the card fills in. The alternative is a
  "Write summary" button.
- **Q2. Daily cap.** *Default:* about 15 summaries a day (50,000 tokens). Raise or lower it in the profile.
- **Q3. Keep summaries current after the map refreshes?** *Default:* yes, for up to 20 of the summaries you opened most
  recently, each time the map rebuilds itself.
