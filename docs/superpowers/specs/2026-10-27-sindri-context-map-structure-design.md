# Sindri context: one home feature per file, a roll-up to features, and a map that checks itself

**Part of** #1365 (steps 3, 4 and 5; steps 1 and 2, the import edges and the data-store edges, are separate PRs in flight).

**Status:** design, revision 1, for the owner's review. No code until the owner has read it · **Date:** 2026-10-27 · **Asked by:** #1365 · **Builds on:** the whole-map spec (`2026-10-23-sindri-context-whole-map-design.md`: areas, §2.2), the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`: §6 graph, lenses) · **Lane:** context · **Next:** the owner reads this; then /specToProvenPR takes §8's phases to PRs.

## 0. Why

On a real map (a TypeScript monorepo, ~17k files, map v26) the map is a multi-label file tagger, not a structure. `BuildMapService` asks graphify for clusters, the model names each one, and `ClusterFeatures.Propose` (`src/Context/Aw.Context.Core/Extract/ClusterFeatures.cs:20`) makes one feature per cluster and one `code implements feature` edge per file in it, all `Provenance.Model` (`ClusterFeatures.cs:53`). Counted from the export in the issue:

| | today |
|---|---|
| `code implements feature` edges | 111,044 (94% of all edges are model-made) |
| features | 2,818 (2,816 model-made) |
| most features one file implements | 19 (a repository module that writes one table implements 11, mostly about other domains) |
| files in the biggest feature | 1,704 (7 more hold over 1,100) |
| `depends-on` / `reads` / `writes` | 0 / 0 / 2 |

A product lens walks only structure edges (`LensProduct._structure`, `src/Context/Aw.Context.Core/Lenses/LensProduct.cs:9`: part-of, on, reads, writes, step-of, depends-on, moves-to) and lists only features, surfaces, actions, things and flows. `implements` is not walked, so a lens on a feature never reaches code, and there is nothing else to walk. Steps 1 and 2 add the file-level edges (`code depends-on code`, `code reads/writes thing`). They are not enough alone: a lens starts at a feature, and a feature has no edges of its own. Steps 3 and 4 give every file a stable place, and move the file edges up onto it. Step 5 stops a map that failed to do this from going out looking healthy.

**Success:** on the fixture of §6, a lens on one feature returns its tables and the other features that write them, with exact counts; on the real map `depends-on`, `reads` and `writes` are non-zero and all `deterministic`, no file has two home features, and no home feature is over the cap.

## 1. Goals and non-goals

Goals: one home feature per non-test source file, chosen by directory and never by the model (step 3); file edges rolled up to features (step 4); a build that measures itself and is refused or marked degraded before anyone sees it (step 5).

Non-goals: no language beyond TS/JS and C#; no Onyx change; no UI redesign (the small UI follow-ups §5 finds are listed, not designed); no change to graphify (its clusters still feed the tags and the labeler); no tuning of the model's naming prompt beyond giving it a different input.

## 2. Step 3: one home feature per file

### 2.1 The rule

Every non-test source file in the extractor's file list gets exactly one `code implements feature` edge, `Provenance.Deterministic`, confidence 1.0, evidence the file itself (as `MapExtractor.Extract` writes for surfaces, `MapExtractor.cs:38`). Its home is found in this order:

1. **Module root:** the nearest ancestor directory holding a `package.json` (JS/TS) or a `.csproj` (C#). A file's own directory counts.
2. **Else the top directory of the repo** (a repo with no package file at all is one module).

Inside a module, a group of files is split **one directory level at a time** while it holds more than `HomeFeatures.SizeCap` files (a named constant, proposed **150**):

- Start with the module root. If it holds at most 150 files (counting everything below it), it is one home feature.
- Otherwise look at its children in this order: `src/` if the module has one, else the module root itself. Each immediate subdirectory becomes its own group; files that sit directly in the directory being split form one group of "root files" homed on the directory's own feature.
- Repeat on every group that is still over 150.
- A nested package (a `package.json` below a module root) is its own module, so the first rule already cuts there.

A directory feature has a key from its path, the way `ClusterFeatures.Unique` builds one today (`/` becomes `.`): `feature:packages.api.src.services.billing`. The key comes from the path and nothing else, so it survives a rebuild, a relabel and a model change, and decisions people made on it (renames, merges) keep applying. Each split group is also `part-of` the feature of the directory it was split from (Deterministic). That gives `AreaAssigner` its roots (`src/Context/Aw.Context.Core/WholeMap/AreaAssigner.cs:7`): the module features, not 2,800 loose clusters.

**Worked example.** `packages/api` holds 1,704 files, over 150, so its `src/` is split:

| directory | files | over 150? | becomes |
|---|---|---|---|
| `src/` (files directly in it) | 25 | no | `api.src` |
| `src/config` | 18 | no | one feature |
| `src/utils` | 96 | no | one feature |
| `src/jobs` | 74 | no | one feature |
| `src/middleware` | 88 | no | one feature |
| `src/models` | 140 | no | one feature |
| `src/repositories` | 233 | yes | split: `read` 130, `write` 103 |
| `src/routes` | 420 | yes | split: `v2` 120, `v1` 300, and `v1` splits again: `admin` 90, `public` 150, `v1`'s own files 60 |
| `src/services` | 610 | yes | split: `billing` 260 (split again: `invoices` 140, `payments` 120), `auth` 180 (split again: `sessions` 110, `oauth` 70), `notify` 95, `search` 75 |

25 + 18 + 96 + 74 + 88 + 140 + 233 + 420 + 610 = 1,704. Result: 18 home features holding files, the biggest 150 (`routes.v1.public`), plus the inner features that only group them (`api`, `src`, `routes`, `routes.v1`, `services`, `services.billing`, `services.auth`, `repositories`), each `part-of` its parent. Today the same package is one 1,704-file feature plus whatever else graphify made of it.

A directory over the cap that has **no subdirectories** (400 flat files) cannot be split by directory. The design leaves it as one over-cap feature and step 5 counts it (Open question 3).

Test files (the extractor already tells them from source: `Recognizers.Source`) get no home feature. A test keeps its existing `tests` edge to what it covers, unchanged.

### 2.2 Where it runs, and the model's part

A new pure function in `Aw.Context.Core/Extract/`, `HomeFeatures.Assign(ContextRepoName, IReadOnlyList<RepoFile>) -> ExtractedMap`, run inside `Extract` in `BuildMapService` right after `MapExtractor.Extract` and before the graphify call (`BuildMapService.cs:132-133`). It needs only the file list (paths and the `package.json` / `.csproj` presence), so it is unit-testable with no repo. It is a sibling of the step-1 and step-2 extractors, and like them Deterministic.

The model **names** the home features and never picks members. `Labels` (`BuildMapService.cs:134`) already sends clusters to an `ILabeler`; it is pointed at the home groups instead: each group goes with its top files (by size, then path) and its most frequent terms, the same shape a cluster has now. A home feature whose label the model cannot give gets the path-derived name `ClusterFeatures.Humanize` builds today (`ClusterFeatures.cs:103`). Unavailable labeling is the existing degraded note (`LabelNote`, `BuildMapService.cs:148`), not an error.

### 2.3 What happens to the model's features

graphify clusters still run. `ClusterFeatures.Propose` keeps making a feature per cluster, but its file links stop being `implements`:

- The edge becomes a new `EdgeType.Tags` (`code tags feature`, `Provenance.Model`, `EdgeRules` From `Code`, To `Feature`). It is a new edge type, not a field, because every reader of `Implements` below filters by type, and a type filter is the one change the compiler and `EdgeRules` enforce.
- A **home feature** is a feature with at least one `implements` edge from a code file. A **tag feature** is a feature with only `tags` edges. This is derived from the edges, not stored.
- Surfaces: today a cluster claims a surface with `part-of` (Model, `ClusterFeatures.cs:56`) when it holds the surface's defining file. After this, the surface is `part-of` the home feature of its defining file (Deterministic). That is a better answer than "the first cluster in path order", and it is the one product-visible change in `part-of`.

Who reads `Implements` today, and what changes for each (all under `src/Context` unless noted; found with a grep for `EdgeType.Implements`):

| Reader | Does | Change |
|---|---|---|
| `Lenses/FileLinks.cs:62,79,86`, `Lenses/DiffAnchors.cs:54`, `GetFileMap/GetFileMapService.cs:40`, `GetLenses/GetLensesService.cs:68`, `Code/CodeFiles.cs:10`, `Lenses/LensCode.cs:31`, `Lenses/LensCoverageBuilder.cs:29`, `Lenses/MapCoverageBuilder.cs:14`, `Lenses/LensConflicts.cs:36`, `History/HistoryLens.cs:84` | a file's features, the code band, coverage | keep reading `Implements`: now exactly one home per file. `FileLinks.FeaturesOf` also returns the file's `Tags`, marked as tags, after the home (the file list and file map show both) |
| `Graph/ComponentHop.cs:14` | from a feature to the other features its files implement | stops producing hops for home edges (a file has one home, so `far` is empty). This was the route through the catch-all; the roll-up of step 4 replaces it |
| `Concepts/ConceptPlan.cs:33` (pin `Paths` globs), `WholeMap/ConceptIndex.cs:80` | which features a path glob or concept holds | keep reading `Implements`; a pin by path now lands on the home features of those paths |
| `GetCode/CodeLexicalSearch.cs:90`, `QueryContext/GraphAnswer.cs:107,145`, `QueryContext/QueryPipeline.Symbols.cs:27` | search and explain: "which features does this file belong to" | read `Implements` **and** `Tags` (tags exist so search finds a file by a cross-cutting name: auth, billing) |
| `Review/ReviewBacklog.cs:41,84`, `Linking/RulesLinker.cs:160`, `Lenses/FileLinks.cs:86` | model `implements` are suggestions a person accepts or rejects (`Confirmation.Of`, `Overrides/Confirmation.cs:12`: confirmed unless Model) | Deterministic home edges are confirmed from the start, so the review queue loses ~111k suggestions. `Tags` stay suggestions (`IsSuggestion` includes them). Open question 4 |
| `Map/DenyCut.cs:229`, `Graph/RepoScope.cs:19`, `RunBacktest/AsOfPruning.cs:13`, `Eval/Backtest/BacktestScoring.cs:19`, `EvalMap/EvalMapService.cs:106`, `BuildMap/BuildMapService.cs:257` | repo scoping, deny cuts, backtests, the namer's file list | keep `Implements`; `DenyCut` and `RepoScope` also treat `Tags` the same way (a denied path loses both) |
| `src/Sindri/Aw.Sindri.Features/Scoping/LensGather.cs:110`, the UI `Api/Context/CodeFile.cs`, `FileFeature.cs`, `ViewModels/Labels.ContextMap.cs:78` | the scoping station and the file list | read the same lens output; `FileFeature.Edge` already carries the edge type, so a tag shows as "tagged" next to "implemented in" |

Tag features must not become areas or concepts. `AreaAssigner` would give each tag feature (no `part-of`) its own area, and `ConceptPlan.Build` lists every feature (`ConceptPlan.cs:19`). Both are told to consider **home features and their ancestors only**; a tag feature keeps its `tags` edges and `belongs-to`-free life, found by search only.

Nothing walks `Tags`: not `LensProduct._structure`, not the roll-up, not the cascade.

### 2.4 Tests (Core, unit lane)

- No file has more than one `implements` edge to a feature (property test over a generated tree of 2,000 paths).
- No home feature is over `SizeCap`, unless it sits in a directory with no subdirectory (the flat case has its own test that it is reported).
- **Stable:** build twice, then move one file between two directories that are not module roots and build again; every other file keeps its home feature key and the moved file's home changes. Rename a directory: the files below it change keys, nothing else does.
- Module roots: a nested `package.json` is its own module; a `.csproj` beside `.cs` files is a module; a repo with neither is one module; a file in `src/` of the root module versus a file directly in the root.
- `Tags` never reached by `LensProduct`, `AreaAssigner` or `ConceptPlan` (a fixture with tag features asserts the area and concept counts do not change when tags are added).

## 3. Step 4: roll the file edges up to features

A pure function, `FeatureRollup.Apply(ExtractedMap) -> ExtractedMap`, in `Aw.Context.Core/Extract/`, run after steps 1 to 3 have all contributed their file-level edges (so after `HomeFeatures.Assign` and after the repo-wide `Plus`, in `ReadRepos`, `BuildMapService.cs:60`, not per repo: an import can cross repos in a multi-repo profile only if its target file is in the map, so it works on the joined map). For home features F and G:

- **F depends-on G** when some file homed in F `depends-on` a file homed in G, F != G.
- **F reads T / F writes T** when some file homed in F `reads` / `writes` the `thing` T.
- Each rolled-up edge is `Provenance.Deterministic`, confidence 1.0, carries a count of file pairs (or files, for data edges), and evidence: the first `FeatureRollup.EvidencePairs` pairs (a named constant, proposed **5**), ordered by path, as `EvidenceRef`s to the two files. The count and the evidence are what the inspector shows: "billing depends on orders: 3 imports, e.g. b1 -> o1".
- Group features roll up too: a feature's inner parents get no edges of their own (the lens already reaches children through `part-of`), so a lens on `services` still shows `services.billing`, and the edges sit on the leaf features that hold files. Open question 5.

**Rules change.** `EdgeRules` already allows `Feature -> Feature` for `DependsOn` (`Graph/EdgeRules.cs:19`). `Reads` and `Writes` take only Surface and Action as From (`EdgeRules.cs:12-13`, `_onSurface`), so step 4 adds `NodeKind.Feature`. Steps 1 and 2 add `Code` to the From of `DependsOn` (today only Feature) and of `Reads`/`Writes` for their own edges; step 4 must wait for both rules to be in `main`, or carry them (§8).

**Worked example.** Three modules and two tables, as a fixture:

| file | module | imports | data |
|---|---|---|---|
| b1 | billing | o1 | writes `invoices` |
| b2 | billing | o1, o2 | reads `invoices` |
| b3 | billing | b1 | writes `orders_t` |
| o1 | orders | u1 | reads `invoices` |
| o2 | orders | none | writes `orders_t` |
| u1 | users | none | reads `orders_t` |
| u2 | users | none | none |

(b3 -> b1 stays inside billing and rolls up to nothing; b2 reading `invoices` adds no new edge since billing already writes it, but it makes billing read it too.) Rolled-up edges:

| edge | count | evidence (first pairs) |
|---|---|---|
| billing depends-on orders | 3 | b1->o1, b2->o1, b2->o2 |
| orders depends-on users | 1 | o1->u1 |
| billing writes invoices | 1 file | b1 |
| billing reads invoices | 1 | b2 |
| billing writes orders_t | 1 | b3 |
| orders reads invoices | 1 | o1 |
| orders writes orders_t | 1 | o2 |
| users reads orders_t | 1 | u1 |

billing -> users has no edge (no file pair). A lens on `orders` at depth 2 then returns 2 features (billing, users), 2 things (`invoices`, `orders_t`), and the edges among those five nodes: 2 depends-on and 6 reads/writes, 8 in all. Of the neighbors, billing is in as the other writer of `orders_t` as well as an importer, and users as its reader. This assumes the cascade follows an edge in both directions, which `LensProduct.Lists` does for `part-of` today and the test pins for `reads`/`writes`.

(Count check against the table: the lens's five nodes are orders, billing, users, invoices, orders_t; the edges among them are the 8 rows above.)

Tests: this fixture with the exact counts above; an edge whose both files are in one feature rolls up to nothing; a rebuild gives the same edges and the same evidence order; 6 pairs give a count of 6 with 5 evidence entries.

## 4. Step 5: the map checks itself before it publishes

### 4.1 Where it runs

`BuildMapService.Save` (`BuildMapService.cs:77`) joins the extraction with the kept map and concepts, then calls `store.SaveDraft` (`:103`). The check runs on the finished `graph` just before that call: it needs the whole joined graph (home features, roll-up, concepts), and it is before any write, so a refused build leaves the last draft and the current map as they were. Promoting a draft to the current map is a separate step (`PromoteMapService`, with the hold and review queue); the check is on the build, because a bad map should not even reach the review queue. The result is `Result.Error` (refuse) or a note added to `MapBuildReport.Degraded` (degraded).

### 4.2 The measures

The issue proposes "two files with an import edge but no shared or adjacent feature". After step 4 that count is **zero by construction**: the roll-up creates a `depends-on` between the homes of every importing pair. It can only fail if the roll-up is broken, which a unit test catches, and it cannot see what actually goes wrong in practice, which is an import the resolver missed (no pair is made, so nothing is counted against it). So it is dropped as a publish gate. The model-vs-structure disagreement is also not a publish problem: a tag feature that spans many homes (auth across ten modules) is normal, and tags are never walked. It is shown as information in the status (below), not gated.

Two measures, both from deterministic edges:

- **(a) Isolated features.** The share of home features with at least 5 files that have no rolled-up `depends-on`, `reads` or `writes` edge in either direction, among all such features. Healthy code is connected; if import resolution or data-store detection failed (an alias the resolver can't follow, an ORM the adapter doesn't know) most features end up isolated, and so does a broken roll-up. Only measured when the map has at least 20 such features, so a small repo cannot trip it.
- **(b) Features over the cap.** The share of home features with more than `SizeCap` files (they exist only in flat directories, §2.1).

Each has two thresholds, named constants in `MapChecks`, proposed and **to be calibrated** by measuring the real map after step 4 merges (§7), because the real distribution is not known yet:

| measure | publish as degraded above | refuse above |
|---|---|---|
| (a) isolated features | 30% | 60% |
| (b) over the cap | 2% | 10% |

### 4.3 What each outcome does

- **Degraded.** The draft is saved. The build report's `Degraded` list (`MapBuildReport.cs`, shown by `MapBuildJobText.cs:14`) gets a note: `structure: 41% of home features have no edge to another feature or a table (degraded above 30%): check imports resolve (SND-CONTEXT-060)`. `ContextMapBuilt` carries the same list today (`Aw.Sindri.Core/Events/ContextMapBuilt.cs`). Today the list is only in the event and the job text, not stored with the map, so `sindri context status` and the app can't show it later. The design stores it (Open question 6): a reason string with the draft (a new nullable column or row beside `map_version_head`), read by the publish status (`ShowPublishStatus`, `MapPublishView`) and shown as "Degraded: <reason>" in `sindri context status` and in the app's map header and the "Build the map" card.
- **Refuse.** `Save` returns the error before `SaveDraft`; the last good map stays; the build job fails with the code below, and the message carries the measure, the number, both thresholds and the fix. The error code style is `SND-CONTEXT-NNN` (`ContextErrors.cs`); `060` to `069` are free (verified: used codes jump from 059 to 070):

| code | name | kind | fix text |
|---|---|---|---|
| `SND-CONTEXT-060` | Map structure is mostly unconnected | Invalid | "N% of home features have no edge (limit 60%): imports or data stores were not resolved; run `sindri context map build` after checking the repo's tsconfig/csproj paths" |
| `SND-CONTEXT-061` | Features are over the size cap | Invalid | "N% of home features hold more than 150 files (limit 10%): a flat directory can't be split; see `sindri context map checks`" |

A read-only way to see the numbers without building is part of the work: `sindri context map checks [--json]` prints the two measures and the tag spread (information only: the share of tag features whose files span more than 8 home features) for the current and the draft map.

### 4.4 Tests

Unit (Core): each measure at its exact boundary on a built graph (30% and 30.1%, 60% and 60.1%), fewer than 20 qualifying features skip (a), a map with no home features passes. Integration: a build over a fixture that trips each threshold gives the degraded note or the code, and in the refuse case no new draft exists (`SaveDraft` not called, the draft number unchanged).

## 5. Migration and what it touches

A map built after this has different features. Old maps stay as built; the next build replaces them (a draft, then the usual promote). Anything keyed by a model feature key:

- **Overrides** (`Overrides/MapOverride.cs`) name their target by key, so a rename, merge, move or reject of an old model feature (`feature:payments.api`) has no target after the rebuild. `MapAccess.WithoutDenied` (`BuildMapService.cs:92`) already undoes decisions whose target the cut removes; that is the path. A decision on a feature that still exists (a home feature whose directory key equals an old cluster key, which `ClusterFeatures.Unique` also builds from the common directory) keeps applying. The PR body counts the overrides lost on the real map.
- **Pinned concepts** (`ConceptPin`, `Engine/ConceptPin.cs:9`): pins by `Paths` glob keep working (§2.3, via `Implements`). Pins by `Features` key glob lose their match where the key was a cluster key; `sindri doctor`'s concept check should warn on a pin that matches nothing (checked in the PR, not designed here).
- **Records linked to features** (`mentions`, `decides` edges onto a feature) have the same fate as overrides. The count of dangling records is measured and reported.
- **Accepted or rejected `implements` suggestions** (override rows with a `Link`) refer to a model `implements` edge that becomes a `tags` edge (new link id, because the id is hashed from the edge, `LinkIds.For`). They need a one-time migration of `Link` ids from `implements` to `tags` for edges that still exist, else decisions people made are silently forgotten. Open question 4.
- UI lists that assume a file has several features: `Labels.ContextMap.cs:78` ("implemented in" wording), `CodeFile.Implements` / `FileFeature` in `src/Sindri/Aw.Sindri.Ui/Api/Context/`, and `ContextCodeText.cs:44` (`implements a, b, c` as a list). They keep working with one entry; showing `tags` beside it is a small follow-up, not part of this work.

## 6. How to verify

Per step, exact counts on a fixture, then re-measure on the real map.

| Step | Fixture | Expected |
|---|---|---|
| 3 | the 1,704-file tree of §2.1, generated | 18 files-holding home features, max 150, 1,704 `implements` edges, 1,704 distinct `from` keys; module with a nested `package.json` splits at it |
| 4 | the 7-file fixture of §3 | the 8 rolled-up edges with the counts and evidence shown; a lens on `orders` returns 2 features, 2 things, 8 edges |
| 5 | maps built to sit exactly on each threshold | the note at 30.1%, the error at 60.1%, none at exactly the limit |

After step 4 merges, re-measure on the map the issue counts and put the table in the PR body: `implements` edges per file (should be exactly 1 for 11,047+ files), features, largest feature, `depends-on` / `reads` / `writes` / `thing` counts, share of `deterministic` edges, isolated-feature rate and over-cap rate. The thresholds of §4.2 are re-set from that run before step 5 merges.

## 7. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Needs |
|---|---|---|
| **S3 — home features** | `HomeFeatures.Assign`, `EdgeType.Tags` and its rule, `ClusterFeatures` writing `tags`, the labeler fed home groups, the reader changes of §2.3, area and concept filter, migration of accepted-link ids; unit lane, plus `Aw.Context` integration and `Aw.Sindri.Ui.Tests` (API goldens and the file-map DTOs change) | nothing to start |
| **S5 — the checks** | `MapChecks` (both measures, thresholds, constants), the two error codes, the degraded note, storing and showing the reason, `sindri context map checks`; unit and integration lanes | nothing to start (written against a built `GraphSnapshot`; it doesn't need S3's output, only its definition of a home feature, which §2.3 fixes) |
| **S4 — roll-up** | `FeatureRollup`, `Feature` added to the From of `Reads`/`Writes`, evidence and counts | S3 (homes) and the `EdgeRules` From additions of steps 1 and 2 on `main` |

S3 and S5 start together as two builders; S4 starts when S3 merges (it can begin on a stub home assignment, but its fixture needs real homes). Each phase is one PR. The recipe version rule (`ci-recipe-versions`) does not apply (no recipe changes). Gate per PR: `heavy --light dotnet build` of the touched projects with `-warnaserror`, the touched projects' unit and integration lanes, `Aw.Architecture.Tests` for S3 (it adds a `Lenses` to `Extract` reference only if the reader changes cross namespaces), `Aw.Sindri.Ui.Tests` for S3, and `bash src/Sindri/aot-smoke/run.sh` for S5 if the `doctor` text or `context status` text changes (`expected.txt`).

## 8. Open questions for the owner

1. **Is 150 files the right cap?** It makes the 1,704-file package 18 features, the biggest 150. 100 would give more, smaller ones; 300 fewer. *Recommend 150, then re-measure on the real map and move it if the median feature is under 10 files.*
2. **Does a home feature name come from the model or from the path?** The design has the model name them, with the path name as the fallback. *Recommend that: the key is the path, so renaming never moves a feature.*
3. **A flat directory with 400 files.** It can't be split by directory. *Recommend leaving it as one over-cap feature and letting check (b) count it, rather than splitting by file-name letter, which is arbitrary and unstable.*
4. **Tags and the accept/reject review.** Today every model `implements` is a suggestion a person can accept or reject. *Recommend: tags stay suggestions, nothing blocks on them, and old accept/reject decisions on edges that still exist are carried over to the new `tags` link ids by a one-time migration in the S3 PR.*
5. **Do inner group features (`services`, `routes`) get roll-up edges of their own?** *Recommend no: only features that hold files get edges; a lens reaches the group through `part-of`. Rolling up inner features duplicates every count.*
6. **Where the degraded reason is kept.** Today `Degraded` is only in the build event and job text. To show it later in `context status` and the app it has to be stored with the draft. *Recommend a nullable text column on the draft's version row, copied on promote, so the app can say "Degraded: ..." for as long as that map is current.*
7. **Thresholds.** 30% / 60% isolated and 2% / 10% over-cap are guesses, not measurements. *Recommend merging S5 with them marked "calibrate" and re-setting them from the first real run (§6).*
8. **A map where step 1 or 2 hasn't landed.** With no file edges, every feature is isolated and (a) would refuse the build. *Recommend (a) is skipped when the map has no `depends-on`, `reads` or `writes` file edges at all, and the degraded note says "no file edges" instead; refusing is for a map that had edges and lost most of them.*

## 9. Where the code differs from the plan this was written to

- The issue and the plan call the check code style `CTX-MAP-0xx`; the repo's is `SND-CONTEXT-NNN` (`ContextErrors.cs`), so §4.3 uses `060` and `061`.
- `code implements feature` edges that are Deterministic do not exist today (only code-to-surface/action ones, `MapExtractor.cs:38`); the rule allows them (`_implemented` includes Feature).
- `Reads`/`Writes` From is Surface and Action only, and `DependsOn` From is Feature only, so steps 1 and 2 have to widen them to Code too; step 4 adds Feature to `Reads`/`Writes` as planned.
- "Degraded" is not stored anywhere today (event and job text only), so showing it in `sindri context status` needs the storage of Open question 6.
- There is no `ConceptPlan.MinFeatures` interaction with the cap: it is a minimum of 3 features per concept group (`ConceptPlan.cs:11`), unrelated to file counts.
- `AreaAssigner` has to ignore tag features or the area count explodes (§2.3); the plan did not mention it.
