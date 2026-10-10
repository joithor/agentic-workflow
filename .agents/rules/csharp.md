---
description: C# build (in progress) — package shape, dependency rules, types, errors, DI, JSON, AOT scope, analyzers
globs:
  - "src/**"
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.slnx"
  - "global.json"
  - "Directory.Build.props"
  - "Directory.Build.targets"
  - "Directory.Packages.props"
  - "nuget.config"
  - "BannedSymbols.txt"
  - "deps.yaml"
  - "schemas/deps.schema.json"
  - "**/.editorconfig"
  - "CodeMetricsConfig.txt"
  - "**/PublicAPI.*.txt"
  - "**/packages.lock.json"
paths:
  - "src/**"
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.slnx"
  - "global.json"
  - "Directory.Build.props"
  - "Directory.Build.targets"
  - "Directory.Packages.props"
  - "nuget.config"
  - "BannedSymbols.txt"
  - "deps.yaml"
  - "schemas/deps.schema.json"
  - "**/.editorconfig"
  - "CodeMetricsConfig.txt"
  - "**/PublicAPI.*.txt"
  - "**/packages.lock.json"
alwaysApply: false
---

# C# Rules

The TypeScript packages are being rewritten in C# on .NET 10 (spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md`, §4a–§4e; the spec's §4b is the source of truth for every number here). The port is decided — don't reopen it. **Clean cutover:** the C# build keeps no backward compatibility with the TS build (no byte-compatible JSON, no golden replay against TS output, no migration of TS state); its contracts are defined by its own tests. The one exception is the **hook-facing CLI and file surface** (the `deps.yaml` Hooks, Statusline and AwLive edges: the CLI flags and output the hooks call, and the files they write, such as `judge/sessions`, `judge/outbox` and `judge/teammate-names.jsonl`): it stays frozen until the switch, checked in CI by running the real hook scripts against the C# binaries. The installed toolkit stays TypeScript until the C# build is complete; before the switch nothing in `setup.sh`, the providers, MCP registration or launchd points at the C# build. Separately, `aw-readiness` and `aw-packets` run before the switch as standalone NativeAOT CLIs that write only under `readiness/` and `packets/` (spec §4c); nothing installs or schedules them. There are no per-package shims.

Each rule names the check that enforces it. "Gap — R1b" means the check doesn't exist yet: follow the rule by hand and don't add new violations.

## Package shape (§4b.1)

```
Host ──► Features ──► Core ◄── Infrastructure
```

| Project | Holds | May reference |
|---|---|---|
| `Aw.<Pkg>.Core` | Domain types, typed ids, value objects, pure logic, **port interfaces**, errors | `Aw.Kernel` (+ `Microsoft.Extensions.DependencyInjection.Abstractions` for its `Add…()` method, and Vogen for its typed ids) |
| `Aw.<Pkg>.Features` | One folder per use case (slice): request/response records, service, validator | `Core`, `Aw.Kernel`, other packages' `Core` |
| `Aw.<Pkg>.Infrastructure` | Repositories over SQL, adapters (git, containers, model CLIs, HTTP), migrations, its own settings (internal, read from the environment `Composition` passes) | `Core`, `Aw.Kernel`, `Aw.Kernel.Data`, and other tier-0 packages (e.g. `Redaction`) through a declared `deps.yaml` edge |
| `Aw.<Pkg>.Host` | Composition root: minimal API app or CLI `Main`, the endpoints and CLI commands (one per slice), `Compose` (which passes the environment to `Add…Infrastructure`), the JSON context | everything in the package, plus `Aw.Kernel.Telemetry` and the tier-0 and lower-tier `Core`s it composes |

A package under ~1,500 lines may use two projects, `Core` and `App`, with the same direction; split into four past the threshold.

| Rule | Check |
|---|---|
| Dependency direction above | Project references (the build fails) + ArchUnitNET in `tests/Aw.Architecture.Tests`: `ARCH-DIR-001` (Core depends on nothing else in its package), `ARCH-DIR-002` (Features: Core and Features only), `ARCH-DIR-003` (Infrastructure: Core and Infrastructure only), `ARCH-DIR-004` (another package only through its Core), `ARCH-DIR-005` (Core and Features use `Aw.Kernel` and no other tier-0 package); `ARCH-PLACE-001` (every namespace under a package root names one of those layers) |
| `Core` has no I/O: no `System.IO`, `System.Net`, `Microsoft.Data.*`, `System.Diagnostics.Process`, `Console`, `DateTime.Now/UtcNow`, `DateTimeOffset.Now/UtcNow`, `Guid.NewGuid` — use `TimeProvider` and `IIdGenerator` ports | `BannedSymbols.txt` → `RS0030` (BannedApiAnalyzers, wired for `*.Core` in `Directory.Build.props`; another project opts in with `AwBannedSymbols=true`, as `Aw.Kernel` does) |
| A slice never references another slice's types; the second use moves the logic into `Core` | ArchUnitNET `ARCH-SLICE-001` (both shapes); `Aw.Analyzers` fast check (gap — R1b) |
| `Infrastructure` row/DTO types are `internal`; repositories return `Core` types | ArchUnitNET `ARCH-INFRA-001` (only the `*ServiceCollectionExtensions` registration class is public in Infrastructure); `ARCH-DIR-001` keeps Infrastructure types out of Core signatures |
| External technology sits behind ports (Seams below): no technology package or type (`Microsoft.Data.*`, `Aw.Kernel.Data`, `SQLitePCLRaw`, `Npgsql`, cache and queue clients, `System.Net.Http`, `System.Diagnostics.Process`, file I/O) in `Core` or `Features`; `Host` uses `Infrastructure` types only in `Composition`. In the small shape the rule applies by namespace: `Aw.<Pkg>.App.Features.*` and `Commands` use no technology type, and only `Composition` uses `Aw.<Pkg>.App.Infrastructure.*` | `Core`: `BannedSymbols.txt` → `RS0030` covers only `N:System.IO`, `N:System.Net` (so `System.Net.Http`), `N:Microsoft.Data` and `T:System.Diagnostics.Process`; `Aw.Kernel.Data`, `SQLitePCL`, `Npgsql` and cache or queue client packages aren't in that list. ArchUnitNET covers all of them by namespace in both shapes: `ARCH-PORT-001` (no technology type in `Core`, `Features` or the small shape's `App.Features` and `App.Commands`; the list, which adds `System.Environment`, is `Technology.cs` in `tests/Aw.Architecture.Tests/Rules`) and `ARCH-PORT-002` (only `Composition` uses `Infrastructure`: the rest of `Host` and the small shape's `App` and `App.Commands` don't). ArchUnitNET doesn't see the body of a top-level-statement `Program` (`<Main>$`), so `Program` stays the scaffold's two calls (`Composition.Compose`, the command table) and review checks it |
| Packages reference other packages only through their `Core` public surface (the reference sits in `Features` (`App` in the small shape) or `Host`) or a CLI/HTTP contract | `node scripts/check-deps.mjs` against `deps.yaml` |
| No namespace cycles inside a project | ArchUnitNET `ARCH-CYCLE-001` (namespace graph per assembly) |
| No `Common`, `Shared`, `Utils`, `Helpers` project or namespace | ArchUnitNET `ARCH-NAME-001` (every registered namespace, every project in `AgenticWorkflow.slnx`) |

## Dependency graph (§4b.1a, §4e)

- Tiers (`deps.yaml` is the tier table of record): 0 `Aw.Kernel` (+ `Data`, `Telemetry`, `Testing`), `Redaction` and `Analyzers` · 1 `Judge`, `Bridge`, `Packets`, `Readiness` · 2 `Scorer`, `Evidence`, `BugFix` · 3 `Sindri`. **Project** edges point strictly down the tiers. Read-only **`db`** and **`file`** edges may point down or across, never up (e.g. Scorer reads Evidence's run files across tier 2). **`cli`** edges may point anywhere (a CLI is a contract). Every edge is declared in `deps.yaml`. Tier 0 is the only tier with project references inside it, ordered by sublevel (0: `Aw.Kernel`, `Redaction`; 1: `Data`, `Telemetry`; 2: `Testing`); a tier-0 project references only a lower sublevel.
- Every edge is declared in `deps.yaml` (schema `schemas/deps.schema.json`) with its kind (`project`, `cli`, `db`, `file`). Adding an edge is a reviewed change to `deps.yaml`. Check: `node scripts/check-deps.mjs`, which compares the declared edges with the project references and with the cli, db and file edges it scans in the source.
- Fan-out: ≤ 4 in-repo project references (`limits.projectFanOut`) and ≤ 6 third-party packages (`limits.thirdPartyFanOut`) per project. `*.Host`, `*.Tests` and `*.Testing` projects are exempt; analyzers are excluded (packages with `PrivateAssets=all`, and project references with `OutputItemType=Analyzer`). Over the limit needs a recorded exception with a reason in `deps.yaml`. Check: `node scripts/check-deps.mjs` (`DEPS-FANOUT-002`/`003`).
- **Test support:** `*.Testing` projects are test-support, like packages marked `test: true` in `deps.yaml` (e.g. `KernelTesting`). They may reference test-support packages without a declared edge, and only `*.Tests` and `*.Testing` projects may reference them. Production code never references test support. Check: `node scripts/check-deps.mjs`.
- **Analyzers:** `Aw.Analyzers` is declared as the `Analyzers` node in `deps.yaml`. It's referenced only with `OutputItemType="Analyzer"` (and `ReferenceOutputAssembly=false`), in a `.csproj` or a `Directory.Build` file, normally `Directory.Build.targets`. Such analyzer references are excluded from fan-out and from `DEPS-PROJ-001`; a runtime reference in a `Directory.Build` file fails `DEPS-PROJ-005`, and an analyzer reference without `OutputItemType="Analyzer"` fails `DEPS-PROJ-006`. Check: `node scripts/check-deps.mjs`.

## Request flow (§4b.2)

- **Endpoints and CLI commands** live in `Host` (`Host/Endpoints/`, `Host/Commands/`). That keeps `Features` free of ASP.NET and of the trim/AOT warnings (`IL2026`, `IL3050`) that minimal-API lambdas raise outside the request-delegate generator, which only runs in the `Host` project. They bind input, call **one** service (resolved as `IService<TReq, TRes>`), and map the `Result` to `TypedResults`/`ProblemDetails` or to an exit code. No branching on domain state. Group with `MapGroup`; use `WebApplication.CreateSlimBuilder`. Check: `Aw.Analyzers` "endpoint calls exactly one service" (gap — R1b).
- **Services** implement `IService<TRequest, TResponse>`, one use case each, no "manager" services. > 5 constructor dependencies is a split signal, > 7 is an error. Check: gap — `Aw.Analyzers` (R1b).
- **Cross-cutting** (validation, logging, transactions, budgets) is a `Decorated<TReq, TRes>` decorator registered explicitly. **No MediatR, no AutoMapper** (commercial licences since 2025); mapping is hand-written. Check: not in `Directory.Packages.props` (review).
- **Repositories:** one interface per aggregate in `Core`, named by intent (`Get`, `SaveStarted`, `ListStartable`). `IReadRepository<TEntity, TId>` covers `Get`/`Exists`; no generic `IRepository<T>`, no `IQueryable`. Writes run in an `IUnitOfWork` (one database transaction; SQLite today).
- **SQL:** ADO.NET over `Microsoft.Data.Sqlite`, typed parameters, one `IRowMapper<T>` per row type; SQL in `const string` fields or embedded `.sql` files; numbered migration files, each tested on a copy of the previous schema. Data access is decided (R8): hand-written ADO.NET with `IRowMapper<T>`; no EF Core, no Dapper. SQLite is the bundled `e_sqlite3` that `Microsoft.Data.Sqlite` ships, as a second file next to each AOT tool that opens a database and listed in `binaries.lock`; never the host's library. Open connections through `Aw.Kernel.Data`'s `SqliteConnectionFactory`: read-write connections get WAL (checked; refused if not granted), `busy_timeout`, `foreign_keys=on` and `synchronous=NORMAL`; read-only ones (`OpenReadOnly`/`BeginRead`) get only `busy_timeout` and `query_only`, so they never rewrite a file's header (`docs/csharp/kernel-data.md`). The migration runner switches `foreign_keys` off around each migration and checks them before commit. Schemas are new C# schemas. **Keep the database replaceable** (the general port rule in Seams below): SQL, pragmas and `Sqlite*` types live only in `Infrastructure` (`App` in the small shape) and `Aw.Kernel.Data`, behind the `Aw.Kernel.Persistence` ports and the package's repository ports, so moving to another engine (e.g. PostgreSQL through Npgsql) is a new adapter plus migrations, not a change to `Core`, `Features` or `Host`. Inside an adapter, keep engine-specific SQL (pragmas, `VACUUM INTO`, `json_*`, `INSERT OR …`) in the migration files or named `const string` fields, not spread through query building.

## Types and errors (§4b.3)

- Every id and constrained primitive is a strongly typed value (`ItemId`, `RepoSlug`, `SizeClass`, `TokenBudget`). **Decided (R9):** Vogen generates validation and equality with its own STJ generation off, because source generators can't see each other's output (a Vogen converter is invisible to the STJ source generator, which then writes the id as `{}`; a Vogen-only parse method is invisible to the request-delegate generator). So:
  - every assembly that declares ids sets `[assembly: VogenDefaults(...)]` with all six arguments of `docs/csharp/typed-ids.md` step 2, verbatim (`Conversions.None`, the STJ factory and primitive equality `Omit`, both casts `CastOperator.None`, `TryFromGeneration.GenerateBoolMethod`); `TypedIdGuard.CheckDefaults` checks each one in every registered assembly that declares ids (`ARCH-ID-001`);
  - each id carries a user-code `[JsonConverter]`, e.g. `[JsonConverter(typeof(StringIdJsonConverter<ItemId>))]`, that reads with `TryFrom` and throws `JsonException` (a 400, never Vogen's exception);
  - `IParsable<T>` is declared in user code: through the Kernel's `ITypedId<TSelf, TValue>` (an id in that form goes behind an endpoint only once an RDG route-binding test passes for it) or a hand-written `TryParse`;
  - the Host maps Vogen's validation exception to a 400;
  - each id has a JSON round-trip test, and an architecture test checks every id follows these rules.
  `ITypedId` is in `Aw.Kernel.Ids`, the `StringIdJsonConverter`/`GuidIdJsonConverter`/`Int32IdJsonConverter` are in `Aw.Kernel.Json`, and the recipe is `docs/csharp/typed-ids.md`. No raw `string`/`Guid` ids in public `Core`/`Features` signatures. Check: `TypedIdGuard` in `tests/Aw.Architecture.Tests` runs over every registered `Aw.*` assembly (`ARCH-ID-001` defaults, `ARCH-ID-002` ids and id properties); the raw-id ban in `Aw.Analyzers` is a gap — R1b.
- Expected failures are `Error` records with a stable catalogue code (`SND-…`, `AW-…`) returned in `Result<T>` from `Aw.Kernel`. Exceptions are for bugs and broken invariants only. No `catch (Exception)` outside `Host` and `Infrastructure` boundary adapters. Check: `CA1031` at error in the root `.editorconfig`; `Host` and `Infrastructure` projects lower it in their own project-level `.editorconfig` (`/new-package` generates it).
- Generic types take ≤ 3 type parameters and are constrained (`where TId : struct, IEquatable<TId>`).
- Nullable is on everywhere (`Nullable=enable` + `TreatWarningsAsErrors`). A `!` needs a `// why:` comment. Check: compiler for nullability; `Aw.Analyzers` for the comment (gap — R1b).
- `required` members and primary constructors preferred; records immutable by default.
- Classes are `internal sealed` by default (`CA1852`, `CA1515`). Only what crosses an assembly is public: the `Add…()` methods, request and response records, port interfaces and errors in `Core`. In the small shape nothing outside the package references `App`, so `App`'s types, its `Add…()` methods and its slice request and response records are `internal` (with `host: cli` `App` is an Exe, and `CA1515` rejects public types there); `App` declares `InternalsVisibleTo` for `Aw.<Pkg>.Tests` and `Aw.<Pkg>.Testing`, and its PublicAPI files stay at `#nullable enable` only. Every **library** project keeps `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` (hosts don't: `Directory.Build.targets` turns the public-API analyzers off for `*.Host`, whose only public type is the generated `Program`): both start with `#nullable enable` (`RS0037`), and every symbol line carries nullability annotations (`RS0036`), e.g. `static Aw.Demo.Core.DemoCoreServiceCollectionExtensions.AddDemoCore(this Microsoft.Extensions.DependencyInjection.IServiceCollection! services) -> Microsoft.Extensions.DependencyInjection.IServiceCollection!`. A record lists its compiler-generated members too (`<Clone>$`, `Deconstruct`, `Equals`, `GetHashCode`, `ToString`, `==`/`!=`, each `get`/`init`); take the lines from the `RS0016` code fix rather than writing them by hand. Every public member has a `///` summary. Tests reach internals through `InternalsVisibleTo`: `Core`, `Features` and `Infrastructure` declare it for `Aw.<Pkg>.Tests` and `Aw.<Pkg>.Testing`; `Host` for `Aw.<Pkg>.Tests` only. In the small shape `App` declares it for both, and `Aw.<Pkg>.Testing` declares it for `Aw.<Pkg>.Tests` because its builders over `App`'s internal records are internal. Check: `RS0016`/`RS0017`/`RS0036`/`RS0037` (PublicApiAnalyzers), `CS1591`.

## JSON (§4b.3)

- System.Text.Json with **one source-generated `JsonSerializerContext` per package**. No reflection-based serialization. Check: trim/AOT analyzers (`IL2026`, `IL3050`) via `IsAotCompatible=true`.
- Use the shared **`AwJson` profile** from `Aw.Kernel.Json`: `AwJson.CreateOptions(<Name>JsonContext.Default)` once per host, then `AwJson.Serialize`/`Deserialize` with those options (camelCase, relaxed escaping, UTC `O` timestamps, `RespectNullableAnnotations`, `RespectRequiredConstructorParameters`). Never construct `JsonSerializerOptions` yourself or serialize through a context's `.Default` options directly. HTTP hosts register the same resolver and options in `ConfigureHttpJsonOptions`.

## Dependency injection (§4b.4)

- `Microsoft.Extensions.DependencyInjection`. **Each project owns one explicit, public extension method** that registers its own types (`services.AddSindriCore()`, `AddSindriFeatures()`, `AddSindriInfrastructure(environment)`); **only `Host` calls them and builds the container.** No assembly scanning (reflection, not AOT-safe).
- So `Core`, `Features` and `Infrastructure` may reference `Microsoft.Extensions.DependencyInjection.Abstractions` (for `Core`, alongside Vogen for its typed ids, its only packages besides `Aw.Kernel`). `Aw.Kernel` stays BCL-only: no `IConfiguration` in Kernel signatures. `Host` never builds an `Infrastructure` settings type: `Composition` passes the environment (a `Func<string, string?>`, `Environment.GetEnvironmentVariable` in `Program.cs`, a lambda in tests; no `IConfiguration`, which the AOT CLI hosts don't reference) to `Add<Name>Infrastructure(environment)`, and `Infrastructure` reads its own settings (an `internal` `<Name>Settings` record: the state directory, a connection string). A technology swap changes `Infrastructure` and the one `Add…()` call in `Composition`, never the rest of `Host`.
- Decorators are registered in the **owning project's `Add…()` method** (outermost first), never in `Host`: `Compose` only calls the projects' `Add…()` methods (choosing one where `Infrastructure` offers several, Seams below), registers the clock, and passes the environment to the `Infrastructure` `Add…()` it picks.
- Each `Host` exposes one `internal static IServiceCollection Compose(IServiceCollection, Func<string, string?> environment, TimeProvider)` that `Program.cs` and the DI validation test both call, so the test covers the real composition.
- Constructor injection only; no service locator; no `IServiceProvider` outside `Host`, with **one exception**: the `IServiceProvider` parameter of a registration factory lambda inside a project's own `Add…()` method. M.E.DI has no decorator support, so that's how a decorator wraps its service:

  ```csharp
  services.AddScoped<StartItemService>();
  services.AddScoped<IValidator<StartItemRequest>, StartItemValidator>();
  services.AddScoped<IService<StartItemRequest, StartItemResponse>>(static sp =>
      new ValidatingService<StartItemRequest, StartItemResponse>(
          sp.GetRequiredService<StartItemService>(),
          sp.GetRequiredService<IValidator<StartItemRequest>>()));
  ```

  The lambda only resolves and constructs; it holds no logic. Check: ArchUnitNET `ARCH-DI-001`. It flags the locator types (`IServiceProvider`, `IKeyedServiceProvider`, `IServiceScopeFactory`, `IServiceScope`, `AsyncServiceScope`, `ActivatorUtilities`, `IServiceProviderIsService`) anywhere except `Host` (with `Program`; in the small shape the `App` root namespace and `App.Commands`) and `*ServiceCollectionExtensions` classes. It can't tell the factory lambda from another member of that class, so keep those classes to registrations.
- Lifetimes: services and repositories scoped per request/command; pure domain services singletons; `TimeProvider` singleton (`FakeTimeProvider` in tests).
- Every host has a DI validation test over `Compose`: it builds with `ValidateOnBuild` + `ValidateScopes`, then **resolves every registered `IService<,>` inside a scope**, because `ValidateOnBuild` can't see what a factory lambda resolves (a missing validator would otherwise fail only at run time). Production `Program.cs` skips the validation flags (CLI startup time); the test carries them. Check: that test (per host).

## Build settings and analyzers (§4b.6)

- `global.json` pins the SDK. `Directory.Build.props` sets `Nullable`, `LangVersion=latest`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `EnableNETAnalyzers=false` (the pinned NetAnalyzers package owns the CA rules), `GenerateDocumentationFile`, NuGet audit (high and critical advisories, `NU1903`/`NU1904`, fail the build), and `RestorePackagesWithLockFile` (locked mode in CI). Severity lives in `.editorconfig` (the bulk `error` baseline plus explicit lines); **never add `AnalysisLevel`**: with the SDK analyzers off it is silently ignored. Don't override these per project, except through the two role switches below.
- **Project roles** (from the name, in `Directory.Build.props`): `*.Tests` and `*.Testing` are test projects, `*.Host` hosts, `*.Analyzers` analyzers; **every other project is a library** (`Core`, `Features`, `Infrastructure`, a small package's `App`, Kernel, `Redaction`) and gets `IsAotCompatible=true`. A library opts out only with `AwAotCompatible=false` and a comment saying why (e.g. `Evidence`: Playwright for .NET has no NativeAOT). A non-`Core` project opts into the banned-symbol list with `AwBannedSymbols=true`.
- **Lock files:** CI fails on a missing `packages.lock.json` (`AW0001`, `Directory.Build.targets`) and on a lock file restore changed or a package that isn't signed (`scripts/dotnet-verify-packages.sh`: lock files present and unchanged, signature-verified locked restore, `dotnet nuget verify --all`).
- `Directory.Packages.props`: Central Package Management with versions pinned as **`[x.y.z]`** (a bare `x.y.z` is only a minimum). **New packages are at least 14 days old** on nuget.org. Analyzers come in as `GlobalPackageReference` and get the same review as runtime dependencies (they run code in every build).
- `nuget.config` maps every package to nuget.org only. Don't add sources.
- **One owner per rule:** where analyzer packs overlap (for example `CA2007` vs `MA0004`), `.editorconfig` names the owner and disables the other. Every rule agents must obey has an explicit severity — never `suggestion`.
- **Ratchet:** existing violations live in the `#### Ratchet baseline (spec §4b.6) ####` block at the end of the root `.editorconfig`, below the sentinel line `# ---- ratchet-baseline: generated entries only below this line ----` (one section per file, lowering only the named rule); that part may only shrink. Not a `baseline.globalconfig`: a global config can't lower a rule that `.editorconfig` sets (editorconfig wins), and global-config sections need absolute paths. The growth check (W2) diffs the entries below the sentinel; it is a gap until W2 lands.
- **Size limits:** file ≤ 300 lines (gap — `Aw.Analyzers`); method ≤ 40 lines (`MA0051`, `MA0051.maximum_lines_per_method = 40` in `.editorconfig`); cyclomatic complexity ≤ 10 (`CA1502`, error in `.editorconfig`, limit `CA1502: 10` in `CodeMetricsConfig.txt`); parameters ≤ 5 (gap — `Aw.Analyzers`).
- **Test projects:** The test policy applies **by location**: every `*.Tests` and `*.Testing` project lives under `tests/` (including `tests/Aw.Kernel.Testing/`), and #89's `tests/.editorconfig` (`root = false`) covers everything there; outside `tests/` it doesn't apply. It sets: `CA1707` (underscore test names), `RS0016`, `RS0017`, `RS0036`, `RS0037` (no public-API tracking), `CA2007` and `CA1515` (xUnit needs public test classes; xUnit1030 forbids `ConfigureAwait(false)` in tests) are off; `GenerateDocumentationFile` stays `true` (IDE0005 needs it) with `CS1591` off.
- **`ConfigureAwait`:** `CA2007` owns it (`MA0004` is off) and, per #89, applies to libraries (`dotnet_code_quality.CA2007.output_kind = DynamicallyLinkedLibrary`). Library code writes `.ConfigureAwait(false)` on every `await`, every `await using` (`await using (x.ConfigureAwait(false)) { … }`, including `CreateAsyncScope()`) and every `await foreach`. Host templates use the same forms so code moved into a library stays compliant.
- **No TODO comments** (`MA0026`): finish the work or open a work item.
- **File name matches the type** (`MA0048`): one top-level type per file.
- Fix a warning; don't suppress it. A `#pragma warning disable` or `[SuppressMessage]` needs a `Justification` and is reviewed like an override.

## AOT scope (§4b.6)

| Entry point | Build |
|---|---|
| Hook binaries; `judge`, `sindri`, `scorer`, `bugfix-state` CLIs | NativeAOT |
| `aw-readiness`, `aw-packets` (standalone CLIs before the switch, §4c) | NativeAOT |
| `Bridge` hosts: REST (minimal API) and stdio MCP, two entry points | NativeAOT (decided: the MCP C# SDK passed the R1a spike with 0 warnings) |
| `Evidence` (ui-evidence) | Framework-dependent (Playwright for .NET has no NativeAOT) |

Every library project (see Project roles above: everything except `*.Tests`, `*.Testing`, `*.Host`, `*.Analyzers`) gets `IsAotCompatible=true` from the root build files; `Evidence`'s libraries opt out with `AwAotCompatible=false` and the reason. `PublishAot=true` only on the entry points above (a Host, or a small package's `App`), in their own project files. AOT publishing runs in its own CI job and in releases; local builds and tests are JIT.

## Seams (§4b.6, factory addendum §17.20–17.21)

The C# schemas carry the distribution and multiplayer seams from their first commit: idempotent events, the event envelope (`actor`, `tenant`, `ring`, `causation`, `correlation`, `trace`) and a transactional outbox (`outbox_commands`, `IOutbox` in `Aw.Kernel`). The other seams are **planned, not in `Aw.Kernel` yet**: `ILedgerStore` (with a shared contract suite), `IBus`, `ISecrets`, `ILease`, `ILauncher` and `IScheduler`, in `Aw.Kernel`/`Aw.Kernel.Telemetry`. Until a planned port lands, a package that needs it declares it in its own `Core`; it moves to Kernel when a second package needs it. `actor` is set by the host process, never by the caller.

**Every external technology sits behind a port, so it can be swapped.** Database, cache, queue or bus, file system,
processes, HTTP clients, git, containers, model CLIs, secrets, clock and ids: `Core` declares the port as an interface,
or uses a Kernel one that exists today (`IReadRepository`, `IUnitOfWork`/`IUnitOfWorkFactory`, `IOutbox`,
`IIdGenerator`, and the BCL's `TimeProvider`); a planned one (above) is declared in the package's `Core` until it lands.
`Core` and `Features` never reference a technology package or its types (`Microsoft.Data.*`, `Npgsql`, a Redis or
queue client, `System.Net.Http`, `System.IO` file access, `System.Diagnostics.Process`); they see only the port.
In the small shape this holds by namespace: `Aw.<Pkg>.App.Features.*` and `Commands` use no technology type, and only
`Composition` uses `Aw.<Pkg>.App.Infrastructure.*`. Check: `ARCH-PORT-001` and `ARCH-PORT-002` (`tests/Aw.Architecture.Tests`).

**Picking an adapter.** Each real implementation of a port is an `Infrastructure` adapter. A second technology for the
same port lives in the same `Infrastructure` project behind its own registration method, `Add<Name><Tech>Infrastructure()`
(e.g. `AddItemsSqliteInfrastructure(environment)` and `AddItemsPostgresInfrastructure(environment)`); its package comes in through a
`Directory.Packages.props` review. `Composition` in the `Host` picks the adapter by which `Add…()` it calls, and
`Infrastructure` reads that adapter's own settings from the environment `Composition` passes (DI above). `Aw.Kernel.Data`
is SQLite-specific today; a second engine needs its own data package, `Aw.Kernel.Data.<Engine>`, added when needed.

**Contracts.** Every port has a contract test suite that the in-memory fake and every real adapter pass, so a new
adapter (PostgreSQL instead of SQLite, a remote queue instead of the outbox table) is proven by the same tests. A Kernel
port's suite is an `Aw.Kernel.Testing` base: today only `ReadRepositoryContract`, with `IUnitOfWork` and `IOutbox`
contracts planned. A package's own port's suite lives in `Aw.<Pkg>.Testing` (spec §4b.5) and derives from a Kernel base
where one fits. BCL ports (`TimeProvider`, faked by `FakeTimeProvider`) are exempt.

Name ports for what the code needs (`IItemStore`, `IJobQueue`), not for the technology (`ISqliteItems`,
`IRedisCache`). Don't add a port for something with one obvious BCL implementation and no I/O (collections,
`JsonSerializer` through AwJson, `CancellationToken`).

## Adding code

- **New package:** `/new-package` (spec: `docs/csharp/scaffolds/new-package.md`).
- **New use case:** `/new-slice` (spec: `docs/csharp/scaffolds/new-slice.md`).
- **By hand**, the slice recipe is: (1) the failing test plus request and response records and a parameterless service stub whose `Handle` throws `NotSupportedException`, committed red but compiling; (2) the real assertion and the ports the service needs; (3) the service, with pure decisions on the domain type in `Core`; (4) the endpoint or CLI command in `Host`; (5) the explicit registration and the JSON context entries.
- Test rules: `.agents/rules/csharp-testing.md`.
