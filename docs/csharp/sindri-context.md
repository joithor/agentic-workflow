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

## S-5A: the onyx adapter

### Shape

- `Aw.Context.Core.Onyx`: `OnyxSettings` (only `Create` makes one: loopback url, a secret-pointer token, a pinned
  release tag; `CheckVersion` compares a reported version), `OnyxRecordKeys` and `OnyxRecordIdentity` (canonical key
  and alias). `SourceHit.Aliases` carries the aliases a hit adds.
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
  environment. Doctor's `context:onyx` is `OnyxDoctor` in the `RunDoctor` slice: it asks the facade's `Sources` for the
  approved profile as a service caller and judges the `onyx` row.

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
the same issue from Onyx and the native Linear adapter (S-5B) is one item.

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
environment a pointer that fails resolves to nothing, which the onyx client reports as `SND-CONTEXT-002`.

### Tests

- `tests/Aw.Context.Testing/Fakes/FakeOnyxServer`: a loopback fake that answers like v4.9.0 (auth by key kind, admin
  search, document size and chunk info, version, `/api/me`).
- `tests/Aw.Context.Tests/Onyx`: `ContextSourceContract` against `FakeOnyxServer`, the client's allowlist, failures,
  settings and record keys; `Core/SourcesSettingsTests` (profile keys, pointer forms, session rule);
  `Infrastructure/ContextSourceCatalogContractTests`.
- `tests/Aw.Sindri.Tests/ContextCalls/OnyxContextTests` (integration, the real host with `env:` fixture pointers):
  fused query with `unchecked` onyx citations, the status row, doctor ok / warn / fail, Onyx down degrading only
  itself; `OnyxProfileTests` (validate and doctor without Onyx); `Secrets/*` for the pointers.
