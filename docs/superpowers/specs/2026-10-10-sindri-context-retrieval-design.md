# Sindri context retrieval engine: the product graph and its retrieval (design)

Status: decisions settled in conversation with the owner, question by question, 2026-10-10. This is the written
spec for review; it goes to `/specToProvenPR` once approved. It refines the factory addendum §17.3 (the context
graph), §17.29.2 and §17.29.3 (the engine, and access from sessions) and §17.17 Q2 (component options), for build
step 3 of §17.29.4. Where they disagree, this document wins for the context retrieval engine.

Read with: addendum `2026-10-09-sindri-factory-addendum.md` §17.3, §17.8, §17.26, §17.29; core spec
`2026-10-07-sindri-design.md` §6.2 (code index), §7.5 (scoping and backtest), §8.3–§8.4 (trust, scrubbing), §11.2
(adapters); `.agents/rules/csharp.md`; `docs/csharp/redaction.md`; issue #75. The TS `sindri/` scope and index
modules are a reference for *what* today's sources read, never for how.

## 1. Purpose and the premise we corrected

The engine answers questions an engineer asks before and during work, joined across three things:

- **intent:** what the business asked for and decided (Linear projects, PRDs and TDDs, comments, Slack threads);
- **the product:** its features and surfaces, how they nest, what state they share, and what a change to one cascades
  into ("Layout organizer > Split Pane > Orientation" affects the tab manager's layout restore);
- **implementation:** where each feature lives in code and what it does.

**Premise corrected.** The starting framing was "the pieces exist; add a retrieval layer on top." Measured against the
repo and the market, that is not right:

| Piece | Where it stands |
|---|---|
| Business intent and hybrid search | Exists as products (Onyx, Atlassian Teamwork Graph, Unblocked, Glean). Commodity: consume, don't build |
| Code index | Exists in TS only, organized by file and symbol (§6.2), TS/JS parser only. The C# rebuild is its own block (§17.29.4 step 4) |
| **Product graph** (features, surfaces, state, cascade) | **Does not exist.** §17.3 gives it one table row. No product surveyed builds it (§13) |
| **The join** (intent ↔ product ↔ code) | **Does not exist.** Edge names only |
| Retrieval and fusion across layers | Small, once the graph exists |

So the core deliverable is the **product graph and the links between layers**. Retrieval is cascade expansion over
that graph plus rank fusion across adapters. Building adapters and RRF first (the old reading of §17.29.4 step 3)
would have produced a weaker copy of an enterprise search engine with no product layer.

```
 Intent layer      Linear projects · Linear Documents (PRDs, TDDs) · issues · comments · Slack
   (Onyx + native Linear adapter, live; Sindri stores refs and derived links only)
        │  decides · mentions        Decision nodes (Sindri's wording + verbatim quote)
        ▼
 PRODUCT GRAPH     Feature ─part-of─► Feature · Surface · Action · Thing · Flow      ← Sindri owns, SQLite
        ▲  implements · tests        (depends-on, reads, writes → `affects` cascade at query time)
        │
 Code layer        symbols · files · modules · call graph (code index block + graphify, adapter)
```

## 2. Decisions and their reasons

| # | Decision | Reason |
|---|---|---|
| D1 | The core deliverable is the product graph plus cross-layer links, not an adapter layer | §1. It is the only part nobody else builds, and every query is anchored on it |
| D2 | **Onyx (community edition) is the business-intent engine**, run unmodified and scoped by its own connector settings. Not ConnectAI | Owner's decision. MIT outside `ee/`, about 55 connectors, a free-edition MCP server, federated (live) Slack search. No fork: connectors and hybrid search are what its team improves weekly |
| D3 | **Onyx is used for recall only; Sindri owns ranking** | Onyx's OpenSearch path today fuses one vector and one keyword query by min-max normalization with fixed 0.5/0.5 weights, with no reranker, no recency and no document boost (`backend/onyx/document_index/opensearch/search.py:81-93`, TODOs at `:157-160`; `hybrid_alpha` and `recency_bias_multiplier` are passed by `context/search/pipeline.py:317-318` but unused there) |
| D4 | A **native read-only Linear adapter** for project structure | Onyx's Linear connector reads issues and comments only (`connectors/linear/connector.py:231-341`): no projects, no Linear Documents (where the owner's PRDs and TDDs live), no project updates, no issue↔PR attachments. The graph and the backtest need all four |
| D5 | One separable package, **`Aw.Context`**, with no Sindri imports | §17.3 Reuse. Sindri composes it |
| D6 | **No rate limiting** in the adapter contract | Every source is on the machine: Onyx on loopback, repos, the index. Upstream API limits are Onyx's concern during its own syncs. A per-query deadline stays |
| D7 | `affects` (the cascade) is **computed at query time**, never stored | Stored, it goes stale on every edge change |
| D8 | Human edits are an **override layer** that survives every rebuild | Ring 1 runs in `assist` mode: the extracted map is a draft until the owner confirms it |
| D9 | **Decision** is its own node: Sindri's wording, citing a verbatim quote, with `supersedes` | What "what was decided about X?" must return. Prior art (context-graph, Bito, linecheck rules) converges on it |
| D10 | Results carry typed **conflicts**: code is the truth for behavior, decisions are the truth for intent | Unblocked's authority rule and linecheck's "spec conflict" (REQ-E3). Disagreement is shown, never resolved silently |
| D11 | **MCP ships in Phase 1**, with HTTP and the CLI | The owner's Claude Code sessions are the main consumer; Phase 1 must be usable from them |
| D12 | The first deployment target is the **owner's work product** (ring 1) | The owner's three related Linear projects, with PRDs, TDDs, decisions and shipped PRs, are the backtest corpus. A fixture app serves the test lanes |
| D13 | The reranker is a **backtest arm, off by default** | It ships only if it beats graph-and-intent ranking on the bars (§9) |
| D14 | Profile-supplied extra scrub patterns go **into `Aw.Redaction`**, not a second scrubber | One scrubber for the whole build; ring 1 needs work-specific identifier shapes |

## 3. Constraints (ring 1 from day one)

- Work accounts only, human-gated, `assist` mode (§17.17 Q5). **No Prism** on this profile, ever (§17.3 Separation).
- Connecting Onyx to company Linear and Slack is company OAuth: the owner does it at setup, with sign-off. Sindri never
  connects a business source itself.
- Model egress: the map labeling pass and the backtest adjudicator send scrubbed code and ticket text to the model
  provider under the owner's Claude login. Both sit behind profile switches (`context.labeling.enabled`,
  `context.eval.adjudicator`) and are off until approved.
- Nothing the engine writes lands in a git worktree: map exports, backtest reports and probe results go under
  `$AW_STATE_DIR/sindri/context/` (0700 dirs, 0600 files), the `sindri scope` guard (SND-SCOPE-025) generalized.
- Before the switch (readiness §4a step 3) nothing in `setup.sh`, the providers or MCP registration points at the C#
  build. Registering the Sindri MCP server in Claude Code is a documented manual step (§8.3).
- Loopback only. Onyx's URL must be a loopback literal; anything else is refused at profile validation.

## 4. Package shape

```
src/Context/
  Aw.Context.Core            domain (nodes, edges, keys, refs, results), ports, pure ranking and packing, errors
  Aw.Context.Features        slices: QueryContext, ShowNode, Neighbors, Path, BuildMap, ReviewMap, SyncSource, SourcesStatus
  Aw.Context.Infrastructure  SQLite store (context.db), adapters (repo docs, Onyx, Linear, code index), ingest sanitizer,
                             model labeler adapter, snippet cache
tests/
  Aw.Context.Tests           by slice; lanes unit / integration / heavy
  Aw.Context.Testing         fakes (FakeContextSource, FakeOnyxServer recorder), builders, the adapter contract base
```

- Tier 1 in `deps.yaml`, beside Judge, Packets and Readiness. Project edges: `Aw.Kernel`, `Aw.Kernel.Data` and
  `Redaction` (Infrastructure only). No edge to anything in `Sindri`.
- Sindri (tier 3) composes it: its Host maps the routes, CLI verbs and MCP tools, and passes the environment to
  `AddContextInfrastructure(environment)`.
- **Open rule question, settled in Phase 1:** `.agents/rules/csharp.md` lets a Host reference only lower-tier **Cores**
  (ARCH-DIR-004), but composing a library package needs its `Add…()` methods from `Features` and `Infrastructure`. The
  same question arrives with every block (Judge, Evidence). Recommendation: a declared `compose` edge kind in
  `deps.yaml` that allows a Host to call a lower-tier package's `Add…()` methods and nothing else, checked by
  `check-deps.mjs` and an ArchUnitNET rule (no other type from that package's Features or Infrastructure used in the
  Host). Phase 1 lands the edge kind, its rule record and its check. If the workflow-engine spec settles this first,
  Phase 1 follows that answer.

## 5. Source adapters

### 5.1 Contract (`Aw.Context.Core`)

```csharp
public sealed record SourceRef(SourceId Source, ExternalId Id, SourceVersion Version, Uri? Permalink);
// Version: git blob sha (repo docs) | commit sha (code) | updatedAt (Onyx doc, Linear doc) | map version (graph).
// (Source, Id) is stable identity; Version changes.

public interface IContextSource
{
    SourceDescriptor Describe();      // id, kind (Intent | Doc | Code), freshness (Live | Snapshot), as-of support
    ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken ct);
    ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken ct);
    ValueTask<SourceHealth> Probe(CancellationToken ct);
}

public interface IChangeFeed         // only sources Sindri derives links from
{
    ValueTask<Result<ChangePage>> Changes(SourceCursor? after, int limit, CancellationToken ct);
}

public sealed record SourceQuery(IReadOnlyList<string> Terms, DateTimeOffset? AsOf, int Limit, TimeSpan Deadline);
public sealed record SourceHit(SourceRef Ref, string Title, int Rank, DateTimeOffset AsOf, bool Leaky);
public sealed record SourceRecord(SourceRef Ref, RecordKind Kind, string Title, string Text,
                                  string? Author, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record ChangePage(IReadOnlyList<SourceRef> Changed, IReadOnlyList<ExternalId> Removed, SourceCursor Next);
```

- **Read-only by construction:** no write method exists on any port.
- **Errors** are `Error` records in `Result<T>` (`AW-CONTEXT-…` codes): `Unavailable` (the source is down; the query
  goes on without it and reports `degraded`), `NotFound`, `Fatal`. No `RateLimited` (D6).
- **Freshness:** every hit carries `AsOf`. `Search` and `Fetch` are live. `Changes` drives a scheduled sync that
  re-derives links when a version moves. A source that can't cut to `AsOf` returns hits with `Leaky = true`.
- **Cursors** are opaque strings persisted per source in `context.db`. A cursor advances only after the links derived
  from its page are committed; replaying a page is idempotent.
- **Deadline:** each `Search` gets the query's remaining deadline. A source that misses it counts as `Unavailable`
  for that query.
- **Adapters return raw text.** Sanitizing, scrubbing and trust tagging happen once, in the engine (§7), so no adapter
  can forget them.

### 5.2 The adapters

| Adapter | Kind | Search | Fetch / version | Change feed | Phase |
|---|---|---|---|---|---|
| `repo-docs` | Doc | Lexical (SQLite FTS5 over doc chunks at HEAD of the profile repos' doc globs) | `git cat-file` at the blob sha; line spans | Commits touching the globs | 1 |
| `onyx` | Intent | Onyx search (MCP `search` tool or HTTP search API), several sub-queries, top-k candidates | Onyx document by id; version `updatedAt` | Onyx document listing by updated time (to verify in Phase 5) | 5 |
| `linear` (native) | Intent | Not used for search (Onyx searches issue text) | Projects, Linear Documents, project updates, issue↔PR attachments; version `updatedAt` | `updatedAt` filter, by team | 6 |
| `code` | Code | Index lookup: symbol name, path, embedding neighbors | Symbol at a commit sha | Index rebuilds | 9 (after the code index block) |

**Onyx scoping** (standard Onyx, configured by the owner): Slack federated (live, nothing indexed) or indexed for the
product's channels only; Linear indexed from a start date covering the backtest projects (its connector has no team
filter, so it reads every issue the key sees after that date); docs connectors limited to the product's spaces.
Sindri stores none of Onyx's corpus, only refs, cursors and derived links.

## 6. Data model

### 6.1 Graph

| Node | Meaning | Key example |
|---|---|---|
| `Feature` | A named capability; nested by `part-of` | `feature:layout.split-pane.orientation` |
| `Surface` | Where a person meets it. `kind`: screen, pane, dialog, command, keybinding, setting, menu, route, api, job | `surface:command/split-vertical` |
| `Action` | What a person or system does on a surface; preconditions, effects | `action:split-pane.split-vertical` |
| `Thing` | State or data the actions read and write, with its states | `thing:pane-layout` |
| `Flow` | An ordered path through surfaces and actions | `flow:open-tab-split-resize` |
| `Decision` | A decision in Sindri's own wording, citing a verbatim quote | `decision:<hash>` |
| `Record` (ref) | Pointer to an intent record (Onyx doc, Linear issue, project, document) | `record:linear/ENG-482` |
| `CodeUnit` (ref) | Pointer to a symbol, file or module in the code index | `code:src/layout/SplitPane.ts#setOrientation` |

| Edge | From → to |
|---|---|
| `part-of` | Feature → Feature; Surface → Feature |
| `on` | Action → Surface |
| `moves-to` | Surface → Surface |
| `reads` / `writes` | Surface or Action → Thing |
| `depends-on` | Feature → Feature |
| `step-of` | Surface or Action → Flow (ordered) |
| `implements` / `tests` | CodeUnit → Feature, Surface or Action |
| `decides` | Decision → node |
| `mentions` | Record → node |
| `quotes` | Decision → Record (the verbatim quote's source and span) |
| `supersedes` | Decision → Decision |

**`affects`** is computed per query: from the anchor, follow `depends-on` (reverse), `writes` → `reads`, and `part-of`
(up and down), to a depth cap (default 2, `context.cascadeDepth`). Each affected node reports its path.

Every node and edge carries:
- a **stable key** from a versioned `EntryKey()` (golden-snapshot tested), plus an alias table, so a rename keeps links;
- **provenance** `deterministic | model | human` and a confidence (1.0 for deterministic and human);
- **evidence**: `SourceRef[]` with versions;
- **computedAt**: map version and commit sha.

**Override layer.** Human actions (accept, reject, rename, merge, move, add, delete) are rows in `map_override`, applied
over every rebuild. A rejected model proposal is never proposed again for the same evidence. A link whose key disappears
is `orphaned`, not deleted, until a human resolves it.

**Model-suggested links** are used for recall, never as the only evidence for a surface (§17.3).

### 6.2 Storage

One SQLite file, `$AW_STATE_DIR/sindri/context/context.db`, through `Aw.Kernel.Data` (WAL, numbered migrations, each
tested on a copy of the previous schema; hand-written ADO.NET with `IRowMapper<T>`).

| Table | Holds |
|---|---|
| `map_version` | id, repo, commit sha, extractor version, created, status (`draft`, `current`) |
| `node`, `node_alias` | key, kind, label, attributes (JSON), provenance, confidence, map version |
| `edge`, `edge_evidence` | from, to, type, provenance, confidence, map version; evidence `SourceRef`s |
| `map_override` | the human layer: action, target key, payload, who, when |
| `decision` | key, wording, quote span, record ref and version, supersedes, provenance |
| `source_cursor` | source id, cursor, last sync, last error code |
| `snippet` | content hash, `SourceRef`, scrubbed text, expires (14 days, §8.4) |
| `doc_chunk` + FTS5 | the repo-docs lexical index (Phase 1) |
| `query_log` | query hash, shape, anchors, result refs and ranks, timings. Never query or result text |

**Fetched live, never stored:** record text (Onyx, Linear), code text (git at the sha), Onyx search results. The
snippet cache holds only scrubbed citation snippets, keyed by content hash, so text already sent to a model is never
sent again.

## 7. Scrubbing and trust

One boundary in `Aw.Context.Infrastructure`. Every record crosses it once, on the way in:

1. **`IngestSanitizer`** (new, C#): removes HTML comments, invisible and control characters, encoded blobs over 200
   characters, remote images and link targets (keeping link text), repeating until the text stops changing (at most 5
   passes). The TS `sanitizeIngest` is the reference for what it removes.
2. **`IRedactor.Scrub`** (`Aw.Redaction`), with the profile's extra patterns (D14, Phase 2). A `Blocked` outcome drops
   the record's text, keeps its ref, and reports `scrub-blocked` in `degraded`.
3. **Trust tag:** intent records and code text are `untrusted`; graph facts and human overrides are `trusted`.

**Egress.** CLI, HTTP and MCP output all reach a model, because sessions read them. Every response is scrubbed again
before it leaves, and untrusted text is wrapped in `<untrusted kind=… ref=…>` fences whose body is escaped so it can't
close its own fence. The labeling pass scrubs code and doc text before sending it.

**Stored text is already clean:** snippets, decision wording and model-written labels are scrubbed before storage; the
query log holds hashes only.

## 8. Query planner and surfaces

### 8.1 Shapes

Every shape resolves to **anchor nodes**, then runs the same pipeline.

| Shape | Input | Anchors from | Phase |
|---|---|---|---|
| `text` | free text | name and alias match over the graph; source searches vote; returns `suggestedAnchors` | 1 (sources only), 3 (graph) |
| `node` | a node key, name or alias | direct | 3 |
| `item` | a Linear key or Sindri item | the record's `decides` and `mentions` links, keys in its text | 6 |
| `symbol` | a symbol or `path[:line]` | `implements` from the code unit | 9 |
| `diff` | a ref range | changed symbols → nodes → cascade | after 9 (not in this spec's phases) |

### 8.2 Pipeline

1. **Anchors.**
2. **Expand:** cascade and neighbors to the depth cap, per edge type.
3. **Candidates:** per source, several sub-queries built from the anchors (names, aliases, parent feature, symbols,
   ticket keys), each with a large `Limit` (default 50), plus deterministic graph neighbors. Source scores are
   discarded; only ranks are kept.
4. **Fusion:** reciprocal rank fusion across sub-queries and layers (`k = 60`).
5. **Features** (Phase 8): cascade distance, edge type (`decides` above `mentions`), provenance (deterministic above
   model), supersession (a later decision demotes an earlier one), recency by `updatedAt`. Weights are profile values
   and registered artifacts (§7.4), tuned by evolve and judged by the backtest.
6. **Rerank** (Phase 8, arm only, off by default): a local cross-encoder or a budgeted model rerank of the top 30.
7. **Reformulate once** when the top results are thin (fewer than `minHits` above a fused-score floor), within the
   deadline.
8. **Pack** into the budget, then **cite**.

No model call happens on the query path unless rerank is on.

### 8.3 Budgets and packing

The request carries `budget` (tokens) and optional per-section caps (the §17.8 injection plan). Sections are filled
in order: **graph** facts (never cut mid-item), **decisions and conflicts**, **code** locations (refs and signatures,
not bodies), **records** (snippets). Within a section items are filled greedily by score. What doesn't fit is listed
as refs only ("+12 more"). The result reports `budget {asked, used}` and `truncated`. Tokens are estimated as
characters / 4 and labeled as an estimate.

### 8.4 Result and citations

```json
{ "query": {...}, "anchors": [...], "mapVersion": "v12@a41b07d", "asOf": "2026-10-10T18:02:11Z",
  "sections": [{ "name": "decisions", "items": [{ "kind": "decision", "node": "decision:…", "text": "…",
                 "score": 0.031, "why": ["decides→feature:layout.split-pane.orientation", "rrf:onyx#2,linear#1"],
                 "cites": ["C1", "C4"] }] }],
  "conflicts": [{ "node": "…", "intent": ["C1"], "behavior": ["C3"], "summary": "…" }],
  "suggestedAnchors": [...], "degraded": [{ "source": "onyx", "code": "AW-CONTEXT-010" }],
  "truncated": true, "budget": { "asked": 6000, "used": 5870 },
  "cites": { "C1": { "source": "onyx", "id": "linear/ENG-482", "version": "2026-09-30T14:02:00Z",
                     "permalink": "https://linear.app/…", "trust": "untrusted" } } }
```

- No claim without at least one citation, and every citation carries a **version**. A source that can't give one
  cites `fetchedAt` with `version: "unstable"`, shown, never hidden.
- A cited version that no longer matches the source at query time is flagged `stale` with both versions.
- `why[]` lists the features that placed each item. It explains results to the owner, labels results for evolve, and
  makes backtest misses debuggable.
- Text output renders the same content with `[Cn]` markers and a sources table, like today's scope maps.

### 8.5 Surfaces

All four call the same `Aw.Context.Features` services; they differ only in transport.

| Surface | Shape | Phase |
|---|---|---|
| **HTTP** | `POST /sindri/context/query`, `GET /sindri/context/node/{key}`, `GET /sindri/context/neighbors/{key}?depth=`, `GET /sindri/context/path?from=&to=`, `POST /sindri/context/map/build`, `GET /sindri/context/map`, `POST /sindri/context/map/review`, `GET /sindri/context/sources` | 1 (query, sources), 3–4 (the rest) |
| **CLI** | `sindri context query "<q>" [--node K \| --item ENG-1 \| --symbol S] [--budget N] [--json]`; `context show`, `neighbors --depth`, `path`, `map build \| status \| review`, `sources status` | as HTTP |
| **MCP** | Streamable HTTP at `127.0.0.1:<api port>/mcp`, served by the Sindri process with the MCP C# SDK. Read-only tools `context_query`, `context_node`, `context_neighbors`, `context_sources`. Tool results carry the same JSON plus a text rendering. Calls are requests, never decisions (§17.29.3) | 1 (`context_query`, `context_sources`), 3 (the rest) |
| **Workflow block** | `context.retrieve`: fills injection-plan sections for a recipe Step | 10 |

Registering MCP in Claude Code (manual, before the switch): `claude mcp add --transport http --scope user sindri
http://127.0.0.1:7410/mcp`. Loopback only, like the rest of the API.

## 9. Evaluation

### 9.1 Corpus

The owner's three related Linear projects on the work product: each holds its PRDs and TDDs (Linear Documents),
issues with decisions in comments, and the PRs that shipped them.

- **Unit: the issue.** Every issue with a merged PR is a sample. Brief = the issue and its project's PRD and TDD as of
  the issue's creation. Truth = files and symbols its PRs changed, mapped to nodes through `implements`. When an
  attachment is missing, PR titles and branch names carrying the issue key link it from git. The three project-level
  replays are also reported, labeled `small sample`.
- The model adjudicator (two runs in reversed order, agreeing answers only, as in TS today) labels surfaces that no PR
  touched.
- **As-of cut per sample:** code and map at the creation commit (no leak); repo docs at that commit's blobs (no leak);
  Onyx and Linear documents created after the cut are dropped, and ones edited after it are kept but labeled `leaky`.
  Whether Linear's API exposes document content history is verified in Phase 6; without it, PRD text is labeled leaky.
- The corpus and every report stay under `$AW_STATE_DIR/sindri/context/eval/`, never in a repo.

### 9.2 Evals

1. **Scope recall:** from the brief alone, does a query surface the nodes and files later touched? Node recall and
   precision; file-level recall@20.
2. **Map quality:** node and link precision on the owner's accept/reject decisions in the override layer;
   **unmapped rate** (files changed in a quarter that map to no node).
3. **Probe set:** about 30 questions the owner confirms once ("what was decided about Orientation?", "what implements
   tab drag?"); precision@5 and citation correctness. Runs in about a minute, as the regression check.

### 9.3 Arms

- Dimension: brief-only baseline · `docs` · `onyx` · `linear` · `code` · `graph` · `all`.
- Ranker: RRF only · RRF + features · + rerank · Onyx `Limit` 20 vs 50.
- A layer or ranker stage is enabled for a repo only when its arm improves recall without dropping precision below the
  bar (§17.3 Proof). Swapping an adapter is an evolve proposal judged on the same arms.

### 9.4 Pass bars (starting values; re-set after the first real runs, recorded in the ledger)

| Metric | Bar |
|---|---|
| Node recall / precision | ≥ 0.60 / ≥ 0.60, recall above brief-only |
| File recall@20 | ≥ 0.50 |
| Probe precision@5 | ≥ 0.80 |
| Citation correctness | 1.0 (a wrong citation fails the probe) |
| Map node precision | ≥ 0.80 |
| Unmapped rate | ≤ 0.20 |
| Query latency p95 (local, warm) | ≤ 2 s without rerank, ≤ 5 s with |
| Backtest token budget | ≤ 1.5M tokens per run (as TS) |
| Map labeling | under `context.labeling.maxTokens` per build |

A run with fewer than 30 issue samples is reported as `small sample` and can't pass.

## 10. Test strategy by lane

| Lane | What | Notes |
|---|---|---|
| `unit` | Domain types and keys (`EntryKey` golden snapshot, alias resolution); cascade traversal; RRF; feature scoring; packing (section order, refs-only overflow, never cutting a graph fact); citation building and `stale`/`unstable` rules; `IngestSanitizer` cases (the TS fixtures as behavior examples, rewritten); fence escaping; result JSON round-trips through `AwJson`; typed-id rules | In-memory fakes; < 10 s |
| `integration` | `context.db` migrations on copies of the previous schema; override layer surviving a rebuild; adapter contract base run against every adapter and its fake (`repo-docs` against a temp git repo, `onyx` against `FakeOnyxServer` replaying recorded responses, `linear` against a recorded GraphQL fake); HTTP routes through `WebApplicationFactory`; the MCP endpoint through the MCP SDK client (tool list, a `context_query` round trip, egress scrubbing of a planted secret); CLI verbs against a running test host; the eval harness end to end on the **fixture app** with a fake model | < 60 s per package; no network |
| `heavy` | Map extraction on the fixture repo with graphify; the full backtest on the fixture corpus; AOT publish of the Sindri host with the MCP endpoint | Nightly |
| manual | Phase signals in the running app (§12) on the owner's machine; the real backtest and probes over work data | Evidence written outside repos |

Every adapter and fake passes one reusable contract base (`ContextSourceContract<TSource>` in `Aw.Context.Testing`).
Tests never touch work data or the network.

## 11. Dependencies on S1 and others

| Needed | By phase | State |
|---|---|---|
| S1 process, `ControlPlaneClient`, `CommandTable` and `EndpointTable` registration | 1 | In the S1 worktree (`feat/sindri-s1-app-shell`), not merged. **Phase 1 needs S1 merged** |
| Profile v1 accepting an optional `context:` section | 1 | S1 rule: optional keys join v1. Phase 1 adds the key; `Aw.Context` owns its settings record |
| `DoctorCheck` registration | 1 | In S1 |
| `IScheduledService` for syncs and map rebuilds | 3, 5 | In S1 |
| The `compose` edge kind (§4) | 1 | Built in Phase 1 unless the workflow-engine spec lands it first |
| MCP host (MCP C# SDK, AOT approved in the readiness spike) | 1 | Built by Phase 1; reused later for item state and requests (§17.29.3) |
| Secret pointers (`keychain:`, `env:`, `op:`, `file:` 0600) | 5 | Not in S1; built by Phase 5 in Sindri Infrastructure |
| C# heavy-job lock (two slots, as the bash lock) | 4 | Not in S1; built by Phase 4 |
| Model runner (Claude CLI) for labeling and the adjudicator | 4, 7 | Judge (#156) once it lands; otherwise `ILabeler` with a Claude CLI adapter in Phase 4 |
| Code index block (§17.29.4 step 4) | 9 | Its own spec |
| Workflow engine (§17.29.4 step 2) | 10 | Its own spec |
| Onyx running, scoped, with company OAuth signed off by the owner | 5 | Owner, at setup |

## 12. Open items (each owned by the named phase)

- Does Onyx's MCP `search` tool include the federated Slack lane, and does it expose `updatedAt`? If not, the adapter
  uses Onyx's HTTP search API (Phase 5).
- An Onyx change feed: a document listing by updated time through its API (Phase 5); without one, links are re-derived
  from search hits only and the gap is reported by `context sources status`.
- Linear document content history for as-of cuts (Phase 6).
- The weights in §8.2 step 5 start as equal and are set by the first backtest (Phase 8).

## 13. Prior art

| Who | What we take |
|---|---|
| Atlassian Teamwork Graph + Code Context (CLI for coding agents) | A CLI and MCP as the agent surface; permissions at query time |
| Unblocked (architecture write-up) | Parallel semantic, lexical, structural and temporal retrievers; authority/recency/relevance features; code = behavior truth, specs = intent truth; one reformulate step |
| context-graph (MIT, local) | Rules-first linking: issue keys, URLs, quoted titles, paths and function names in text; the tools' own links; one person across tools; model-added decisions backed by quotes; never re-send text already read |
| Pendo | Surface anchors from UI elements, and suggested features a human confirms |
| linecheck MVP (owner's, ADR-4/6/8) | Places, moves, actions, things, flows with `codeRefs` and confidence; static extraction plus one labeling pass; depth-2 cascade. Generalized here beyond web routes |
| Onyx, Potpie, Cognee, Graphiti | Considered as cores and rejected: none models product surfaces; Onyx stays as the intent engine |

None of them has a product layer. None publishes retrieval evals, so §9 decides.

## 14. Review findings and answers

*Filled by the `/autoplan` pass below.*

## Phases

Each phase is one PR. Sizes are XS ≤ 80, S ≤ 250, M ≤ 600 changed lines of production code (tests excluded),
L above that (split before starting). Every phase ends with its signal shown in the running app, with evidence.

### Phase 1: `Aw.Context` skeleton, repo-docs adapter, planner skeleton, HTTP + CLI + MCP

- **Goal:** a usable first slice. A Claude Code session asks Sindri a question and gets cited, versioned, scrubbed
  results from the repos' PRDs and plans.
- **Files:** `src/Context/Aw.Context.{Core,Features,Infrastructure}` (contract §5.1, `SourceRef`, result types,
  `IngestSanitizer`, RRF, packing, citation builder, `QueryContext` and `SourcesStatus` slices, `repo-docs` adapter with
  FTS5, `context.db` migration 1); `tests/Aw.Context.{Tests,Testing}` (contract base, fakes); Sindri Host: routes,
  `context query` and `context sources status` verbs, the MCP endpoint with `context_query` and `context_sources`,
  profile `context:` key, doctor check `context:sources`; `deps.yaml` (package, edges, `compose` edge kind),
  `scripts/check-deps.mjs`, rule record; `docs/sindri/context.md`.
- **Size:** L as listed. Split into 1a (package, contract, sanitizer, repo-docs adapter, planner skeleton, CLI and HTTP:
  M) and 1b (MCP host and tools: S) **if** the plan step measures 1a above M. The phase isn't done until 1b ships.
- **S1:** needs S1 merged (process, client, endpoint and command tables, profile, doctor).
- **Signal:** `sindri context query "split pane orientation" --json` returns cited `repo:` results with blob shas, and
  in a Claude Code session with Sindri's MCP added, `context_query` returns the same results; a planted secret in a doc
  comes back as `[REDACTED:…]`.

### Phase 2: profile extra scrub patterns in `Aw.Redaction`

- **Goal:** ring-1 identifier shapes are scrubbed everywhere the build scrubs.
- **Files:** `src/Redaction/Aw.Redaction` (a constructor or factory taking extra `RedactionRule`s, validated, with match
  timeouts), PublicAPI files, `docs/csharp/redaction.md`; `Aw.Context` wiring from `context.scrub.extraPatterns`.
- **Size:** S.
- **S1:** profile key (S1 rule); otherwise none.
- **Signal:** with an extra pattern `ACME-\d{6}` approved in the profile, `sindri context query` shows
  `[REDACTED:profile/acme-id]` where a doc held `ACME-123456`.

### Phase 3: product graph store, override layer, `node` / `neighbors` / `path`, cascade

- **Goal:** the product graph exists and answers cascade questions, from a hand-authored map first.
- **Files:** `context.db` migration 2 (graph tables, overrides); `EntryKey` and aliases; `context map import <file>`
  (a YAML map the owner writes, kept outside repos); `ShowNode`, `Neighbors`, `Path` slices; `node` shape and graph
  section in packing; routes, verbs and MCP tools `context_node`, `context_neighbors`; doctor `context:map`.
- **Size:** M.
- **S1:** `IScheduledService` (not yet used); none new.
- **Signal:** after `sindri context map import ~/…/product-map.yaml`,
  `sindri context query --node feature:layout.split-pane.orientation --json` returns its subtree and an `affects` list
  with paths, and `context_node` returns the same from a session.

### Phase 4: the extractor and map review

- **Goal:** Sindri proposes the map from the repo; the owner accepts or rejects; overrides survive rebuilds.
- **Files:** extractor (deterministic anchors: command and keybinding registries, settings keys, routes, UI component
  names and test ids; graphify clusters as candidate features; a labeling pass behind `context.labeling.enabled`),
  `BuildMap` and `ReviewMap` slices, the C# heavy-job lock, `ILabeler` (Claude CLI adapter unless Judge's runner has
  landed), a scheduled rebuild service.
- **Size:** M (split extraction and labeling if over).
- **S1:** `IScheduledService`.
- **Signal:** `sindri context map build` prints proposed nodes with provenance and confidence;
  `sindri context map review` accepts some and rejects others; a second build keeps every decision and proposes no
  rejected node again.

### Phase 5: Onyx adapter, secret pointers, sync, links, decisions and conflicts

- **Goal:** intent joins the graph: tickets and threads link to nodes, decisions appear with quotes, and conflicts
  between intent and code show.
- **Files:** `onyx` adapter (MCP or HTTP search; §12 open items resolved here), secret pointer resolver in Sindri
  Infrastructure, `SyncSource` slice and scheduled service, rules-first linker (§13), `Decision` extraction (labeling
  pass, quote-backed), `supersedes`, conflict detection, `FakeOnyxServer`.
- **Size:** M (split linking and decisions if over).
- **S1:** `IScheduledService`.
- **Signal:** with Onyx scoped to the product's sources, `sindri context query --node <feature> --json` lists linked
  Linear issues and Slack threads with `onyx:` citations carrying `updatedAt`, at least one `decision` item quoting its
  source, and `degraded: onyx` when Onyx is stopped while the graph results still return.

### Phase 6: native Linear adapter and the `item` shape

- **Goal:** project structure (projects, Linear Documents, updates, issue↔PR attachments) enters the graph.
- **Files:** `linear` adapter (read-only GraphQL, team filter), `item` shape, document version handling, as-of check
  of document history (§12).
- **Size:** S–M.
- **S1:** secret pointers (Phase 5).
- **Signal:** `sindri context query --item ENG-482 --json` returns the issue's project PRD and TDD (`linear:doc/…`
  citations), the nodes it touches and the PRs that shipped it.

### Phase 7: evaluation harness

- **Goal:** the backtest, map-quality and probe evals of §9, run on the fixture corpus in CI and on the owner's corpus
  by hand.
- **Files:** corpus builder (issue samples, PR ground truth, as-of cuts), arms runner, adjudicator through the model
  runner, probe set file (outside repos), report writer, `sindri context eval backtest | probes | map`.
- **Size:** M.
- **S1:** heavy-job lock (Phase 4).
- **Signal:** `sindri context eval backtest --arms all` prints per-arm node recall and precision, file recall@20 and
  the pass line for the owner's three projects, with the sample count and leaky share.

### Phase 8: ranker features and the rerank arm

- **Goal:** stage 5 features and the optional rerank stage, each enabled only by a passing arm.
- **Files:** feature scoring, weights as profile values and registered artifacts, rerank port with a local adapter,
  reformulate step.
- **Size:** S–M.
- **S1:** none new.
- **Signal:** `sindri context eval backtest --arms ranker` shows RRF-only vs + features vs + rerank, and the profile
  enables only the arms that passed.

### Phase 9: code index adapter and the `symbol` shape

- **Goal:** implementation joins the graph from the C# code index block.
- **Files:** `code` adapter over the block's read-only db, `implements` links from symbols, `symbol` shape, the
  conflict check against code behavior.
- **Size:** S–M.
- **S1:** none; **depends on the code index block** (§17.29.4 step 4).
- **Signal:** `sindri context query --symbol src/layout/SplitPane.ts:setOrientation --json` returns the features it
  implements, their cascade and their decisions.

### Phase 10: the `context.retrieve` workflow block

- **Goal:** recipes get retrieved context in their injection-plan sections.
- **Files:** the block, its contract tests, its registration with the engine.
- **Size:** S.
- **S1:** none; **depends on the workflow engine** (§17.29.4 step 2).
- **Signal:** a design-studio recipe run shows a Step whose injection plan holds a cited `context` section, and the run
  receipt lists the citations.
