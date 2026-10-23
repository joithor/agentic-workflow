# Sindri context: the whole map on open, and a query zooms into it

**Part D of** `2026-10-23-sindri-deployable-context-design.md` (the deployable unit). In the pod this is the owner's page, reached through `scripts/sindri-pod.sh connect`; it is not on the shared door.

**Status:** design, revision 1, for the owner's review · **Date:** 2026-10-23 · **Asked by:** the owner, 2026-10-23 (relayed by
sindri-meta): "the context map page must show the FULL map as soon as it's opened (the whole mapping structure, visualized), and a
query zooms into that map; today it only shows something after a search." · **Builds on:** the context retrieval spec
(`2026-10-10-sindri-context-retrieval-design.md`: §8 graph, S-3a/S-3b, S-LB1a/b lenses and subgraph), the F3 context page
(`~/.agentic-workflow/mockups/f3/specs/context.md`, built as U9 in `docs/csharp/sindri-ui.md`), the explain spec
(`2026-10-20-sindri-context-explain-design.md`), the API surface spec (`2026-10-10-sindri-api-surface-design.md` §3.7, §4.2, §9) ·
**Lanes:** context (the data side, W1–W2), ui (the page, W3–W4) · **Next:** the owner reads this; then /specToProvenPR takes §9's
phases to PRs.

## 0. Why

The context map page opens empty. Nothing is read until a person types a starting point or a question
(`ContextPageViewModel.cs:5-9`, "reads nothing before an anchor"), and then the page draws one neighbourhood: three bands around
the anchors, from `GET /sindri/context/lenses`. A person who opens the page to see what Sindri knows about the product sees a text
box. The owner wants the opposite order: the whole map first, so the structure is visible at a glance, and a query as a way to
zoom into part of it.

Nothing on the server serves a whole map today. Every graph read takes anchors or keys (`lenses`, `subgraph`, `neighbors`, `node`,
`path`), `GET /sindri/context/map` returns counts only, and `lenses` with no anchor fails with `SND-CONTEXT-003`. The map has no
levels or groups either: graphify's communities are used as extraction input (`GraphifyGraph.Clusters`) and are not stored.

**Success:** opening the page shows the current map of the repo, fitted to the window, within a second on the owner's box, with no
typing and no setup. Typing a starting point or question zooms the camera into the matching part of that same map, with the rest
still visible behind it, and one key (Esc) or one click ("Whole map") goes back.

## 1. Decisions

| # | Decision | By |
|---|---|---|
| W1 | The page reads the whole map when it opens; a query no longer replaces the map, it zooms into it | owner, 2026-10-23 |
| W2 | One new read, `GET /sindri/context/map/whole`, serves the map at two levels of detail: **nodes** (every node and edge) when the map fits a node budget, **areas** (one entry per top-level feature, with counts and the edges between areas) when it doesn't; one area can then be opened on its own | context lane (writer's call; reversible) |
| W3 | Areas are the map's own features, not a new clustering: every node gets exactly one area by fixed rules over the edges it already has (§2.2), so the grouping is explainable and stable from one map version to the next | writer's call (reversible) |
| W4 | Layout stays on the client (the app's pure, deterministic view-model layout, as F3's `ContextMapLayout`); the server sends structure only, no coordinates | writer's call, matches F3 |
| W5 | No new owner step anywhere on this page. Building the map stays the existing one-click "Build the map"; the whole map needs no approval, profile key or setup | the owner's standing rule: everything that needs the owner is one click |
| W6 | Agent sessions may read the whole map of their own repo (it is a query, like every other map read), labels fenced as untrusted; there is **no** MCP tool for it, since a whole map is too large to be useful in an agent's context (`context_lenses` and `context_node` stay the agent's tools) | writer's call; owner Q2 |

## 2. The data side (context lane)

### 2.1 The route

`GET /sindri/context/map/whole` (query class: human via terminal or app, session cut to its repo, service), registered in
`EndpointTable.MapContextQueries`, a row in the API spec's §3.7 table, no catalog row (queries have none).

| Parameter | Meaning | Default and limits |
|---|---|---|
| `detail` | `auto`, `nodes` or `areas` | `auto`: `nodes` when the map's node count is at most the budget (§2.3), else `areas`. `nodes` on a map over the budget answers `areas` with `"fallback": "over-budget"`, never an error |
| `area` | one area key: answers that area's nodes and edges in full (up to the area cap, §2.3), plus its edges to other areas collapsed to area level | absent; an unknown area is `SND-CONTEXT-200` (404) |
| `locate` | a node key, repeatable: the answer's `located[]` says which area each one is in | up to 100 (more is `SND-CONTEXT-006`); a key not in the map is `{key, area: null}` |
| `mapVersion` | read a retained version (`v12`, `12`, `v12@a41b07d`), as `lenses` does | the current version; a version not kept is the existing S-LB1a code |
| `draft` | read the draft map | false; no draft is the existing draft code |
| `repo` | a human caller's repo, when the profile has more than one | the profile's only repo; a session's is always its own (`X-Sindri-Repo`) |

No map is the existing `SND-CONTEXT-030`, which the page already turns into its "No map" state with the "Build the map" button.

The answer, `WholeMapView` (one record set in `SindriJsonContext`, mirrored in `SindriUiJsonContext`):

```text
{ schemaVersion: 1, mapVersion, mapCommit?, draft, asOf, repo,
  detail: "nodes" | "areas", fallback?: "over-budget",
  counts: { nodes, edges, byKind: { feature, surface, action, thing, flow, decision, record, code } },
  areas: [ { key, label, trust, members, byKind: {…}, unassigned: bool } ],          // always present, sorted by key
  areaEdges: [ { from, to, type, count, confirmed } ],                                 // between areas; confirmed = how many of count
  nodes?: [ { key, label, kind, area, alsoIn: [area], trust, provenance, confidence, confirmed } ],   // detail=nodes, or the one area
  edges?: [ { from, to, type, link, provenance, confidence, confirmed } ],
  area?: { key, truncated, counts },                                                   // when area= was given
  located?: [ { key, area } ],
  truncated: bool }
```

`areas` and `areaEdges` are always there, so the page can draw the overview and a mini-map from any answer. Labels follow the map's
text rules (`MapText`: scrubbed at ingest and egress, 200 characters); a session or a service gets them fenced
`<untrusted source="map">`, a human gets clean text beside `trust`, exactly as `lenses` does. The answer carries no evidence, quotes or
decision text: the inspector still reads those per node from `GET /sindri/context/node`.

CLI: `sindri context map whole [--detail auto|nodes|areas] [--area KEY] [--locate KEY]… [--map-version V] [--draft] [--json]`.
The text form prints the counts line and one line per area (`label · members · features, surfaces, code files`), biggest first,
so a person at a terminal sees the same overview. Exit 0 ok, 1 no map or not found, 2 usage.

### 2.2 Areas: one per top-level feature

A pure function in `Aw.Context.Core` (`MapAreas.Assign(GraphSnapshot) → AreaIndex`), unit-tested on its own, gives every node one
area and up to 5 others (`alsoIn`, for a node that genuinely sits in several). The rules use only the edge types the map has today
(`EdgeRules`):

1. **Features.** A feature's area is the root of its `part-of` chain (a feature that is part of no other feature is a root and
   its own area). A cycle (the map shouldn't have one; the rule must still end) roots at the smallest key on the cycle.
2. **Surfaces** take the area of the feature they are `part-of`. **Actions** take their surface's area (`on`). **Flows** take the
   area most of their steps' surfaces are in (`step-of`). **Things** take the area of the surfaces and actions that read or write
   them most (`reads`, `writes`).
3. **Code** takes the area of what it `implements` or `tests`, most edges first.
4. **Decisions** take the area of what they `decide`; **records** the area of what they `mention`; a decision that only `quotes`
   records takes those records' area.
5. Ties go to the area with the smallest key; the other areas a node reaches, up to 5, go in `alsoIn`. A node no rule reaches
   (a record that mentions nothing yet, a code file no feature claims) goes in one area, `area:unassigned`, shown to people as
   "Not linked to a feature yet" (owner Q3).

The rules read one map version, so the same version always gives the same areas, and an area's key is its root feature's key, so
an area keeps its key across versions as long as that feature does.

### 2.3 Size, budget and cost

No whole-repo map has been measured yet: the only real number is the setup doc's sample doctor row (412 nodes, 655 edges), and the
code index (about 3,100 files on a large repo, code-index spec) holds the symbols, which the map does not. The defaults are set so a
map of that order arrives in one answer, and are checked against the first real map of this repo (W1's proof, §8):

| Limit | Default | Why |
|---|---|---|
| Node budget for `nodes` | 1,500 nodes | about 300 KB of JSON at ~200 bytes a node with its edges; one answer, one layout pass |
| Area cap for `area=` | 500 nodes per area, then `area.truncated` with full `counts` | the page opens a big area in one read; a bigger area keeps its counts and a "more not shown" chip, as F3 bands do |
| Areas | 500, then `truncated` | an areas answer stays well under 1 MB for any map |
| Edges | every edge between returned nodes; `areaEdges` aggregated by (from, to, type) | edges are cheaper than nodes and the page needs them for the layout |

They are constants in `WholeMapRules`, not profile keys (no owner setting to keep, W5); changing them is a reviewed code change.

The answer is computed from the stored graph of one version and kept in memory per (repo, mapVersion, draft, caller scope), the
last 4 answers, since a map version never changes once written (a draft is recomputed when the draft changes). The first read of a
version on a 1,500-node map should take well under 200 ms on the owner's box; W1 measures it and records the number in
`docs/csharp/sindri-context.md`.

### 2.4 Errors

The retrieval spec's code-range row gains **200–209** for this stage (195–199 stay free): `SND-CONTEXT-200` "No such area in this
map" (404, fix: read `areas` from `map/whole` first); 201–209 reserved. Everything else reuses existing codes: 006 (bad
`detail`, more than 100 `locate`), 030 (no map), the S-LB1a version codes and the draft code.

## 3. The page (ui lane)

The context lane owns the contract above; the ui lane owns the page, its layout and its look. This section is the behaviour the
page must have. The ui lane writes the visual slice spec (in its F3 spec, beside `context.md`) and builds it.

### 3.1 States

| State | When | What the person sees |
|---|---|---|
| **Whole map** (new, the page's first state) | the page opens with no item | `GET map/whole` (detail `auto`) the moment the page is first shown; the map is fitted to the stage; the head reads "Context · the whole map · v<N>" |
| Whole map, by area | the answer is `detail: areas` | one tile per area (label, member count, a small row of kind counts), the edges between areas drawn with their counts; clicking a tile opens it (`area=`), double-click or zooming in past the level threshold does the same |
| **Zoomed in** (new) | the person types a starting point or a question and presses Enter, or picks a suggestion, or comes from an item | the existing read runs (`query`, then `lenses`, as today); the camera eases to the box around the answer's nodes (`Motion.Base`; instant under reduced motion); those nodes and their edges are drawn at full strength in their places on the whole map, everything else stays drawn behind at the hover-dim strength (0.42); the lens's band detail (intent above, code below) is the existing inspector and band rows for those nodes. "Whole map" (a button in the control row) or Esc goes back and fits again |
| Item entry | the page is opened from an item | Zoomed in on the item's anchors, as above, with the whole map loaded behind |
| No map | `SND-CONTEXT-030` | the existing empty state and its one-click "Build the map" |
| Building | a map build job is running | the existing building overlay; the whole map is read again when the job ends |
| Refusal | 003, 037 and friends from the query | the existing banner with its suggestions, over the whole map instead of an empty stage |

Selecting a node, the inspector, the Files view, the pinned/live switch and the "Why this matters" card keep their F3 behaviour.
The card keeps the explain spec's rule: it shows for one starting point only, so on the whole map with nothing chosen it reads
"Pick a starting point to see why it matters."

### 3.2 Layout and drawing

- **Where nodes sit.** The whole map uses the same three bands as F3 (why we're doing this, what it touches in the product, what it
  touches in the code), with areas as columns: an area's product nodes sit in its column of the product band, its records and
  decisions above it, its code below. Columns are ordered by the barycenter of the edges between areas, ties by key, so the layout
  is deterministic for a version and moves little between versions. The exact geometry is the ui lane's (`WholeMapLayout`, pure and
  unit-tested like `ContextMapLayout`).
- **Zoom range.** The whole-map world is larger than F3's 1360 x 872, so its zoom floor is "fit the whole world" rather than 62%;
  the ceiling stays 100%.
- **Level of detail on zoom.** Below a zoom threshold (the ui lane picks it, about 35%), nodes are drawn as dots in their area's
  colour with no labels and no pills, and each area shows one label; above it, the F3 chips and cards. Label pills for in-area edges
  stay hover-only (F3 decision 2).
- **Cost.** At 1,500 nodes the page must not create one control per node and pill eagerly, as F3's single-neighbourhood page does
  today (`ContextMapWorld.PlaceAll`): the whole map draws nodes and edges in one `Render` pass (as `GraphEdgesControl` already does
  for edges) and only builds controls for what is on screen above the threshold. Budget: the first frame within 1 s of the page
  opening on the owner's box for a 1,500-node map, and panning at the display's frame rate.
- **Mini-map.** The existing `MiniMapControl` draws from `areas` and `areaEdges`, so it works at both levels.

### 3.3 One click

Nothing on this page needs the owner except what already does: "Build the map" (one click, a job) and accept or reject on a
suggested link (one click each, F3). Promoting a draft stays the protected typed approval it is today (retrieval spec), and this
page does not offer it.

## 4. What stays as it is

- `lenses`, `subgraph`, `node`, `neighbors`, `path`, `query`, `filemap` and their goldens are unchanged; the page keeps using them
  for the zoomed-in read and the inspector.
- No MCP tool changes (W6). `context_lenses` stays the agent's way into the map.
- No new table, no migration: areas are computed from the stored graph.
- No profile key (W5).

## 5. Security

- The route is a query and follows every rule queries have: repo scoping for sessions, egress scrub and untrusted fencing of labels,
  ProblemDetails errors, `X-Sindri-After`. It returns no evidence, quotes or decision text, so it shows less than `lenses` does per
  node.
- A session can't read another repo's whole map (the same `GraphSnapshot.ScopedTo` cut as `lenses`), proven by a test.
- The in-memory answer cache is keyed by caller scope, so a session's fenced answer is never served to a human or the reverse.

## 6. Testing

- **Unit (context):** `MapAreas.Assign` on built graphs: each rule in §2.2, ties, cycles, `alsoIn` capped at 5, unassigned, and the
  same areas for the same version twice. `WholeMapRules`: the auto choice at the budget and one over it, the over-budget fallback,
  the area cap and its counts, 100 and 101 `locate` keys.
- **Integration (context):** the route against a temp `context.db`: goldens under `tests/Aw.Sindri.Tests/Golden/Api/context-whole/`
  (`nodes-small.json`, `areas-over-budget.json`, `area-open.json`, `area-truncated.json`, `locate.json`, `no-map.json`,
  `session-scoped.json`), a session refused another repo, a session's labels fenced, a draft read, a retained version read.
- **UI (ui lane):** `WholeMapLayoutTests` (deterministic, columns by barycenter, unassigned last), `ContextPageViewModelTests`
  (the page reads `map/whole` on first show; a query keeps the whole map and sets the zoom target; Esc and "Whole map" go back),
  headless frames at 1440 and 900 for the whole map, by area, zoomed in and no map, and the existing `ContextStateTests`. The UI's
  DTOs are pinned against the W1 goldens (`ContextGoldenTests`).
- **Owner check (once, by hand):** §8.

## 7. Open questions for the owner (plain words; the default is what we do if you say nothing)

| Q | Question | Default |
|---|---|---|
| Q1 | When the map is big, should the first screen still try to show every item, or group them by feature first? | Show every item up to about 1,500; above that, group by feature and open a group with a click |
| Q2 | Should agent sessions be able to read the whole map of their repo? | Yes, through the HTTP route like every other map read, labels marked untrusted; no new agent tool |
| Q3 | Items that aren't linked to any feature yet (a ticket that mentions nothing, a code file no feature claims): show them? | Yes, in their own group, "Not linked to a feature yet" |
| Q4 | When a query zooms in, should the rest of the map stay visible behind it? | Yes, dimmed; Esc or "Whole map" brings it back |

## 8. Proof

- W1's PR: the goldens, the unit and integration lanes, and one measurement on the first real map of this repo (the first-run
  scratch home, once the owner has approved it): node and edge counts, the `auto` choice, the answer's size in bytes and its time
  cold and warm, recorded in the PR body and `docs/csharp/sindri-context.md`. If the real map is far from §2.3's assumptions, the PR
  proposes new numbers in writing before merging.
- W3's PR: the page opening on the whole map on the UI fixture (`scripts/sindri-ui-fixture.sh context up`, extended with a
  whole-map fixture over the budget and one under), screenshots of each §3.1 state at 1440 and 900, and the first-frame time.
- W4's PR: a recording or a screenshot pair of a query zooming in and Esc going back, on the fixture and on the real map.
- Owner check, once: open the app on the real map; the whole map shows with no typing; type a feature's name; the camera zooms to
  it with the rest dimmed behind; Esc goes back.

## 9. Phases and parallel plan (for /specToProvenPR)

| Phase | Lane | What | Needs |
|---|---|---|---|
| **W1 — the whole map read** | context | `MapAreas.Assign` (Core), `WholeMapRules`, the query service and its in-memory cache, `GET /sindri/context/map/whole` with `detail` and `locate`, the CLI verb, `SND-CONTEXT-200` and the 200–209 range row, goldens, the docs section; the measurement (§8) | nothing; the goldens are the first commit so ui can start from them |
| **W2 — open one area** | context | `area=` with its cap and truncation, `area-open` and `area-truncated` goldens | W1 |
| **W3 — the page opens on the whole map** | ui | DTOs and client for `map/whole`, the open-time read, `WholeMapLayout`, the one-pass drawing and level of detail, the by-area tiles (opening one waits for W2's route: until then a tile click zooms to the tile), the mini-map from areas, the states of §3.1 except Zoomed in | W1's goldens (not W1's merge) |
| **W4 — a query zooms in** | ui | the zoomed-in state over the whole map, the camera ease, "Whole map" and Esc, item entry through it, the card's "Pick a starting point" line | W3; W2 for opening an area on zoom |

Parallel: W1 and W3 start together; W1's first commit is its goldens, which W3 builds its DTOs and fake against. W2 runs beside
W3. W4 follows W3. The context lane's builders take W1 then W2; the ui lane's take W3 then W4. Each phase is one PR, scoped gate:
build plus the unit and integration lanes of the touched projects, and `Aw.Sindri.Ui.Tests` in every phase (W1 and W2 add API
goldens the UI pins; team rule).

## 10. Settled in review, and left to the builders

Left to the builders: the exact zoom threshold for the level of detail, the area tile's look, the camera ease curve, the cache's
size if 4 proves wrong, and whether `MapAreas` lives beside `LensesBuilder` or in its own folder. Each is recorded in code comments
or the docs where it lands.
