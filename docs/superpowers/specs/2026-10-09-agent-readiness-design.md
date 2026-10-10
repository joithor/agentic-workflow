# Agent readiness for agentic-workflow — design

Status: draft for review, revision 2 · 2026-10-09 (applies /autoplan findings A1–A19; the C# port itself is decided and was not under review)
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
- **Gap:** Factory doesn't publish the full criteria list. Factory's own report is the score of record when we can
  run it. Our own check (§8) tracks the criteria we can reproduce, and it marks every criterion where our check and
  Factory's might disagree.

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
gets CI and golden contract tests as a safety net. The standards in §4 still apply, expressed in .NET tooling.

**What moves, and to what:**

| Today (TS) | C# / .NET |
|---|---|
| `tsc` strict | Nullable reference types enabled, `TreatWarningsAsErrors`, analyzer levels per §4b.6 |
| Biome / typescript-eslint (planned) | Roslyn analyzers (built-in + `Microsoft.CodeAnalysis.NetAnalyzers`), `.editorconfig` severities, `dotnet format` in pre-commit and CI |
| dependency-cruiser (planned) | One project per layer (§4b.1) with project references as the boundary, plus ArchUnitNET architecture tests and `deps.yaml` (§4b.1a) |
| Zod schemas, JSON Schema export | Records with `System.Text.Json` source generation; validators per schema; JSON Schema generated from the types for editor autocomplete |
| Vitest | xUnit (or TUnit), with the lanes and time budgets in §4 |
| better-sqlite3 | `Microsoft.Data.Sqlite`, same files and schemas, WAL mode |
| Fastify (bridge) | ASP.NET Core minimal APIs |
| `@modelcontextprotocol/sdk` | The official MCP C# SDK |
| Node CLIs started per hook call | **NativeAOT** single-file binaries for hook and CLI entry points only (§4b.6 AOT table), so startup stays fast. Reflection-free JSON (source generators) is required for them |
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
1. **Freeze the contracts first.** Each package's external surface is captured as golden tests: CLI arguments,
   output and exit codes, JSON files, the ledger and SQLite schemas, MCP tool schemas and HTTP routes. They run
   against the TS and the C# builds alike.
2. **One package at a time,** smallest and least shared first: `judge` → `scorer` → `skills/bugFixOrchestrator`
   state CLI → `mcp-bridge` → `skills/ui-evidence` → `sindri` (by module: profile and ledger, scrub, observe,
   index, scope, evolve).
3. **Cut over per package** when its golden tests pass on C#, its own new unit tests meet the lanes, and an
   install on both macOS and Linux works. The TS package is deleted in the same PR series, never kept alongside
   it for long.
4. **Shared data stays compatible.** The ledger, index and state files keep their schemas and versions, so a C#
   `sindri` reads what the TS one wrote, and the reverse during cutover.
5. **Each package rewrite is a packet** (addendum §17.4), built with today's skills on the host. That makes the
   rewrite the first real test of the packet format and the thin end-to-end slice (F0.5), before the factory
   exists.

**Risks to watch:**
- NativeAOT limits (reflection, some libraries). Every CLI builds AOT in CI from day one.
- The Plan 5 evolve suites and judge calibration data have TS-specific fixtures. They're ported as golden data,
  and their pass rates must not drop at cutover.
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
| `Aw.<Pkg>.Core` | Domain types, strongly typed ids and value objects, pure logic, **port interfaces** (repositories, clock, process runner), errors | `Aw.Kernel` only |
| `Aw.<Pkg>.Features` | One folder per use case (a slice): request and response records, the service (handler), the validator, and the endpoint or CLI mapping | `Core`, `Aw.Kernel` |
| `Aw.<Pkg>.Infrastructure` | Repository implementations over SQL, external adapters (git, containers, model CLIs, HTTP), migrations | `Core`, `Aw.Kernel` |
| `Aw.<Pkg>.Host` | The composition root: the minimal API app or the CLI `Main`, DI registration, configuration, the JSON context | everything in the package |
| `Aw.Kernel` (shared) | `Result<T>`, `Error`, clock and id ports, value-object conventions, the generic `IService<TReq, TRes>` pipeline and decorators, the byte-compat JSON settings (§4c) | the BCL only |
| `Aw.Kernel.Data` · `Aw.Kernel.Telemetry` · `Aw.Kernel.Testing` | SQLite access and migrations runner · logging and OpenTelemetry setup with the scrubbing processor · fakes base, builders, contract-test bases, golden harness | `Aw.Kernel` plus their one external dependency each |

**Small packages:** a package under about 1,500 lines may use two projects, `Core` and `App` (features,
infrastructure and host together), with the same dependency direction. It splits into four when it crosses the
threshold.

**Rules** (enforced by project references, which fail the build, and ArchUnitNET tests):
- `Core` has no I/O. It doesn't reference `System.IO`, `System.Net`, `Microsoft.Data.*`, `System.Diagnostics.Process`,
  `Console`, `DateTime.Now`/`UtcNow` or `Guid.NewGuid`. They're banned through `BannedSymbols.txt` (the
  BannedApiAnalyzers package), and time and ids come from injected ports (`TimeProvider`, `IIdGenerator`).
- One slice never references another slice's types. Shared behavior moves into `Core` as a domain service or a port.
  A second use of the same logic is the trigger to move it (the "generalize at the second case" signal, §6.2).
- `Infrastructure` row and DTO types are `internal`. They never leave the project, and repositories return `Core` types.
- Packages reference other packages only through their `Core` project's public surface, or through a CLI or HTTP
  contract. Their `Infrastructure` and `Features` are never referenced.

### 4b.1a No cycles, no spider web

The dependency graph is a **declared, layered, acyclic tree** at every level, and CI fails on any drift from the
declaration.

**Package tiers** (a package may depend only on packages in a lower tier, never on its own tier):

| Tier | Packages | May depend on |
|---|---|---|
| 0 | `Aw.Kernel` and its `Data`, `Telemetry`, `Testing` parts | the BCL plus one external package each |
| 1 | `Judge`, `Bridge`, `Redaction` (the scrubbing patterns now copied between judge and sindri) | Tier 0 |
| 2 | `Scorer`, `Evidence` (ui-evidence), `BugFix` | Tiers 0–1 (today they call judge, and scorer reads `judge.db`) |
| 3 | `Sindri` | Tiers 0–2, **only through their `Core` contracts or their CLIs** |
| — | Every `Host` | Its own package's projects, plus the tier-0 and lower-tier `Core`s it composes |

- **Declared graph, every edge kind:** `deps.yaml` (with a JSON Schema) lists each package's tier and every allowed
  edge, with its kind: `project` (a reference), `cli` (one package runs another's binary), `db` (one package opens
  another's SQLite file) or `file` (shared state files). Project edges are checked against the real references. CLI,
  database and file edges are checked by a scan of process launches, connection strings and known state paths. Any
  undeclared edge fails CI. A `db` edge is read-only and should become a CLI or a published view over time (today:
  scorer reads `judge.db`). Adding an edge is
  a reviewed change to `deps.yaml` (and an approval-tier change once it is a rule record).
- **Prefer contracts over references:** a package calls a same-tier or higher-tier package only through its CLI or
  HTTP contract, never through a project reference. Copied logic is a smell: the redaction patterns copied between
  judge and sindri move into the tier-1 `Redaction` package, and scorer's labeler, which sindri copies, moves into
  scorer's `Core` for sindri to call through scorer's CLI.
- **Inside a package**, the four-project shape (§4b.1) is itself a fixed DAG. **Inside a project,** feature
  folders and namespaces must not form cycles. ArchUnitNET's slice-cycle rule checks every namespace in every
  project (MSBuild already refuses circular *project* references; this covers the namespaces inside one).
- **Fan-out limits** (every `Host` is exempt, because composing is its job): a project references at most 4 other in-repo projects and at most 6 third-party packages
  (analyzers and test packages excluded). Going over is a ratchet violation that needs a recorded reason.
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
  - Why not EF Core: its NativeAOT support is officially experimental in EF Core 10.
  - Why not Dapper.AOT: reports on .NET 10 conflict.
  - R1 includes a one-day spike that re-checks both. If either passes the AOT golden tests, it may replace the
    hand-written mappers (decision R8).

### 4b.3 Types, generics and errors

- **Strongly typed ids and value objects** for every id and every constrained primitive (`ItemId`, `RepoSlug`,
  `SizeClass`, `TokenBudget`). They're generated with Vogen (System.Text.Json and ADO.NET conversions, with
  analyzers that ban `default` instances), or written by hand as `readonly record struct` if the R1 spike finds an
  AOT problem. A raw `string` or `Guid` in a public signature of `Core` or `Features` is an analyzer error.
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
- **JSON:** System.Text.Json with one source-generated `JsonSerializerContext` per package. No reflection-based
  serialization (it's AOT- and trim-analyzer enforced).

### 4b.4 Dependency injection

- `Microsoft.Extensions.DependencyInjection`. Registration lives **only** in `Host`. Each project exposes one explicit
  extension method (`services.AddSindriCore()`, `AddSindriInfrastructure(config)`). There's no assembly scanning,
  because it's reflection and not AOT-safe.
- Constructor injection only: no service locator, and no `IServiceProvider` outside `Host`.
- Lifetimes: services and repositories are scoped per request or command, pure domain services are singletons, and
  `TimeProvider` is a singleton (a `FakeTimeProvider` in tests).
- A DI validation test builds the container with `ValidateOnBuild` and `ValidateScopes` for every host.

### 4b.5 Test-driven development without spaghetti

- **Red first.** Every slice starts with a failing test that names the behavior (`StartItem_parks_when_budget_is_exhausted`).
  The commit history shows the red test before the code (the existing `/specToProvenPR` red-commit rule).
- **Tests by feature, not by layer:** `StartItemTests`, not `ItemRepositoryTests` plus `ItemServiceTests`.
- **Lanes:**
  - `unit`: `Core` logic and services with in-memory fakes. No I/O, under 10 s per package.
  - `integration`: repositories on a real temp SQLite file; endpoints through `WebApplicationFactory`. Under 60 s.
  - `heavy`: real containers, browsers and index builds. Nightly or alone.

  Lanes are test traits. CI fails a test that runs over its lane's time.
- **Fakes, not mocks.** Each port has one in-memory fake in a shared `Aw.<Pkg>.Testing` project. A generic contract-test
  base class (`ReadRepositoryContract<TEntity, TId>`, `ItemRepositoryContract`) runs the **same tests against the fake
  and the SQL implementation**, so the fake can't drift. Mocking libraries are not used.
- **Test data:** builders with sensible defaults (`AnItem().WithSize(S).Build()`), deterministic ids and
  `FakeTimeProvider`, and no shared mutable fixtures. Golden files are used for CLI output and JSON contracts.
- **Architecture tests** (ArchUnitNET, which is maintained; NetArchTest's last release was in 2023) run in the unit
  lane. They cover the dependency directions, no slice-to-slice references, the `internal` row types and the naming
  rules.
- **Framework:** xUnit v3 by default, or TUnit if it holds the lane budgets better under AOT (decision R7, settled
  in the R1 spike).

### 4b.6 Build settings and analyzers (repo root)

- `global.json` pins the .NET SDK.
- `Directory.Build.props`:
  - language and nullable: `Nullable=enable`, `LangVersion=latest`, `ImplicitUsings=enable`;
  - warnings: `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, `AnalysisLevel=latest-recommended`
    (raised to `latest-all` per category where the noise is acceptable, decided during R1);
  - `IsAotCompatible=true` on `Core`, `Features`, `Infrastructure` and Kernel projects (it turns on the trim and AOT
    analyzers). `PublishAot=true` only on the entry points in the table below;
  - `GenerateDocumentationFile=true` for public API;
  - `RestorePackagesWithLockFile=true`, plus `RestoreLockedMode` in CI.
- `Directory.Packages.props`: Central Package Management with exact versions. Analyzers are added as
  `GlobalPackageReference`: `Microsoft.CodeAnalysis.NetAnalyzers`, `Meziantou.Analyzer`, `Roslynator.Analyzers`,
  `Microsoft.CodeAnalysis.BannedApiAnalyzers`, `Microsoft.CodeAnalysis.PublicApiAnalyzers`. New packages follow
  the same 14-day minimum age as the existing pins.
- **One owner per rule (A14):** where analyzer packs overlap (for example CA2007 and MA0004 on `ConfigureAwait`),
  `.editorconfig` names one owning rule and disables the others. Existing violations are captured in a generated
  `baseline.globalconfig` that lowers only those rule-and-file pairs; the ratchet fails CI if the baseline grows.
- `.editorconfig` sets every rule's severity explicitly (no "suggestion" level for anything agents should obey).
  The size limits: file ≤ 300 lines, method ≤ 40 lines, cyclomatic complexity ≤ 10, parameters ≤ 5. Existing
  violations go into a ratchet baseline that only shrinks.
- `Aw.Analyzers`: a small in-repo analyzer project for the rules no package covers:
  - no raw ids in public signatures;
  - `!` needs a reason;
  - no cross-slice references (fast feedback; the architecture test is authoritative);
  - endpoint lambdas call exactly one service.

**AOT scope** (verified in the R1a spike; anything that fails ships framework-dependent with a startup budget):

| Entry point | Build | Why |
|---|---|---|
| Hook binaries, `judge`, `sindri`, `scorer`, `bugfix-state` CLIs | NativeAOT | Run per hook call or per command; startup matters |
| `Bridge` host (MCP + HTTP) | NativeAOT if the MCP C# SDK passes the spike, else framework-dependent | Long-running; startup matters less |
| `Evidence` (ui-evidence) | Framework-dependent | Playwright for .NET doesn't support NativeAOT |

AOT publishing runs only in a separate CI job and in releases. Local builds and tests are JIT, so the inner loop
stays fast.

**Seams for distribution** (factory addendum §17.20): the C# ledger sits behind a `LedgerStore` port with a
shared contract-test suite, every event is idempotent, `Lease`, `Launcher` and `Scheduler` are ports, and every row
carries a tenant and ring key. These cost little now and avoid a second rewrite later.

### 4b.7 Repository tree

```
agentic-workflow/
├── global.json · Directory.Build.props · Directory.Packages.props · .editorconfig · BannedSymbols.txt
├── AgenticWorkflow.slnx
├── src/
│   ├── Aw.Kernel/                     # Result, Error, IService<,>, decorators, contracts
│   ├── Aw.Analyzers/                  # repo-specific Roslyn rules
│   ├── Sindri/   (Sindri.Core · Sindri.Features · Sindri.Infrastructure · Sindri.Host)
│   ├── Judge/    (same four projects)
│   ├── Scorer/   (…)
│   └── Bridge/   (…)                  # minimal API host
├── tests/
│   ├── Aw.Architecture.Tests/         # one assembly for every rule in §4b.1
│   ├── Sindri.Tests/                  # by feature; lanes as traits
│   ├── Sindri.Testing/                # fakes + builders + contract bases
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
| DI only at composition roots; ports passed in | DI only in `Host`; explicit `Add…()` methods (§4b.4) |
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

- `AGENTS.md` lists the one-line commands: `dotnet build`, `dotnet test --filter Lane=unit`, `dotnet format --verify-no-changes`.
  It also has the slice recipe ("to add a use case: test, request and response, service, endpoint, registration")
  and points to the `/new-package` and `/new-slice` scaffolds.
- The scaffolds produce compliant code with a red test, so an agent's first move is always the TDD step.
- Rule records carry the analyzer ID that enforces each rule. A denial or build error names the rule, and
  `sindri rules explain <id>` shows the why.

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

## 4c. Cutover contract (written before any port)

No package is ported until this contract and its harness exist. Every rule here is a CI check.

**Byte compatibility (A1).** `Aw.Kernel` defines one `JsonSerializerOptions` profile that matches what the TS
packages write today:
- camelCase names, JS-compatible escaping (no `\u0027` where Node writes `'`), the same null and
  number handling, and `toISOString()`-identical timestamps (UTC, millisecond precision, `Z`);
- a canonical JSON writer (sorted keys, no whitespace) for anything hashed, such as judge's `input_digest`;
- a compat test suite that feeds recorded TS outputs through the C# reader and writer and requires identical bytes.

**Golden harness (A1, A15).** Each package's contract is recorded from the TS build and replayed against the C# build:
- CLI arguments, environment variables, stdin, stdout, stderr and exit codes;
- JSON and state files, ledger and SQLite schemas and rows, MCP tool schemas and HTTP routes;
- a fake model provider, so calls to model CLIs are recorded and replayed;
- a startup-time budget per CLI;
- **the real hook scripts** in `config/hooks/` run against the C# binary, because their behavior is what users
  depend on.

Outputs are compared after a declared normalization (ids, times), never by loose matching.

**Fail-closed during cutover (A1).** Some hooks treat a judge error as "allow". A cutover run counts every
fail-open event. A package cuts over only with **zero** fail-open events across the golden and replay corpus, and
hooks that call a ported binary log a `HOOK-FAILOPEN` event that the daily digest lists.

**Regex parity for the scrubber and gates (A2).**
- One shared pattern file is the source for both builds while both exist.
- Patterns use explicit ASCII classes (`[0-9]`, `[A-Za-z0-9_]`), `RegexOptions.CultureInvariant`, and never
  `NonBacktracking` where lookarounds exist.
- Every match has a timeout. A timeout **blocks** (the commit, the write or the egress) and is logged. It never passes.
- A differential corpus (real secrets shapes, Unicode look-alikes, long inputs) runs both builds in CI and fails on
  any difference, until the TS scrubber is deleted.

**Coexistence and rollback (A5, A18).**
- Each package is ported **whole**, not module by module, behind one entry-point shim: `bin/<tool>` runs the C# or
  TS build according to `runtime.<package>: ts | cs` in the profile. Rollback is one profile change.
- During a package's cutover its ledger and state schemas are frozen. Neither build migrates them.
- The TS package is frozen (bug fixes only) once its port starts, and deleted when the C# build has run as default
  for 14 days with no rollback.
- `setup.sh`, the provider installers, MCP registration (`node dist/mcp.js` today) and the launchd jobs call the
  shim, never a runtime directly.
- CI publishes prebuilt binaries per OS and architecture (macOS arm64, Linux x64 and arm64) as release assets.
  `setup.sh` downloads and verifies them by checksum, or builds from source with `--from-source`.
- The evolve registry's module paths move with a test that fails if any registered suite disappears (`registry.ts`
  enumerates paths today).

**Packets for ports (A6).** A minimal C# packet schema with `validate` and `freeze` is built in R1a, so every port
runs as a packet from the start.

## 4d. CI, identity and supply chain (public repo)

**Agent identity (A9).**
- Agents push and open PRs as a **GitHub App** (or a fine-grained token) that is not the owner's account. The
  owner's admin token is never available inside agent sessions.
- `main` is protected by a ruleset with an **empty bypass list**: required checks, linear history, no force pushes.
  There's no bypass label.
- The owner merges as themself. CODEOWNERS covers the protected modules and is backed by a required check (a solo
  owner can't approve their own PR, so a review requirement would block every merge).

**Workflows (A10).**
- `permissions: read-all` at the workflow level, with write scopes granted per job only where needed.
- `pull_request_target` and `workflow_run` with untrusted checkouts are banned. Forks get no secrets.
- Every action is pinned to a full commit SHA. `zizmor` lints the workflows.
- Model or API keys are only used in scheduled or manually dispatched workflows, never on pull requests.
- Required checks always report a status: a path-filtered job reports a no-op success instead of skipping, so a
  skipped job can't block or silently pass a merge (A11).
- Dependabot (or Renovate) with grouped updates and a 14-day cooldown, matching the existing pin policy.

**NuGet and Node (A16).**
- `nuget.config` with package source mapping (nuget.org only), signed-package validation, and lockfiles in locked
  mode in CI. Analyzer packages run code in every build, so they get the same review as runtime dependencies.
- The TS-indexer sidecar installs with `--ignore-scripts`, runs with no network and no secrets, and talks over
  stdin and stdout only (a versioned JSON protocol with its own golden tests).

**Runtime hardening (A16).**
- The bridge binds to loopback **in code**, ignoring `ASPNETCORE_URLS` and Kestrel URL settings, with a test that
  tries to override it.
- The scrubber runs as a processor on logs and OpenTelemetry exports, so new logging can't carry secrets out.
- The devcontainer is for humans, and it is never the agent sandbox. Docker-in-Docker means root on the host, so
  agent sessions keep using the Sindri containers of the main spec §8.2.

## 5. Criteria plan by level

Each row is a criterion family, the change that satisfies it, and its owner workstream (§6).

| Level | Criterion family | Change | WS |
|---|---|---|---|
| 1 | README, linter, type checker, unit tests | .NET analyzers and `dotnet format` (§4b.6); one-line root commands | W2 |
| 2 | AGENTS.md, devcontainer, pre-commit hooks, branch protection | Devcontainer; pre-commit (format, analyzers, scrub, shape signals); the `main` ruleset (§4d) | W2, W5, W4 |
| 2 | Pinned dependencies, documented build | `global.json`, Central Package Management, locked restore (§4b.6) | W1 |
| 3 | Integration tests, tests runnable locally | Lanes, one root `test` command | W3 |
| 3 | Secret scanning, CODEOWNERS | GitHub secret scanning + push protection, a scrub job in CI, CODEOWNERS backed by a check (§4d) | W4 |
| 3 | Structured logging, tracing, metrics | One logger module (JSON lines, levels, ids); OTel spans and metrics in bridge and sindri (exporter off by default, local collector in the devcontainer) | W7 |
| 3 | Issue and PR templates, labels | Templates; label scheme (`type:`, `area:`, `size:`, `ring:`) synced from a file | W6 |
| 4 | Fast CI feedback | Required CI under 5 minutes for lint, types and unit tests; affected-package detection; heavy lanes nightly | W3 |
| 4 | Flaky-test detection | The quarantine job (§4) | W3 |
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
| W5 Dev environment | Devcontainer (Node, uv, Docker-in-Docker for Serena and sindri containers, Ollama optional); `.env.example` covering every env var the packages read | |
| W6 Task discovery | Issue templates (bug, feature, skill change, idea card), PR template with the evidence section, label sync | Matches the addendum's work-item kinds |
| W7 Observability | One shared logging module; OTel in bridge and sindri; `doctor` reports exporter state | |
| W8 Docs and delivery | `AGENTS.md` command executor; freshness check; promotion metrics | |
| W9 Product signals | A page that maps existing telemetry and experiment machinery onto the pillar | Mostly documentation of what exists |
| W10 Code shape | The §4b shape applied in each port; `deps.yaml` edges and tiers; merging duplicate logic (redaction, labeler, locks, `Result`, error codes, CLI output) found by the code index's clone layer | Happens inside the ports, not as a separate refactor of the TS |

**Order:** see §10. W3 (CI) and W4 (security) come first, because they make every later change checkable.

## 7. Turning it on

Every workstream ends with a "Turn it on" task, as Sindri plans do: the check becomes required on `main`, and the
next PR shows it passing.

## 8. Readiness as a Sindri module

- `sindri readiness check [--json]` runs our reproducible criteria, per repo and per app, and records each result
  in the ledger. It is built in **R1a as the first package scaffolded from the template**, so the template proves
  itself and the Level-4 gate is measurable from the start.
- **Output contract:**
  - each criterion has a state: `pass`, `fail`, `n/a` (with a reason) or `unknown` (we can't reproduce Factory's
    check);
  - the summary shows the level per pillar and overall, and the delta since the last run;
  - exit codes: 0 when the target level holds, 1 when it doesn't, 2 on a tool error;
  - a first run with no baseline records one and says so;
  - `--json` follows a versioned schema.
- **Every CI failure message** names the rule id, the measured value, the limit and the fix command. That covers lane
  budgets, ratchets, `deps.yaml` drift, fan-out and analyzer rules. Text is never the only carrier of meaning:
  colors are paired with words, and `NO_COLOR` is respected (A19).
- **Overrides are records, not labels:** an override names the rule, the reason, the approver and an expiry date,
  and lives in `overrides.yaml`. An expired override fails CI. Quarantined tests leave quarantine after 20 green
  runs in a row (A17).
- **Dependency graph in PRs:** CI renders the graph and adds a plain-text list of added and removed edges (A19).
- When the Factory CLI is available, `sindri readiness import <report>` stores Factory's report as the score of
  record and lists every criterion where the two disagree.
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
| R4 | Score of record | Factory's `/readiness-report` once Droid is installed; our check until then |
| R5 | Tighter TS flags | Superseded by R6 for the rewritten packages; applies only to the TS that stays (mods, the TS-indexer sidecar) |
| R6 | Language and runtime | **Decided:** C# on .NET 10 LTS, NativeAOT CLIs, strangler rewrite per package (§4a) |
| R7 | Test framework | xUnit v3 (most common, best tooling) or TUnit (faster, source-generated); recommend xUnit v3 unless lane budgets fail in the R1 spike |
| R8 | Data access under NativeAOT | Hand-written ADO.NET with `IRowMapper<T>` (§4b.2) unless the R1 spike shows EF Core 10 or Dapper.AOT passing the AOT golden tests |
| R9 | Strongly typed ids | Vogen, or hand-written `readonly record struct` if the spike finds an AOT problem |
| R10 | Agent identity | A GitHub App installed on the repo (preferred) or a fine-grained token, with contents and pull-request write only |

## 10. Rollout and sizing (rough, one builder with agents)

Repo size today: about 24.6k lines of TypeScript source and 31.2k lines of tests, of which sindri is about half.
Recent pace: about 49 commits in 60 days. The sizes below are estimates to be replaced by each packet's own.

| Step | Contents | Size | Exit |
|---|---|---|---|
| R0 | Baseline: Factory's report if Droid is available, plus a quick manual pass over the pillars | XS | Baseline recorded |
| R1a | **Safety and measurement first:**<br>• CI hardened for a public repo, the `main` ruleset and the agent identity (§4d);<br>• the cutover contract and harness (§4c): byte-compat profile, golden harness running the real hooks, the scrubber differential corpus, entry-point shims, the install and MCP-registration migration, prebuilt-binary releases;<br>• golden recordings for every TS package;<br>• the AOT spike (R7, R8, R9, the MCP C# SDK);<br>• a minimal C# packet schema with `validate` and `freeze` (§4c);<br>• `sindri readiness check` as the first templated package (§8) | M (1–2 weeks) | Golden tests green on TS; readiness check reports a baseline; agents can't bypass `main` |
| R1b | **The template:**<br>• `Directory.Build.props`, Central Package Management, analyzers with one owner per rule, the ratchet baseline, `.editorconfig`;<br>• `Aw.Kernel` and its `Data`, `Telemetry` and `Testing` parts;<br>• `deps.yaml` with every edge kind and its checks (§4b.1a);<br>• the `/new-package` and `/new-slice` scaffolds (inputs, generated files, a red test) and `AGENTS.md` for C#;<br>• rule records for the §4b rules;<br>• W5 devcontainer and W6 templates | M (1–2 weeks) | A scaffolded package passes every check; the readiness check reports Level 4 for the template |
| R2 | **The ports, one packet per package, whole:** `Judge` and `Redaction` → `Scorer` → `BugFix` → `Bridge` → `Evidence` → `Sindri`. Sindri is sized per module inside its packet. **In parallel,** new factory code (addendum F0 schemas, rule records) is written in C# from the start | L–XL (estimate 6–10 weeks; each packet carries its own) | Every package on C# by default through the shim; TS deleted 14 days after each cutover |
| R3 | Remaining Level 4 criteria that need usage data (promotion frequency, flake rates, doc freshness over time), and W9 | S–M | Level 4 passed. **Factory work (F0) starts here** |
| R4 | Addendum F0–F3 plus the readiness module's weekly workflow | With the addendum | Level 5 passed |

**Stop rules:**
- A package that overruns its packet's estimate by 2× is reviewed with the human before more work goes in.
- **R2 as a whole** overrunning 1.5× its summed estimate triggers a re-plan with the human: reorder, cut scope, or
  run more packets in parallel. The port itself is decided and isn't reopened.
- CI cost while both builds exist: TS jobs run only for packages whose `runtime` is still `ts`, so cost falls with
  each cutover.
