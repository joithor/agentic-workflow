# `Aw.Context` — the context engine package (C#)

The context retrieval engine (spec `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`), a tier-1
package with no Sindri imports. Sindri reaches it only through the `IContextEngine` facade in `Aw.Context.Core` and
composes it through the `compose` edge. The user's guide is `docs/sindri/context.md`. Each stage adds its own section.

## S-1A: the package, repo-docs, the query pipeline

### Shape

| Project | Holds |
|---|---|
| `Aw.Context.Core` | `Facade.IContextEngine` (`CheckSettings`, `Query`, `Sources`, `Refresh`) and its records (`Engine.ContextScope`, `ContextQuery`, `ContextCaller`, `ContextSettings`); the source contract (`Sources.IContextSource`, `IChangeFeed`, `SourceRef` with `Canonical`, the typed ids `SourceId`, `ExternalId`, `SourceVersion`, `RecordKey`, `SourceCursor`); the result records (`Results.*`, spec §8.4); ports `Trust.IEgressScrubber`, `Trust.IIngestFilter`, `Trust.IScrubberFactory` (S-2W), `Docs.ISourceCatalog`, `Docs.IDocIndex`, `Models.IModelPort` (README X18: every model call; adapted in Sindri's Host over Judge's provider port by the stage that first calls a model); pure rules: `ScopeRules`, `SettingsReader`, `QueryRules`, `RankFusion`, `Packer`, `TokenEstimate`, `CitationStates`, `UntrustedFence`, `Permalinks` |
| `Aw.Context.Features` | `ContextEngine` (the facade: one line per method, dispatching to a slice) and the slices `QueryContext`, `SourcesStatus`, `RefreshDocs` |
| `Aw.Context.Infrastructure` | `Store.ContextDatabase` (context.db), `Docs.SqliteDocIndex` and `Docs.RepoDocsSource` (FTS5 over scrubbed chunks), `Git.GitCli`, `Scrubbing.IngestSanitizer`, `Scrubbing.RedactionScrubber` (both scrub ports over `Aw.Redaction`) and `Scrubbing.RedactionScrubberFactory` (S-2W) |

`context.repos` must name profile repos, and with a `context:` section every repo in scope needs a name the engine can
use (`ContextRepoName`): otherwise `SND-CONTEXT-001` at validate and approve (`IContextEngine.CheckSettings(section,
repos)`, through `SettingsReader.Check`); an unknown `context.repos` name is also 001 at query time. A profile without
`context:` is never refused for this, and `ScopeRules.Usable` leaves an unusable name out of the scope.

**Placement and limits (README X61).** `SourceDescriptor` carries two init properties so the source contract doesn't
assume a local process: `Placement` (`SourcePlacement.Local` or `Shared`, default `Local`) and `Limits`
(`SourceLimits`: requests per window and max concurrency, each optional; null means none). CTX D6's "no rate limiting"
is a property of local sources, which declare no limits; it is not a global rule. Nothing enforces limits yet. The
contract base checks that the placement is defined, that any declared limits are well formed (at least one, positive,
a request count with its window), and that a `Local` source declares none. repo-docs is `Local` with no limits.

`deps.yaml`: Context → Kernel, KernelData and Redaction (Infrastructure only) are `project` edges. Sindri → Context is a
`project` edge (Sindri's Features and Host use Core) plus a `compose` edge (Sindri's Host calls `AddContextFeatures` and
`AddContextInfrastructure` in `Composition`, nothing else: ARCH-COMPOSE-001).

**Sindri's side.** `SindriProfile.Context` is a `JsonElement?`, left out of the canonical JSON when absent. Validate and
approve call `IContextEngine.CheckSettings`. The slice folder `Aw.Sindri.Features.ContextCalls` holds:
- `ContextScopes`, the one mapping from the approved profile (the read-only port `IApprovedProfile`, so the query
  handlers hold no write port: ARCH-CQRS-002) and `ICallerContextAccessor.Current` to `ContextScope`. The engine's
  client comes from the actor's label: `cli` and `mcp` are themselves, `ui`, `prover` and plain HTTP are http, services
  are service.
- The queries `QueryContextRequest` → `ContextResult` and `GetContextSourcesRequest` → `SourcesView`.
- The scheduled service `ContextDocsRefreshService` (`context-docs-refresh`, event `context.sources.synced`).

Every surface resolves those services, MCP included (S-1M). `ContextCalls` is deliberately one slice folder for every
context use case: `ContextScopes` is shared, it can't move to Sindri Core (only Features and Host may reference another
package's Core, DEPS-PROJ-008), and ARCH-SLICE-001 forbids sharing it across slice folders. Later stages add their
context services here. The routes are `GET /sindri/context/query` and
`GET /sindri/context/sources`. The CLI verbs are `Host/Commands/ContextVerbs`, with text output in `ContextText`.
`Host/Catalog/ContextRows` holds `SyncSources` (`job`, `pending:S-5B`, no route mapped until S-5B, after API P6).

### Query pipeline (Phase 1, text shape)

1. `SettingsReader.Read`, then `ScopeRules.Resolve`: sessions are gated (020), and a session is scoped to its repo (003).
2. `QueryRules.Check`: one shape. Text becomes at most 16 lower-case terms. The budget is capped. Anchor shapes are 003
   until the graph exists.
3. Every source in `ISourceCatalog.For(settings, repos)` is searched in parallel under the deadline. An error or a
   missed deadline makes that source degraded (010); all of them failing is 011.
4. `RankFusion.Fuse`: hits sharing a canonical key merge, keeping each source's best rank. Each source is re-ranked
   densely, then RRF with k = 60 runs across sources.
5. The top 20 are fetched in turn until the deadline (what isn't fetched in time isn't shown), cleaned whole by
   `IIngestFilter` (sanitize, collapse titles, scrub; line-preserving), then cut to 64 KB. The sanitizer drops inline and
   HTML images and reduces reference-style ones to their text, so no image survives ingest wherever its definition sits, and removes
   remote reference definitions also inside block quotes and list items (one-token destination, optional title).
6. One batched `Versions` call per source, each with only its own references, gives each citation its state.
7. `ResultAssembly`: one `record` item and one citation each. Every field, the citation's key and ids included, is
   scrubbed again on egress (a blocked field is `[REDACTED:blocked]` alone). The title (at most 200 characters) and
   the text are fenced as untrusted, and the items are packed into the budget.

No model is called on the query path.

### Repo-docs

- **Index.** `SqliteDocIndex.Refresh` lists the tree at HEAD (`git ls-tree -r -z`) and matches the globs (`Glob`). It
  reads changed blobs (their size first: over 1 MiB keeps a row without chunks and counts as skipped), cleans each
  document whole and line-preserving (a redacted span keeps its newlines), then chunks the clean text by heading (at
  most 40 lines or 2,000 characters; `DocChunker`), so a secret or a hidden comment that crosses a chunk boundary
  can't slip through. Only then does one short transaction remove stale docs, write new ones and move the cursor
  (`source_cursor`). A document the scrubber blocks keeps its `doc` row with `blocked = 1` and has no chunks; one git
  can't read on this run is skipped and keeps its earlier rows. A path the ids can't carry (`RepoDocs.TryKey` and
  `TryChunkId` refuse it: a control or format character, or near 1 KB) is never indexed and counts as skipped on every
  refresh. A repo git can't read records `SND-CONTEXT-010` as its
  `last_error`, and the refresh reports it in its row.
- **Source.** `RepoDocsSource` searches with each term as a quoted FTS5 phrase (`DocSql.Phrase`), OR-ed and ranked by
  `bm25`, filtered to the repos in scope. A record is a chunk, `<repo>/<path>#L<a>-L<b>`, at the blob sha. The
  canonical key is the document, `repo:<repo>/<path>`. Fetch reads the blob from git at the cited sha, cleans it whole
  (as the index did) and cuts the line span; Versions makes one tree listing per repo.
- **Git.** `GitCli` passes arguments through `ArgumentList` with no shell, removes the caller's `GIT_*` variables,
  accepts shas only as `^[0-9a-f]{7,64}$` (never an option or a ref expression), and kills a process that misses 30 s
  or the caller's token. `cat-file -e` exiting 1 is a missing object (NotFound, `003`); any other failure is `010`.
- **Logs.** No log line carries question or record text. Kernel telemetry keeps ASP.NET's request lines
  (`Microsoft.AspNetCore.Hosting.Diagnostics`, which hold the `?q=` query string) at Warning and above, in every host.

### Store

`$AW_SINDRI_HOME/context/context.db`; Context reads `AW_SINDRI_HOME` (else `$AW_STATE_DIR/sindri-app`) itself, from the
environment Sindri's `Composition` passes. Migrations are embedded, named `NNNN_<stage>_<name>.sql` and numbered on the
branch (spec "Shared files"). This stage adds `0001_s1a_repo_docs.sql`, which creates `source_cursor`, `doc`, `doc_chunk`,
`doc_chunk_fts` (FTS5, external content, kept in step by triggers). The directory is 0700. The file and its `-wal` and
`-shm` are 0600, restored after every write. `Readable` never creates the file: a source with no store answers no hits,
and the status reads "never refreshed".

### Errors

| Code | Kind (HTTP) | CLI exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-001` | Invalid (400) | 2 | The profile's `context:` section is invalid; the message starts with the key path |
| `SND-CONTEXT-002` | Invalid (400) | 2 | A configured source isn't set up, or its secret pointer doesn't resolve (later stages) |
| `SND-CONTEXT-003` | NotFound (404) | 1 | Unknown anchor, or a record outside a session's repo scope (the same answer) |
| `SND-CONTEXT-004` | Unavailable (503) | 2 | The store can't be opened or migrated (the message keeps the AW-DATA code) |
| `SND-CONTEXT-005` | Invalid (400) | 1 | A map file or an output path inside a git worktree (links resolved) |
| `SND-CONTEXT-006` | Invalid (400) | 1 | The question is invalid: no words, two shapes, depth or budget out of range |
| `SND-CONTEXT-010` | Unavailable | — | One source is unavailable: a `degraded` entry, never the answer's error |
| `SND-CONTEXT-011` | Unavailable (503) | 2 | Every source is unavailable |
| `SND-CONTEXT-020` | Forbidden (403) | 1 | Session reads are off (`context.sessions.enabled: false`), on every transport. Defined by S-1A (spec §3); S-1M maps it to the MCP tool error |

### Tests

`tests/Aw.Context.Tests`:
- Every port's contract suite from `tests/Aw.Context.Testing/Engine`, bound to its fake and its real adapter:
  `ContextSourceContract<T>` (`FakeContextSource` unit; `RepoDocsSource` integration), `DocIndexContract<T>`
  (`FakeDocIndex` and `SqliteDocIndex`, both integration: the fake reads the fixture repo's files),
  `ScrubberContract` (`FakeScrubber` and `RedactionScrubber`, unit), `SourceCatalogContract<T>` (`FixedSourceCatalog`
  unit; `RepoDocsCatalog` integration), `ChangeFeedContract<T>` and `ModelPortContract<T>` (fakes; their adapters come
  with the stages that call them) and `ContextEngineContract<T>` (the real engine over the fakes, and
  `FakeContextEngine`, which Sindri's tests use).
- `ContextDatabaseTests`: FTS5 in the bundled `e_sqlite3`, the file modes, and migrations named for their stage.
- `RepoDocsTests`: a planted secret never reaches `doc_chunk`; a key or hidden comment across a chunk boundary reaches
  neither the index nor a fetch; a fetched span matches the blob's lines; oversized and unreadable docs; git's exit-code
  mapping; an edit reads as a new version.
- The pure rules (unit) and the facade over the fakes (`QueryContextTests`, `SourcesStatusTests`).

In `tests/Aw.Sindri.Tests/ContextCalls`:
- the routes through the real pipeline: human, session gate, scope, foreign Origin, codes, and no question or record
  text in any log line;
- the caller mapping, and validate and approve refusing a bad `context:` section or repo;
- the CLI's argument parsing and text output.

## S-2W: extra scrub patterns, per call

Spec Phase 2 and §7 (D14: one scrubber). The profile's `context.scrub.extraPatterns` reach every scrub the engine does.

- **Settings.** `SettingsReader` reads `scrub: { extraPatterns: [{ id, pattern }] }` into
  `ContextSettings.ExtraPatterns` (`Trust.ScrubPattern(Rule, Pattern)`, as written). Unknown keys are refused by the
  section records, a null entry with `context.scrub.extraPatterns[i]`. Core can't check a regex (ARCH-DIR-005 keeps
  `Aw.Redaction` out of Core and Features), so the entries are checked behind a port.
- **`Trust.IScrubberFactory.For(extraPatterns) -> Result<Scrubbers>`.** `Scrubbers(Ingest, Egress, Ruleset)` are one
  call's ports. `RedactionScrubberFactory` (Infrastructure, singleton) builds them with
  `Redactor.Create(extras, Redactor.DefaultMatchTimeout, TimeProvider)` (so the whole-scan deadline runs on the host's
  clock) and maps the first `ExtraPatternError` to `SND-CONTEXT-001` at
  `context.scrub.extraPatterns[<Index>].id|pattern`, with Redaction's message (which never quotes the pattern). It
  keeps the last build, keyed by ruleset, so calls under an unchanged profile don't compile again.
- **Per call.** There is no scrubber singleton any more. `QueryContextService`, `RefreshDocsService` and
  `SourcesStatusService` read the settings, then `For(settings.ExtraPatterns)`; `ISourceCatalog.For(settings, repos,
  scrubbers)` hands them to the sources (`RepoDocsSource` cleans fetched documents with them),
  `IDocIndex.Refresh(repo, globs, scrubbers, ct)` cleans and records with them, and `IDocIndex.Status(repo, ruleset,
  ct)` counts what is outdated. `ContextEngine.CheckSettings` reads the keys, then builds the scrubbers, then checks
  the repos, so `profile validate` and `approve` refuse a pattern Redaction refuses. The approved profile can change
  at run time (the scheduler re-reads approvals), and the next call picks it up.
- **`Trust.ScrubRuleset`** (Vogen, 16–64 lowercase hex): the first 32 hex characters of SHA-256 over
  `IngestSanitizer.Version`, the text of every sanitizer regex (`IngestSanitizer.Rules`), the built-in redaction rules'
  `Redactor.RulesDigest` (their ids, patterns and options, so a widened rule under the same id counts) and each extra
  (id, pattern), length-prefixed. Changing a regex therefore re-cleans by itself; raise `IngestSanitizer.Version` too
  with any change to what the sanitizer removes in code (S-2W made it 3).
- **A ruleset per stored doc.** Migration `0003_s2w_scrub_ruleset.sql` adds `doc.ruleset` (default `''`, so every
  row S-1A wrote is outdated). A refresh treats a document as current only when its blob **and** ruleset match; one
  stored under another ruleset is cleaned again (`RepoRefresh.Recleaned` counts those indexed with text, so they are
  among `Added`; one the new rules block or one over the size cap counts as blocked or skipped instead; Sindri carries
  it on `ContextRepoSynced`). Search filters `d.ruleset = @ruleset`, so a chunk cleaned with other rules is never a hit
  until it is cleaned again, and `DocIndexStatus.Outdated` turns the repo's status row `degraded` with the count.
- **Sanitizer (S-1A review run 5 deferrals).** `HtmlImage` removes the elements that fetch whatever their attributes
  (`img`, `image`, `meta`, `iframe`, `frame`, `object`, `embed`, `link`, `base`, `script`, SVG `set` and `animate…`);
  `HtmlTag` + `RemoteAttribute` remove any other start tag carrying `src`, `srcset`, `imagesrcset`, `srcdoc`, `lowsrc`,
  `dynsrc`, `poster`, `background`, `href`, `data`, `action`, `formaction`, `style` or `ping` anywhere in the tag. Both
  read quoted values (which may hold `>`) first, then the tag raw up to its first `>` (`RawFetchingTag`, `RawTag`), so
  a tag whose quotes don't balance or whose value is too long is removed, not kept. `StyleBlock` removes
  `<style>…</style>`. The remote
  definition's label takes backslash escapes and one line break (not a blank line), and labels are matched as
  CommonMark does (whitespace runs collapsed, case ignored), so `[x][p q]` meets `[p\nq]: https://…`.
- **Not here: the map's text.** S-3a's map export and status (and S-3b's `node`/`affects` answers) don't scrub yet;
  they must build `Scrubbers` per call through `IScrubberFactory` and pass every text field through `Egress`, as the
  repo-docs pipeline does (follow-up for S-3b).
- **Not here: telemetry.** Telemetry's redactor stays `Redactor.Default`: log lines and spans carry codes, counts and hashes,
  never record or query text (§7), and the telemetry redactor is fixed at start while the profile changes at run time.

### Tests (S-2W)

- `ScrubberFactoryContract<T>` (`Aw.Context.Testing`), bound to `FakeScrubberFactory` and `RedactionScrubberFactory`
  (unit): an extra pattern on both ports, the ruleset follows the list, refusals at their key paths.
- `DocIndexContract<T>` gains "a document stored under another ruleset is cleaned again" (fake and SQLite).
- `RedactionScrubberFactoryTests`: Redaction's refusals (Unicode class, empty-matching, built-in or reserved id, nulls)
  at their key paths without the pattern; the last build reused; the fingerprint is length-prefixed.
- `RepoDocsTests`: with a pattern added, nothing stored under the old rules is a hit, the refresh re-cleans both docs,
  `doc_chunk` loses the raw id and gains the placeholder, and a fetch answers the placeholder.
- `QueryContextTests`, `SourcesStatusTests`, `SettingsReaderTests`, `TextRulesTests` (the HTML carriers and labels),
  and `ContextEngineContract` (an extra pattern that doesn't parse is 001 at its key path).
## S-3a: the product graph store, map import / export, map status, doctor, purge

### Shape

| Project | Adds |
|---|---|
| `Aw.Context.Core` | `Graph`: `NodeKey` (typed id; the kind is the scheme), `NodeKind`, `NodeKinds` (surface kinds), `EdgeType`, `EdgeRules` (the §6.1 edge table), `EvidenceRef`, `GraphNode`, `GraphEdge`, `NodeAlias`, `DecisionRow` (`ToKey()`: `decision:` + SHA-256 of record and quote), `GraphSnapshot` (`Resolve`, `ScopedTo`), `RepoScope`, `TrustPolicy`, `EntryKey` (version 1, golden-tested), `IKeyMigration`, `KeyMigrations`. `Overrides`: `MapOverride` (`ToId()`: a content hash, so a re-import keeps one row), `OverrideId`, `OverrideAction`, `RejectionIdentity`, `OverrideApplier` (pure; orphaned overrides kept). `Map`: `MapDocument` (the file), `MapDocumentValidator` (all or nothing, 031/032 with key paths, key migration), `MapAccess` (the read gate and scope, the export document), `MapRetention`, the result records, and the ports `IGraphStore`, `IMapFileReader`, `IExportStore`. `Facade.IContextMap` (`PreviewMap`, `ImportMap`, `ExportMap`, `MapStatus`, `CheckMap`, `CheckRetention`, `PurgeExpired`); `IContextEngine : IContextMap` |
| `Aw.Context.Features` | `ContextMap` (one line per method) over the slices `PreviewMap`, `ImportMap`, `ExportMap`, `ShowMapStatus`, `CheckMap`, `CheckRetention`, `PurgeMap`; `ContextEngine` delegates the map methods to it |
| `Aw.Context.Infrastructure` | `Graph.SqliteGraphStore` (over `Store.ContextDatabase`), `Map.MapFileReader` (YamlDotNet's parser and representation model to JSON, then AwJson; no reflection serializer), `Map.FileExportStore`, `Map.Worktrees` |

**Sindri's side** (`Aw.Sindri.Features.ContextCalls`): the queries `GetContextMapRequest` → `MapStatusView`
(`GET /sindri/context/map`) and `ExportContextMapRequest` → `MapExported` (`GET /sindri/context/map/export?path=`);
the command `ImportContextMapRequest(Path, Hash?, Confirmed)` → `ImportContextMapResponse`
(`POST /sindri/context/map/import`; catalog row `ImportContextMap`: `protected`, terminal only,
`cli-only:typed-confirmation`), which appends `context.map.imported` (`ContextMapImported`: version, repo, hash,
counts); the scheduled service `ContextPurgeService` (`context-purge`); doctor rows `context:map` and
`context:retention` in `RunDoctorService`. CLI: `Host/Commands/ContextMapVerbs` and `ContextMapText`. The map JSON
types are in `SindriJsonContext`; the reader and the exports folder take its `JsonSerializerOptions`.

`GET /sindri/context/map/export` is the one query that writes: a file under `exports/` (owner-only, purged after 14
days), never the store or the ledger. It is a query because the API spec lists it as one, and it changes no state a
command or event describes; a later file-writing route should not take it as a precedent without the same reasons.
Import and preview refuse a session-kind caller in the engine too (SND-CONTEXT-020), whatever surface reaches them.

**Not on this base:** the API lane's shared challenge. The import is shaped like `profile approve` (human, terminal,
content hash, `confirmed`) and adopts the challenge when that lane ships it.

### Store

Migration `0002_s3a_graph.sql` (after S-1A's `0001`): `map_version` (status `building|draft|current|retained`, one
`current` by a partial unique index), `node`, `node_evidence`, `node_alias`, `edge`, `edge_evidence`, `decision`
(wording and quote only: the record it quotes is its `quotes` edge) and `map_override` (outlives versions; ids are
content hashes, `ON CONFLICT DO NOTHING`). Trust is stored as computed from provenance, never read from a file. A save
writes a `building` version in batches of 500 statements, then makes it `current` and the old one `retained` (with
the file's overrides) in one transaction; a failed batch deletes the building version.

### Errors

| Code | Kind (HTTP) | CLI exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-030` | NotFound (404) | 1 | No map yet |
| `SND-CONTEXT-031` | Invalid (400) | 1 | The map file is invalid (schema version, shape, unknown kind, disallowed edge, unreadable); key paths given; nothing written |
| `SND-CONTEXT-032` | Invalid (400) | 1 | The map file refers to a node it doesn't define (edge end, alias target); nothing written |
| `SND-CONTEXT-033` | Invalid (400) | 1 | Export path outside `$AW_SINDRI_HOME/context/exports/` |
| `SND-CONTEXT-034` | Conflict (409) | 1 | The map file changed since its hash was confirmed; nothing written |

### Tests

`tests/Aw.Context.Tests`: `Map/` (keys and the `EntryKey` golden snapshot, the validator, key migration with a
synthetic v2 and "no override orphaned", the override layer, repo scope, the port contracts bound to fakes and real
adapters, the store's file modes and the YAML → store round trip); `ImportMap/`, `ExportMap/` (with the golden export
`ExportMap/Golden/fixture-export.json`), `MapStatus/`, `MapHealth/`, `PurgeMap/` through the facade over the fakes.
`tests/Aw.Context.Testing/Map`: `FakeGraphStore`, `FakeExportStore`, `FakeMapFileReader` and their contract bases,
`SampleMap`, `FixtureMap` (the split-panes product map). `tests/Aw.Sindri.Tests/ContextCalls/ContextMapApiTests`: the
routes through the real pipeline (preview, protected import and its refusals, event, export, session scope, purge).
