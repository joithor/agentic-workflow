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
| `route` / `command` | e.g. `POST /items/{id}/start` or `check` | required for `http` / `cli` |
| `ports` | list of `Core` port interfaces the service needs | each exists in `Core`, or the generator adds an empty interface plus its fake and contract base |

The service always takes `TimeProvider`, so it counts toward the limit: **`ports` + 1 ≤ 5** constructor dependencies
(§4b.2 split signal). More is refused with `NS-DEPS: <n> constructor dependencies (ports + TimeProvider); the limit is 5
— split the use case`. **Ask the user** for missing inputs.

## Generated files

Paths are for `shape: full`; in a `small` package, `Features/` and the Host files live inside `Aw.<Pkg>.App/`.

```
src/<Pkg>/Aw.<Pkg>.Features/<Slice>/
├── <Slice>Request.cs        # public sealed record <Slice>Request(<typed ids / value objects>), /// summary
├── <Slice>Response.cs       # public sealed record <Slice>Response(...), /// summary
├── <Slice>Service.cs        # internal sealed class, IService<<Slice>Request, <Slice>Response>, throws NotImplementedException
└── <Slice>Validator.cs      # internal sealed class, IValidator<<Slice>Request>; empty rule set
src/<Pkg>/Aw.<Pkg>.Host/
├── Endpoints/<Slice>Endpoint.cs   # http: internal static, Map<Slice>(RouteGroupBuilder) — bind, one service call, ToHttpResult()
├── Endpoints/<Slice>Body.cs       # http: internal sealed record <Slice>Body(...) with ToRequest()
└── Commands/<Slice>Command.cs     # cli: internal static, binds args → one service call → exit code
tests/Aw.<Pkg>.Tests/<Slice>/
└── <Slice>Tests.cs          # the red test (below)
tests/Aw.<Pkg>.Testing/
└── <Slice>RequestBuilder.cs # public sealed class with deterministic defaults; A.<Slice>Request() returns it
```

The endpoint lives in `Host`, never in `Features`: `Host` is the Web SDK project where the request-delegate generator
runs, so the minimal-API lambda is AOT-safe there, while in an `IsAotCompatible` `Features` library it would raise
IL2026/IL3050 (errors under warnings-as-errors). The test reaches the `internal` service through the
`InternalsVisibleTo` that `/new-package` declares.

### Files edited

| File | Change |
|---|---|
| `src/<Pkg>/Aw.<Pkg>.Features/<Pkg>FeaturesServiceCollectionExtensions.cs` | Registers the service (scoped), its validator, and the validation decorator, explicitly |
| `src/<Pkg>/Aw.<Pkg>.Features/PublicAPI.Unshipped.txt` | The request and response records and their members |
| `src/<Pkg>/Aw.<Pkg>.Host/Endpoints/EndpointTable.cs` or `Commands/CommandTable.cs` | One line mapping the route or command |
| `src/<Pkg>/Aw.<Pkg>.Host/<Pkg>JsonContext.cs` | `[JsonSerializable]` for `<Slice>Request`, `<Slice>Response`, and `<Slice>Body` (http) |
| `src/<Pkg>/Aw.<Pkg>.Core/PublicAPI.Unshipped.txt` | Only if a new port interface is added |
| `tests/Aw.<Pkg>.Testing/A.cs` | One factory method: `public static <Slice>RequestBuilder <Slice>Request() => new();` |
| `tests/Aw.<Pkg>.Testing/` | A fake (and contract base) per new port, one type per file |

No other slice is read or referenced. Shared logic is never copied from another slice; if it's needed, the generator
stops and says: "Move it into `Core` first (second use → generalize)."

### Template: `<Slice>Service.cs`

```csharp
namespace Aw.<Pkg>.Features.<Slice>;

internal sealed class <Slice>Service(/* ports */ TimeProvider clock)
    : IService<<Slice>Request, <Slice>Response>
{
    public ValueTask<Result<<Slice>Response>> Handle(<Slice>Request request, CancellationToken ct)
        => throw new NotImplementedException("<Slice>: make <Slice>Tests pass");
}
```

### Template: `Endpoints/<Slice>Endpoint.cs` (http)

```csharp
namespace Aw.<Pkg>.Host.Endpoints;

internal static class <Slice>Endpoint
{
    internal static RouteGroupBuilder Map<Slice>(this RouteGroupBuilder group)
    {
        group.MapPost("<route>", static async (<Slice>Body body,
                IService<<Slice>Request, <Slice>Response> service, CancellationToken ct)
            => (await service.Handle(body.ToRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
```

The lambda binds, calls one service and maps the result. It has no branches. `ToHttpResult()` is the Host's
`HttpResults` helper generated by `/new-package`.

### Template: the red test

```csharp
namespace Aw.<Pkg>.Tests.<Slice>;

public sealed class <Slice>Tests
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture));

    [Fact, Trait("Lane", "unit")]
    public async Task <Slice>_<behavior>()
    {
        var service = new <Slice>Service(/* fakes from Aw.<Pkg>.Testing */ _clock);
        var request = A.<Slice>Request().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert <behavior>");
    }
}
```

It fails with `NotImplementedException` today. The `Assert.Fail` line is the agent's first edit: replace it with the real
assertion, run it, see it fail for the right reason, then implement. No `TODO` comment (MA0026 is an error), and no
`ConfigureAwait` in the test (test projects have CA2007 off; xUnit1030).

## Commands the generator runs

```bash
dotnet build -warnaserror                                         # 0 warnings
dotnet test --filter-trait "Lane=unit"                            # exactly one failure: <Slice>Tests.<Slice>_<behavior>
dotnet format --verify-no-changes
```

Under the heavy-job lock. Commit: `test: red <Slice> test (<behavior>)`.

## Done criteria

1. Build 0 warnings; the new test is the only failing test; format clean; architecture tests green.
2. `git status` shows only the files listed above.
3. The generator prints the next steps in order: write the assertion → implement `<Slice>Service` (pure decisions on the
   domain type, not in the service) → add the integration test if the slice touches SQL → run the lane.

The generator's own test suite scaffolds a slice in a package built by `/new-package` and asserts criterion 1 against
the real root files.

## Failure behavior

- A check fails other than the expected red test → revert every created or edited file and print the rule id, the
  measured value and the fix.
- `NS-EXISTS` when the slice folder exists; `NS-SURFACE` when `surface` doesn't match the package's host; `NS-DEPS` as
  above.
