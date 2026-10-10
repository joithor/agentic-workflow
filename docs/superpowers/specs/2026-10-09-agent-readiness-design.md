# Agent readiness for agentic-workflow — design

Status: draft for review, revision 6 · 2026-10-10 (rounds 1–2 applied; owner decision: **one clean cutover, no backward
compatibility** — no TS parity, no data migration, no replay; decisions from the R1a PRs and the R1b template PRs
folded in; R1b checks of record in §4b.10)
Target: **Level 5 (Autonomous)** on Factory's agent-readiness framework. Level 4 is a prerequisite for the
factory work in `2026-10-09-sindri-factory-addendum.md` (§17.16). Level 5 is reached together with that work.

## 1. Why

The factory addendum asks agents to build, review and evolve this repo with little human direction. Agents can
only do that safely in a repo that tells them how to work and checks what they do: one command to build and test,
enforced style and types, fast and trustworthy CI, clear boundaries between packages, and machine-readable task
discovery. Today the repo has strict TypeScript and good tests per package, but none of it is enforced in CI. So
the refactoring comes first, and the factory is built on top of it.

This is also the first job for the core tenet: readiness becomes a Sindri module (§8) that keeps measuring the
repo and proposes the next fix.

## 2. The framework

Sources: [Factory agent readiness](https://factory.com/product/agent-readiness), [docs overview](https://docs.factory.com/agent-readiness/overview).

- **Levels:** 1 Functional · 2 Documented · 3 Standardized · 4 Optimized · 5 Autonomous. Passing **80% of a
  level's criteria** unlocks the next level.
- **Nine pillars:** Style & Validation · Build System · Testing · Documentation · Development Environment ·
  Debugging & Observability · Security · Task Discovery · Product & Experimentation.
- **Scope:** some criteria are evaluated once per repo (for example CODEOWNERS and branch protection). Others are
  evaluated per app, and a monorepo reports them as fractions such as `3 / 4`. Here the apps are `mcp-bridge`,
  `scorer`, `judge`, `sindri`, `skills/ui-evidence`, `skills/bugFixOrchestrator` and the `mods/aw-live` plugin.
- **Tools:** `/readiness-report` and `/readiness-fix` (Factory app or Droid CLI), plus a Readiness Reports API.
- **Gap:** Factory doesn't publish the full criteria list, and its own report needs a paid Factory account, which
  this project won't use (decision R4). **Our check is the score of record.** It covers every criterion we can
  reproduce from Factory's public pillars, levels and published examples, and marks any criterion it can't
  reproduce as `unknown`. Scouting (§17.18) watches Factory's public docs for criteria changes.

## 3. Where the repo stands (quick check, 2026-10-09)

| Pillar | Present | Missing |
|---|---|---|
| Style & Validation | `strict: true` in all six TS packages | No linter, no formatter, no pre-commit for lint or types, no `.editorconfig` |
| Build System | `npm run build` and `typecheck` per package; `setup.sh` | No root workspace or single command; no pinned Node version (`.nvmrc`); no lockfile policy across packages |
| Testing | Vitest in every package; bash test suites; real-browser test in ui-evidence | No CI running them; no test lanes or time budgets; no flaky-test detection; no coverage gate in CI |
| Documentation | `AGENTS.md` (with `CLAUDE.md` symlink), `README.md`, `planning/*`, rules index and `sync-rules.sh --check` | Freshness isn't checked automatically; commands in `AGENTS.md` aren't executed by a check |
| Development Environment | `setup.sh --dry-run`, `docs/onboarding.html` | No devcontainer, no `.env.example` |
| Debugging & Observability | JSONL tick logs and hook decision logs in sindri; scorer reports | No shared structured-logging convention, no metrics or tracing (OTel) for bridge or sindri |
| Security | Safety hooks, sindri scrubber pre-commit | **`main` is not protected**; no CODEOWNERS; no secret scanning in CI; no dependency update bot |
| Task Discovery | Plan files read by the plan-file tracker; 9 labels | No issue or PR templates; no labeling scheme |
| Product & Experimentation | scorer telemetry; judge prompt-sorter shadow mode; evolve offline comparisons | Not framed or reported as product analytics and experiments |

**Biggest gap:** there is **no CI at all** (`.github/workflows` doesn't exist), and `main` has no protection. The
merge gate in `AGENTS.md` is run by hand.

## 4. Engineering standards (what "ready" means here)

The standards match the ones set for the ring-2 product, adapted to this repo. **§4b is the single source of
truth** for every rule, number and tool: limits, analyzer levels, test lanes, architecture-test library. Other
sections point to it rather than restating it. Each rule becomes a rule record (addendum §17.6) with a
deterministic check wherever possible.

The TypeScript that stays (§4a: the Claude Code mods and the TS-indexer sidecar) uses `tsc` strict plus
`noUncheckedIndexedAccess`, Biome, and the same size limits as §4b.6. Bash in `config/`, `scripts/` and
`providers/` is checked with `shellcheck` and `shfmt`.

## 4a. Language: C# on .NET (decided 2026-10-09)

The human decided to rewrite the toolkit's TypeScript packages in **C# on .NET 10 (LTS)**, running the same on
Linux and macOS. **The readiness foundation comes first** (rollout R1): the standards, rules, template, shared
building blocks and CI checks are set up for C# before any package is ported, so each ported package is
compliant from its first commit. The TypeScript is not linted or refactored, since it is being replaced. It only
gets CI as a safety net until the switch. The standards in §4 still apply, expressed in .NET tooling.

**What moves, and to what:**

| Today (TS) | C# / .NET |
|---|---|
| `tsc` strict | Nullable reference types enabled, `TreatWarningsAsErrors`, analyzer levels per §4b.6 |
| Biome / typescript-eslint (planned) | Roslyn analyzers (built-in + `Microsoft.CodeAnalysis.NetAnalyzers`), `.editorconfig` severities, `dotnet format` in pre-commit and CI |
| dependency-cruiser (planned) | One project per layer (§4b.1) with project references as the boundary, plus ArchUnitNET architecture tests and `deps.yaml` (§4b.1a) |
| Zod schemas, JSON Schema export | Records with `System.Text.Json` source generation; validators per schema; JSON Schema generated from the types for editor autocomplete |
| Vitest | xUnit v3 on Microsoft.Testing.Platform (R7), with the lanes and time budgets in §4b.5 |
| better-sqlite3 | `Microsoft.Data.Sqlite` with its bundled SQLite (§4b.2), new C# schemas, WAL mode |
| Fastify (bridge) | ASP.NET Core minimal APIs |
| `@modelcontextprotocol/sdk` | The official MCP C# SDK |
| Node CLIs started per hook call | **NativeAOT** binaries for hook and CLI entry points only (§4b.6 AOT table), so startup stays fast; a tool that opens a database ships the bundled SQLite library beside it. Reflection-free JSON (source generators) is required for them |
| Playwright (ui-evidence) | Playwright for .NET |

**What stays outside C#, on purpose:**
- **Claude Code mods** (`mods/aw-live`) run in-process in Claude Code, so they stay TypeScript. They stay thin and
  call the C# CLIs.
- **Indexing TypeScript repos** needs the TypeScript compiler API. It becomes a small, pinned Node sidecar behind
  the `Index` adapter. C# repos get a Roslyn indexer. Work and product repos that are TS keep full support.
- **graphify** (Python) stays an external, sandboxed tool behind its adapter.
- **Bash** for installers and `setup.sh`, checked with shellcheck and shfmt.
- **Agent sessions** are started through the `claude` CLI (as today), so there's no dependency on an Agent SDK.

**How the rewrite runs (strangler, never big-bang):**
1. **Contracts are designed, not recorded.** Each package's external surface (CLI arguments, output and exit
   codes, JSON files, SQLite schemas, MCP tool schemas and HTTP routes) is defined by the C# package's own tests.
   The TS behavior is the reference for what the contract should do, not a byte-for-byte target, and the C# build
   is free to fix what the TS got wrong, except on the hook-facing CLI and file surface, which is frozen until the
   switch (§4c).
2. **One package at a time,** lowest tier first, in the §10 R2 order: `Redaction` → `judge` and `mcp-bridge` →
   `scorer`, `skills/ui-evidence` and the `skills/bugFixOrchestrator` state CLI → `sindri` (by module: profile and
   ledger, scrub, observe, index, scope, evolve).
3. **One clean cutover (decided 2026-10-10).** The installed TS toolkit stays untouched while the C# build is
   ported and proven beside it. Each package is "done" when its tests pass in every lane within budget, its AOT
   entry points build and pass the AOT CI job, the real hook scripts pass against its binaries in CI, and it
   builds on macOS and Linux. When **every** package is done, one reversible switch moves the install to C#.
4. **No data compatibility.** The C# build never reads TS-written ledgers, indexes or state files, and the TS build
   never reads C# data. At the switch the TS packages' state is moved aside and the C# build starts with fresh state in its
   own schemas (which include the multiplayer seams, §4b.6).
5. **Each package rewrite is a packet** (addendum §17.4), built with today's skills on the host. That makes the
   rewrite the first real test of the packet format and the thin end-to-end slice (F0.5), before the factory
   exists.

**Risks to watch:**
- NativeAOT limits (reflection, some libraries). Every CLI builds AOT in CI from day one.
- The Plan 5 evolve suites and judge calibration data have TS-specific fixtures. They're ported as test data where
  they still apply, and their pass rates are re-baselined on the C# build before the switch.
- Rewrite fatigue: the rewrite is time-boxed per package, and a package that overruns 2× its estimate is reviewed
  before more work goes into it.

## 4b. C# code standards and package organization

The human's preferences set the shape:
- minimal endpoints;
- services;
- the repository pattern over a SQL database;
- strong generics and strong typing used through dependency injection;
- test-driven development;
- no spaghetti.

The research behind these choices is listed in Sources at the end of this section. Every rule below becomes a
rule record (addendum §17.6) with the check named in its row. A rule with no check is a gap to close in R1.
The records live one per file in `rules/engineering/csharp/<slug>.yaml`, listed in
`rules/engineering/csharp.inventory.yaml` and validated against `schemas/rule-record.schema.json` by
`node scripts/check-rules.mjs` (merge gate; R1b, #137). §4b.10 lists the check of record for each rule.

### 4b.1 Architectural shape: feature slices on a clean dependency direction

Two common .NET shapes trade off differently. Clean Architecture controls complexity with layers, and fits
features that share a rich domain. Vertical slices control it by isolating features, and fit features of very
different complexity. The usual middle ground, used here: **organize by feature, and keep the clean dependency
direction.**

```
Host ──► Features ──► Core ◄── Infrastructure
 │                     ▲            │
 └──── composes ───────┴────────────┘   (Host is the only place that knows every project)
```

| Project (per package) | Holds | May reference |
|---|---|---|
| `Aw.<Pkg>.Core` | Domain types, strongly typed ids and value objects, pure logic, **port interfaces** (repositories, clock, process runner), errors, its own `Add…()` registration method | `Aw.Kernel`, plus `Microsoft.Extensions.DependencyInjection.Abstractions` for that method and Vogen for its typed ids (R9) |
| `Aw.<Pkg>.Features` | One folder per use case (a slice): request and response records, the service (handler), the validator | `Core`, `Aw.Kernel`, other packages' `Core` |
| `Aw.<Pkg>.Infrastructure` | Repository implementations over SQL, external adapters (git, containers, model CLIs, HTTP), migrations | `Core`, `Aw.Kernel`, `Aw.Kernel.Data`, and other tier-0 packages through a declared `deps.yaml` edge |
| `Aw.<Pkg>.Host` | The composition root: the minimal API app or the CLI `Main`, the endpoints and CLI commands (one per slice, so the request-delegate generator covers them), `Compose` (calls each project's `Add…()`), settings, the JSON context | everything in the package, plus `Aw.Kernel.Telemetry` and the tier-0 and lower-tier `Core`s it composes |
| `Aw.Kernel` (shared) | `Result<T>`, `Error`, clock and id ports, value-object conventions, the generic `IService<TReq, TRes>` pipeline and decorators, the shared JSON profile (§4b.3) | the BCL only |
| `Aw.Kernel.Data` · `Aw.Kernel.Telemetry` · `Aw.Kernel.Testing` | SQLite access and migrations runner · logging and OpenTelemetry setup with the scrubbing processor · fakes base, builders, contract-test bases, golden-file assertions | `Aw.Kernel` plus their one external dependency each; Telemetry also references `Redaction` (its scrubbing processor) and Testing also references `Aw.Kernel.Data` (contract tests run on SQLite) |

**Small packages:** a package under about 1,500 lines may use two projects, `Core` and `App` (features,
infrastructure and host together), with the same dependency direction. It splits into four when it crosses the
threshold.

**Rules** (enforced by project references, which fail the build, and ArchUnitNET tests):
- `Core` has no I/O. It doesn't reference `System.IO`, `System.Net`, `Microsoft.Data.*`, `System.Diagnostics.Process`,
  `Console`, `DateTime.Now`/`UtcNow` or `Guid.NewGuid`. They're banned through `BannedSymbols.txt` (the
  BannedApiAnalyzers package), and time and ids come from injected ports (`TimeProvider`, `IIdGenerator`).
  Technology packages the banned list can't name (`Aw.Kernel.Data`, `SQLitePCL`, `Npgsql`, cache and queue clients)
  are kept out of `Core`, `Features` and a small package's `Commands` by an architecture test, and `Core` and
  `Features` use no tier-0 package other than `Aw.Kernel` (§4b.10).
- One slice never references another slice's types. Shared behavior moves into `Core` as a domain service or a port.
  A second use of the same logic is the trigger to move it (the "generalize at the second case" signal, §6.2).
- `Infrastructure` row and DTO types are `internal`. They never leave the project, and repositories return `Core` types.
  The architecture test is stricter: in `Infrastructure` only the `*ServiceCollectionExtensions` class is public.
- Packages reference other packages only through their `Core` project's public surface, or through a CLI or HTTP
  contract. Their `Infrastructure` and `Features` are never referenced.

### 4b.1a No cycles, no spider web

The dependency graph is a **declared, layered, acyclic tree** at every level, and CI fails on any drift from the
declaration.

**Package tiers** (a package takes `project` dependencies only on lower tiers; read-only `db` and `file` edges may also point across a tier, see the direction rule below):

| Tier | Packages | May depend on |
|---|---|---|
| 0 | `Aw.Kernel` and its `Data`, `Telemetry`, `Testing` parts, `Analyzers` (build-time only and edge-less: referenced only as an analyzer), and `Redaction` (the scrubbing patterns now copied between judge and sindri; moved here by §4e B10) | the BCL plus one external package each, and lower tier-0 sublevels (below) |
| 1 | `Judge`, `Bridge`, `Packets`, `Readiness` | Tier 0 |
| 2 | `Scorer`, `Evidence` (ui-evidence), `BugFix` | Tiers 0–1 (today they call judge, and scorer reads `judge.db`) |
| 3 | `Sindri` | Tiers 0–2, **only through their `Core` contracts or their CLIs** (every package reaches a non-tier-0 package only through its `Core`; `deps.yaml` check DEPS-PROJ-007) |
| — | Every `Host` | Its own package's projects, plus the tier-0 and lower-tier `Core`s it composes |

- **Tier 0 is ordered inside:** it is the one tier with internal project references. `deps.yaml` gives each tier-0
  package a sublevel (0: `Aw.Kernel`, `Redaction`; 1: `Data`, `Telemetry`; 2: `Testing`), and a tier-0 project
  references only a lower sublevel. Every other tier keeps the rule above: never a project reference within the tier.
- **Who holds a cross-package reference:** only `Features` (`App` in the small shape) and `Host` reference another
  package's `Core`; `Core` and `Infrastructure` never reference a package above tier 0 (DEPS-PROJ-008). Test
  projects follow their owning package's edges.
- **Declared graph, every edge kind:** `deps.yaml` (with a JSON Schema) lists each package's tier and every allowed
  edge, with its kind: `project` (a reference), `cli` (one package runs another's binary), `db` (one package opens
  another's SQLite file) or `file` (shared state files). Project edges are checked against the real references. CLI,
  database and file edges are checked by a scan of process launches, connection strings and known state paths. Any
  undeclared edge fails CI. **Direction:** `project` edges point strictly down the tiers; `db` and `file` edges
  point down or across, are read-only, and are slated to become CLIs or published views over time (today: scorer
  reads `judge.db`, and reads Evidence's run files across tier 2, §4e B10). Adding an edge is
  a reviewed change to `deps.yaml` (and an approval-tier change once it is a rule record).
- **Prefer contracts over references:** a package calls a same-tier or higher-tier package only through its CLI or
  HTTP contract, never through a project reference (read-only `db` and `file` edges across a tier are the declared,
  temporary exception above). Copied logic is a smell: the redaction patterns copied between
  judge and sindri move into the tier-0 `Redaction` package, and scorer's labeler, which sindri copies, moves into
  scorer's `Core` for sindri to call through scorer's CLI.
- **Inside a package**, the four-project shape (§4b.1) is itself a fixed DAG. **Inside a project,** feature
  folders and namespaces must not form cycles. A namespace-graph check over the ArchUnitNET model covers every
  namespace in every registered production assembly (MSBuild already refuses circular *project* references; this
  covers the namespaces inside one). Test projects and the AOT smoke apps aren't registered, so it doesn't cover them.
- **Fan-out limits** (every `Host` and all test code are exempt: composing is the Host's job): a project references at most 4 other in-repo projects and at most 6 third-party packages
  (analyzers and test packages excluded; DEPS-FANOUT-002/003). Per package, at most 4 other packages through
  declared project edges (`limits.packageFanOut` in `deps.yaml`, DEPS-FANOUT-001). Going over either is a ratchet
  violation that needs a recorded exception with a reason and an expiry.
- **No dumping grounds:** there is no `Common`, `Shared`, `Utils` or `Helpers` project or namespace. Code shared by
  two packages goes into `Aw.Kernel` only if it is domain-free and stable; otherwise it is duplicated until a
  second case proves the abstraction (§6.2), then extracted into the owning package's `Core`.
- **Stability direction:** lower tiers change least. Fan-in and fan-out per project are reported in CI, and a
  tier-0 or tier-1 change that would add a dependency on a higher tier is impossible by the rules above.
- **Visible:** CI renders the package and project graph (Mermaid from `deps.yaml` plus the real references) into
  the PR summary whenever it changes, so a new edge is reviewed as a picture, not found later.

### 4b.2 Request flow: minimal endpoint → service → repository → SQL

```csharp
// Features/StartItem/StartItem.cs — one slice, one file per public type in practice
public sealed record StartItemRequest(ItemId Item, SizeClass Size);
public sealed record StartItemResponse(SessionId Session);

internal sealed class StartItemService(IItemRepository items, ISessionLauncher launcher, TimeProvider clock)
    : IService<StartItemRequest, StartItemResponse>
{
    public async ValueTask<Result<StartItemResponse>> Handle(StartItemRequest req, CancellationToken ct)
    {
        var item = await items.Get(req.Item, ct);                 // Result<Item>
        if (item.IsError) return item.Error;
        // pure decisions live on the domain type, not in the service
        var plan = item.Value.PlanStart(req.Size, clock.GetUtcNow());
        if (plan.IsError) return plan.Error;
        return (await launcher.Start(plan.Value, ct)).Map(s => new StartItemResponse(s));
    }
}

// Host/Endpoints.cs — minimal endpoint: bind, call, map. No logic.
group.MapPost("/items/{id}/start", async (ItemId id, StartItemBody body, IService<StartItemRequest, StartItemResponse> svc, CancellationToken ct)
    => (await svc.Handle(new(id, body.Size), ct)).ToHttpResult());   // TypedResults + ProblemDetails
```

- **Endpoints and CLI commands** only bind input, call one service, and map the `Result` to HTTP (`TypedResults`,
  `ProblemDetails`) or to an exit code and output. They contain no branching on domain state. Group endpoints per
  feature with `MapGroup`. Use `WebApplication.CreateSlimBuilder` and the request-delegate source generator, so
  endpoints are AOT-compatible.
- **Services** implement the generic `IService<TRequest, TResponse>` and do one use case each. There are no "manager"
  or god services. A service with more than 5 constructor dependencies is a split signal, and it's an analyzer error
  over 7.
- **Cross-cutting behavior** (validation, logging, transactions, budgets) is a generic decorator
  `Decorated<TReq, TRes>` around `IService<,>`, registered explicitly. There's no MediatR or AutoMapper: both moved to
  commercial licences in 2025, and explicit code is easier for agents to follow. Mapping is hand-written.
- **Repositories:** one interface per aggregate in `Core`, named by intent (`IItemRepository.Get`, `.SaveStarted`,
  `.ListStartable`).
  - A small generic base, `IReadRepository<TEntity, TId>`, covers `Get` and `Exists` for consistency, but there's no
    generic `IRepository<T>` exposing `IQueryable` or arbitrary filters. Queries are named methods, so the SQL lives
    in one place.
  - Writes run inside an `IUnitOfWork` (one SQLite transaction) passed by the transaction decorator.
- **SQL:**
  - Data access is plain ADO.NET over `Microsoft.Data.Sqlite`, with typed parameter binding and an
    `IRowMapper<T>` per row type.
  - SQL lives in `const string` fields next to the repository, or in embedded `.sql` files. Schema changes are numbered
    SQL migration files, and a migration test runs each one on a copy of the previous schema.
  - **Decided (R8, R1a spike, `docs/readiness/aot-spike.md`):** EF Core 10 and Dapper.AOT both run under NativeAOT,
    but neither builds with `TreatWarningsAsErrors`. EF Core fails on IL2026/IL3050 in the `DbContext` constructor
    and calls NativeAOT and query precompilation experimental. Dapper.AOT fails only on `Dapper.dll`'s
    assembly-level IL2104/IL3053, and suppressing those hides any call its generator can't intercept: the spike's
    generic `Query<T>` built clean and then threw `PlatformNotSupportedException` at runtime.
  - **Native SQLite (decided):** the bundled `e_sqlite3` that `Microsoft.Data.Sqlite` ships, as a second file next
    to each AOT tool that opens a database, listed in `binaries.lock`. The same SQLite runs on every machine, and it
    avoids depending on the host library (on Debian and Ubuntu the unversioned `libsqlite3.so` the OS provider loads
    ships only in the `-dev` package). Every connection sets `journal_mode=wal`, `busy_timeout` and
    `foreign_keys=on`.

### 4b.3 Types, generics and errors

- **Strongly typed ids and value objects** for every id and every constrained primitive (`ItemId`, `RepoSlug`,
  `SizeClass`, `TokenBudget`). **Decided (R9):** Vogen generates validation, equality and the analyzer that bans
  `default` literals (VOG009), with its System.Text.Json generation **off**. Source generators can't see each
  other's output: a Vogen-generated STJ converter is invisible to the STJ source generator (the id serializes as
  `{}`, with no warning for string ids), and a parse method that exists only in Vogen's output is invisible to the
  request-delegate generator (a route id stops binding).
  - every package that declares ids sets `[assembly: VogenDefaults(conversions: Conversions.None,
    systemTextJsonConverterFactoryGeneration: SystemTextJsonConverterFactoryGeneration.Omit)]`;
  - each id has a user-code `[JsonConverter]` that reads with `TryFrom` and throws `JsonException` (a 400, never
    Vogen's exception). A generic Kernel converter (`[JsonConverter(typeof(StringIdJsonConverter<ItemId>))]`)
    satisfies this;
  - `IParsable<T>` must be declared in user code so the request-delegate generator sees it: either the id declares
    the Kernel's `ITypedId<TSelf, TValue>` (which extends `IParsable<TSelf>`), or the id writes `TryParse` by hand
    with Vogen's parse generation off. Only the hand-written `TryParse` was verified through the request-delegate
    generator in the R1a spike, so an id that uses the `ITypedId` form is used behind an endpoint only once an RDG
    route-binding test passes for that form;
  - the scaffolds (`/new-slice`, `/new-package`) generate the converter and `IParsable<T>` with each id;
  - the Kernel JSON profile sets `RespectRequiredConstructorParameters` and `RespectNullableAnnotations`, because a
    missing property otherwise deserializes to an uninitialized id without calling the converter;
  - an uninitialized id can still reach a handler (the RDG passes one for an empty query value, `?id=`), so the
    Host maps Vogen's validation exception to a 400 at the boundary;
  - an architecture test fails if any value object lacks a user-code `[JsonConverter]` or has Vogen STJ
    conversions on, and each id has a JSON round-trip test (including missing and null values).

  A raw `string` or `Guid` in a public signature of `Core` or `Features` is an analyzer error. A `Guid` always
  counts; a `string` counts only when its member is named as an id (so ordinary text stays a `string`); a typed id's
  own members are exempt.
  - A public `Guid` or `int` id needs `NoWarn RS0041` on its `Core` (Vogen's generated member is
    nullable-oblivious); `/new-slice` adds it with the first such id.
- **`Result<T>` and `Error`** live in `Aw.Kernel`:
  - expected failures (not found, invalid, conflict, rate-limited, budget exceeded) are `Error` records with a stable
    code from the error catalogue (`SND-…`, `AW-…`);
  - exceptions are only for bugs and broken invariants;
  - analyzers flag a `catch (Exception)` outside `Host` and `Infrastructure` boundary adapters.
- **Generics carry the design:** `IService<TReq, TRes>`, `IReadRepository<TEntity, TId>`, `IRowMapper<T>`,
  `IValidator<T>`, `Decorated<TReq, TRes>`, and generic contract-test base classes (§4b.5). They're constrained (for
  example `where TId : struct, IEquatable<TId>`). Generic types never take more than 3 type parameters.
- **Nullable** is on everywhere. `!` needs a `// why:` comment, checked by a custom analyzer. `required` members and
  primary constructors are preferred, and records are immutable by default.
- **Visibility:** classes are `internal sealed` by default. Public API is tracked with `PublicApiAnalyzers`
  (`PublicAPI.Shipped.txt`), so a change to a package's public surface shows in the diff.
- **JSON:** System.Text.Json with one source-generated `JsonSerializerContext` per package and one shared Kernel
  profile (`AwJson`): camelCase names in declaration order; relaxed escaping (only what JSON requires); null
  properties written; timestamps in the `O` round-trip form (UTC, seven fractional digits and `Z`), and reading
  requires `Z` or an offset; two-space indentation with `\n`; the strict required and nullable settings above.
  There is no requirement to match the bytes the TS build writes. No reflection-based
  serialization (it's AOT- and trim-analyzer enforced).

### 4b.4 Dependency injection

- `Microsoft.Extensions.DependencyInjection`. Each project owns one explicit extension method that registers its own
  types and decorators (`services.AddSindriCore()`, `AddSindriInfrastructure(settings)`). Only `Host` calls them and
  builds the container (its `Compose`). There's no assembly scanning, because it's reflection and not AOT-safe.
- Constructor injection only: no service locator. `IServiceProvider` appears outside `Host` only as the parameter of a
  registration factory lambda inside a project's own `Add…()` method. The architecture test can't isolate the
  lambda, so it allows any member of a `*ServiceCollectionExtensions` class (and, in the small shape, the `App` root
  namespace).
- Composition shape: decorators are registered in the owning project's `Add…()` method, outermost first, never in
  `Host`; each host has one `Compose` method that `Program.cs` and the DI validation test both call. Hosts start
  telemetry there with `AddKernelTelemetry(TelemetryOptions.FromEnvironment(service, environment, clock))`, and
  `Program.cs` resolves `TelemetryRuntime` once the container is built (one runtime per process; R1b, #136).
- Nothing reads `IConfiguration`: settings come from the environment `Compose` passes. OpenTelemetry brings the
  configuration assemblies into every host, and `BannedSymbols.txt` doesn't apply to hosts, so this needs an
  architecture rule over the host assemblies (a gap until it lands).
- Lifetimes: services and repositories are scoped per request or command, pure domain services are singletons, and
  `TimeProvider` is a singleton (a `FakeTimeProvider` in tests).
- A DI validation test builds the container with `ValidateOnBuild` and `ValidateScopes` for every host.

### 4b.5 Test-driven development without spaghetti

- **Red first.** Every slice starts with a failing test that names the behavior (`StartItem_parks_when_budget_is_exhausted`).
  The commit history shows the red test before the code (the existing `/specToProvenPR` red-commit rule).
- **Tests by feature, not by layer:** `StartItemTests`, not `ItemRepositoryTests` plus `ItemServiceTests`.
- **Lanes:**
  - `unit`: `Core` logic and services with in-memory fakes. No I/O, under 10 s per package.
  - `integration`: repositories on a real temp SQLite file; endpoints through `WebApplicationFactory` or the real
    host listening on loopback. Under 60 s.
  - `heavy`: real containers, browsers and index builds. Nightly or alone.

  Lanes are test traits, run from the repo root with `dotnet test --solution AgenticWorkflow.slnx --filter-trait
  "Lane=<lane>" --ignore-exit-code 8` (Microsoft.Testing.Platform options only, no `--logger`). Exit code 8 means a
  test project has no tests in that lane (an integration-only project in the unit lane), so every lane-filtered run
  ignores it; the unfiltered `dotnet test --solution AgenticWorkflow.slnx` keeps exit 8 as a failure. A lane's budget
  is the wall clock of `dotnet test --project <test project> --filter-trait "Lane=<lane>" --ignore-exit-code 8` per
  test project, summed per package; CI fails a package over its budget. Each test's own time from xUnit's CTRF report
  is checked against the same budget as an extra per-test cap (`Aw.Kernel.Testing` ships the check). The
  per-test cap reads one CTRF report per test project, so a lane run writes them with `--report-xunit-ctrf
  --results-directory <dir>` (never a fixed `--report-xunit-ctrf-filename` with `--solution`, which keeps only the
  last project's report), then `dotnet run --project tests/Aw.LaneCap.Testing -- <dir>` checks them (R1b, #133).
  CTRF reports carry the OS user name, so they are never committed.
- **Every test carries exactly one `Lane` trait.** The per-test cap reports a test without exactly one known lane
  (LANE-TEST-002); a scan of every built test assembly is the planned complete check.
- **Fakes, not mocks.** Each port has one in-memory fake in a shared `Aw.<Pkg>.Testing` project. A generic contract-test
  base class (`ReadRepositoryContract<TEntity, TId>`, `ItemRepositoryContract`) runs the **same tests against the fake
  and the SQL implementation**, so the fake can't drift. Mocking libraries are not used. Test-support projects
  (`*.Testing`) are referenced only by test code.
- **Test data:** builders with sensible defaults (`AnItem().WithSize(S).Build()`), deterministic ids and
  `FakeTimeProvider`, and no shared mutable fixtures. Golden files are used for CLI output and JSON contracts.
- **Architecture tests** (ArchUnitNET, which is maintained; NetArchTest's last release was in 2023) run in the unit
  lane, in `tests/Aw.Architecture.Tests` (rule ids in §4b.10). They cover the dependency directions, no
  slice-to-slice references, the `internal` row types and the naming rules. They run over the assemblies listed in
  the `Packages.Production` registry (`tests/Aw.Architecture.Tests/Packages.cs`), which `/new-package` appends to; a
  production project in the solution that is missing from the registry fails.
- **Framework (decided, R7):** xUnit v3 on Microsoft.Testing.Platform. Tests run JIT; the AOT binaries are checked
  by the AOT CI job, which runs the CLI smoke tests and the real hook scripts against them. Both frameworks were well
  inside the lane budgets in the R1a spike.

### 4b.6 Build settings and analyzers (repo root)

- `global.json` pins the .NET SDK exactly (`rollForward: disable`, no prereleases) and sets the test runner to
  Microsoft.Testing.Platform.
- `Directory.Build.props`:
  - language and nullable: `Nullable=enable`, `LangVersion=latest`, `ImplicitUsings=enable`;
  - warnings: `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, and `EnableNETAnalyzers=false`
    because the pinned NetAnalyzers package owns the CA rules (with the SDK copy also on, every CA diagnostic is
    reported twice). There is no `AnalysisLevel`: the SDK applies it only when `EnableNETAnalyzers` is true and the
    package ships no replacement, so severity comes from `.editorconfig` (a bulk `error` baseline, explicit
    per-rule lines, and `dotnet_analyzer_diagnostic.category-<X>.severity` lines where a whole category is raised);
  - `GenerateDocumentationFile=true` everywhere (IDE0005 needs it); test projects turn off CS1591 instead;
  - NuGet audit: NU1903 and NU1904 (high, critical) fail the build, NU1901 and NU1902 stay warnings, because the
    advisory data is live and would otherwise turn every branch red without a repo change. NU1900 (the advisory feed
    couldn't be read) stays an error so the high and critical gate is never silently skipped; CI retries the restore
    once;
  - `RestorePackagesWithLockFile=true`, plus `RestoreLockedMode` in CI. Locked mode only catches reference drift, so
    CI also fails on a missing lock file (`Directory.Build.targets`) and on a lock file that restore changed
    (`scripts/dotnet-verify-packages.sh`).
- `Directory.Build.targets` (evaluated after the project body, so a project's own settings are seen):
  - `IsAotCompatible=true` on every library project: everything except `*.Tests`, `*.Testing`, `*.Host` and
    `*.Analyzers` (so `Core`, `Features`, `Infrastructure`, a small package's `App`, Kernel and Redaction). A library
    that can't be AOT-compatible opts out with `AwAotCompatible=false` and a reason in its `.csproj`. Only TFMs that
    support it (net8.0 and later), so a netstandard2.0 source generator doesn't fail with NETSDK1210. `PublishAot=true`
    only on the entry points in the table below;
  - `InvariantGlobalization=true` on every project with `PublishAot=true`: no ICU dependency (a Linux binary without
    it needs `libicu` at runtime);
  - `EnableRequestDelegateGenerator=true` on every `*.Host` project, so JIT endpoint tests run the same generated
    binding as the AOT binary;
  - the missing-lock-file check (AW0001): under CI, a project without a committed `packages.lock.json` fails;
  - the placement check (AW0002): every `*.Tests` and `*.Testing` project, including `Aw.Kernel.Testing`, lives
    under `tests/`, and nothing else does.
- Test projects (under `tests/`) get their analyzer policy from `tests/.editorconfig`: CA1707, CA2007, CA1515,
  RS0016, RS0017, RS0036, RS0037 and CS1591 off, each with its reason, plus xUnit1051 off (MA0040 owns the
  pass-a-`CancellationToken` rule). `GenerateDocumentationFile` stays on there too. CA2007 elsewhere applies to libraries only
  (`output_kind = DynamicallyLinkedLibrary`): hosts have no SynchronizationContext.
- `Directory.Packages.props`: Central Package Management with exact versions (`[x.y.z]`; a bare version is a
  minimum). Analyzers are added as
  `GlobalPackageReference`: `Microsoft.CodeAnalysis.NetAnalyzers`, `Meziantou.Analyzer`, `Roslynator.Analyzers`,
  `Microsoft.CodeAnalysis.BannedApiAnalyzers`, `Microsoft.CodeAnalysis.PublicApiAnalyzers`. New packages follow
  the same 14-day minimum age as the existing pins.
- **One owner per rule (A14):** where analyzer packs overlap (for example CA2007 and MA0004 on `ConfigureAwait`),
  `.editorconfig` names one owning rule and disables the others. Roslynator, which overlaps the IDE rules most, is
  off except for rules opted in by name. Existing violations are captured in a generated block at the end of the
  root `.editorconfig`, after a fixed sentinel line, that lowers only those rule-and-file pairs; the ratchet check
  (W2) will fail CI if that block grows. (Not a `baseline.globalconfig`: a global config can't lower a rule `.editorconfig` sets,
  and its sections need absolute paths.)
- `.editorconfig` sets every rule's severity explicitly (no "suggestion" level for anything agents should obey).
  The size limits: file ≤ 300 lines, method ≤ 40 lines, cyclomatic complexity ≤ 10, parameters ≤ 5. Existing
  violations go into a ratchet baseline that only shrinks.
  Also from `.editorconfig`: no `TODO` comments (MA0026; finish the work or open a work item) and one top-level type
  per file, named after it (MA0048). A suppression (`#pragma warning disable`, `[SuppressMessage]`) or a project-level
  severity override needs a written reason and is reviewed like an override.
- `nuget.config` maps every package source to nuget.org only (package source mapping).
- `Aw.Analyzers`: a small in-repo analyzer project for the rules no package covers:
  - no raw ids in public signatures;
  - `!` needs a reason;
  - no cross-slice references (fast feedback; the architecture test is authoritative);
  - endpoint lambdas call exactly one service;
  - the size limits no off-the-shelf analyzer covers: file ≤ 300 lines, parameters ≤ 5, constructor dependencies
    (≤ 5, an error over 7) and generic arity ≤ 3.

  It runs on production projects only, not on test code under `tests/`. Class constructors fall under the
  constructor-dependency rule rather than the parameter rule; record constructors (data shapes) and lambdas aren't
  counted. The 6–7 constructor-dependency split signal is reported at `suggestion`, the one deliberate exception to
  "never suggestion" below (an owner question, §9 R11). Ids in §4b.10.

**AOT scope** (verified in the R1a spike; anything that fails ships framework-dependent with a startup budget):

| Entry point | Build | Why |
|---|---|---|
| Hook binaries, `judge`, `sindri`, `scorer`, `bugfix-state` CLIs | NativeAOT | Run per hook call or per command; startup matters |
| `Bridge` hosts: REST (minimal API) and stdio MCP, two entry points as today | NativeAOT (decided: the MCP C# SDK passed the R1a spike with 0 warnings) | MCP tools use the SDK's low-level list/call handlers, so tool schemas and error shapes are explicit in code |
| `Evidence` (ui-evidence) | Framework-dependent | Playwright for .NET doesn't support NativeAOT |

AOT publishing runs only in a separate CI job and in releases. Local builds and tests are JIT, so the inner loop
stays fast.

**Seams for distribution and multiplayer** (factory addendum §17.20, §17.21): a `LedgerStore` port with a shared
contract-test suite, idempotent events, an event envelope (`actor`, `tenant`, `ring`, `causation`, `correlation`,
`trace`), a transactional outbox, and `Lease`, `Launcher`, `Scheduler`, `Bus` and `Secrets` ports, in `Aw.Kernel`
and `Aw.Kernel.Telemetry`. The C# build never shares files with the TS build (one clean cutover, §4a), so the
seams go into the C# schemas **from the first commit**, and the C# build starts with fresh state at the switch.

**Every external technology sits behind a port** (database, cache, queue or bus, file system, processes, HTTP, git,
containers, model CLIs, secrets, clock and ids). `Core` declares the port, named for what the code needs
(`IItemStore`, not `ISqliteItems`); each technology is one `Infrastructure` adapter; only the host's `Compose`
picks the adapter. Every port has a contract-test suite that its in-memory fake and every real adapter pass (BCL
ports such as `TimeProvider`, faked by `FakeTimeProvider`, are exempt). In
R1b the Kernel ports have theirs in `Aw.Kernel.Testing`: `ReadRepositoryContract`, and `UnitOfWorkContract` and
`OutboxContract` (R1b, #133), run against the fakes and against `Aw.Kernel.Data`'s SQLite adapters, which include
the transactional outbox `SqliteOutbox` with its table from `SqliteOutboxSchema.CreateMigration`. Logging's port
is `ILogSink` in `Aw.Kernel.Telemetry`, with its contract in `tests/Aw.Kernel.Telemetry.Testing` (R1b, #136).

### 4b.7 Repository tree

```
agentic-workflow/
├── global.json · Directory.Build.props · Directory.Build.targets · Directory.Packages.props · nuget.config
├── .editorconfig · BannedSymbols.txt · CodeMetricsConfig.txt
├── AgenticWorkflow.slnx
├── src/
│   ├── Aw.Kernel/                     # Result, Error, IService<,>, decorators, contracts
│   ├── Aw.Kernel.Data/ · Aw.Kernel.Telemetry/
│   ├── Aw.Analyzers/                  # repo-specific Roslyn rules
│   ├── Redaction/ · Readiness/ · Packets/
│   ├── Sindri/   (Aw.Sindri.Core · Aw.Sindri.Features · Aw.Sindri.Infrastructure · Aw.Sindri.Host)
│   ├── Judge/    (Aw.Judge.Core · … · Aw.Judge.Host)
│   ├── Scorer/   (…)
│   ├── Bridge/   (…)                  # minimal API host
│   └── …
├── tests/
│   ├── .editorconfig                  # the test-project analyzer policy (§4b.6)
│   ├── Aw.Architecture.Tests/         # one assembly for every rule in §4b.1
│   ├── Aw.Kernel.Testing/             # tier 0 with Aw.Kernel, but lives here like every *.Testing (AW0002)
│   ├── Aw.Sindri.Tests/               # by feature; lanes as traits
│   ├── Aw.Sindri.Testing/             # fakes + builders + contract bases
│   └── …
├── tools/ts-indexer/                  # the pinned Node sidecar (§4a)
├── mods/                              # Claude Code mods (TypeScript, thin)
├── skills/ · config/ · providers/ · scripts/   # unchanged locations
```

The skill packages that hold code today (`skills/ui-evidence`, `skills/bugFixOrchestrator`) move their code into
`src/` as packages and keep only `SKILL.md` and scripts under `skills/`.

### 4b.8 Mapping from the ring-2 handbook's standards

| Handbook standard (TypeScript) | C# equivalent here |
|---|---|
| Four layers, allowed dependency directions, enforced by dependency-cruiser | Four projects per package, project references and ArchUnitNET (§4b.1) |
| Public API surface per package | `internal` by default and PublicApiAnalyzers |
| Internal folder template per package | Feature-slice folders (§4b.2) |
| DI only at composition roots; ports passed in | Each project's explicit `Add…()` registers its own types; only `Host` calls them and builds the container (§4b.4) |
| Error taxonomy and `AppResult` | `Result<T>` and `Error` with catalogue codes (§4b.3) |
| zod at boundaries only | Source-generated JSON and `IValidator<T>` at endpoints and CLI inputs |
| Branded ids | Vogen strongly typed ids (§4b.3) |
| ORM types never leave the database adapter | `internal` row types in `Infrastructure` |
| Keeping type-check fast | Build time per project recorded in CI; a regression over 20% fails |
| Module size and complexity budgets | `.editorconfig` limits and a ratchet baseline (§4b.6) |
| Ports for time, randomness and processes; fakes vs real services | `TimeProvider`, `IIdGenerator` and process ports; fakes with shared contract tests (§4b.5) |
| Test pyramid, budgets, builders, deterministic ids and clock, golden files, mocks policy | §4b.5 lanes, builders, `FakeTimeProvider`, golden files, no mocking libraries |
| Tenancy at the architecture level | Not applicable to this toolkit. The ring split (work vs personal) plays that role in profiles and accounts |

### 4b.9 Agent readiness of the code itself

- `AGENTS.md` lists the one-line commands, run from the repo root: `dotnet build`, `dotnet test --solution
  AgenticWorkflow.slnx` (all tests), `dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit"
  --ignore-exit-code 8` (the unit lane), `dotnet format --verify-no-changes`. Test options are Microsoft.Testing.Platform options only (no `--logger`).
  It also has the slice recipe ("to add a use case: test, request and response, service, endpoint, registration")
  and points to the `/new-package` and `/new-slice` scaffolds.
- The scaffolds produce compliant code with a red test, so an agent's first move is always the TDD step.
- The scaffolds exist (R1b, #139): `/new-package` and `/new-slice` are skills that drive
  `scripts/scaffold/new-package.mjs` and `scripts/scaffold/new-slice.mjs`, and the CI job "C# scaffold generators"
  scaffolds a small CLI package and a full http package into a temporary tree and builds and tests them.
- Rule records carry the analyzer ID that enforces each rule, and `node scripts/check-rules.mjs` enforces that every
  check id the C# rules name is in a record and exists in the repo (RULE-COVER-002, RULE-CHECK-001; R1b, #137). A
  denial or build error names the rule; `sindri rules explain <id>`, which shows the why, is not built yet.

### 4b.10 Checks of record (R1b)

The rule ids the R1b template PRs added. Each failure message names the rule id, what was found (the measured value and
the limit, where the rule has one) and the fix (§8). Where a check is still in an open PR, the PR is named; the rule record (§4b intro) says `pending` until it
merges.

| Area | Ids | Check | PR |
|---|---|---|---|
| Dependency direction; another package only through its `Core`; `Core` and `Features` use no tier-0 package but `Aw.Kernel` | ARCH-DIR-001…005 | `tests/Aw.Architecture.Tests` | #131 |
| Every type in its project's layer namespace | ARCH-PLACE-001 | same | #131 |
| No slice-to-slice references | ARCH-SLICE-001 (authoritative); AW1003 (fast analyzer check) | same; `Aw.Analyzers` | #131, #132 |
| `Infrastructure` types internal | ARCH-INFRA-001 | `tests/Aw.Architecture.Tests` | #131 |
| No namespace cycles | ARCH-CYCLE-001 | same | #131 |
| No `Common`, `Shared`, `Utils`, `Helpers` | ARCH-NAME-001 | same | #131 |
| Service-locator types only in `Host` and registration classes | ARCH-DI-001 | same | #131 |
| No technology types outside `Infrastructure`; only `Composition` uses it | ARCH-PORT-001, ARCH-PORT-002 | same | #131 |
| Typed ids (R9: defaults, user-code converter) | ARCH-ID-001, ARCH-ID-002 (`TypedIdGuard`, moved from `Aw.Kernel.Tests`) | same | #131 |
| The `Packages.Production` registry matches the solution | ARCH-REG-001, ARCH-REG-002 | same | #131 |
| Raw ids in public `Core`/`Features` signatures; `!` without `// why:`; endpoint lambda calls exactly one service; file ≤ 300 lines; parameters ≤ 5; constructor dependencies > 7 (error) and 6–7 (`suggestion`); generic arity ≤ 3 | AW1001, AW1002, AW1004, AW1005, AW1006, AW1007, AW1008, AW1009 | `Aw.Analyzers` (`CA1005` off, owned by AW1009) | #132 |
| Per-test lane cap; exactly one known lane | LANE-TEST-001, LANE-TEST-002 | `tests/Aw.LaneCap.Testing` over CTRF reports | #133 |
| Kernel port contracts | `UnitOfWorkContract`, `OutboxContract` (with `ReadRepositoryContract`) | `Aw.Kernel.Testing` | #133 |
| Redaction timeout blocks | AW-REDACT-001 | `Aw.Redaction` | #130 |
| Rule records cover every named check and each check exists | RULE-COVER-*, RULE-CHECK-* | `node scripts/check-rules.mjs` | #137 |
| Scaffold inputs and refusals; generator steps | NP-* (`/new-package`), NS-* (`/new-slice`), SCAFFOLD-* | `scripts/scaffold/*.mjs`, CI job "C# scaffold generators" | #139 |
| Every environment variable read is documented | ENV-EXAMPLE-001…004 | `scripts/check-env-example.sh` | #140 |

Still gaps after R1b: the `IConfiguration` ban for hosts (§4b.4), the tier-0 one-external-package cap (§4b.1a), the
20% build-time regression gate (§4b.8) and the complete one-`Lane`-trait scan (§4b.5).

**Sources:**
[Vertical slice vs Clean Architecture](https://milanjovanovic.tech/blog/vertical-slice-vs-clean-architecture) ·
[Testing vertical slices](https://milanjovanovic.tech/blog/testing-vertical-slices-dotnet) ·
[.NET 10 clean architecture template (minimal APIs, Result, no MediatR)](https://codewithmukesh.com/resources/clean-architecture-template/) ·
[Clean architecture in .NET 10](https://codewithmukesh.com/blog/clean-architecture-dotnet/) ·
[EF Core 10 NativeAOT status](https://www.c-sharpcorner.com/article/ef-core-10-nativeaot-whats-actually-broken-and-why2/) ·
[Dapper.AOT on .NET 10 report](https://www.c-sharpcorner.com/article/dapper-aot-is-broken-on-net-10-heres-the-proof) ·
[Data access in Native AOT](https://www.thinktecture.com/en/net/data-access-in-net-native-aot-with-sessions/) ·
[NetArchTest](https://github.com/BenMorris/NetArchTest) ·
[Architectural tests in .NET](https://mareks-082.medium.com/architectural-tests-in-net-1bd5d19b0ba8) ·
[Vogen value objects](https://docsearch.algolia.com/mcp/docs/repo/stevedunn/vogen) ·
[Directory.Build organization and Central Package Management](https://tessl.io/registry/skills/github/dotnet/skills/directory-build-organization)

## 4c. Cutover contract (clean cutover; written before any port)

No package is ported until this contract exists. Every rule here is a CI check. **Owner decision (2026-10-10): the
cutover is clean.** The C# build doesn't reproduce the TS build's bytes, doesn't read its data and isn't replayed
against it. It is proven by its own tests.

**Proof without parity (A1, A15).** Each package proves itself before the switch:
- unit and integration lanes within budget (§4b.5) and the architecture tests (§4b.1);
- CLI output and JSON contracts as golden files recorded from the **C# build** (`Aw.Kernel.Testing`), compared after a
  declared normalization (ids, times), never by loose matching;
- a fake model provider, so calls to model CLIs are deterministic in tests;
- a startup-time budget per CLI;
- the AOT CI job: every NativeAOT entry point is published and smoke-tested;
- **the real hook scripts** in `config/hooks/` run against the C# binaries in CI, because their behavior is what
  users depend on.

**Digests.** New C# digests over JSON use one canonical form (sorted keys, the Kernel profile) and carry a
`digestVersion` field. Nothing reproduces TS digest bytes (B4).

**Fail-open is visible (A1, B5).** Some hooks treat a judge error as "allow". Every hook that can fail open logs a
`HOOK-FAILOPEN` event with its cause (payload content never), the daily digest lists them, and the hook CI tests
assert that no tested path fails open against the C# binaries.

**One pattern source for the scrubber and gates (A2).**
- The tier-0 `Redaction` package owns the patterns; the TS scrubbers keep theirs until the TS build is deleted.
- Patterns use explicit ASCII classes (`[0-9]`, `[A-Za-z0-9_]`), `RegexOptions.CultureInvariant`, and never
  `NonBacktracking` where lookarounds exist.
- Every match has a timeout. A timeout **blocks** (the commit, the write or the egress) and is logged. It never passes.
  In `Aw.Redaction` (R1b, #130) a timeout returns the `Blocked` outcome with `AW-REDACT-001` and withholds the text;
  the caller logs it. The budget scales with input size (by default 1 s per pattern per 128K characters), so a large
  ordinary input isn't blocked for its size alone.
- Evasions are folded, not enumerated: the rules run on the text as written and on a folded copy (zero-width
  characters dropped, Unicode spaces and Latin look-alikes mapped to ASCII, JSON escapes decoded), and hits from both
  are mapped back to the original text.
- Deliberately narrower than `detect-secrets.sh`: a `NAME=value` secret needs a digit in its value, and judge's bare
  40+ hex rule (which erases git SHAs and digests) isn't carried over. Retiring the hook into these patterns (§4e
  B12) accepts that, or adds the hook's shapes as rules first.
- Redaction's tests carry a corpus of synthetic secret shapes, Unicode look-alikes, zero-width joiners, escaped
  newlines and long inputs.

**One cutover, reversible (A5, A18, decided 2026-10-10).**
- **During the port** the installed toolkit stays TS. The C# build lives in the same repo under `src/` and builds to
  its own output directory. Nothing in `setup.sh`, the providers, MCP registration or launchd points at it.
- **Trying C# early:** `./setup.sh --runtime cs --prefix <dev dir>` installs the C# build into a separate directory for
  a hand test or a dev session, without touching the real install.
- **The switch:** `./setup.sh --runtime cs` **moves** only the TS packages' state aside to one timestamped archive:
  the namespaces and files `deps.yaml` lists for them (`judge/`, `scorer/`, `sindri/`, `ui-evidence/`,
  `context-guard/`, `locks/` and the hook-owned files under `judge/`), plus two fixed extras the switch names itself
  because `deps.yaml` can't express them: the per-repo bug-fix run state `<repo-slug>/bugfix/` and the bridge's
  `bridge.db` (outside the state root). The C# BugFix writes to its own namespace, `bugfix/<repo-slug>/<ticket-slug>`
  (so tickets don't collide across repos), and the bugFixOrchestrator skill text changes at the switch to pass that
  path; open bug-fix runs restart at the switch. Skill and workflow state is runtime-neutral and
  stays where it is through the switch and a rollback: `<repo-slug>/` (plans, reviews, proof, verification,
  design), `digests/`, `external-skills/`, `providers/`, `toolkit/` and `logs/`. The switch then installs the C#
  binaries and rewrites hooks, MCP registration and launchd jobs to call them. The ported packages start with empty
  namespaces (Readiness and Packets keep theirs, below), so they can never see TS files; nothing is migrated. The C#
  build refuses to start if TS-schema files remain in its packages' namespaces. The check excludes `judge/sessions`,
  `judge/outbox` and `judge/teammate-names.jsonl`, which the hooks write in the frozen shape on both runtimes.
- **State written before the switch:** `Aw.Readiness` and `Aw.Packets` run before the switch as their own
  NativeAOT CLIs (`aw-readiness check`, `aw-packets validate|freeze`) and write only under their own namespaces
  (`readiness/`, `packets/`), which the TS build never touches and the switch keeps. After the switch, sindri's C#
  host exposes them as `sindri readiness …` and `sindri packets …`. When
  sindri's C# ledger first runs, it imports Readiness's history into the ledger and keeps the file.
- **Hooks during the port:** the hook-facing CLI and file surface (the `deps.yaml` Hooks, Statusline and AwLive edges)
  is frozen until the switch.
- **Rollback:** `./setup.sh --runtime ts --restore <archive>` reinstalls the TS build, moves the archived TS
  namespaces and files (including `bridge.db` and `<repo-slug>/bugfix/`) back, and discards the C# packages'
  post-switch state, including the C# bridge database (never skill and workflow state).
  The TS build stays in the repo, tagged, for 14 days after the switch, then it's deleted.
- **The switch happens on the always-on box first**, then the laptop after a day with no rollback.
- **TS stays frozen to bug fixes** once porting starts.
- **Prebuilt binaries (B6).** Release builds per OS and architecture carry build-provenance attestations. A committed
  `binaries.lock` maps each tool and platform to its expected SHA-256 (and the bundled SQLite library shipped next to
  a tool), so the check doesn't trust a checksum
  published in the same release. `setup.sh` installs the binaries the checkout's `binaries.lock` names and verifies
  them, and contributors build from source by default (`--prebuilt` opts in). Release tags are protected by a tag
  ruleset (only the owner creates `v*` tags), releases are immutable, and sindri's `stable` and `next` channels map to
  tags.
- The evolve registry's module paths move with a test that fails if any registered suite disappears (`registry.ts`
  enumerates paths today).

**Packets for ports (A6, B2).** `Aw.Packets` (schema, `validate`, `freeze`) is a standalone C# package built at the end
of R1b, before any port, so every port runs as a packet. It doesn't depend on sindri, so it runs while sindri is
still TS.

## 4d. CI, identity and supply chain (public repo)

**Agent identity (A9).**
- Agents push and open PRs as the **GitHub App** `sindri-agentic-workflow`, never as the owner. Its permissions are
  contents, pull requests and issues write, checks **read**, metadata read; no workflows, administration or secrets.
- **Honest gap until containers (B7):** until agent sessions run in the main spec's containers (rollout R4), they run
  as the owner's OS user, so the owner's `gh` login and keychain are reachable in principle. Until then: sessions get a
  scrubbed environment (`GH_TOKEN` set to a short-lived App token, `GH_CONFIG_DIR` pointed at an App-only profile),
  and a hook denies `gh auth`, `security find-*` and reads of the owner's `gh` config.
- The `main` ruleset requires each required check **from the GitHub Actions source**, so a check run posted by any
  other app can't satisfy it.
- `main` is protected by a ruleset with an **empty bypass list**: required checks, linear history, no force pushes.
  There's no bypass label.
- The owner merges as themself. CODEOWNERS covers the protected modules and is backed by a required check (a solo
  owner can't approve their own PR, so a review requirement would block every merge).

**Workflows (A10).**
- `permissions: contents: read` at the workflow level (not `read-all`, which also grants read on issues, checks,
  security events and more), with write scopes granted per job only where needed.
- `pull_request_target` and `workflow_run` with untrusted checkouts are banned. Forks get no secrets.
- Every action is pinned to a full commit SHA. `zizmor` lints the workflows.
- Model or API keys live in a **`main`-only environment** with the owner as required reviewer (B8), used only by
  scheduled or manually dispatched workflows on `main`. They are never repository secrets that any pushed branch
  could reach.
- Required checks always report a status: a path-filtered job reports a no-op success instead of skipping, so a
  skipped job can't block or silently pass a merge (A11).
- Dependabot (or Renovate) with grouped updates and a 14-day cooldown, matching the existing pin policy.

**NuGet and Node (A16).**
- `nuget.config` with package source mapping (nuget.org only), signed-package validation, and lockfiles in locked
  mode in CI. Restore applies the trusted-signer policy only when signature verification is on (off by default on
  macOS; `DOTNET_NUGET_SIGNATURE_VERIFICATION=true` turns it on) and only to packages it extracts, so a warm cache
  is never re-checked. The enforcement step is `scripts/dotnet-verify-packages.sh` in CI: lock files present and
  unchanged, a signature-verified locked restore, and `dotnet nuget verify --all` over every locked package. One
  carve-out: a package whose only failures are an expired signature (NU3037) or a timestamp from a since-distrusted
  authority (NU3028), which restore accepts, passes with a warning; any other failure fails. Analyzer packages run
  code in every build, so they get the same review as runtime dependencies.
- The TS-indexer sidecar installs with `--ignore-scripts`, runs with no network and no secrets, and talks over
  stdin and stdout only (a versioned JSON protocol with its own contract tests).

**Runtime hardening (A16).**
- The bridge binds to loopback **in code**, and nothing outside the code can change it:
  - no command-line args or environment variables reach the web host's configuration (an http host clears the
    configuration sources: `CreateSlimBuilder` still reads `ASPNETCORE_*`, `DOTNET_*` and unprefixed environment
    variables), `PreferHostingUrls` is pinned off, and Kestrel listens with `Listen(IPAddress.Loopback, port)` only;
  - the http host's JSON options are the `AwJson` profile with the resolver chain replaced, plus `AddProblemDetails()`
    so error results serialize;
  - a test starts the bridge with each override vector (`ASPNETCORE_URLS`, `DOTNET_URLS`, `ASPNETCORE_HTTP_PORTS`,
    `ASPNETCORE_PREFERHOSTINGURLS`, `ASPNETCORE_Kestrel__Endpoints__*` and the unprefixed
    `Kestrel__Endpoints__*` in the environment and on the command line, `--urls`) and
    checks the only listener is `127.0.0.1:<port>` (the R1a spike found two of them binding all interfaces in a
    default `CreateSlimBuilder(args)` host);
  - requests with a non-loopback `Host` get 400 (B12, DNS rebinding) and a browser `Origin` that isn't loopback gets
    403 (the MCP Streamable HTTP rule);
  - not carried over from the TS bridge: the `HOST` variable, the `ALLOW_REMOTE` escape hatch and wildcard CORS
    (`@fastify/cors` with `origin: true` today).
- The scrubber runs as a processor on logs and OpenTelemetry exports, so new logging can't carry secrets out
  (`Aw.Kernel.Telemetry`, R1b, #136). Per path: log text and fields are scrubbed and secret-named fields withheld
  whole; with a trace exporter, span display names, tag values and status descriptions are rewritten in place, and a
  span with a secret in what can't be rewritten (operation name, trace state, tag keys, events, links) is dropped; a
  metric instrument whose tags carry a secret is withheld from every export. A blocked scrub never passes raw text,
  a scrub failure is never thrown into the caller, and what was withheld is counted on
  `aw.telemetry.scrub.withheld`. Exporters are host-supplied adapters and off by default.
- The devcontainer is for humans, and it is never the agent sandbox. Docker-in-Docker means root on the host, so
  agent sessions keep using the Sindri containers of the main spec §8.2.

## 4e. Round-2 resolutions (B10–B13)

- **Tiers (B10):** `Redaction` is domain-free and moves to tier 0 next to `Aw.Kernel`, so judge and sindri both depend
  on it downward. Scorer reading Evidence's run files is declared as a `file` edge. The new outbox table is named
  `outbox_commands` to avoid `judge/outbox`. Sindri's fan-out exceeds the limit and carries a recorded exception
  until its modules split.
- **Readiness target (B11):** the target level is per ring in `readiness.yaml` (ring 0: Level 4 before F0, Level 5
  with F3). Exit code 0 means every repo in scope meets its ring's target.
- **Overrides (B11):** requested by PR only. `overrides.yaml` is CODEOWNERS-protected and an approval-tier file, so an
  agent PR can't grant itself one.
- **Weekly items (B11):** deduplicated by criterion id and closed automatically when the criterion passes.
- **Quarantine (B11):** a test enters on 2 flaky failures in 7 days and leaves after 20 green runs in a row.
- **Hook and dispatcher safety (B12):** the outbox dispatcher re-runs the gate checks on each command at dispatch
  time. `actor` is set by the host process, never by the caller. `detect-secrets.sh` is retired into the
  `Redaction` package's patterns. The bridge checks the `Host` header (DNS rebinding).
- **Text fixes (B13):** stale `§4` references point to §4b; the colour rule reads "colour is never the only carrier of
  meaning"; test-lane filters use xUnit v3 trait syntax; a doc-migration checklist (README, ARCHITECTURE, TESTING,
  CODE_STYLE, ERD, AGENTS.md, rules) and a CI job graph are R1b deliverables.

## 5. Criteria plan by level

Each row is a criterion family, the change that satisfies it, and its owner workstream (§6).

| Level | Criterion family | Change | WS |
|---|---|---|---|
| 1 | README, linter, type checker, unit tests | .NET analyzers and `dotnet format` (§4b.6); one-line root commands | W2 |
| 2 | AGENTS.md, devcontainer, pre-commit hooks, branch protection | Devcontainer; pre-commit (format, analyzers, scrub, shape signals); the `main` ruleset (§4d) | W2, W5, W4 |
| 2 | Pinned dependencies, documented build | `global.json`, Central Package Management, locked restore (§4b.6) | W1 |
| 3 | Integration tests, tests runnable locally | Lanes, one root `test` command | W3 |
| 3 | Secret scanning, CODEOWNERS | GitHub secret scanning + push protection, a scrub job in CI, CODEOWNERS backed by a check (§4d) | W4 |
| 3 | Structured logging, tracing, metrics | One logger module (JSON lines, levels, ids; `Aw.Kernel.Telemetry`); OTel spans and metrics in bridge and sindri (exporter off by default; the OTLP adapter and a local collector, run on the host, arrive with the first exporting host) | W7 |
| 3 | Issue and PR templates, labels | Templates; label scheme (`type:`, `area:`, `size:`, `ring:`) in `.github/labels.yml`, synced to the repo by the owner | W6 |
| 4 | Fast CI feedback | Required CI under 5 minutes for lint, types and unit tests; affected-package detection; heavy lanes nightly | W3 |
| 4 | Flaky-test detection | The quarantine job (§4e B11, §8) | W3 |
| 4 | Deployment frequency | "Release" here means promoting a stable channel (§7.7 of the main spec). Track promotion frequency and lead time | W8 |
| 4 | Documentation freshness | A check that executes every command in `AGENTS.md` and fails on drift; link checks; doc age against code changes | W8 |
| 4 | Product analytics and experiments | Report scorer and ledger telemetry as product analytics; evolve's A/B and the judge's shadow modes as experiment infrastructure, with a page that lists them | W9 |
| 5 | Self-improving systems | Sindri evolve (Plan 5), plus the readiness module proposing its own fixes (§8) | Addendum |
| 5 | Complex requirements decomposed into parallel execution | Packets, workflow generation and stacked epics (addendum F0–F3) | Addendum |

## 6. Workstreams (the refactoring basis)

| WS | Scope | Notes |
|---|---|---|
| W1 Workspace | The .NET solution, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`; root commands | §4b.6, §4d |
| W2 Style | Analyzers with one owner per rule, `.editorconfig`, the ratchet baseline, shellcheck and shfmt (§4b.6) | Applies to each package as it's ported |
| W3 Tests and CI | GitHub Actions: lint, types, unit and integration per affected package, bash tests, `sync-rules --check`, `setup.sh --dry-run`, `claude plugin validate`; nightly heavy lane; quarantine job; time budgets | The `AGENTS.md` merge gate becomes required checks |
| W4 Security | Branch protection with required checks and linear history; CODEOWNERS; secret scanning and push protection; Dependabot (or Renovate) with grouped updates; scrub job in CI | Single maintainer: CODEOWNERS is for the protected modules (§7.7) |
| W5 Dev environment | Devcontainer for humans (.NET SDK, Node, uv; no Docker-in-Docker, §4d: Serena, the sindri containers and the optional collector run on the host; Ollama optional); `.env.example` covering every env var the packages read, enforced by `scripts/check-env-example.sh` (ENV-EXAMPLE-001…004) with `scripts/env-scan-ignore.txt` for OS and host variables | |
| W6 Task discovery | Issue templates (bug, feature, skill change, idea card), PR template with the evidence section, label sync | Matches the addendum's work-item kinds |
| W7 Observability | One shared logging module; OTel in bridge and sindri; `doctor` reports exporter state (`TelemetryRuntime.IsExporting`). The OTLP adapter must set `service.name` on the resource explicitly | |
| W8 Docs and delivery | `AGENTS.md` command executor; freshness check; promotion metrics | |
| W9 Product signals | A page that maps existing telemetry and experiment machinery onto the pillar | Mostly documentation of what exists |
| W10 Code shape | The §4b shape applied in each port; `deps.yaml` edges and tiers; merging duplicate logic (redaction, labeler, locks, `Result`, error codes, CLI output) found by the code index's clone layer | Happens inside the ports, not as a separate refactor of the TS |

**Order:** see §10. W3 (CI) and W4 (security) come first, because they make every later change checkable.

## 7. Turning it on

Every workstream ends with a "Turn it on" task, as Sindri plans do: the check becomes required on `main`, and the
next PR shows it passing.

## 8. Readiness as a Sindri module

- `sindri readiness check [--json]` (before the switch, the standalone NativeAOT `aw-readiness check [--json]`, §4c)
  runs our reproducible criteria, per repo and per app. It is built in **R1b as
  one of the first two packages scaffolded from the template** (§10), so the template proves itself and the Level-4
  gate is measurable from the start.
- **Where results live:** until sindri is ported, `Aw.Readiness` keeps its own history under its own `deps.yaml`
  namespace (`readiness/`), for the delta and the first-run baseline; the switch keeps it (§4c). When sindri's C#
  ledger first runs, it imports that history and keeps the file; results are recorded in the ledger from then on.
- **Usage metrics for R3** (promotion frequency, flake rates, doc freshness over time) come from external sources
  where possible (git tags, the GitHub Actions API). Anything else is measured from the switch.
- **Output contract:**
  - each criterion has a state: `pass`, `fail`, `n/a` (with a reason) or `unknown` (we can't reproduce Factory's
    check);
  - the summary shows the level per pillar and overall, and the delta since the last run;
  - exit codes: 0 when the target level holds, 1 when it doesn't, 2 on a tool error;
  - a first run with no baseline records one and says so;
  - `--json` follows a versioned schema.
- **Every CI failure message** names the rule id, the measured value, the limit and the fix command. That covers lane
  budgets, ratchets, `deps.yaml` drift, fan-out and analyzer rules. Text is never the only carrier of meaning:
  colour is never the only carrier of meaning, and `NO_COLOR` is respected (A19).
- **Overrides are records, not labels:** an override names the rule, the reason, the approver and an expiry date,
  and lives in `overrides.yaml`. An expired override fails CI. Quarantined tests leave quarantine after 20 green
  runs in a row (A17).
- **Dependency graph in PRs:** CI renders the graph and adds a plain-text list of added and removed edges (A19).

- A weekly standing workflow at the `propose` tier turns the top failing criteria into work items, so the factory
  keeps the repo ready as it changes.
- Ring-1 repos get the same check through `sindri repo onboard` (record-only first).
- The criteria list is a registered artifact. Scouting (§17.18) watches Factory's published criteria for changes.

## 9. Decisions needed

| # | Decision | Recommendation |
|---|---|---|
| R1 | Lint and format tooling | Settled by R6: .NET analyzers with one owner per rule (§4b.6); Biome only for the TS that stays |
| R2 | One lockfile vs per-package | Settled by R6: Central Package Management with locked restore |
| R3 | Branch protection for a solo maintainer | A ruleset with an empty bypass list and a separate agent identity (§4d) |
| R4 | Score of record | **Decided:** our own check (`Aw.Readiness`). No paid Factory account or Droid |
| R5 | Tighter TS flags | Superseded by R6 for the rewritten packages; applies only to the TS that stays (mods, the TS-indexer sidecar) |
| R6 | Language and runtime | **Decided:** C# on .NET 10 LTS, NativeAOT CLIs, strangler rewrite per package (§4a) |
| R7 | Test framework | **Decided (R1a spike):** xUnit v3 on Microsoft.Testing.Platform, lanes as traits, budgets per §4b.5 |
| R8 | Data access under NativeAOT | **Decided (R1a spike):** hand-written ADO.NET with `IRowMapper<T>` over the bundled SQLite shipped beside each tool (§4b.2); EF Core 10 and Dapper.AOT fail warnings-as-errors |
| R9 | Strongly typed ids | **Decided (R1a spike):** Vogen with its STJ generation off, a user-code converter per id and `IParsable<T>` declared in user code (§4b.3) |
| R10 | Agent identity | A GitHub App installed on the repo (preferred) or a fine-grained token, with contents and pull-request write only |
| R11 | AW1008 at `suggestion` | **Open (owner).** The 6–7 constructor-dependency split signal is reported at `suggestion`. Keep it as a recorded exception to "never suggestion", or raise it to `warning` |
| R12 | Rule record shape | **Open (owner).** R1b records carry a `checks` list (one record per rule, several checks each) plus `section`, `gap`, `gapNote`, `relatedIds` (addendum §17.6). Keep the list form, or one record per check |
| R13 | How hosts are kept off `IConfiguration` | **Open (owner).** An architecture rule over host assemblies is proposed; `BannedSymbols.txt` doesn't apply to hosts |

## 10. Rollout and sizing (rough, one builder with agents)

Repo size today: about 24.6k lines of TypeScript source and 31.2k lines of tests, of which sindri is about half.
Recent pace: about 49 commits in 60 days. The sizes below are estimates to be replaced by each packet's own.

| Step | Contents | Size | Exit |
|---|---|---|---|
| R0 | Baseline: a quick manual pass over the nine pillars, recorded per criterion | XS | Baseline recorded |
| R1a | **Safety and measurement:**<br>• CI hardened for a public repo, the agent App, the `main` and tag rulesets, the `main`-only key environment (§4d);<br>• fail-open logging in every hook (§4c, B5);<br>• the AOT spike (R7, R8, R9, the MCP C# SDK);<br>• the readiness baseline by a throwaway script (B2) | M (1–2 weeks) | Fail-open logging live; baseline recorded; agents can't bypass `main` |
| R1b | **The template:**<br>• `Directory.Build.props`, Central Package Management, analyzers with one owner per rule, the ratchet baseline, `.editorconfig`;<br>• `Aw.Kernel` and its `Data`, `Telemetry` and `Testing` parts;<br>• `deps.yaml` with every edge kind and its checks (§4b.1a);<br>• the `/new-package` and `/new-slice` scaffolds (inputs, generated files, a red test) and `AGENTS.md` for C#;<br>• rule records for the §4b rules;<br>• W5 devcontainer and W6 templates;<br>• **`Aw.Readiness` and `Aw.Packets` as the first two standalone packages built from the template** (B2) | M (1–2 weeks) | A scaffolded package passes every check; `Aw.Readiness` reports Level 4 for the template; the R2 estimate is fixed here as the stop-rule baseline (B9) |
| R2 | **The ports, one packet per package:** `Redaction` → `Judge` and `Bridge` → `Scorer`, `Evidence` and `BugFix` → `Sindri` (by module inside its packet). Then **the switch**: `./setup.sh --runtime cs` on the always-on box, then the laptop. **In parallel,** new factory code (addendum F0 schemas, rule records) is written in C# from the start | L–XL (estimate 6–10 weeks; each packet carries its own) | All packages done (§4a step 3); the switch done on both hosts; TS deleted 14 days later |
| R3 | Remaining Level 4 criteria that need usage data (promotion frequency, flake rates, doc freshness over time), and W9 | S–M | Level 4 passed. **Factory work (F0) starts here** |
| R4 | Addendum F0–F3 plus the readiness module's weekly workflow | With the addendum | Level 5 passed |

**Stop rules:**
- A package that overruns its packet's estimate by 2× is reviewed with the human before more work goes in.
- **R2 as a whole** overrunning 1.5× its summed estimate triggers a re-plan with the human: reorder, cut scope, or
  run more packets in parallel. The port itself is decided and isn't reopened.
- CI cost while both builds exist: TS jobs run only on PRs that touch TS (bug fixes), and the C# jobs run on everything
  else.

**Dependencies, not a strict sequence.** R1a, R1b and R2 overlap:
- R1b's build settings, analyzers, `deps.yaml`, the `Aw.Kernel` core, scaffold skeletons and C# `AGENTS.md` need
  nothing from R1a and can start at once.
- `Aw.Kernel.Data`, `Aw.Kernel.Testing` and typed ids waited for the AOT spike's answers (R7–R9); those are now
  decided (`docs/readiness/aot-spike.md`).
- A package's port starts when enough of the template exists to scaffold it. CI (R1a) runs its tests.
- The readiness baseline and the agent token helper block nothing.
