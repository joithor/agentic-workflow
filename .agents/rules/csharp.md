---
description: C# build (in progress) — package shape, dependency rules, types, errors, DI, JSON, AOT scope, analyzers
globs:
  - "src/**"
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.slnx"
  - "global.json"
  - "Directory.Build.props"
  - "Directory.Packages.props"
  - "nuget.config"
  - "BannedSymbols.txt"
  - "deps.yaml"
paths:
  - "src/**"
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.slnx"
  - "global.json"
  - "Directory.Build.props"
  - "Directory.Packages.props"
  - "nuget.config"
  - "BannedSymbols.txt"
  - "deps.yaml"
alwaysApply: false
---

# C# Rules

The TypeScript packages are being rewritten in C# on .NET 10 (spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md`, §4a–§4e; the spec's §4b is the source of truth for every number here). The port is decided — don't reopen it. **One cutover at parity:** the installed toolkit stays TypeScript until every package passes its golden tests against the C# build; nothing in `setup.sh`, the providers, MCP registration or launchd points at `src/` before the switch. There are no per-package shims.

Each rule names the check that enforces it. "Gap — R1b" means the check doesn't exist yet: follow the rule by hand and don't add new violations.

## Package shape (§4b.1)

```
Host ──► Features ──► Core ◄── Infrastructure
```

| Project | Holds | May reference |
|---|---|---|
| `Aw.<Pkg>.Core` | Domain types, typed ids, value objects, pure logic, **port interfaces**, errors | `Aw.Kernel` only |
| `Aw.<Pkg>.Features` | One folder per use case (slice): request/response records, service, validator, endpoint or CLI mapping | `Core`, `Aw.Kernel` |
| `Aw.<Pkg>.Infrastructure` | Repositories over SQL, adapters (git, containers, model CLIs, HTTP), migrations | `Core`, `Aw.Kernel` |
| `Aw.<Pkg>.Host` | Composition root: minimal API app or CLI `Main`, DI registration, config, the JSON context | everything in the package |

A package under ~1,500 lines may use two projects, `Core` and `App`, with the same direction; split into four past the threshold.

| Rule | Check |
|---|---|
| Dependency direction above | Project references (the build fails) + ArchUnitNET in `tests/Aw.Architecture.Tests` (gap — R1b) |
| `Core` has no I/O: no `System.IO`, `System.Net`, `Microsoft.Data.*`, `System.Diagnostics.Process`, `Console`, `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow`, `Guid.NewGuid` — use `TimeProvider` and `IIdGenerator` ports | `BannedSymbols.txt` → `RS0030` (BannedApiAnalyzers, wired for `*.Core` in `Directory.Build.props`) |
| A slice never references another slice's types; the second use moves the logic into `Core` | ArchUnitNET (gap — R1b); `Aw.Analyzers` fast check (gap — R1b) |
| `Infrastructure` row/DTO types are `internal`; repositories return `Core` types | ArchUnitNET (gap — R1b) |
| Packages reference other packages only through their `Core` public surface or a CLI/HTTP contract | `deps.yaml` checker (§4b.1a) |
| No namespace cycles inside a project | ArchUnitNET slice-cycle rule (gap — R1b) |
| No `Common`, `Shared`, `Utils`, `Helpers` project or namespace | ArchUnitNET naming rule (gap — R1b) |

## Dependency graph (§4b.1a, §4e)

- Tiers: 0 `Aw.Kernel` (+ `Data`, `Telemetry`, `Testing`) and `Redaction` · 1 `Judge`, `Bridge` · 2 `Scorer`, `Evidence`, `BugFix` · 3 `Sindri`. A package depends only on lower tiers; same-tier or upward calls go through a CLI or HTTP contract.
- Every edge is declared in `deps.yaml` with its kind (`project`, `cli`, `db`, `file`). Adding an edge is a reviewed change to `deps.yaml`. Check: the `deps.yaml` checker in CI.
- Fan-out: ≤ 4 in-repo project references and ≤ 6 third-party packages per project (analyzers and test packages excluded; every `Host` exempt). Over the limit needs a recorded reason. Check: `deps.yaml` checker (gap — R1b for the counts).

## Request flow (§4b.2)

- **Endpoints and CLI commands** bind input, call **one** service, map the `Result` to `TypedResults`/`ProblemDetails` or to an exit code. No branching on domain state. Group with `MapGroup`; use `WebApplication.CreateSlimBuilder` and the request-delegate generator. Check: `Aw.Analyzers` "endpoint calls exactly one service" (gap — R1b).
- **Services** implement `IService<TRequest, TResponse>`, one use case each, no "manager" services. > 5 constructor dependencies is a split signal, > 7 is an error. Check: gap — `Aw.Analyzers` (R1b).
- **Cross-cutting** (validation, logging, transactions, budgets) is a `Decorated<TReq, TRes>` decorator registered explicitly. **No MediatR, no AutoMapper** (commercial licences since 2025); mapping is hand-written. Check: not in `Directory.Packages.props` (review).
- **Repositories:** one interface per aggregate in `Core`, named by intent (`Get`, `SaveStarted`, `ListStartable`). `IReadRepository<TEntity, TId>` covers `Get`/`Exists`; no generic `IRepository<T>`, no `IQueryable`. Writes run in an `IUnitOfWork` (one SQLite transaction).
- **SQL:** ADO.NET over `Microsoft.Data.Sqlite`, typed parameters, one `IRowMapper<T>` per row type; SQL in `const string` fields or embedded `.sql` files; numbered migration files, each tested on a copy of the previous schema. The data-access choice is settled by the AOT spike (decision R8, `docs/readiness/aot-spike.md`).

## Types and errors (§4b.3)

- Every id and constrained primitive is a strongly typed value (`ItemId`, `RepoSlug`, `SizeClass`): Vogen or a hand-written `readonly record struct` (decision R9). No raw `string`/`Guid` ids in public `Core`/`Features` signatures. Check: `Aw.Analyzers` (gap — R1b).
- Expected failures are `Error` records with a stable catalogue code (`SND-…`, `AW-…`) returned in `Result<T>` from `Aw.Kernel`. Exceptions are for bugs and broken invariants only. No `catch (Exception)` outside `Host` and `Infrastructure` boundary adapters. Check: `CA1031` (gap — R1b to scope it to those projects).
- Generic types take ≤ 3 type parameters and are constrained (`where TId : struct, IEquatable<TId>`).
- Nullable is on everywhere (`Nullable=enable` + `TreatWarningsAsErrors`). A `!` needs a `// why:` comment. Check: compiler for nullability; `Aw.Analyzers` for the comment (gap — R1b).
- `required` members and primary constructors preferred; records immutable by default.
- Classes are `internal sealed` by default. Public surface is tracked in `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`. Check: `RS0016`/`RS0017` (PublicApiAnalyzers).

## JSON (§4b.3, §4c)

- System.Text.Json with **one source-generated `JsonSerializerContext` per package**. No reflection-based serialization. Check: trim/AOT analyzers (`IL2026`, `IL3050`) via `IsAotCompatible=true`.
- Files the TS build writes today are read and written byte-identically: use the shared options profile in `Aw.Kernel` (camelCase, JS-compatible escaping, `toISOString()` timestamps, key insertion order where digests depend on it). New digests carry a `digestVersion`. Check: the byte-compat test suite (golden TS outputs).

## Dependency injection (§4b.4)

- `Microsoft.Extensions.DependencyInjection`; registration only in `Host`, through one explicit extension method per project (`services.AddSindriCore()`). No assembly scanning (reflection, not AOT-safe).
- Constructor injection only; no service locator; no `IServiceProvider` outside `Host`.
- Lifetimes: services and repositories scoped per request/command; pure domain services singletons; `TimeProvider` singleton (`FakeTimeProvider` in tests).
- Every host has a DI validation test (`ValidateOnBuild` + `ValidateScopes`). Check: that test (per host).

## Build settings and analyzers (§4b.6)

- `global.json` pins the SDK; `Directory.Build.props` sets `Nullable`, `LangVersion=latest`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`, `GenerateDocumentationFile`, `RestorePackagesWithLockFile` (locked mode in CI). Don't override these per project.
- `Directory.Packages.props`: Central Package Management with exact versions. **New packages are at least 14 days old** on nuget.org. Analyzers come in as `GlobalPackageReference` and get the same review as runtime dependencies (they run code in every build).
- `nuget.config` maps every package to nuget.org only. Don't add sources.
- **One owner per rule:** where analyzer packs overlap (for example `CA2007` vs `MA0004`), `.editorconfig` names the owner and disables the other. Every rule agents must obey has an explicit severity — never `suggestion`.
- **Ratchet:** existing violations live in `baseline.globalconfig`, which may only shrink. CI fails if it grows.
- **Size limits:** file ≤ 300 lines (gap — `Aw.Analyzers`), method ≤ 40 lines (`MA0051`), cyclomatic complexity ≤ 10 (`CA1502`), parameters ≤ 5 (gap — `Aw.Analyzers`).
- Fix a warning; don't suppress it. A `#pragma warning disable` or `[SuppressMessage]` needs a `Justification` and is reviewed like an override.

## AOT scope (§4b.6)

| Entry point | Build |
|---|---|
| Hook binaries; `judge`, `sindri`, `scorer`, `bugfix-state` CLIs | NativeAOT |
| `Bridge` host (MCP + HTTP) | NativeAOT if the MCP C# SDK passes the AOT spike, else framework-dependent |
| `Evidence` (ui-evidence) | Framework-dependent (Playwright for .NET has no NativeAOT) |

`IsAotCompatible=true` on `Core`, `Features`, `Infrastructure` and Kernel projects; `PublishAot=true` only on the entry points above. AOT publishing runs in its own CI job and in releases; local builds and tests are JIT.

## Seams (§4b.6, factory addendum §17.20–17.21)

The C# schemas carry the distribution and multiplayer seams from their first commit: a `LedgerStore` port with a shared contract suite, idempotent events, the event envelope (`actor`, `tenant`, `ring`, `causation`, `correlation`, `trace`), a transactional outbox (`outbox_commands`), and `Lease`, `Launcher`, `Scheduler`, `Bus`, `Secrets` ports in `Aw.Kernel`/`Aw.Kernel.Telemetry`. `actor` is set by the host process, never by the caller.

## Adding code

- **New package:** `/new-package` (spec: `docs/csharp/scaffolds/new-package.md`).
- **New use case:** `/new-slice` (spec: `docs/csharp/scaffolds/new-slice.md`). The recipe: red test → request and response records → service → endpoint or CLI mapping → DI registration.
- Test rules: `.agents/rules/csharp-testing.md`.
