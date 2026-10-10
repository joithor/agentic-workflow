# `/new-slice` — scaffold spec

Status: **spec only** (R1b). The generator isn't built yet; this file is its contract. When it ships it becomes a skill
under `skills/new-slice/` whose `SKILL.md` points here. Rules it must satisfy: `.agents/rules/csharp.md`,
`.agents/rules/csharp-testing.md`, spec §4b.2 and §4b.5.

## Purpose

Add one use case (a slice) to an existing package, starting with a failing test that names the behavior. The recipe
it encodes: **test → request and response → service → endpoint or CLI mapping → registration.**

## Inputs

| Input | Form | Rule |
|---|---|---|
| `package` | an existing package name, e.g. `Readiness` | `src/<package>/` exists and is in `deps.yaml` |
| `slice` | PascalCase verb-first use case, e.g. `CheckRepo`, `StartItem` | `^[A-Z][A-Za-z0-9]{2,40}$`; no existing `Features/<slice>/` folder |
| `behavior` | snake_case sentence for the first test, e.g. `reports_level_one_for_an_empty_repo` | becomes `<Slice>_<behavior>` |
| `surface` | `cli`, `http` or `none` | must match the package's host; `cli` adds a command, `http` adds an endpoint |
| `route` / `command` | e.g. `POST /items/{id}/start` or `check` | required for `http` / `cli` |
| `ports` | list of `Core` port interfaces the service needs (≤ 5) | each exists in `Core`, or the generator adds an empty interface plus its fake (and contract base) — never more than 5 |

**Ask the user** for missing inputs. A sixth port is refused with `NS-DEPS: services take at most 5 dependencies — split
the use case`.

## Generated files

Paths are for `shape: full`; in a `small` package, `Features/` lives inside `Aw.<Pkg>.App/`.

```
src/<Pkg>/Aw.<Pkg>.Features/<Slice>/
├── <Slice>Request.cs        # public sealed record <Slice>Request(<typed ids / value objects>)
├── <Slice>Response.cs       # public sealed record <Slice>Response(...)
├── <Slice>Service.cs        # internal sealed class, IService<<Slice>Request, <Slice>Response>, body throws NotImplementedException
├── <Slice>Validator.cs      # internal sealed class, IValidator<<Slice>Request>; empty rule set
└── <Slice>Mapping.cs        # cli: command binding → one service call → exit code; http: MapPost/MapGet → ToHttpResult()
tests/<Pkg>.Tests/<Slice>/
└── <Slice>Tests.cs          # the red test (below)
```

### Files edited

| File | Change |
|---|---|
| `src/<Pkg>/Aw.<Pkg>.Features/DependencyInjection.cs` | Registers the service (scoped), its validator, and the validation decorator, explicitly |
| `src/<Pkg>/Aw.<Pkg>.Host/Endpoints.cs` or the CLI command table | One line mapping the route or command to `<Slice>Mapping` |
| `src/<Pkg>/Aw.<Pkg>.Host/<Pkg>JsonContext.cs` | `[JsonSerializable(typeof(<Slice>Request))]` and `(typeof(<Slice>Response))` |
| `src/<Pkg>/Aw.<Pkg>.Core/PublicAPI.Unshipped.txt` | Only if a new port interface is added |
| `tests/<Pkg>.Testing/` | A fake (and contract base) per new port |

No other slice is read or referenced. Shared logic is never copied from another slice; if it's needed, the generator
stops and says: "Move it into `Core` first (second use → generalize)."

### Template: `<Slice>Service.cs`

```csharp
namespace Aw.<Pkg>.Features.<Slice>;

internal sealed class <Slice>Service(/* ports, ≤ 5 */ TimeProvider clock)
    : IService<<Slice>Request, <Slice>Response>
{
    public ValueTask<Result<<Slice>Response>> Handle(<Slice>Request request, CancellationToken ct)
        => throw new NotImplementedException("<Slice>: make <Slice>Tests pass");
}
```

### Template: `<Slice>Mapping.cs` (http)

```csharp
internal static class <Slice>Mapping
{
    public static RouteGroupBuilder Map<Slice>(this RouteGroupBuilder group)
    {
        group.MapPost("<route>", static async (<Slice>Body body,
                IService<<Slice>Request, <Slice>Response> service, CancellationToken ct)
            => (await service.Handle(body.ToRequest(), ct)).ToHttpResult());
        return group;
    }
}
```

The lambda binds, calls one service and maps the result. It has no branches.

### Template: the red test

```csharp
namespace Aw.<Pkg>.Tests.<Slice>;

public sealed class <Slice>Tests
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    [Fact, Trait("Lane", "unit")]
    public async Task <Slice>_<behavior>()
    {
        var service = new <Slice>Service(/* fakes from <Pkg>.Testing */ _clock);
        var request = A.<Slice>Request().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // TODO(red): assert the behavior named in the test
    }
}
```

`A.<Slice>Request()` is a builder the generator adds to `<Pkg>.Testing/Builders.cs` with deterministic defaults. The
test fails with `NotImplementedException`. The `TODO(red)` line is the agent's first edit: replace it with the real
assertion, run it, see it fail for the right reason, then implement.

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

## Failure behavior

- A check fails other than the expected red test → revert every created or edited file and print the rule id, the
  measured value and the fix.
- `NS-EXISTS` when the slice folder exists; `NS-SURFACE` when `surface` doesn't match the package's host.
