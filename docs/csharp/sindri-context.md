# `Aw.Context` — the context engine package (C#)

The context retrieval engine (spec `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`), a tier-1
package with no Sindri imports. Sindri reaches it only through the `IContextEngine` facade in `Aw.Context.Core` and
composes it through the `compose` edge. The user's guide is `docs/sindri/context.md`. Each stage adds its own section.

## S-1A: the package, repo-docs, the query pipeline

### Shape

| Project | Holds |
|---|---|
| `Aw.Context.Core` | `Facade.IContextEngine` (`CheckSettings`, `Query`, `Sources`, `Refresh`) and its records (`Engine.ContextScope`, `ContextQuery`, `ContextCaller`, `ContextSettings`); the source contract (`Sources.IContextSource`, `IChangeFeed`, `SourceRef` with `Canonical`, the typed ids `SourceId`, `ExternalId`, `SourceVersion`, `RecordKey`, `SourceCursor`); the result records (`Results.*`, spec §8.4); ports `Trust.IEgressScrubber`, `Trust.IIngestFilter`, `Docs.ISourceCatalog`, `Docs.IDocIndex`, `Models.IModelPort` (README X18: every model call; adapted in Sindri's Host over Judge's provider port by the stage that first calls a model); pure rules: `ScopeRules`, `SettingsReader`, `QueryRules`, `RankFusion`, `Packer`, `TokenEstimate`, `CitationStates`, `UntrustedFence`, `Permalinks` |
| `Aw.Context.Features` | `ContextEngine` (the facade: one line per method, dispatching to a slice) and the slices `QueryContext`, `SourcesStatus`, `RefreshDocs` |
| `Aw.Context.Infrastructure` | `Store.ContextDatabase` (context.db), `Docs.SqliteDocIndex` and `Docs.RepoDocsSource` (FTS5 over scrubbed chunks), `Git.GitCli`, `Scrubbing.IngestSanitizer` and `Scrubbing.RedactionScrubber` (both scrub ports over `Aw.Redaction`) |

`deps.yaml`: Context → Kernel, KernelData and Redaction (Infrastructure only) are `project` edges. Sindri → Context is a
`project` edge (Sindri's Features and Host use Core) plus a `compose` edge (Sindri's Host calls `AddContextFeatures` and
`AddContextInfrastructure` in `Composition`, nothing else: ARCH-COMPOSE-001).

**Sindri's side.** `SindriProfile.Context` is a `JsonElement?`, left out of the canonical JSON when absent. Validate and
approve call `IContextEngine.CheckSettings`. The slice folder `Aw.Sindri.Features.ContextCalls` holds:
- `ContextScopes`, the one mapping from the approved profile and `ICallerContextAccessor.Current` to `ContextScope`.
  The engine's client comes from the actor: `…:cli` is cli, `…:mcp` is mcp, other HTTP is http, services are service.
- The queries `QueryContextRequest` → `ContextResult` and `GetContextSourcesRequest` → `SourcesView`.
- The scheduled service `ContextDocsRefreshService` (`context-docs-refresh`, event `context.sources.synced`).

Every surface resolves those services, MCP included (S-1M). The routes are `GET /sindri/context/query` and
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
5. The top 20 are fetched (raw), then cut to 64 KB and cleaned once by `IIngestFilter`: sanitize, collapse titles,
   scrub.
6. One batched `Versions` call per source gives each citation its state.
7. `ResultAssembly`: one `record` item and one citation each. Every field is scrubbed again on egress (a blocked
   field is `[REDACTED:blocked]` alone), untrusted text is fenced, and the items are packed into the budget.

No model is called on the query path.

### Repo-docs

- **Index.** `SqliteDocIndex.Refresh` lists the tree at HEAD (`git ls-tree -r -z`) and matches the globs (`Glob`). It
  reads changed blobs, chunks them by heading (at most 40 lines or 2,000 characters; `DocChunker`) and cleans every
  chunk and the title. Only then does one short transaction remove stale docs, write new ones and move the cursor
  (`source_cursor`). A document the scrubber blocks keeps its `doc` row with `blocked = 1` and has no chunks. A repo
  git can't read records `SND-CONTEXT-010` as its `last_error`, and the refresh reports it in its row.
- **Source.** `RepoDocsSource` searches with each term as a quoted FTS5 phrase (`DocSql.Phrase`), OR-ed and ranked by
  `bm25`, filtered to the repos in scope. A record is a chunk, `<repo>/<path>#L<a>-L<b>`, at the blob sha. The
  canonical key is the document, `repo:<repo>/<path>`. Fetch reads the blob from git at the cited sha and line span;
  Versions makes one tree listing per repo.
- **Git.** `GitCli` passes arguments through `ArgumentList` with no shell, removes the caller's `GIT_*` variables,
  accepts shas only as `^[0-9a-f]{7,64}$` (never an option or a ref expression), and times out at 30 s.

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
| `SND-CONTEXT-005` | Invalid (400) | 1 | An output path inside a git worktree (later stages) |
| `SND-CONTEXT-006` | Invalid (400) | 1 | The question is invalid: no words, two shapes, depth or budget out of range |
| `SND-CONTEXT-010` | Unavailable | — | One source is unavailable: a `degraded` entry, never the answer's error |
| `SND-CONTEXT-011` | Unavailable (503) | 2 | Every source is unavailable |
| `SND-CONTEXT-020` | Forbidden (403) | 1 | Session reads are off (`context.sessions.enabled: false`), on every transport. Defined by S-1A (spec §3); S-1M maps it to the MCP tool error |

### Tests

`tests/Aw.Context.Tests`:
- The contract suites from `tests/Aw.Context.Testing/Engine` run against the fakes (unit) and the real adapters
  (integration): `ContextSourceContract<T>` (`FakeContextSource`, `RepoDocsSource` on the fixture repo),
  `DocIndexContract<T>` (`FakeDocIndex`, `SqliteDocIndex`) and `ScrubberContract` (`FakeScrubber`,
  `RedactionScrubber`).
- `ContextDatabaseTests`: FTS5 in the bundled `e_sqlite3`, the file modes, and migrations named for their stage.
- `RepoDocsTests`: a planted secret never reaches `doc_chunk`, and an edit reads as a new version.
- The pure rules (unit) and the facade over the fakes (`QueryContextTests`, `SourcesStatusTests`).

In `tests/Aw.Sindri.Tests/ContextCalls`:
- the routes through the real pipeline: human, session gate, scope, foreign Origin, codes;
- the caller mapping;
- the CLI's argument parsing and text output.

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
