# `/new-package` — scaffold spec

Status: **spec only** (R1b). The generator isn't built yet; this file is its contract. When it ships it becomes a skill
under `skills/new-package/` whose `SKILL.md` points here, and the first two packages built with it are `Aw.Readiness`
and `Aw.Packets` (spec §10 R1b). Rules it must satisfy: `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`,
spec §4b.

## Purpose

Create a new C# package that passes every check on its first commit: it builds with zero warnings, its tests run in
the right lanes, its architecture tests pass, it is declared in `deps.yaml`, and its first test is red so the agent's
first move is the TDD step.

## Inputs

| Input | Form | Rule |
|---|---|---|
| `name` | PascalCase, e.g. `Readiness` | `^[A-Z][A-Za-z0-9]{2,30}$`; not `Common`, `Shared`, `Utils`, `Helpers`; no existing `src/<name>/` |
| `tier` | `0`–`3` | Must match the tier table in spec §4b.1a / `deps.yaml`. Tier 0 is reserved for `Aw.Kernel*` and `Redaction` |
| `shape` | `small` (Core + App) or `full` (Core, Features, Infrastructure, Host) | `small` unless the package is expected over ~1,500 lines |
| `host` | `cli`, `http` or `none` | `cli` → NativeAOT console `Main`; `http` → minimal API with `CreateSlimBuilder`; `none` → library |
| `depends` | list of package names | Each must be in a lower tier (checked against `deps.yaml`); declared as `project` edges to their `Core` |
| `firstSlice` | PascalCase use-case name, e.g. `CheckRepo` | Passed to `/new-slice` to generate the first red test |

Inputs come from the arguments, then **Ask the user** for anything missing. Refuse (exit with the rule id) on any rule
violation; never guess a tier.

## Generated files (`shape: full`, `host: cli`)

```
src/<Name>/
├── Aw.<Name>.Core/
│   ├── Aw.<Name>.Core.csproj          # IsAotCompatible=true; refs Aw.Kernel; BannedSymbols applies
│   ├── PublicAPI.Shipped.txt          # empty
│   ├── PublicAPI.Unshipped.txt        # empty
│   ├── Errors.cs                      # static class <Name>Errors — catalogue prefix from deps.yaml (e.g. RDY-)
│   └── DependencyInjection.cs         # public static IServiceCollection Add<Name>Core(this IServiceCollection)
├── Aw.<Name>.Features/
│   ├── Aw.<Name>.Features.csproj      # IsAotCompatible=true; refs Core, Aw.Kernel
│   └── DependencyInjection.cs         # Add<Name>Features()
├── Aw.<Name>.Infrastructure/
│   ├── Aw.<Name>.Infrastructure.csproj
│   └── DependencyInjection.cs         # Add<Name>Infrastructure(IConfiguration)
└── Aw.<Name>.Host/
    ├── Aw.<Name>.Host.csproj          # OutputType=Exe; PublishAot=true (cli); AssemblyName=<name lowercase>
    ├── Program.cs                     # composition root only: build services, run the command
    └── <Name>JsonContext.cs           # [JsonSerializable] source-generated context, Kernel JSON options
tests/
├── <Name>.Tests/
│   ├── <Name>.Tests.csproj            # xUnit v3 (or the R7 choice)
│   ├── HostTests.cs                   # DI validation: ValidateOnBuild + ValidateScopes (Lane=unit) — green
│   └── <FirstSlice>/<FirstSlice>Tests.cs   # from /new-slice — RED
└── <Name>.Testing/
    ├── <Name>.Testing.csproj
    └── Builders.cs                    # empty builder root: public static class A
```

`shape: small` replaces `Features`, `Infrastructure` and `Host` with one `Aw.<Name>.App` project (same contents, same
DI methods, `OutputType=Exe` when `host` isn't `none`). `host: http` adds `Endpoints.cs` with one `MapGroup("/<name>")`
and a loopback-only bind (spec §4d: the bind is set in code and a test tries to override it).

### Files edited

| File | Change |
|---|---|
| `AgenticWorkflow.slnx` | Adds every new project under `/src/<Name>/` and `/tests/` folders |
| `deps.yaml` | Adds the package with its `tier`, its error-code prefix, and one `project` edge per `depends` entry |
| `tests/Aw.Architecture.Tests/Packages.cs` | Registers the package's assemblies so every §4b.1 rule covers them |
| `Directory.Packages.props` | Nothing unless a new package version is needed (then it must be ≥ 14 days old) |

No other file changes. In particular the scaffold never touches `setup.sh`, `providers/`, `config/` or MCP
registration: nothing is installed before the cutover.

### Template: `Program.cs` (cli)

```csharp
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddSingleton(TimeProvider.System)
    .Add<Name>Core()
    .Add<Name>Features()
    .Add<Name>Infrastructure(Configuration.FromEnvironment());

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true,
});

return await Cli.Run(args, provider, <Name>JsonContext.Default);   // binds args, calls one service, maps Result → exit code
```

`Cli.Run` and `Configuration.FromEnvironment` come from `Aw.Kernel`; if they don't exist yet when the generator is
built, the generator's own PR adds them to `Aw.Kernel` first.

### Template: `HostTests.cs`

```csharp
public sealed class HostTests
{
    [Fact, Trait("Lane", "unit")]
    public void Container_builds_with_every_registration_valid()
    {
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider())
            .Add<Name>Core()
            .Add<Name>Features()
            .Add<Name>Infrastructure(TestConfiguration.Empty());

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider);
    }
}
```

## The red test

The last step calls `/new-slice <Name> <firstSlice>`, which writes `tests/<Name>.Tests/<FirstSlice>/<FirstSlice>Tests.cs`
with one failing test. The package is left **red on purpose**: `dotnet build` passes with zero warnings, `HostTests`
passes, the slice test fails with `NotImplementedException` from the generated service stub. The scaffold's commit
message is `test: <name> package scaffold with a red <firstSlice> test`.

## Commands the generator runs (in order)

```bash
dotnet restore --locked-mode || dotnet restore          # first restore writes packages.lock.json for new projects
dotnet build -warnaserror                               # must report 0 warnings, 0 errors
dotnet test --filter-trait "Lane=unit"                  # HostTests pass; the slice test fails (expected)
dotnet format --verify-no-changes
<deps.yaml checker>                                     # the new package and edges are declared
```

All of them run under the heavy-job lock.

## Done criteria

1. The commands above produce exactly: build 0 warnings; one failing test (the slice's), every other test green;
   format clean; `deps.yaml` check green.
2. `tests/Aw.Architecture.Tests` passes with the new assemblies registered.
3. `git status` shows only the files listed above.
4. The generator prints the next step: "Make `<FirstSlice>Tests` pass: implement the service in
   `Features/<FirstSlice>/` (see `/new-slice`)."

## Failure behavior

- Any check fails other than the expected red test → the generator reverts everything it created or edited (it
  records the file list before writing) and prints the rule id, the measured value and the fix.
- Re-running with the same `name` refuses: `NP-EXISTS: src/<Name> already exists`.
