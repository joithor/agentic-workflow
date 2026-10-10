# Typed ids (decision R9)

Every id and constrained primitive in a package's public `Core` and `Features` signatures is a typed id
(spec §4b.3): `ItemId`, `RunId`, `TokenBudget`, never a raw `string`, `Guid` or `int`. The AOT spike
(`docs/readiness/aot-spike.md`, R9) chose **Vogen with `Conversions.None`, plus a JSON converter named in user code**,
and this recipe adds the `ITypedId` contract that ties them to `Aw.Kernel`.

## What guarantees what

| Guarantee | Where it holds | Enforced by |
|---|---|---|
| No `default` literal for an id (`default(ItemId)`, `ItemId x = default`) | compile time | Vogen's analyzer, VOG009 |
| A non-nullable id property without an initializer, `required` or a constructor is flagged | compile time, in projects that reference Vogen | Vogen's analyzer, VOG038 |
| A JSON value the type refuses (null, `""`, wrong token, uppercase Guid hex, a value `Validate` rejects) never becomes an id | every JSON boundary, values and dictionary keys | the Kernel converter: parse, `TryFrom`, else `JsonException` → AW-KERNEL-001 (through the Kernel JSON profile) or a 400 (ASP.NET body binding) |
| A missing id property never becomes an uninitialized id | every JSON boundary | the JSON profile's `RespectRequiredConstructorParameters`; ids that aren't constructor parameters need `required` or `[JsonRequired]` |
| Route or query text the type refuses never becomes an id | minimal-API routes and query strings | `IParsable<T>.TryParse` (Vogen generates it and runs `Validate`) → 400. Route text is parsed by `Guid.TryParse`, so it accepts any Guid layout and case, unlike JSON |
| An empty query value (`?id=`) never reaches a handler as an id | minimal-API hosts | the Host's middleware maps Vogen's `ValueObjectValidationException` to 400 (below) |
| An id writes exactly as its primitive would, in one canonical form | every JSON write | the Kernel converters (strings as the profile writes a plain string; Guids lowercase `D`; int keys canonical), so a record read and written back keeps its bytes |

What is **not** guaranteed: an uninitialized id can still exist at run time through array allocation
(`new ItemId[3]`) or `Unsafe`. Reading `.Value` on one throws (Debug and Release). Nothing at a boundary produces one.

## The recipe

**1. Add Vogen to the project that declares ids** (the package's `Core`; the spec's Core row needs to allow it, see
the PR). Vogen's generated members are part of the id's public API, so the reference flows to every project
that references this `Core`:

```xml
<PackageReference Include="Vogen" />   <!-- version pinned in Directory.Packages.props -->
```

**2. One `VogenDefaults.cs` per assembly that declares ids:**

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

**3. One file per id, named after it (MA0048):**

```csharp
using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Judge.Core;

/// <summary>A judge decision.</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]          // no IComparable without operators (MA0097)
[JsonConverter(typeof(GuidIdJsonConverter<DecisionId>))]            // in user code, where STJ's generator sees it
public readonly partial struct DecisionId : ITypedId<DecisionId, Guid>
{
    private static Validation Validate(Guid value) =>               // required: Vogen's AddValidationMethod hint is an error
        value == Guid.Empty ? Validation.Invalid("DecisionId must not be empty") : Validation.Ok;
}
```

`comparison: Omit` has to be on each id (`VogenDefaults` has no `comparison` parameter in Vogen 8.0.7). The cost:
ids don't sort on their own; `OrderBy(x => x.Id.Value)` or a comparer when order matters.

**`ITypedId` is load-bearing, not decoration.** It declares `IParsable<TSelf>` in user code, which is what the
request-delegate generator sees. A Vogen id without it gets `IsParsable = False` and is bound as a JSON body, so
`/items/{id}` fails at run time under NativeAOT (the AOT spike's 500; `proof/readiness-c3-typed-ids/raw/rdg-control.txt`).
With it, binding goes through Vogen's generated, validating `TryParse`; nothing needs hand-writing.

**Committed route-binding test (required before an id is used behind an endpoint).** The proof above is a throwaway
probe. The first package with `host: http` (or an `Aw.Host.Tests` project, if one exists first) commits a test that
compiles a minimal API through the request-delegate generator, with `ITypedId` ids as a route parameter
(`/items/{id}`) and a query parameter (`/items?id=`), and the host's 400 middleware. Through `WebApplicationFactory`
it asserts: valid text → 200 with the id's value; text the type refuses → 400; an empty `?id=` → 400; and it fails if
the id binds as a body (the endpoint's `ParameterBindingMetadata` reports the parameter as route/query with
`hasTryParse: true`), so losing `IParsable` turns into a red test, not a production 500.

| Backing type | Converter | JSON value | Dictionary key |
|---|---|---|---|
| `string` | `StringIdJsonConverter<TId>` | a string, written as the profile writes any string; `""` is refused | the string |
| `Guid` | `GuidIdJsonConverter<TId>` | a string, lowercase `D`; any other layout or uppercase is refused | the same form |
| `int` | `Int32IdJsonConverter<TId>` | a number | `"1200"`; `"01"`, `"+1"`, `"1.5"` are refused |

New ids come from the id port, never from `Guid.NewGuid` (banned in Core and Kernel): `generator.New<DecisionId>()`.

**4. Minimal-API hosts map an uninitialized id to 400.** ASP.NET passes `default(T)` for a parsable parameter whose
value is empty (`?id=`) without calling `TryParse`, so the handler gets an uninitialized id and its first `.Value`
throws. Proven under NativeAOT (`raw/aot-smoke.txt`: 500 without this, 400 with it):

```csharp
app.Use(async (context, next) =>
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
```

## SQL

`Aw.Kernel.Data` provides the binders and nothing id-specific; each package's `IRowMapper<T>` owns its ids. Bind
`id.Value` (a `Guid` through Kernel.Data's `Guid` overload, which stores lowercase `D` TEXT, the same canonical form as
JSON; never `AddWithValue` with a `Guid`, which Microsoft.Data.Sqlite stores as a 16-byte BLOB). Read back with
`DecisionId.TryFrom(reader.GetGuid(i), out var id)`; a refused value is a mapper bug and throws.

## The guard

`TypedIdGuard` (`tests/Aw.Kernel.Tests/TypedIds/TypedIdGuard.cs`) finds the silent failures. It matches Vogen's
attributes by name, never references Vogen, and runs over any assembly:

- `CheckDefaults(assembly)`: the assembly has `VogenDefaults` with every argument above.
- `CheckTypes(types)`: every value object (`[ValueObject]` or `[ValueObject<T>]`) names a `[JsonConverter]` outside
  generated code, implements `ITypedId<,>` and sets no conversions of its own; and every typed-id property, in any
  assembly (Features request and response records, Host bodies, Infrastructure DTOs), is required on read by the
  constructor System.Text.Json actually uses, `required` or `[JsonRequired]`. Nullable ids (`RunId?`) are exempt.

Until `Aw.Architecture.Tests` exists, it covers only the Kernel's own ids (it is internal to `tests/Aw.Kernel.Tests`,
which nothing may reference); a package's ids are a gap until then. **In R1b it moves to `Aw.Architecture.Tests`**, which runs it over every `Aw.*` assembly so
no package can forget it. A compile-time version (converter presence) is a candidate `Aw.Analyzers` rule.

## Public ids and PublicAPI files

With the defaults above, a public string id adds 17 lines to the project's public API (`raw/publicapi-probe-trimmed.txt`;
25 before the trim). `dotnet format analyzers --diagnostics RS0016` adds only the type itself: its code fix skips
generated code. Record the rest from the build:

```bash
dotnet build src/<Pkg>/Aw.<Pkg>.Core 2>&1 \
  | sed -n "s/.*RS0016: Symbol '\(.*\)' is not part of the declared public API.*/\1/p" | sort -u \
  >> src/<Pkg>/Aw.<Pkg>.Core/PublicAPI.Unshipped.txt
```

Review the lines it adds; they are the id's public surface.

## Not covered yet

- `long`-backed ids: add `Int64IdJsonConverter<TId>` with the first such id.
- The "no raw `string`/`Guid` in public `Core`/`Features` signatures" analyzer: an `Aw.Analyzers` gap (R1b). It needs
  an exemption for each id's generated `IParsable` string members.
- `Aw.Kernel.Data` overloads constrained on `ITypedId` (`BindId<TId>`, `GetId<TId>`): a follow-up once this and
  `Aw.Kernel.Data` are both merged.
