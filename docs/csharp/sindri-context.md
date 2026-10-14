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
`context:retention` in the `RunDoctor` slice's `ContextChecks`, which `ProfileFacts.Context` runs (so
`RunDoctorService` stays within AW1007's dependency limit). CLI: `Host/Commands/ContextMapVerbs` and `ContextMapText`. The map JSON
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

## S-5A: the onyx adapter

### Shape

- `Aw.Context.Core.Onyx`: `OnyxSettings` (only `Create` makes one: loopback url, a secret-pointer token, a pinned
  release tag; `CheckVersion` compares a reported version), `OnyxRecordKeys` and `OnyxRecordIdentity` (canonical key
  and alias). `SourceHit.Aliases` carries the aliases a hit adds; `RankFusion` merges every hit's aliases into
  `FusedItem.Aliases` (source by source, best first, each once); the query cleans each at ingest and `ResultAssembly`
  egress-scrubs each on its own into `Citation.Aliases` (a blocked or empty one is dropped). JSON `cites.<n>.aliases`;
  the text form shows the first beside the key.
- `Aw.Context.Core.Engine`: `SourcesReader` reads `context.sources` (`onyx`, `linear.teams`) for `SettingsReader`;
  `ContextSettings.Onyx` and `ContextSettings.LinearTeams` hold the result. `ScopeRules.MayRead` keeps intent sources
  from session callers until `context.repoTeams` exists.
- `Aw.Context.Infrastructure.Onyx` (all internal): `OnyxSource` (`IContextSource`), `OnyxClient`, `OnyxRequests` and
  `OnyxRoute` (the allowlist), `OnyxVersionProbe`, the wire records and their own `OnyxWireJsonContext`, and
  `OnyxSources.Create(OnyxSettings, IReadOnlySet<string> teams)`, the per-call factory registered by
  `AddContextInfrastructure`.
- `Aw.Context.Infrastructure.Sources.ContextSourceCatalog` replaces S-1A's repo-docs catalog: repo-docs, then onyx when
  the profile names it; no repo in scope gives no source (`SourceCatalogContract`).
- Sindri: `Composition` passes `ContextEnvironment(environment)` to `AddContextInfrastructure`, which resolves a name
  that is a secret pointer through `ISecretPointers` (`SecretPointerEnvironment`); every other name reads the real
  environment. Doctor's `context:onyx` is `OnyxDoctor` in the `RunDoctor` slice, the third row of `ContextChecks` (after
  S-3a's `context:map` and `context:retention`): it resolves the key's pointer first, then asks the facade's `Sources`
  for the approved profile as a service caller and judges the `onyx` row.

### Routes and allowlist

The adapter can send only these requests to Onyx v4.9.0; `OnyxRequests` refuses anything else before it reaches the
network (tested), and the client has no proxy and follows no redirects.

| Route | Use | Key |
|---|---|---|
| `POST /api/admin/search` | `Search`: document ids, `updated_at`, `semantic_identifier`, links; no model | admin |
| `GET /api/document/document-size-info` | `Fetch`: how many chunks a document has | admin |
| `GET /api/document/chunk-info` | `Fetch`: the chunks' text | admin |
| `GET /api/version` | `Probe` and doctor: `backend_version` against the pin | none |
| `GET /api/me` | `Probe` and doctor: the key's role and account type | the key |

### Keys and aliases

Onyx's Linear connector uses the issue's UUID as its document id, so those hits key directly on
`linear:issue/<uuid>` (lower case). The identifier (`ENG-482`) becomes an alias only when the connector-built title
prefix (`[ENG-482] `) and the `linear.app` link name the same identifier and its team is in
`context.sources.linear.teams`. Slack threads (`<channel>__<thread_ts>`) key as `slack:<channel>/<thread_ts>`; anything
else as `onyx:<source_type>/<document id>`. Body text never contributes. Fusion deduplicates on the canonical key, so
the same issue from Onyx and the native Linear adapter (S-6) is one item.

### Versions

`Versions` is always `SND-CONTEXT-010`: no v4.9.0 route gives `updated_at` by id, so onyx citations are `unchecked`.
`SupportsAsOf` is false.

### Errors

| Code | Kind (HTTP) | Exit | Meaning |
|---|---|---|---|
| `SND-CONTEXT-002` | Invalid (400) | 2 | `context.sources.onyx.token` is missing or isn't a secret pointer (`profile validate`; the text isn't repeated); at run time, a pointer that doesn't resolve (a row and doctor code) |
| `SND-CONTEXT-050` | Forbidden | — | Onyx refused the key (403), or the key lacks the admin role `POST /api/admin/search` needs: a row, doctor or `degraded` code |
| `SND-CONTEXT-051` | Unavailable | — | Onyx answered with an unexpected status or shape (not the pinned version's): a `degraded` code |
| `SND-CONTEXT-052` | NotFound | — | Onyx has no such document: the item is dropped, not degraded |
| `SND-CONTEXT-053` | Forbidden | — | The admin key belongs to a person's account, not a service account: a `degraded` row and a doctor `warn` |
| `SND-CONTEXT-055` | Invalid (400) | 2 | The url isn't loopback or the version isn't a pinned release tag (`profile validate`); at run time, Onyx reports another version (a row and doctor `fail`) |

An onyx failure during a query is always the `degraded` entry `SND-CONTEXT-010` with the cause in its message; the
answer's own error is `011` only when repo-docs is down too. The pointer errors are Sindri's: `SND-SECRET-001`
(malformed), `002` (doesn't resolve) and `003` (unsafe `file:`), in `docs/csharp/sindri.md`; through the context
environment a pointer that fails resolves to nothing, which the onyx client reports as `SND-CONTEXT-002` naming only
the pointer's scheme and pointing to `sindri doctor`. Doctor's `OnyxDoctor` resolves the pointer through
`ISecretPointers` before the probe and fails with that `SND-SECRET-00x` code and message. A query never checks the
version (§8.8 makes it a doctor check; §5.1's errors are Unavailable, NotFound and Fatal): `055` shows in doctor and
the row, and a reply the pinned shape can't read degrades onyx (`010` with cause `051`).

### Tests

- `tests/Aw.Context.Testing/Fakes/FakeOnyxServer`: a loopback fake that answers like v4.9.0 (auth by key kind, admin
  search, document size and chunk info, version, `/api/me`).
- `tests/Aw.Context.Tests/Onyx`: `ContextSourceContract` against `FakeOnyxServer`, the client's allowlist, failures,
  settings and record keys; `Core/SourcesSettingsTests` (profile keys, pointer forms, session rule);
  `Infrastructure/ContextSourceCatalogContractTests`.
- `tests/Aw.Sindri.Tests/ContextCalls/OnyxContextTests` (integration, the real host with `env:` fixture pointers):
  fused query with `unchecked` onyx citations, the status row, doctor ok / warn / fail, Onyx down degrading only
  itself; `OnyxProfileTests` (validate and doctor without Onyx); `Secrets/*` for the pointers.
