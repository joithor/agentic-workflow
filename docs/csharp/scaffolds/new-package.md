# `/new-package` — scaffold spec

Status: **spec only** (R1b). The generator isn't built yet; this file is its contract. When it ships it becomes a skill
under `skills/new-package/` whose `SKILL.md` points here, and the first two packages built with it are `Aw.Readiness`
and `Aw.Packets` (spec §10 R1b). Rules it must satisfy: `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`,
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
| `host` | `cli`, `http` or `none` | `cli` → NativeAOT console app; `http` → minimal API with `CreateSlimBuilder`; `none` → library |
| `depends` | list of package names | Each must be in a lower tier (checked against `deps.yaml`). Project edges from this package's `Features` (`App` in the small shape) to their `Core`. Refused with `NP-FANOUT` if that project would hold more than 4 in-repo references |
| `firstSlice` | PascalCase use-case name, e.g. `CheckRepo`, plus its `behavior` | Passed to `/new-slice` to generate the first red test |

Inputs come from the arguments, then **Ask the user** for anything missing. Refuse (exit with the rule id) on any rule
violation; never guess a tier or a prefix. `Readiness` and `Packets` are `host: cli` packages that ship before the
switch as the standalone NativeAOT CLIs `aw-readiness` and `aw-packets` (spec §4c).

## Before the scaffold: package pins

The templates need `Microsoft.Extensions.DependencyInjection.Abstractions` (Core, Features, Infrastructure),
`Microsoft.Extensions.DependencyInjection` (Host, tests), `Microsoft.Extensions.TimeProvider.Testing` (tests) and, for
`host: http`, `Microsoft.AspNetCore.Mvc.Testing` (integration tests), and `Vogen` (Core, for typed ids, R9); `xunit.v3` is already pinned. If any isn't in
`Directory.Packages.props`, the generator **stops** and prints the pin commit to make first: add each `PackageVersion`
(pinned as `[x.y.z]`, at least 14 days old), refresh the lock files it changes (with `CentralPackageTransitivePinningEnabled`
a new pin can turn an existing project's `Transitive` entry into `CentralTransitive`), and commit that on its own.
The scaffold itself never edits `Directory.Packages.props`, so its whole-solution locked restore can't fail on NU1004.

## Generated files (`shape: full`, `host: cli`)

```
src/<Name>/
├── Aw.<Name>.Core/
│   ├── Aw.<Name>.Core.csproj          # refs Aw.Kernel + DI.Abstractions; InternalsVisibleTo Aw.<Name>.Tests, Aw.<Name>.Testing
│   ├── packages.lock.json             # written by the first restore
│   ├── PublicAPI.Shipped.txt          # "#nullable enable" only
│   ├── PublicAPI.Unshipped.txt        # "#nullable enable", then every public symbol, annotated (below)
│   ├── VogenDefaults.cs               # [assembly: VogenDefaults(conversions: Conversions.None, …Omit, …)] (R9)
│   ├── <Name>Errors.cs                # KernelErrors shape: public static readonly ErrorDefinition fields + All (below)
│   └── <Name>CoreServiceCollectionExtensions.cs   # public static Add<Name>Core(this IServiceCollection)
├── Aw.<Name>.Features/
│   ├── Aw.<Name>.Features.csproj      # refs Core, Aw.Kernel, DI.Abstractions, each `depends` Core;
│   │                                  # InternalsVisibleTo Aw.<Name>.Tests, Aw.<Name>.Testing
│   ├── packages.lock.json
│   ├── PublicAPI.Shipped.txt · PublicAPI.Unshipped.txt
│   └── <Name>FeaturesServiceCollectionExtensions.cs   # public static Add<Name>Features(this IServiceCollection)
├── Aw.<Name>.Infrastructure/
│   ├── Aw.<Name>.Infrastructure.csproj   # InternalsVisibleTo Aw.<Name>.Tests, Aw.<Name>.Testing
│   ├── .editorconfig                  # root = false; [*.cs] dotnet_diagnostic.CA1031.severity = none  (boundary adapters)
│   ├── packages.lock.json
│   ├── PublicAPI.Shipped.txt · PublicAPI.Unshipped.txt
│   ├── <Name>Settings.cs              # public sealed record <Name>Settings(string StateDirectory)
│   └── <Name>InfrastructureServiceCollectionExtensions.cs   # public static Add<Name>Infrastructure(this IServiceCollection, <Name>Settings)
└── Aw.<Name>.Host/
    ├── Aw.<Name>.Host.csproj          # OutputType=Exe; PublishAot=true (cli); AssemblyName=<name lowercase>;
    │                                  # refs DI; InternalsVisibleTo Aw.<Name>.Tests
    ├── .editorconfig                  # root = false; [*.cs] dotnet_diagnostic.CA1031.severity = none  (composition root)
    ├── packages.lock.json
    ├── Program.cs                     # calls Composition.Compose, then the command table
    ├── Composition.cs                 # internal static class Composition { Compose(...) }
    ├── SettingsFromEnvironment.cs     # internal static: builds <Name>Settings from environment variables
    ├── Commands/CommandTable.cs       # internal static: command name → handler; /new-slice adds one line per slice
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
    ├── Aw.<Name>.Testing.csproj       # refs Core and Features (builders, fakes)
    ├── packages.lock.json
    └── A.cs                           # builder root: public static class A
```

**Analyzer obligations the templates meet** (root settings from #89):

- Every public type and member has a `///` summary (`CS1591` is an error in non-test projects).
- Public API files, **library projects only** (`Core`, `Features`, `Infrastructure`, `App`; `Directory.Build.targets` turns these analyzers off for `*.Host`) (`RS0016`/`RS0017`/`RS0036`/`RS0037`): both files start with `#nullable enable`; Shipped has nothing
  else; Unshipped lists every public symbol with nullability annotations, e.g.
  `static Aw.<Name>.Core.<Name>CoreServiceCollectionExtensions.Add<Name>Core(this Microsoft.Extensions.DependencyInjection.IServiceCollection! services) -> Microsoft.Extensions.DependencyInjection.IServiceCollection!`.
  Records also list their compiler-generated members (about 12 lines each). The generator writes these lines from the
  `RS0016` code fix (`dotnet format analyzers <project> --diagnostics RS0016`), never by hand, and then checks the header.
- One top-level type per file, named after it (`MA0048`); no `TODO` comments (`MA0026`); no discarded results (`IDE0058`).
- Library code uses `.ConfigureAwait(false)` on every `await`, `await using` and `await foreach` (`CA2007`); the Host
  templates do the same.
- Usings: the generator emits exactly the namespaces each file uses (`IDE0005` and `IDE0161` are errors); the
  templates below show them.
- Test projects follow the test policy set by #89 in `tests/.editorconfig` (`root = false`): `CA1707`, `RS0016`,
  `RS0017`, `RS0036`, `RS0037`, `CA2007` and `CA1515` off; `GenerateDocumentationFile=true` with `CS1591` off.

**`shape: small`** replaces `Features`, `Infrastructure` and `Host` with one `Aw.<Name>.App` project: same files under
`Features/`, `Infrastructure/`, `Commands/` (or `Endpoints/`), same `Add…()` methods, `OutputType=Exe` when `host`
isn't `none` (then `PublishAot=true` too). `App` is a library under `Directory.Build.props`' project roles, so it gets
`IsAotCompatible=true` from the root. Its `.editorconfig` lowers `CA1031` only for the boundary folders:

```ini
root = false
[{Infrastructure/**.cs,Commands/**.cs,Endpoints/**.cs,Program.cs,Composition.cs}]
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
└── LoopbackGuard.cs                   # middleware: non-loopback Host → 400; non-loopback browser Origin → 403
tests/Aw.<Name>.Tests/LoopbackBindingTests.cs   # every override vector, Host 400, Origin 403 (Lane=integration)
```

`LoopbackBinding` builds the host with `WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] })`: no
command-line args and no environment variables reach the web host's configuration, `PreferHostingUrls` is pinned off,
and Kestrel listens with `Listen(IPAddress.Loopback, port)` only. `LoopbackBindingTests` starts the host once per
override vector and checks the only listener is `127.0.0.1:<port>`: `ASPNETCORE_URLS`, `DOTNET_URLS`,
`ASPNETCORE_HTTP_PORTS`, `ASPNETCORE_PREFERHOSTINGURLS`, `Kestrel__Endpoints__*` (each in the environment and on the
command line) and `--urls` (the R1a spike found two of these binding all interfaces in a default
`CreateSlimBuilder(args)` host). It also sends a request with `Host: example.com` (expects 400) and one with
`Origin: https://example.com` (expects 403). No `HOST` variable, no `ALLOW_REMOTE` switch, no wildcard CORS.

The http Host applies the same JSON profile in `ConfigureHttpJsonOptions`: `o.SerializerOptions` is read-only, so it
copies the settings and resolver of `AwJson.CreateOptions(<Name>JsonContext.Default)` into it. And
`HttpResults` maps Vogen's validation exception to a 400 as well as `Result` errors to `ProblemDetails`.

`HttpResults` lives in each HTTP host
for now; the second HTTP package moves it into a tier-0 Kernel part (second use → generalize). It can't go in
`Aw.Kernel`, which is BCL-only.

### Files edited

| File | Change |
|---|---|
| `AgenticWorkflow.slnx` | Adds every new project under `/src/<Name>/` and `/tests/` folders |
| `deps.yaml` | **The node:** if the package is declared already (as `Readiness` and `Packets` are), check that `tier`, `src` (`src/<Name>`) and `projectPrefix` (`Aw.<Name>`) match the inputs, else refuse `NP-DEPS-MISMATCH`. If it isn't declared, add it (`id: <Name>`, `kind: package`, `tier`, `projectPrefix: Aw.<Name>`, `ts: null`, `src: src/<Name>`, `note`). **The edges, one rule for both cases:** for every cross-package project edge the scaffold creates (always `<Name> → Kernel`, since `Core`, `Features` and `Infrastructure` reference `Aw.Kernel`, plus one per `depends` entry), a `planned` edge in `deps.yaml` becomes `current`, and a missing edge is added as `current` with a note. Planned edges the scaffold doesn't create stay `planned` (otherwise `DEPS-STALE-001`) |
| `tests/Aw.Architecture.Tests/Packages.cs` | Registers the package's assemblies so every §4b.1 rule covers them |

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
    internal static IServiceCollection Compose(IServiceCollection services, <Name>Settings settings, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .Add<Name>Core()
            .Add<Name>Features()
            .Add<Name>Infrastructure(settings);
}
```

### Template: `Program.cs` (cli)

```csharp
using Aw.<Name>.Host;
using Aw.<Name>.Host.Commands;
using Aw.Kernel.Json;
using Microsoft.Extensions.DependencyInjection;

var services = Composition.Compose(new ServiceCollection(), SettingsFromEnvironment.Read(), TimeProvider.System);
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
using Aw.<Name>.Infrastructure;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.<Name>.Tests;

public sealed class HostTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_registered_service_resolves_in_a_scope()
    {
        var settings = new <Name>Settings(StateDirectory: Path.Combine(Path.GetTempPath(), "aw-<name>-di-test"));
        var services = Composition.Compose(new ServiceCollection(), settings, new FakeTimeProvider());
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
for p in src/<Name>/*/*.csproj tests/Aw.<Name>.*/*.csproj; do dotnet restore "$p"; done   # new projects only: writes their packages.lock.json
dotnet restore --locked-mode                            # whole solution; must pass — existing lock files never change here
dotnet format analyzers <each new library project> --diagnostics RS0016   # writes the PublicAPI.Unshipped.txt lines (not hosts)
dotnet build -warnaserror                               # must report 0 warnings, 0 errors
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8   # HostTests pass; the slice test fails (expected); exit 8 = empty lane in some project
dotnet format --verify-no-changes
node scripts/check-deps.mjs                             # the package and edges are declared (once the deps-graph PR #92 merges)
```

All of them run under the heavy-job lock.

## Done criteria

1. The commands above produce exactly: both restores succeed; build 0 warnings; one failing test (the slice's), every
   other test green; format clean; deps check green.
2. `tests/Aw.Architecture.Tests` passes with the new assemblies registered.
3. `git status` shows only the files listed above (including each new project's `packages.lock.json`).
4. The generator prints the next step: "Make `<FirstSlice>Tests` pass: replace the `Assert.Fail` with the real
   assertion, add the ports the service needs, then implement it in `Features/<FirstSlice>/` (see `/new-slice`)."

The generator's own test suite scaffolds two packages — one declared in `deps.yaml` already and one that isn't — and
builds each against the real root files (`Directory.Build.props`,
`.editorconfig`, `tests/.editorconfig`, `Directory.Packages.props`) and asserts criterion 1, so template drift from the
root settings fails the generator's CI, not the first user.

## Failure behavior

- Any check fails other than the expected red test → the generator reverts everything it created or edited (it
  records the file list before writing) and prints the rule id, the measured value and the fix.
- Re-running with the same `name` refuses: `NP-EXISTS: src/<Name> already exists`. A taken `errorPrefix` refuses with
  `NP-PREFIX`; a missing package pin stops with `NP-PINS` and the pin commit to make.
