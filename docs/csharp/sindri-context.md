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
| `SND-CONTEXT-005` | Invalid (400) | 1 | An output path inside a git worktree (later stages) |
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

## S-1M: the MCP endpoint, `mcp register`, the doctor rows

### Where the code lives

- `src/Sindri/Aw.Sindri.Host/Mcp/`: the SDK-facing code, like `Host/Endpoints` (Features takes no SDK or ASP.NET
  types). `McpEndpoint` registers the server (`ModelContextProtocol.AspNetCore` 2.2.0, `WithHttpTransport(Stateless =
  true)`), maps it at `/sindri/mcp` after the `LoopbackGuard`, and adds `UseMcpCaller` before the caller middleware:
  any `Origin` is a 403 (`SND-API-008`); the caller, terminal and token headers are dropped and `X-Sindri-Client: mcp`
  set, so the request is `session:mcp` and the engine sees client `mcp`. `McpToolTable` lists the tools explicitly
  (low-level `WithListToolsHandler`/`WithCallToolHandler`, hand-written schemas; API P8 appends its rows).
  `ContextTools` binds arguments (022), checks `X-Sindri-Repo` (021), and calls the ContextCalls services the HTTP
  routes use (`IService<QueryContextRequest, ContextResult>`, `IService<GetContextSourcesRequest, SourcesView>`), so
  the engine's gate (020) and scope (003) apply. `McpResults` puts the answer in `structuredContent` (the host's AwJson
  options; `context_sources` wraps the view in `McpSourcesAnswer` with the answering `instance`, since Claude Code
  reads `structuredContent` and not the text) beside `McpSummaries`' text; an error is `isError` with `McpToolError {code, message}`.
- `Aw.Sindri.Core/Mcp/`: `McpRegistration` (the claude arguments, the port rule), `McpScope.Judge` (a user-wide
  `sindri` entry in one provider file), and the ports `IClaudeCli` and `IProviderFiles`.
- `Aw.Sindri.Features/RegisterMcp/` (`sindri mcp register`) and `Aw.Sindri.Features/ContextCalls/ContextDoctorChecks`
  with `ContextChecks` (doctor's `context:sources` and `context:mcp-scope`): an `IDoctorChecks` (Core port) that the
  RunDoctor slice's `WithMoreChecks` decorator appends after the core rows, applying the exit rule to the whole list.
- `Aw.Sindri.Infrastructure/Mcp/`: `ProcessClaudeCli` (argument list, no shell, 30 s budget) and
  `ProviderConfigFiles`, both reading `McpSettings` (`AW_SINDRI_CLAUDE_BIN`, `HOME`, `CLAUDE_CONFIG_DIR`, `CODEX_HOME`).

### Errors (S-1M range 021–029; 020 is S-1A's)

| Code | Kind | Surface | Meaning |
|---|---|---|---|
| `SND-CONTEXT-020` | Forbidden | MCP tool error | Session reads off (S-1A's code, mapped) |
| `SND-CONTEXT-021` | Invalid | MCP tool error | The call has no `X-Sindri-Repo` |
| `SND-CONTEXT-022` | Invalid | MCP tool error | An argument of the wrong type, or an unknown one |
| `SND-CONTEXT-023` | NotFound | MCP tool error | Unknown tool |
| `SND-CONTEXT-024` | Invalid | CLI exit 1 | `mcp register`: not a profile repo |
| `SND-CONTEXT-025` | Unavailable | CLI exit 2 | `mcp register --apply`: claude couldn't run or failed |
| `SND-CONTEXT-026` | Conflict | doctor `warn` | A user-wide `sindri` MCP entry in a provider's config |

Every facade error passes through to the MCP tool error unchanged (`isError`, text `<code> <message>`).

### Tests

- `tests/Aw.Sindri.Tests/ContextCalls/ContextMcpTests` (integration): the MCP SDK's own client against a started
  host: tools/list, a `context_query` round trip (citations with blob shas; the summary doesn't repeat record text;
  the client sends no `Origin`), a planted secret scrubbed and planted instructions fenced, every refusal's code (020
  twice, the second with human headers that MCP ignores; 003, 021, 022 twice, 006, 023), `context_sources`, the guard
  (foreign `Host` 400, foreign and loopback `Origin` 403, no `Mcp-Session-Id`), and no question or record text in logs.
- `RegisterMcpTests` (unit), `ClaudeCliContract` (the fake; `ProcessClaudeCli` over a recording stub script, never the
  real claude), `CheckContextTests` (each provider's file shapes, malformed and unreadable files, the sources states),
  `ProviderFilesContract` (the fake; `ProviderConfigFiles` over a scratch home).
