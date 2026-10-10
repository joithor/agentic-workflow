# `/new-package` — scaffold spec

Status: **built** (R1b). This file is the contract; the generator is `scripts/scaffold/new-package.mjs`, driven by the
`/new-package` skill (`skills/new-package/SKILL.md`), and the first two packages built with it are `Aw.Readiness` and
`Aw.Packets` (spec §10 R1b). Its tests: `scripts/tests/scaffold.test.sh` (inputs, refusals, files, revert; no dotnet)
and `scripts/scaffold/e2e.sh` (scaffold, build and test real packages; CI job "C# scaffold generators"). Rules it must satisfy: `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`,
spec §4b, and the root build settings (`Directory.Build.props`, `.editorconfig`, `tests/.editorconfig`,
`Directory.Packages.props` from the .NET solution PR #89).

## Purpose

Create a new C# package that passes every check on its first commit: it builds with zero warnings, its tests run in
the right lanes, its architecture tests pass, it is declared in `deps.yaml`, and its first test is red so the agent's
first move is the TDD step.

## Inputs

| Input | Form | Rule |
|---|---|---|
| `name` | PascalCase, e.g. `Readiness` | `^[A-Z][A-Za-z0-9]{2,30}$`; not `Common`, `Shared`, `Utils`, `Helpers`; no existing `src/<Name>/` |
| `tier` | `0`–`3` | Must match `deps.yaml` (the tier table of record). Tier 0 is reserved for `Aw.Kernel*`, `Redaction` and `Analyzers` |
| `errorPrefix` | e.g. `READINESS` | `^[A-Z0-9]{2,16}$`; codes are `AW-<errorPrefix>-NNN` (the form `ErrorCatalogue.Validate` accepts: `^(AW\|SND)-[A-Z0-9]+-[0-9]{3}$`; `SND-` is reserved for Sindri). Unique: no `"AW-<errorPrefix>-` in `src/**/*Errors.cs`; never derived or guessed |
| `note` | one line: what the package is for | Used for the `deps.yaml` node when the package isn't declared yet |
| `shape` | `small` (Core + App) or `full` (Core, Features, Infrastructure, Host) | `small` unless the package is expected over ~1,500 lines |
| `host` | `cli`, `http` or `none` | `cli` → NativeAOT console app; `http` → minimal API with `CreateSlimBuilder`; `none` → library. `http` needs `shape: full`: `shape: small` with `host: http` is refused with `NP-SMALL-HTTP` (the Web SDK's generated public `Program` would land in `App`, a library with `RS0016` on) |
| `depends` | list of package names | Each must be in a lower tier (checked against `deps.yaml`). Project edges from this package's `Features` (`App` in the small shape) to their `Core`. Refused with `NP-FANOUT` if that project would hold more than 4 in-repo references |
| `binary` | the executable name, e.g. `aw-readiness` | `^[a-z][a-z0-9-]{1,40}$`; default `<name lowercase>`. The `AssemblyName` of the Host (or the small shape's `App`) |
| `firstSlice` | PascalCase use-case name, e.g. `CheckRepo`, plus its `behavior` | Passed to `/new-slice` to generate the first red test, with the slice's `command` or `route`, `ids` and `ports` (`new-slice.md`) |

Inputs come from the arguments, then **Ask the user** for anything missing (the generator refuses a missing one with
`NP-INPUT`). Refuse (exit with the rule id) on any rule violation; never guess a tier or a prefix. The ids:
`NP-INPUT`, `NP-NAME` (format or a reserved name), `NP-EXISTS`, `NP-TIER` (not 1–3), `NP-DEPS-MISMATCH`, `NP-PREFIX`,
`NP-DEPENDS` (not a declared package, not a lower tier, or tier 0), `NP-FANOUT`, `NP-SMALL-HTTP`, `NP-PINS`, and the
first slice's `NS-*` ids.

```bash
node scripts/scaffold/new-package.mjs --name Readiness --tier 1 --error-prefix READINESS --shape small --host cli \
  --binary aw-readiness --first-slice CheckRepo --behavior reports_level_one_for_an_empty_repo --command check
``` `Readiness` and `Packets` are `host: cli` packages that ship before the
switch as the standalone NativeAOT CLIs `aw-readiness` and `aw-packets` (spec §4c).

## Before the scaffold: package pins

The templates need `Microsoft.Extensions.DependencyInjection.Abstractions` (Core, Features, Infrastructure),
`Microsoft.Extensions.DependencyInjection` (Host, tests), `Microsoft.Extensions.TimeProvider.Testing` (tests) and, for
`host: http`, `Microsoft.AspNetCore.Mvc.Testing` (integration tests), and `Vogen` (Core, for typed ids, R9); `xunit.v3` is already pinned. All of
them are pinned since R1b (the DI pair and `Mvc.Testing` at 10.0.12). If any isn't in
`Directory.Packages.props`, the generator **stops** and prints the pin commit to make first: add each `PackageVersion`
(pinned as `[x.y.z]`, at least 14 days old), refresh the lock files it changes (with `CentralPackageTransitivePinningEnabled`
a new pin can turn an existing project's `Transitive` entry into `CentralTransitive`), and commit that on its own.
The scaffold itself never edits `Directory.Packages.props`, so its whole-solution locked restore can't fail on NU1004.

## Generated files (`shape: full`, `host: cli`)

```
src/<Name>/
├── Aw.<Name>.Core/
│   ├── Aw.<Name>.Core.csproj          # refs Aw.Kernel + DI.Abstractions + Vogen (R9); InternalsVisibleTo Aw.<Name>.Tests, Aw.<Name>.Testing;
│   │                                  # NoWarn RS0041 (Vogen's Guid/int ids, below)
│   ├── packages.lock.json             # written by the first restore
│   ├── PublicAPI.Shipped.txt          # "#nullable enable" only
│   ├── PublicAPI.Unshipped.txt        # "#nullable enable", then every public symbol, annotated (below)
│   ├── VogenDefaults.cs               # the six-argument VogenDefaults block, verbatim (template below, R9)
│   ├── <Name>Errors.cs                # KernelErrors shape: public static readonly ErrorDefinition fields + All (below)
│   └── <Name>CoreServiceCollectionExtensions.cs   # public static Add<Name>Core(this IServiceCollection)
├── Aw.<Name>.Features/
│   ├── Aw.<Name>.Features.csproj      # refs Core, Aw.Kernel, DI.Abstractions, each `depends` Core;
│   │                                  # InternalsVisibleTo Aw.<Name>.Tests, Aw.<Name>.Testing
│   ├── packages.lock.json
│   ├── PublicAPI.Shipped.txt · PublicAPI.Unshipped.txt
│   └── <Name>FeaturesServiceCollectionExtensions.cs   # public static Add<Name>Features(this IServiceCollection): a block body
│                                      # ending in `return services;`, which /new-slice inserts its registrations before
├── Aw.<Name>.Infrastructure/
│   ├── Aw.<Name>.Infrastructure.csproj   # InternalsVisibleTo Aw.<Name>.Tests, Aw.<Name>.Testing
│   ├── .editorconfig                  # root = false; [*.cs] dotnet_diagnostic.CA1031.severity = none  (boundary adapters)
│   ├── packages.lock.json
│   ├── PublicAPI.Shipped.txt · PublicAPI.Unshipped.txt
│   ├── <Name>Settings.cs              # internal sealed record <Name>Settings(string StateDirectory)
│   ├── SettingsFromEnvironment.cs     # internal static: reads <Name>Settings from the environment Composition passes (template below)
│   └── <Name>InfrastructureServiceCollectionExtensions.cs   # public static Add<Name>Infrastructure(this IServiceCollection, Func<string, string?> environment)
└── Aw.<Name>.Host/
    ├── Aw.<Name>.Host.csproj          # OutputType=Exe; PublishAot=true; AssemblyName=<binary>;
    │                                  # refs DI; InternalsVisibleTo Aw.<Name>.Tests
    ├── .editorconfig                  # root = false; [*.cs] dotnet_diagnostic.CA1031.severity = none  (composition root)
    ├── packages.lock.json
    ├── Program.cs                     # calls Composition.Compose, then the command table
    ├── Composition.cs                 # internal static class Composition { Compose(...) }; the only file that uses Infrastructure
    ├── Commands/CommandTable.cs       # internal static: command name → handler; /new-slice adds one switch arm per slice
    ├── Commands/CommandOutput.cs      # internal static: Result → one JSON line on stdout (0) or the error on stderr (1); usage (2)
    └── <Name>JsonContext.cs           # internal partial class, [JsonSerializable] source-generated; used only through AwJson.CreateOptions
tests/
├── Aw.<Name>.Tests/
│   ├── Aw.<Name>.Tests.csproj         # xUnit v3 (R7) + DI + TimeProvider.Testing; <Using Include="Xunit" />;
│   │                                  # refs every src project above and Aw.<Name>.Testing
│   ├── packages.lock.json
│   ├── HostTests.cs                   # DI validation over Composition.Compose (Lane=unit) — green
│   ├── <Name>ErrorsTests.cs           # ErrorCatalogue.Validate(<Name>Errors.All) succeeds (Lane=unit) — green
│   └── <FirstSlice>/<FirstSlice>Tests.cs   # from /new-slice — RED
└── Aw.<Name>.Testing/
    ├── Aw.<Name>.Testing.csproj       # refs Core, Features (App in the small shape) and Aw.Kernel.Testing (builders, fakes,
    │                                  # contract bases); no package of its own (xUnit comes through Aw.Kernel.Testing)
    ├── packages.lock.json
    └── A.cs                           # builder root: public static class A
```

**Analyzer obligations the templates meet** (root settings from #89):

- Every public type and member has a `///` summary (`CS1591` is an error in non-test projects).
- Public API files, **library projects only** (`Core`, `Features`, `Infrastructure`, `App`; `Directory.Build.targets` turns these analyzers off for `*.Host`) (`RS0016`/`RS0017`/`RS0036`/`RS0037`): both files start with `#nullable enable`; `App`'s files hold only that line (its types are `internal`, below); Shipped has nothing
  else; Unshipped lists every public symbol with nullability annotations, e.g.
  `static Aw.<Name>.Core.<Name>CoreServiceCollectionExtensions.Add<Name>Core(this Microsoft.Extensions.DependencyInjection.IServiceCollection! services) -> Microsoft.Extensions.DependencyInjection.IServiceCollection!`.
  Records also list their compiler-generated members (about 12 lines each). The generator writes these lines from the
  `RS0016` code fix (`dotnet format analyzers <project> --diagnostics RS0016`), never by hand, and then checks the header.
  The code fix skips source-generated code, so a typed id's Vogen-generated members (about 17 lines for a string id)
  are recorded from the build's `RS0016` output instead, with the step in `docs/csharp/typed-ids.md` ("Public ids and
  PublicAPI files"), and reviewed. `/new-slice` runs both steps on `Core` when it adds ids.
- One top-level type per file, named after it (`MA0048`); no `TODO` comments (`MA0026`); no discarded results (`IDE0058`).
- Library code uses `.ConfigureAwait(false)` on every `await`, `await using` and `await foreach` (`CA2007`); the Host
  templates do the same.
- Usings: the generator emits exactly the namespaces each file uses (`IDE0005` and `IDE0161` are errors); the
  templates below show them.
- Test projects follow the test policy set by #89 in `tests/.editorconfig` (`root = false`): `CA1707`, `RS0016`,
  `RS0017`, `RS0036`, `RS0037`, `CA2007` and `CA1515` off; `GenerateDocumentationFile=true` with `CS1591` off. `MA0182`
  ("internal type is never used") is off for `*.Testing` projects only: their builders are used by `*.Tests` through
  `InternalsVisibleTo`, and in the small shape they are internal.
- Private fields are `_camelCase` (the root naming rule, `IDE1006`), static ones included; literal `bool` and `null`
  arguments are named (`MA0003`); string comparisons say their `StringComparison` (`MA0127`, `CA1309`).
- Vogen 8.0.7 wraps a `Guid` or `int` id's generated `ToString(string, IFormatProvider)` in `#nullable disable`, which
  `RS0041` (oblivious types in the public API) rejects with nothing to fix in user code, and `.editorconfig` sections
  don't reach source-generated files. So `Core` sets `<NoWarn>$(NoWarn);RS0041</NoWarn>` with that reason; `RS0016`
  still tracks the member (declared with the `~` marker the build reports).

**`shape: small`** replaces `Features`, `Infrastructure` and `Host` with one `Aw.<Name>.App` project: same files under
`Features/`, `Infrastructure/`, `Commands/`, same `Add…()` methods, `OutputType=Exe` when `host`
is `cli` (then `PublishAot=true` too; `http` is refused for `small`, see Inputs). `App` is a library under `Directory.Build.props`' project roles, so it gets
`IsAotCompatible=true` from the root. Nothing outside the package references `App`, and with `host: cli` it is an Exe,
where `CA1515` (an error under the root `.editorconfig`) rejects public types. So `App`'s types, its `Add…()` methods
and its slice request and response records are `internal`; `App` declares `InternalsVisibleTo Aw.<Name>.Tests,
Aw.<Name>.Testing`, and its PublicAPI files stay at `#nullable enable` only. `Aw.<Name>.Testing` declares
`InternalsVisibleTo Aw.<Name>.Tests`, because its builders over those internal records are internal too. Its `.editorconfig` lowers `CA1031` only
for the boundary folders:

```ini
root = false
[{Infrastructure/**.cs,Commands/**.cs,Program.cs,Composition.cs}]
dotnet_diagnostic.CA1031.severity = none
```

**`host: none`** generates no Host and no `HostTests`: there is no composition root to validate. The hosts that compose
the library carry the DI validation.

**`host: http`** uses `Microsoft.NET.Sdk.Web` for Host (its request-delegate generator runs with `PublishAot`), and adds:

```
Aw.<Name>.Host/
├── Endpoints/EndpointTable.cs         # internal static: MapGroup("/<name>"); /new-slice adds one Map<Slice>() line
├── HttpResults.cs                     # internal static: Result<T> → TypedResults / ProblemDetails (ToHttpResult)
├── LoopbackBinding.cs                 # the only listener: Listen(IPAddress.Loopback, port) (spec §4d, below)
├── LoopbackGuard.cs                   # middleware: non-loopback Host → 400; non-loopback browser Origin → 403
└── ValueObjectValidation.cs           # middleware: Vogen's ValueObjectValidationException → 400 (typed-ids.md step 4)
tests/Aw.<Name>.Tests/LoopbackBindingTests.cs   # every override vector, Host 400, Origin 403 (Lane=integration)
```

`LoopbackBinding.Build(args, environment, clock)` refuses any command-line argument (`ArgumentException`), builds the
host with `WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] })`, clears every configuration source
(`ASPNETCORE_*`/`DOTNET_*` environment variables reach the host's configuration otherwise, even with `Args = []`) and adds
an empty in-memory one (the host writes its own settings), pins `PreferHostingUrls` off, and listens with
`Listen(IPAddress.Loopback, port)` only. The port is `AW_<NAME>_PORT` from the environment `Composition` gets, `0` (a free
port) by default. `LoopbackBindingTests` starts the host once per
override vector and checks the only listener is `127.0.0.1:<port>`: `ASPNETCORE_URLS`, `DOTNET_URLS`,
`ASPNETCORE_HTTP_PORTS`, `ASPNETCORE_PREFERHOSTINGURLS`, `Kestrel__Endpoints__*` (each in the environment and on the
command line) and `--urls` (the R1a spike found two of these binding all interfaces in a default
`CreateSlimBuilder(args)` host). It also sends a request with `Host: example.com` (expects 400) and one with
`Origin: https://example.com` (expects 403); the command-line vector is `Build(["--urls", …])`, refused. The tests set
process environment variables, so they share an xUnit collection with parallelization off (`ProcessEnvironment.cs`).
No `HOST` variable, no `ALLOW_REMOTE` switch, no wildcard CORS. An http package's test project sets
`EnableRequestDelegateGenerator=true`, so its id route-binding tests (`new-slice.md`) compile through the
request-delegate generator, like the Host.

The http Host applies the same JSON profile in `ConfigureHttpJsonOptions` by copying the settings, converters and
resolver of `AwJson.CreateOptions(<Name>JsonContext.Default)` into `o.SerializerOptions`. It copies because
`AwJson.CreateOptions` returns read-only options (it calls `MakeReadOnly()`) and `SerializerOptions` has no setter to
assign them to; the instance it returns is still mutable during configuration.

`HttpResults` maps only a returned `Result`: values to `TypedResults`, errors to `ProblemDetails`. It never sees Vogen's
`ValueObjectValidationException`, which is thrown during binding or at an uninitialized id's first `.Value` (ASP.NET
passes `default(T)` for an empty `?id=` without calling `TryParse`), before any `Result` exists. That exception is
mapped to 400 by pipeline middleware, `ValueObjectValidation.cs`, registered in `Program.cs` with
`app.UseValueObjectValidation()` before the endpoint table is mapped:

```csharp
using Vogen;

namespace Aw.<Name>.Host;

/// <summary>Maps Vogen's validation exception (an uninitialized id from an empty route or query value) to 400.</summary>
internal static class ValueObjectValidation
{
    internal static WebApplication UseValueObjectValidation(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (ValueObjectValidationException) when (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        });
        return app;
    }
}
```

The body is `docs/csharp/typed-ids.md` step 4, proven under NativeAOT (500 without it, 400 with it). It is covered by
the route-binding test `/new-slice` adds before the first id binds a route (`tests/Aw.<Name>.Tests/Ids/<Id>RouteBindingTests.cs`,
Lane=integration): through `WebApplicationFactory`, with this middleware in the pipeline, valid route or query text →
200 with the id's value; text the type refuses → 400; an empty `?id=` → 400; and the endpoint's
`ParameterBindingMetadata` reports the id as route/query with `hasTryParse: true`, never as a body.

`HttpResults` lives in each HTTP host
for now; the second HTTP package moves it into a tier-0 Kernel part (second use → generalize). It can't go in
`Aw.Kernel`, which is BCL-only.

### Files edited

| File | Change |
|---|---|
| `AgenticWorkflow.slnx` | Adds every new project under `/src/<Name>/` and `/tests/` folders |
| `deps.yaml` | **The node:** if the package is declared already (as `Readiness` and `Packets` are), check that `tier`, `src` (`src/<Name>`) and `projectPrefix` (`Aw.<Name>`) match the inputs, else refuse `NP-DEPS-MISMATCH`. If it isn't declared, add it (`id: <Name>`, `kind: package`, `tier`, `projectPrefix: Aw.<Name>`, `ts: null`, `src: src/<Name>`, `note`). **The edges, one rule for both cases:** for every cross-package project edge the scaffold creates (always `<Name> → Kernel`, since `Core`, `Features` and `Infrastructure` reference `Aw.Kernel`, plus one per `depends` entry), a `planned` edge in `deps.yaml` becomes `current`, and a missing edge is added as `current` with a note. Planned edges the scaffold doesn't create stay `planned` (otherwise `DEPS-STALE-001`) |
| `tests/Aw.Architecture.Tests/Packages.cs` | One line per new production project, by project name (`"Aw.<Name>.Core",` … `"Aw.<Name>.Host",` even though the Host sets `AssemblyName`; `Core` and `App` in the small shape), appended to `Packages.Production`, so every §4b.1 rule covers them (`ARCH-REG-001` fails otherwise) |
| `tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj` | One `ProjectReference` per new production project in the `Registered packages` item group, so each registered project's assembly loads (`ARCH-REG-002`) |
| `tests/Aw.Architecture.Tests/packages.lock.json` | Rewritten by the unlocked restore below: it lists the new projects and any new transitive packages |

No other file changes. In particular the scaffold never touches `Directory.Packages.props` (see "Before the scaffold"),
`setup.sh`, `providers/`, `config/` or MCP registration: nothing is installed before the cutover.

### Template: `Composition.cs`

```csharp
using Aw.<Name>.Core;
using Aw.<Name>.Features;
using Aw.<Name>.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.<Name>.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .Add<Name>Core()
            .Add<Name>Features()
            .Add<Name>Infrastructure(environment);
}
```

`Composition` passes the environment, never a settings record: `Infrastructure` reads its own settings, so swapping a
technology (`.agents/rules/csharp.md`, Seams) changes `Infrastructure` and this one `Add…()` call, not the rest of
`Host`. When `Infrastructure` offers more than one adapter for a port (`Add<Name>SqliteInfrastructure(environment)`,
`Add<Name>PostgresInfrastructure(environment)`), this is where one is picked. The environment is a `Func<string, string?>` rather
than `IConfiguration`: the AOT CLI hosts reference only `Microsoft.Extensions.DependencyInjection`.

`using Microsoft.Extensions.DependencyInjection;` is emitted only for a `Microsoft.NET.Sdk` host (`host: cli`). For
`host: http` the Web SDK's implicit usings already include it, and a duplicate fails the build (`IDE0005` is an error).

### Template: `Program.cs` (cli)

```csharp
using Aw.<Name>.Host;
using Aw.<Name>.Host.Commands;
using Aw.Kernel.Json;
using Microsoft.Extensions.DependencyInjection;

var services = Composition.Compose(new ServiceCollection(), Environment.GetEnvironmentVariable, TimeProvider.System);
var json = AwJson.CreateOptions(<Name>JsonContext.Default);   // the one Kernel JSON profile
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
await using (provider.ConfigureAwait(false))
{
    return await CommandTable.Run(args, provider, json).ConfigureAwait(false);
}
```

`CommandTable.Run(string[] args, IServiceProvider provider, JsonSerializerOptions json)` binds the arguments, opens a scope (`var scope = provider.CreateAsyncScope(); await using
(scope.ConfigureAwait(false)) { … }`), resolves one `IService<TReq, TRes>`, calls it, and maps the `Result` to an exit
code and output (written with `AwJson.Serialize(response, json)`). It is generated in the Host; it moves into a Kernel part only when a second CLI package proves the shape.

### Template: `SettingsFromEnvironment.cs` (Infrastructure)

```csharp
namespace Aw.<Name>.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static <Name>Settings Read(Func<string, string?> environment)
        => new(StateDirectory: environment("AW_STATE_DIR") is { Length: > 0 } root
            ? Path.Combine(root, "<name>")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow", "<name>"));
}
```

`Add<Name>Infrastructure(environment)` calls `Read` once and registers the record as a singleton. The record and this
class are `internal`, so no `Host` or test code names them; an adapter that needs another setting (a connection string)
adds it here.

### Template: `VogenDefaults.cs` (Core, R9)

Verbatim from `docs/csharp/typed-ids.md` step 2. `TypedIdGuard.CheckDefaults` checks all six arguments
(for every registered assembly, `ARCH-ID-001` in `tests/Aw.Architecture.Tests`), and `TryFromGeneration.GenerateBoolMethod` is what `ITypedId`'s `static abstract bool TryFrom` binds to:

```csharp
using Vogen;

// No Vogen conversions: the System.Text.Json source generator can't see Vogen's converters and silently writes {}.
// The rest keeps an id's public surface to what ITypedId needs (Value, the bool TryFrom, IParsable).
[assembly: VogenDefaults(
    conversions: Conversions.None,
    systemTextJsonConverterFactoryGeneration: SystemTextJsonConverterFactoryGeneration.Omit,
    primitiveEqualityGeneration: PrimitiveEqualityGeneration.Omit,
    toPrimitiveCasting: CastOperator.None,
    fromPrimitiveCasting: CastOperator.None,
    tryFromGeneration: TryFromGeneration.GenerateBoolMethod)]
```

The Core csproj carries `<PackageReference Include="Vogen" />` (version from `Directory.Packages.props`); Vogen's
generated members are part of each id's public API, so the reference flows to every project that references this `Core`.

### Template: `<Name>Errors.cs`

```csharp
using Aw.Kernel;

namespace Aw.<Name>.Core;

/// <summary>The <Name> error catalogue (codes AW-<errorPrefix>-NNN).</summary>
public static class <Name>Errors
{
    /// <summary>AW-<errorPrefix>-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-<errorPrefix>-001", ErrorKind.Invalid, "<Name> input failed validation");

    /// <summary>Every <Name> error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
```

`<Name>ErrorsTests` asserts `ErrorCatalogue.Validate(<Name>Errors.All)` succeeds (format, uniqueness), the same shape as
`KernelErrors` in `Aw.Kernel`.

### Template: `HostTests.cs`

```csharp
using Aw.<Name>.Host;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.<Name>.Tests;

public sealed class HostTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_registered_service_resolves_in_a_scope()
    {
        var stateRoot = Path.Combine(Path.GetTempPath(), "aw-<name>-di-test");
        var services = Composition.Compose(
            new ServiceCollection(), name => name == "AW_STATE_DIR" ? stateRoot : null, new FakeTimeProvider());
        var serviceTypes = services
            .Select(d => d.ServiceType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IService<,>))
            .ToList();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotEmpty(serviceTypes);
        Assert.All(serviceTypes, t => Assert.NotNull(scope.ServiceProvider.GetRequiredService(t)));
    }
}
```

`ValidateOnBuild` can't see inside the decorator factories `/new-slice` registers, so the test resolves every
`IService<,>` in a scope: a missing inner service or validator registration fails here, not at the first command.

## The red test

The last step calls `/new-slice <Name> <firstSlice> <behavior>`, which writes
`tests/Aw.<Name>.Tests/<FirstSlice>/<FirstSlice>Tests.cs` with one failing test and the stubs it needs to compile. The
package is left **red on purpose**: `dotnet build` passes with zero warnings, `HostTests` passes, the slice test fails
with the `NotSupportedException` thrown by the parameterless service stub. The scaffold's commit message is
`test: <name> package scaffold with a red <firstSlice> test`.

## Commands the generator runs (in order)

```bash
for p in src/<Name>/*/*.csproj tests/Aw.<Name>.*/*.csproj tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj; do dotnet restore "$p" --force; done   # the new projects, plus the architecture tests that now reference them: writes their packages.lock.json (--force: a no-op cache from an earlier, reverted run would skip the lock file)
dotnet restore --locked-mode                            # whole solution; must pass — no other lock file changes here
dotnet format analyzers <each new library project> --diagnostics RS0016 --severity error   # writes the PublicAPI.Unshipped.txt lines (Core, Features, Infrastructure; not hosts or App)
dotnet build -warnaserror                               # must report 0 warnings, 0 errors
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8   # HostTests pass; the slice test fails (expected); exit 8 = empty lane in some project
dotnet format --verify-no-changes
node scripts/check-deps.mjs                             # the package and edges are declared
```

All of them run under the heavy-job lock (the generator takes it, or runs inside a caller's with `AW_HEAVY_LOCK_HELD=1`).
They run with the developer's environment: under `CI=true` (locked restore, the `AW0001` lock-file guard) restoring a
new project is refused by design, so `scripts/scaffold/e2e.sh` runs the generators with `CI` unset.

## Done criteria

1. The commands above produce exactly: both restores succeed; build 0 warnings; one failing test (the slice's, failing
   with the stub's `NotSupportedException`), every other test green; format clean; deps check green.
2. `tests/Aw.Architecture.Tests` passes with the new assemblies registered (the unit-lane run above includes it).
3. `git status` shows only the files listed above (including each new project's `packages.lock.json`); the generator
   fails with `SCAFFOLD-STRAY` otherwise.
4. The generator prints the next step: "Make `<FirstSlice>Tests` pass: replace the `Assert.Fail` with the real
   assertion, add the ports the service needs, then implement it in `Features/<FirstSlice>/` (see `/new-slice`)."

The generator's own test suite, `scripts/scaffold/e2e.sh`, scaffolds two packages — the first package `deps.yaml`
declares that has no `src/` yet, as `small`/`cli`, and an undeclared one as `full`/`http` — each in a throwaway worktree,
builds each against the real root files (`Directory.Build.props`, `.editorconfig`, `tests/.editorconfig`,
`Directory.Packages.props`) and asserts criterion 1, then adds a second slice to the first (after making its first
slice green) and runs the second's integration lane, so template drift from the root settings fails the generator's CI,
not the first user.

## Failure behavior

- Any check fails other than the expected red test → the generator reverts everything it created or edited (it
  records the file list before writing, and each lock file before a restore rewrites it) and prints the rule id
  (`SCAFFOLD-RESTORE`, `-PUBLICAPI`, `-BUILD`, `-RED`, `-FORMAT`, `-DEPS`, `-STRAY`), the measured value and the fix.
- Re-running with the same `name` refuses: `NP-EXISTS: src/<Name> already exists`. A taken `errorPrefix` refuses with
  `NP-PREFIX`; a missing package pin stops with `NP-PINS` and the pin commit to make; `shape: small` with `host: http`
  refuses with `NP-SMALL-HTTP`.
