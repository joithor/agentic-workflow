# `/new-package` — scaffold spec

Status: **spec only** (R1b). The generator isn't built yet; this file is its contract. When it ships it becomes a skill
under `skills/new-package/` whose `SKILL.md` points here, and the first two packages built with it are `Aw.Readiness`
and `Aw.Packets` (spec §10 R1b). Rules it must satisfy: `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`,
spec §4b, and the root build settings (`Directory.Build.props`, `.editorconfig`, `Directory.Packages.props` from the
.NET solution PR #89).

## Purpose

Create a new C# package that passes every check on its first commit: it builds with zero warnings, its tests run in
the right lanes, its architecture tests pass, it is declared in `deps.yaml`, and its first test is red so the agent's
first move is the TDD step.

## Inputs

| Input | Form | Rule |
|---|---|---|
| `name` | PascalCase, e.g. `Readiness` | `^[A-Z][A-Za-z0-9]{2,30}$`; not `Common`, `Shared`, `Utils`, `Helpers`; no existing `src/<Name>/` |
| `tier` | `0`–`3` | Must match the tier table in spec §4b.1a / `deps.yaml`. Tier 0 is reserved for `Aw.Kernel*` and `Redaction` |
| `errorPrefix` | e.g. `RDY` | `^[A-Z]{2,4}$`; unique across the error catalogues (`src/**/*Errors.cs`); never derived or guessed |
| `shape` | `small` (Core + App) or `full` (Core, Features, Infrastructure, Host) | `small` unless the package is expected over ~1,500 lines |
| `host` | `cli`, `http` or `none` | `cli` → NativeAOT console app; `http` → minimal API with `CreateSlimBuilder`; `none` → library |
| `depends` | list of package names | Each must be in a lower tier (checked against `deps.yaml`). Declared as `project` edges from this package's `Features` (`App` in the small shape) to their `Core`. Refused with `NP-FANOUT` if that project would hold more than 4 in-repo references |
| `firstSlice` | PascalCase use-case name, e.g. `CheckRepo`, plus its `behavior` | Passed to `/new-slice` to generate the first red test |

Inputs come from the arguments, then **Ask the user** for anything missing. Refuse (exit with the rule id) on any rule
violation; never guess a tier or a prefix.

## Generated files (`shape: full`, `host: cli`)

```
src/<Name>/
├── Aw.<Name>.Core/
│   ├── Aw.<Name>.Core.csproj          # refs Aw.Kernel + Microsoft.Extensions.DependencyInjection.Abstractions;
│   │                                  # InternalsVisibleTo Aw.<Name>.Tests, Aw.<Name>.Testing
│   ├── packages.lock.json             # written by the first restore
│   ├── PublicAPI.Shipped.txt          # empty
│   ├── PublicAPI.Unshipped.txt        # every public symbol below, one per line
│   ├── <Name>Errors.cs                # public static class <Name>Errors — codes "<errorPrefix>-001"…
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
    │                                  # InternalsVisibleTo Aw.<Name>.Tests
    ├── .editorconfig                  # root = false; [*.cs] dotnet_diagnostic.CA1031.severity = none  (composition root)
    ├── packages.lock.json
    ├── Program.cs                     # calls Composition.Compose, then the command table
    ├── Composition.cs                 # internal static class Composition { Compose(...) }
    ├── SettingsFromEnvironment.cs     # internal static: builds <Name>Settings from environment variables
    ├── Commands/CommandTable.cs       # internal static: command name → handler; /new-slice adds one line per slice
    └── <Name>JsonContext.cs           # internal partial class, [JsonSerializable] source-generated, Kernel JSON options
tests/
├── Aw.<Name>.Tests/
│   ├── Aw.<Name>.Tests.csproj         # xUnit v3 (or the R7 choice); refs every src project above
│   ├── packages.lock.json
│   ├── HostTests.cs                   # DI validation over Composition.Compose (Lane=unit) — green
│   └── <FirstSlice>/<FirstSlice>Tests.cs   # from /new-slice — RED
└── Aw.<Name>.Testing/
    ├── Aw.<Name>.Testing.csproj
    ├── packages.lock.json
    └── A.cs                           # builder root: public static class A
```

Every public type and member gets a `///` summary (CS1591 is an error in non-test projects) and a line in its project's
`PublicAPI.Unshipped.txt` (RS0016). Every file holds one top-level type named after the file (MA0048). Non-test code
awaits with `.ConfigureAwait(false)` (CA2007). Test projects need neither: `CA2007` and `CA1515` are off there (set in
`Directory.Build.props`/`.editorconfig` by #89).

`shape: small` replaces `Features`, `Infrastructure` and `Host` with one `Aw.<Name>.App` project: same files, same
`Add…()` methods, one `.editorconfig` lowering CA1031, `OutputType=Exe` when `host` isn't `none`.

`host: http` uses `Microsoft.NET.Sdk.Web` for Host (which turns the request-delegate generator on with `PublishAot`),
and adds:

```
Aw.<Name>.Host/
├── Endpoints/EndpointTable.cs         # internal static: MapGroup("/<name>"); /new-slice adds one Map<Slice>() line
├── HttpResults.cs                     # internal static: Result<T> → TypedResults / ProblemDetails (ToHttpResult)
└── LoopbackBinding.cs                 # binds 127.0.0.1 in code, ignoring ASPNETCORE_URLS (spec §4d)
tests/Aw.<Name>.Tests/LoopbackBindingTests.cs   # tries to override the bind (Lane=integration)
```

`HttpResults` lives in each HTTP host for now. The second HTTP package moves it into a tier-0 Kernel part (second use →
generalize); it can't go in `Aw.Kernel`, which is BCL-only.

### Files edited

| File | Change |
|---|---|
| `AgenticWorkflow.slnx` | Adds every new project under `/src/<Name>/` and `/tests/` folders |
| `deps.yaml` | Adds the package with its `tier`, and one `project` edge per `depends` entry |
| `tests/Aw.Architecture.Tests/Packages.cs` | Registers the package's assemblies so every §4b.1 rule covers them |
| `Directory.Packages.props` | Nothing unless a new package version is needed (then it must be ≥ 14 days old) |

No other file changes. In particular the scaffold never touches `setup.sh`, `providers/`, `config/` or MCP
registration: nothing is installed before the cutover.

### Template: `Composition.cs`

```csharp
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
using Microsoft.Extensions.DependencyInjection;

var services = Composition.Compose(new ServiceCollection(), SettingsFromEnvironment.Read(), TimeProvider.System);
await using var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
return await CommandTable.Run(args, provider, <Name>JsonContext.Default).ConfigureAwait(false);
```

`CommandTable.Run` binds the arguments, resolves one `IService<TReq, TRes>`, calls it, and maps the `Result` to an exit
code and output. It is generated in the Host; it moves into a Kernel part only when a second CLI package proves the shape.

### Template: `HostTests.cs`

```csharp
namespace Aw.<Name>.Tests;

public sealed class HostTests
{
    [Fact, Trait("Lane", "unit")]
    public void Container_builds_with_every_registration_valid()
    {
        var settings = new <Name>Settings(StateDirectory: Path.Combine(Path.GetTempPath(), "aw-<name>-di-test"));
        var services = Composition.Compose(new ServiceCollection(), settings, new FakeTimeProvider());

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider);
    }
}
```

## The red test

The last step calls `/new-slice <Name> <firstSlice> <behavior>`, which writes
`tests/Aw.<Name>.Tests/<FirstSlice>/<FirstSlice>Tests.cs` with one failing test and the stubs it needs to compile. The
package is left **red on purpose**: `dotnet build` passes with zero warnings, `HostTests` passes, the slice test fails
with `NotImplementedException` from the generated service stub. The scaffold's commit message is
`test: <name> package scaffold with a red <firstSlice> test`.

## Commands the generator runs (in order)

```bash
for p in src/<Name>/*/*.csproj tests/Aw.<Name>.*/*.csproj; do dotnet restore "$p"; done   # new projects only: writes their packages.lock.json
dotnet restore --locked-mode                            # whole solution; must pass — existing lock files never change here
dotnet build -warnaserror                               # must report 0 warnings, 0 errors
dotnet test --filter-trait "Lane=unit"                  # HostTests pass; the slice test fails (expected)
dotnet format --verify-no-changes
node scripts/check-deps.mjs                             # the new package and edges are declared (once the deps-graph PR #92 merges)
```

All of them run under the heavy-job lock.

## Done criteria

1. The commands above produce exactly: both restores succeed; build 0 warnings; one failing test (the slice's), every
   other test green; format clean; deps check green.
2. `tests/Aw.Architecture.Tests` passes with the new assemblies registered.
3. `git status` shows only the files listed above (including each new project's `packages.lock.json`).
4. The generator prints the next step: "Make `<FirstSlice>Tests` pass: replace the `Assert.Fail` with the real
   assertion, then implement the service in `Features/<FirstSlice>/` (see `/new-slice`)."

The generator's own test suite builds a scaffolded package against the real root files (`Directory.Build.props`,
`.editorconfig`, `Directory.Packages.props`) and asserts criterion 1, so template drift from the root settings fails
the generator's CI, not the first user.

## Failure behavior

- Any check fails other than the expected red test → the generator reverts everything it created or edited (it
  records the file list before writing) and prints the rule id, the measured value and the fix.
- Re-running with the same `name` refuses: `NP-EXISTS: src/<Name> already exists`. A taken `errorPrefix` refuses with
  `NP-PREFIX`.
