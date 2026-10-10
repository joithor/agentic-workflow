# `/new-slice` — scaffold spec

Status: **spec only** (R1b). The generator isn't built yet; this file is its contract. When it ships it becomes a skill
under `skills/new-slice/` whose `SKILL.md` points here. Rules it must satisfy: `.agents/rules/csharp.md`,
`.agents/rules/csharp-testing.md`, spec §4b.2 and §4b.5, and the root build settings from the .NET solution PR #89.

## Purpose

Add one use case (a slice) to an existing package, starting with a failing test that names the behavior. The recipe
it encodes: **test (with compiling stubs) → service → endpoint or CLI command → registration.**

## Inputs

| Input | Form | Rule |
|---|---|---|
| `package` | an existing package name, e.g. `Readiness` | `src/<package>/` exists and is in `deps.yaml` |
| `slice` | PascalCase verb-first use case, e.g. `CheckRepo`, `StartItem` | `^[A-Z][A-Za-z0-9]{2,40}$`; no existing `Features/<slice>/` folder |
| `behavior` | snake_case sentence for the first test, e.g. `reports_level_one_for_an_empty_repo` | becomes `<Slice>_<behavior>` |
| `surface` | `cli`, `http` or `none` | must match the package's host; `cli` adds a command, `http` adds an endpoint |
| `route` / `command` | e.g. `POST /items/{id}/start` or `check` | required for `http` / `cli`. Each `{name}` segment of `route` names a typed id (from `ids` or already in `Core`); ask the user which when it isn't clear |
| `ids` | list of new typed ids the request uses, each `Name:primitive`, e.g. `ItemId:string` (primitive is `string`, `Guid` or `int`) | not already declared in `Core`; existing ids are reused |
| `ports` | list of `Core` port interfaces the service needs | each exists in `Core`, or the generator adds an empty interface plus its fake and contract base |

The implemented service always takes `TimeProvider`, so it counts toward the limit: **`ports` + 1 ≤ 5** constructor
dependencies (§4b.2 split signal). The red stub takes none; the limit is checked on the declared `ports`. More is refused with `NS-DEPS: <n> constructor dependencies (ports + TimeProvider); the limit is 5
— split the use case`. **Ask the user** for missing inputs.

## Generated files

Paths are for `shape: full`; in a `small` package, `Features/` and the Host files live inside `Aw.<Pkg>.App/`.

```
src/<Pkg>/Aw.<Pkg>.Features/<Slice>/
├── <Slice>Request.cs        # public sealed record <Slice>Request(<typed ids / value objects>), /// summary
├── <Slice>Response.cs       # public sealed record <Slice>Response(...), /// summary
├── <Slice>Service.cs        # internal sealed class, no constructor parameters, Handle throws NotSupportedException
└── <Slice>Validator.cs      # internal sealed class, IValidator<<Slice>Request>; accepts everything until a rule is added
src/<Pkg>/Aw.<Pkg>.Host/
├── Endpoints/<Slice>Endpoint.cs   # http: internal static, Map<Slice>(RouteGroupBuilder) — bind route ids + body, one service call, ToHttpResult()
├── Endpoints/<Slice>Body.cs       # http: internal sealed record <Slice>Body(...) with ToRequest(<route ids>)
└── Commands/<Slice>Command.cs     # cli: internal static, binds args → one service call → exit code
tests/Aw.<Pkg>.Tests/<Slice>/
└── <Slice>Tests.cs          # the red test (below)
src/<Pkg>/Aw.<Pkg>.Core/Ids/      # one file per entry in `ids` (R9)
└── <Id>.cs                  # Vogen [ValueObject<prim>], [JsonConverter(typeof(<Conv><<Id>>))], ITypedId<<Id>, prim>, Validate
tests/Aw.<Pkg>.Tests/Ids/
├── <Id>Tests.cs             # JSON round-trip through AwJson; an invalid, missing or null value → AW-KERNEL-001 from AwJson.Deserialize; Parse/TryParse (Lane=unit)
└── <Id>RouteBindingTests.cs # http, when a `{name}` in `route` uses the id: route/query binding, refused → 400, `?id=` → 400 (Lane=integration)
tests/Aw.<Pkg>.Testing/
└── <Slice>RequestBuilder.cs # public sealed class with deterministic defaults; A.<Slice>Request() returns it
```

`<Conv>` by primitive: `string` → `StringIdJsonConverter`, `Guid` → `GuidIdJsonConverter`, `int` → `Int32IdJsonConverter`
(all in `Aw.Kernel.Json`). `<Id>Tests` reads with `AwJson.Deserialize`, which catches the converter's `JsonException`
and returns `AW-KERNEL-001` (`KernelErrors.Invalid`) in a failed `Result`, so the test asserts that error for an
invalid, a missing and a null value (spec §4b.3), never a thrown exception.

The endpoint lives in `Host`, never in `Features`: `Host` is the Web SDK project where the request-delegate generator
runs, so the minimal-API lambda is AOT-safe there, while in an `IsAotCompatible` `Features` library it would raise
IL2026/IL3050 (errors under warnings-as-errors). The test reaches the `internal` service through the
`InternalsVisibleTo` that `/new-package` declares.

### Files edited

| File | Change |
|---|---|
| `src/<Pkg>/Aw.<Pkg>.Features/<Pkg>FeaturesServiceCollectionExtensions.cs` | Registers the service, its validator, and the validation decorator as the `IService<,>` (template below) |
| `src/<Pkg>/Aw.<Pkg>.Features/PublicAPI.Unshipped.txt` | The request and response records and all their members, compiler-generated ones included, annotated — written by the `RS0016` code fix (`dotnet format analyzers <project> --diagnostics RS0016`) |
| `src/<Pkg>/Aw.<Pkg>.Host/Endpoints/EndpointTable.cs` or `Commands/CommandTable.cs` | One line mapping the route or command |
| `src/<Pkg>/Aw.<Pkg>.Host/<Pkg>JsonContext.cs` | `[JsonSerializable]` for `<Slice>Request`, `<Slice>Response`, and `<Slice>Body` (http) |
| `src/<Pkg>/Aw.<Pkg>.Core/PublicAPI.Unshipped.txt` | When `ids` is non-empty or a new port interface is added. Each new id adds its type (from the `RS0016` code fix) and its Vogen-generated members (about 17 lines for a string id), which the code fix skips: those are recorded from the build's `RS0016` output (`docs/csharp/typed-ids.md`, "Public ids and PublicAPI files") and reviewed |
| `tests/Aw.<Pkg>.Testing/A.cs` | One factory method: `public static <Slice>RequestBuilder <Slice>Request() => new();` |
| `tests/Aw.<Pkg>.Testing/` | A fake (and contract base) per new port, one type per file |

No other slice is read or referenced. Shared logic is never copied from another slice; if it's needed, the generator
stops and says: "Move it into `Core` first (second use → generalize)."

### Template: `<Slice>Service.cs`

```csharp
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.<Pkg>.Features.<Slice>;

internal sealed class <Slice>Service : IService<<Slice>Request, <Slice>Response>
{
    public ValueTask<Result<<Slice>Response>> Handle(<Slice>Request request, CancellationToken cancellationToken)
        => throw new NotSupportedException("<Slice>: make <Slice>Tests pass");
}
```

The stub has **no constructor parameters**: an unread parameter is `CS9113` and `NotImplementedException` is `MA0025`,
both errors under the root settings. The `ports` (and `TimeProvider`) arrive in the implement step; the generator has
already created any new port interface and its fake, and prints the constructor to write.

### Template: a typed id (`Core/Ids/<Id>.cs`, R9)

```csharp
using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.<Pkg>.Core.Ids;

/// <summary><Id>: what it identifies.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<<Id>>))]
public readonly partial struct <Id> : ITypedId<<Id>, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("<Id> must not be blank") : Validation.Ok;
}
```

`ITypedId<TSelf, TValue>` declares `IParsable<TSelf>` in user code, and the converter reads with `TryFrom` and throws
`JsonException` (AW-KERNEL-001 through `AwJson.Deserialize`, a 400 in ASP.NET body binding). `ITypedId` and the `StringIdJsonConverter`/`GuidIdJsonConverter`/`Int32IdJsonConverter` ship
with the typed-ids PR (#100, C3); the recipe is `docs/csharp/typed-ids.md`. The package's `VogenDefaults.cs` (from
`/new-package`) sets all six arguments of `docs/csharp/typed-ids.md` step 2, verbatim. **Before an id in this form binds a route** (an
http slice whose `route` has `{id}`), the generator adds an RDG route-binding test for it in
`tests/Aw.<Pkg>.Tests/Ids/<Id>RouteBindingTests.cs` (Lane=integration): an id in the `ITypedId` form is used behind an
endpoint only once that test passes; otherwise the id gets a hand-written `TryParse` instead. The test maps its own
`/items/{id}` and `/items?id=` endpoints with the id, runs the Host's `ValueObjectValidation` middleware (from
`/new-package`, `docs/csharp/typed-ids.md` step 4), and asserts through `WebApplicationFactory`: valid text → 200 with
the id's value; text the type refuses → 400; an empty `?id=` → 400; and the endpoint's `ParameterBindingMetadata`
reports the id as route/query with `hasTryParse: true`, so losing `IParsable` is a red test, not a production 500. The architecture test
(`TypedIdGuard`, #100) covers every id in the assembly.

### Template: `<Slice>Validator.cs`

```csharp
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.<Pkg>.Features.<Slice>;

internal sealed class <Slice>Validator : IValidator<<Slice>Request>
{
    public Result<Unit> Validate(<Slice>Request value) => Unit.Value;
}
```

### Template: the registration (added to `Add<Pkg>Features()`)

```csharp
services
    .AddScoped<<Slice>Service>()
    .AddScoped<IValidator<<Slice>Request>, <Slice>Validator>()
    .AddScoped<IService<<Slice>Request, <Slice>Response>>(static sp =>
        new ValidatingService<<Slice>Request, <Slice>Response>(
            sp.GetRequiredService<<Slice>Service>(),
            sp.GetRequiredService<IValidator<<Slice>Request>>()));
```

The edit also adds `using Aw.<Pkg>.Features.<Slice>;`, plus `using Aw.Kernel.Services;` when the file doesn't have it yet (the first slice); the file already has `Microsoft.Extensions.DependencyInjection` for `IServiceCollection`. M.E.DI has no decorators, so the decorator is a factory registration. Its `IServiceProvider` parameter is the one
exception to "no `IServiceProvider` outside `Host`" (`.agents/rules/csharp.md`, DI): it lives only inside the project's
own `Add…()` method and only resolves and constructs. `HostTests` resolves every `IService<,>` in a scope, which covers
what `ValidateOnBuild` can't see inside the factory.

### Template: `Endpoints/<Slice>Endpoint.cs` (http)

```csharp
using Aw.<Pkg>.Core.Ids;
using Aw.<Pkg>.Features.<Slice>;
using Aw.Kernel.Services;

namespace Aw.<Pkg>.Host.Endpoints;

internal static class <Slice>Endpoint
{
    internal static RouteGroupBuilder Map<Slice>(this RouteGroupBuilder group)
    {
        _ = group.MapPost("<route>", static async (<Id> id, <Slice>Body body,
                IService<<Slice>Request, <Slice>Response> service, CancellationToken ct)
            => (await service.Handle(body.ToRequest(id), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
```

Shown for `route` `POST /items/{id}/start`. For each `{name}` in `route` the lambda takes one typed-id parameter of
that name (`<Id> name`), in route order, before the body; the request is built from those ids plus the body with
`<Slice>Body.ToRequest(<Id> id, …)`. A route with no `{name}` segment gives `ToRequest()`. Each file emits exactly the namespaces its own types use (new-package.md): `<Slice>Endpoint.cs` emits `using Aw.<Pkg>.Core.Ids;` when `route` has a `{name}` segment, and `<Slice>Body.cs` emits it when `route` has a `{name}` segment or any body field's type is in `Aw.<Pkg>.Core.Ids`. A missing using fails with CS0246, and an unused one fails the build too (`IDE0005` is an error). The lambda binds, calls one
service and maps the result; it has no branches, and the discard keeps `IDE0058` quiet. Route text binds through the
id's `IParsable` `TryParse` (Vogen-generated, runs `Validate`; refused text → 400). Body JSON goes through the AwJson
profile the Host applies in `ConfigureHttpJsonOptions`. An uninitialized id's `ValueObjectValidationException` is
mapped to 400 by the Host's `ValueObjectValidation` middleware, not here. `ToHttpResult()` is the Host's `HttpResults`
helper generated by `/new-package`; it maps only the returned `Result`.

### Template: `Endpoints/<Slice>Body.cs` (http)

```csharp
using Aw.<Pkg>.Core.Ids;
using Aw.<Pkg>.Features.<Slice>;

namespace Aw.<Pkg>.Host.Endpoints;

internal sealed record <Slice>Body(<body fields>)
{
    internal <Slice>Request ToRequest(<Id> id) => new(id, <body fields>);
}
```

The body carries only what isn't in the route; route ids never repeat in it.

### Template: the red test

```csharp
using Aw.<Pkg>.Features.<Slice>;
using Aw.<Pkg>.Testing;

namespace Aw.<Pkg>.Tests.<Slice>;

public sealed class <Slice>Tests
{
    [Fact, Trait("Lane", "unit")]
    public async Task <Slice>_<behavior>()
    {
        var service = new <Slice>Service();
        var request = A.<Slice>Request().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert <behavior>");
    }
}
```

It fails with the stub's `NotSupportedException`. The `Assert.Fail` line is the agent's first edit: replace it with the
real assertion, add the fakes and a `FakeTimeProvider` as the service gains its ports, see the test fail for the right
reason, then implement. No `TODO` comment (`MA0026`), no `ConfigureAwait` in the test, underscore names allowed: the
test policy set by #89 in `tests/.editorconfig` turns off `CA1707`, `RS0016`, `RS0017`, `RS0036`, `RS0037`, `CA2007`
and `CA1515` for test projects, with `GenerateDocumentationFile=true` and `CS1591` off.

## Commands the generator runs

```bash
# when `ids` is non-empty or a port was added: the code fix on Core, then the generated id members from the build output
dotnet format analyzers src/<Pkg>/Aw.<Pkg>.Core --diagnostics RS0016       # each id's type and any new port
dotnet build src/<Pkg>/Aw.<Pkg>.Core 2>&1 \
  | sed -n "s/.*RS0016: Symbol '\(.*\)' is not part of the declared public API.*/\1/p" | sort -u \
  >> src/<Pkg>/Aw.<Pkg>.Core/PublicAPI.Unshipped.txt                      # Vogen's generated members (typed-ids.md); review them
dotnet format analyzers src/<Pkg>/Aw.<Pkg>.Features --diagnostics RS0016   # PublicAPI lines for the new records
dotnet build -warnaserror                                         # 0 warnings
dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8   # exactly one failure: <Slice>Tests.<Slice>_<behavior>; exit 8 = empty lane in some project
dotnet format --verify-no-changes
```

Under the heavy-job lock. Commit: `test: red <Slice> test (<behavior>)`.

## Done criteria

1. Build 0 warnings; the new test is the only failing test; format clean; architecture tests green.
2. `git status` shows only the files listed above.
3. The generator prints the next steps in order: write the assertion → add the ports to the constructor → implement `<Slice>Service` (pure decisions on the
   domain type, not in the service) → add the integration test if the slice touches SQL → run the lane.

The generator's own test suite scaffolds a slice in a package built by `/new-package` and asserts criterion 1 against
the real root files.

## Failure behavior

- A check fails other than the expected red test → revert every created or edited file and print the rule id, the
  measured value and the fix.
- `NS-EXISTS` when the slice folder exists; `NS-SURFACE` when `surface` doesn't match the package's host; `NS-DEPS` as
  above.
