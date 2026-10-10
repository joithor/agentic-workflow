# Agent readiness for agentic-workflow — design

Status: draft for review · 2026-10-09
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

These match the standards set for the ring-2 product, adapted to this repo. They become rules (§17.6 of the
addendum) with deterministic checks wherever possible.

**Types**
- `strict` plus `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes` and `noImplicitOverride` in every
  package. Migration is per package, with a ratchet: a new error is a CI failure, and the old error count only
  goes down.
- No `any` outside Fastify boundaries (already a merge-gate item; now a lint rule). No non-null `!` assertions
  without a comment.
- Types are checked within a budget per package (`tsc --extendedDiagnostics` time is recorded in CI; a regression
  over 20% fails).

**Code shape**
- Each package is organized as a pure core (domain logic, no I/O), use cases, and adapters (I/O). Ports are
  injected (the `deps` pattern sindri already uses). Boundaries are checked by `dependency-cruiser`: core imports
  no adapter, and packages import each other only through declared entry points.
- Size and complexity budgets per file and function (for example ≤ 400 lines per file, cyclomatic complexity
  ≤ 15), enforced by the linter with a ratchet. The sindri shape signals (§6.2) report trends.
- One way to do each thing: a shared `Result` type, error codes, CLI output conventions and logging. Duplicates
  are listed in §6 and merged.

**Tests**
- Lanes: `unit` (no I/O, < 10 s per package), `integration` (in-memory SQLite and fakes, < 60 s), `heavy`
  (real browser, real index builds; run alone). Each test file declares its lane. A test that is too slow for its
  lane fails CI.
- Zero automatic retries. A flaky test is quarantined by a CI job that reruns failures, records them, and opens an
  issue.
- Coverage thresholds per package stay as `planning/TESTING.md` sets them, now enforced in CI.
- Every test follows `/testAudit`'s authoring gate.

**Lint and format**
- Decision R1 (§9): **Biome** for formatting and most lint rules (one fast tool), plus `typescript-eslint` only
  for the type-aware rules Biome lacks (floating promises, unsafe `any`) and `dependency-cruiser` for boundaries.
- `shellcheck` and `shfmt` for the bash in `config/`, `scripts/` and `providers/`.

## 4a. Language: C# on .NET (decided 2026-10-09)

The human decided to rewrite the toolkit's TypeScript packages in **C# on .NET 10 (LTS)**, running the same on
Linux and macOS. **The readiness foundation comes first** (rollout R1): the standards, rules, template, shared
building blocks and CI checks are set up for C# before any package is ported, so each ported package is
compliant from its first commit. The TypeScript is not linted or refactored, since it is being replaced. It only
gets CI and golden contract tests as a safety net. The standards in §4 still apply, expressed in .NET tooling.

**What moves, and to what:**

| Today (TS) | C# / .NET |
|---|---|
| `tsc` strict | Nullable reference types enabled, `TreatWarningsAsErrors`, `AnalysisLevel latest-all` |
| Biome / typescript-eslint (planned) | Roslyn analyzers (built-in + `Microsoft.CodeAnalysis.NetAnalyzers`), `.editorconfig` severities, `dotnet format` in pre-commit and CI |
| dependency-cruiser (planned) | One project per layer (`Core`, `UseCases`, `Adapters`, `Cli`) with project references as the boundary, plus architecture tests (for example NetArchTest) |
| Zod schemas, JSON Schema export | Records with `System.Text.Json` source generation; validators per schema; JSON Schema generated from the types for editor autocomplete |
| Vitest | xUnit (or TUnit), with the lanes and time budgets in §4 |
| better-sqlite3 | `Microsoft.Data.Sqlite`, same files and schemas, WAL mode |
| Fastify (bridge) | ASP.NET Core minimal APIs |
| `@modelcontextprotocol/sdk` | The official MCP C# SDK |
| Node CLIs started per hook call | **NativeAOT** single-file binaries for every CLI and hook entry point, so startup stays fast. Reflection-free JSON (source generators) is required for AOT |
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
| `Aw.Kernel` (shared, one per repo) | `Result<T>`, `Error`, `TimeProvider`-based clock helpers, id and value-object conventions, the logging setup, the generic `IService<TReq, TRes>` pipeline, test-contract base classes | the BCL only |

**Rules** (enforced by project references, which fail the build, and ArchUnitNET tests):
- `Core` has no I/O. It doesn't reference `System.IO`, `System.Net`, `Microsoft.Data.*`, `System.Diagnostics.Process`,
  `Console`, `DateTime.Now`/`UtcNow` or `Guid.NewGuid`. They're banned through `BannedSymbols.txt` (the
  BannedApiAnalyzers package), and time and ids come from injected ports (`TimeProvider`, `IIdGenerator`).
- One slice never references another slice's types. Shared behavior moves into `Core` as a domain service or a port.
  A second use of the same logic is the trigger to move it (the "generalize at the second case" signal, §6.2).
- `Infrastructure` row and DTO types are `internal`. They never leave the project, and repositories return `Core` types.
- Packages reference other packages only through their `Core` project's public surface, or through a CLI or HTTP
  contract. Their `Infrastructure` and `Features` are never referenced.

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
  - `IsAotCompatible=true` on every non-test project, which turns on the trim and AOT analyzers;
  - `GenerateDocumentationFile=true` for public API;
  - `RestorePackagesWithLockFile=true`, plus `RestoreLockedMode` in CI.
- `Directory.Packages.props`: Central Package Management with exact versions. Analyzers are added as
  `GlobalPackageReference`: `Microsoft.CodeAnalysis.NetAnalyzers`, `Meziantou.Analyzer`, `Roslynator.Analyzers`,
  `Microsoft.CodeAnalysis.BannedApiAnalyzers`, `Microsoft.CodeAnalysis.PublicApiAnalyzers`. New packages follow
  the same 14-day minimum age as the existing pins.
- `.editorconfig` sets every rule's severity explicitly (no "suggestion" level for anything agents should obey).
  The size limits: file ≤ 300 lines, method ≤ 40 lines, cyclomatic complexity ≤ 10, parameters ≤ 5. Existing
  violations go into a ratchet baseline that only shrinks.
- `Aw.Analyzers`: a small in-repo analyzer project for the rules no package covers:
  - no raw ids in public signatures;
  - `!` needs a reason;
  - no cross-slice references (fast feedback; the architecture test is authoritative);
  - endpoint lambdas call exactly one service.

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

## 5. Criteria plan by level

Each row is a criterion family, the change that satisfies it, and its owner workstream (§6).

| Level | Criterion family | Change | WS |
|---|---|---|---|
| 1 | README, linter, type checker, unit tests | Biome + typed lint; root scripts | W1, W2 |
| 2 | AGENTS.md, devcontainer, pre-commit hooks, branch protection | Devcontainer; pre-commit (format, lint, scrub, shape signals); protect `main` with required checks | W2, W5, W4 |
| 2 | Pinned dependencies, documented build | Root workspace, `.nvmrc`, lockfile per package checked in CI | W1 |
| 3 | Integration tests, tests runnable locally | Lanes, one root `test` command | W3 |
| 3 | Secret scanning, CODEOWNERS | GitHub secret scanning + push protection, a scrub job in CI, CODEOWNERS | W4 |
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
| W1 Workspace | Root `package.json` with npm workspaces for the six packages (keep per-package lockfiles or move to one, decision R2); `.nvmrc`; root `typecheck`, `test`, `lint`, `build` | Doesn't change how `setup.sh` builds |
| W2 Style | Biome config, typed lint, `.editorconfig`, shellcheck and shfmt; ratchet files for existing violations | Fix-forward in small PRs per package |
| W3 Tests and CI | GitHub Actions: lint, types, unit and integration per affected package, bash tests, `sync-rules --check`, `setup.sh --dry-run`, `claude plugin validate`; nightly heavy lane; quarantine job; time budgets | The `AGENTS.md` merge gate becomes required checks |
| W4 Security | Branch protection with required checks and linear history; CODEOWNERS; secret scanning and push protection; Dependabot (or Renovate) with grouped updates; scrub job in CI | Single maintainer: CODEOWNERS is for the protected modules (§7.7) |
| W5 Dev environment | Devcontainer (Node, uv, Docker-in-Docker for Serena and sindri containers, Ollama optional); `.env.example` covering every env var the packages read | |
| W6 Task discovery | Issue templates (bug, feature, skill change, idea card), PR template with the evidence section, label sync | Matches the addendum's work-item kinds |
| W7 Observability | One shared logging module; OTel in bridge and sindri; `doctor` reports exporter state | |
| W8 Docs and delivery | `AGENTS.md` command executor; freshness check; promotion metrics | |
| W9 Product signals | A page that maps existing telemetry and experiment machinery onto the pillar | Mostly documentation of what exists |
| W10 Code shape | Pure core / adapters split where a package mixes them; dependency-cruiser rules; size and complexity ratchets; merge duplicate utilities (locks, `Result`, error codes, CLI output, scrubbing) found by the code index's clone layer | The largest piece. Driven by the index's clone report, one package per PR |

**Order:** W1 → W2 → W3 → W4 come first, because they make every later change checkable. Then W6, W5, W7 and W8
run in parallel. W10 runs package by package once W3 is in. W9 last.

## 7. Turning it on

Every workstream ends with a "Turn it on" task, as Sindri plans do: the check becomes required on `main`, and the
next PR shows it passing.

## 8. Readiness as a Sindri module

- `sindri readiness check [--json]` runs our reproducible criteria, per repo and per app, and records each result
  in the ledger.
- When the Factory CLI is available, `sindri readiness import <report>` stores Factory's report as the score of
  record and lists every criterion where the two disagree.
- A weekly standing workflow at the `propose` tier turns the top failing criteria into work items, so the factory
  keeps the repo ready as it changes.
- Ring-1 repos get the same check through `sindri repo onboard` (record-only first).
- The criteria list is a registered artifact. Scouting (§17.18) watches Factory's published criteria for changes.

## 9. Decisions needed

| # | Decision | Recommendation |
|---|---|---|
| R1 | Lint and format tooling | Biome + typed `typescript-eslint` rules + dependency-cruiser |
| R2 | One lockfile vs per-package | Keep per-package lockfiles for now (`setup.sh` builds each); revisit after W1 |
| R3 | Branch protection for a solo maintainer | Required checks and no force pushes; admins may bypass only through a recorded label. The factory's own merges still go through the checks |
| R4 | Score of record | Factory's `/readiness-report` once Droid is installed; our check until then |
| R5 | Tighter TS flags | Superseded by R6 for the rewritten packages; applies only to the TS that stays (mods, the TS-indexer sidecar) |
| R6 | Language and runtime | **Decided:** C# on .NET 10 LTS, NativeAOT CLIs, strangler rewrite per package (§4a) |
| R7 | Test framework | xUnit v3 (most common, best tooling) or TUnit (faster, source-generated); recommend xUnit v3 unless lane budgets fail in the R1 spike |
| R8 | Data access under NativeAOT | Hand-written ADO.NET with `IRowMapper<T>` (§4b.2) unless the R1 spike shows EF Core 10 or Dapper.AOT passing the AOT golden tests |
| R9 | Strongly typed ids | Vogen, or hand-written `readonly record struct` if the spike finds an AOT problem |

## 10. Rollout and sizing (rough, one builder with agents)

| Step | Contents | Size | Exit |
|---|---|---|---|
| R0 | Baseline: run Factory's report if possible, plus our check; record the level per pillar | XS | Baseline in the ledger |
| R1 | **Readiness foundation, before any port.** Everything a ported package must meet exists and is enforced first:<br>• the .NET solution template: `Directory.Build.props` (nullable, warnings as errors, analyzers, AOT), `.editorconfig` severities, the layer-per-project layout, architecture tests, and size and complexity rules;<br>• the shared building blocks every package uses: `Result`, error codes, CLI output, JSON logging with OpenTelemetry, SQLite access, the `deps` port pattern;<br>• test lanes with time budgets, and the flake quarantine;<br>• CI with fast required checks, plus AOT builds on Linux and macOS;<br>• W4 security, W6 templates, W5 devcontainer (.NET, the Node sidecar, uv);<br>• the rules from the ring-2 handbook's standards ported to rule records (§17.6) with Roslyn or architecture-test checks;<br>• `AGENTS.md` and `.agents/rules/` rewritten for C#, and a `/new-package` scaffold that creates a compliant project;<br>• golden contract tests for every TS package (§4a step 1), run in CI as the rewrite's safety net | M–L (2–3 weeks) | A scaffolded empty package passes every check; golden tests green on TS; the readiness check reports Level 4 for the template |
| R2 | The strangler rewrite (§4a), one package per packet. Each package is scaffolded from the template, so it is born compliant, and cuts over only when its golden tests and every R1 check pass | L–XL (4–8 weeks, estimate per package in each packet) | All packages on C#. The per-app readiness fraction rises with each cutover |
| R3 | Remaining Level 4 criteria that need real usage data (deploy and promotion frequency, flake rates, doc freshness over time), and W9 | S–M | Level 4 passed. **Factory work (F0) starts here** |
| R4 | Addendum F0–F3 plus the readiness module | With the addendum | Level 5 passed |

Workstreams W1 (npm workspace) and W2 (Biome and typescript-eslint) apply only to the TypeScript that stays
(§4a). For everything else, the .NET solution and analyzers replace them.
