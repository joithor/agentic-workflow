# R1a AOT spike: test framework, data access, typed ids and the MCP SDK under NativeAOT

Spike for spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4b.2, §4b.3, §4b.5, §4b.6 and
decisions R7, R8 and R9. The code is in `spikes/` (not `src/`). It has its own `Directory.Build.props`,
`Directory.Build.targets`, `Directory.Packages.props`, `nuget.config` and `global.json`, so it neither inherits
nor affects the product build. Every number below is reproducible with the commands in each section.

## Decisions

| # | Question | Recommendation | Confidence |
|---|---|---|---|
| R7 | Test framework | **xUnit v3 on Microsoft.Testing.Platform.** Both frameworks run the same 30 tests far inside the 10 s unit-lane budget under JIT (warm `dotnet test` ≈ 0.85 s; the test app alone ≈ 0.23 s), and lane filters work in both. Tests run JIT per §4b.6, so TUnit's NativeAOT test runner isn't needed: AOT behavior is proven by the golden harness against the published binaries | High |
| R8 | Data access under NativeAOT | **Hand-written ADO.NET with `IRowMapper<T>`** (as §4b.2 already says). 0 trim/AOT warnings, the smallest binary (2.6 MB), and the fastest. Dapper.AOT works but its `Dapper.dll` still raises IL2104/IL3053, which fail a warnings-as-errors build. EF Core 10 works but raises IL2026/IL3050/IL3002/IL2104/IL3053 plus CS9270/CS8669/CS0618, labels NativeAOT and query precompilation "experimental", and is 6× larger | High |
| R9 | Strongly typed ids | **Vogen with `Conversions.None` plus hand-written STJ converters declared in user code** (the "hybrid"). 0 warnings, it works under AOT, and it keeps Vogen's validation and its compile-time `default()` ban (VOG009). Plain Vogen STJ conversions serialize ids as `{}` (the STJ source generator can't see Vogen's generated converters), and Vogen's `int` converter raises IL2026/IL3050. A hand-written `readonly record struct` works with 0 warnings but can't stop `default` | High |
| MCP | MCP C# SDK 2.2.0 for the Bridge host | **NativeAOT.** Stdio (low-level handlers *and* `[McpServerTool]` with `WithTools<T>(options)`) and Streamable HTTP on `CreateSlimBuilder` all publish with 0 warnings and pass initialize → tools/list → tools/call (ok, bad args, unknown tool). Loopback binding in code overrides `ASPNETCORE_URLS`. The SDK escapes `'` as `'`; that's fine for JSON-RPC but must not leak into §4c byte-compat files | High |

## Environment

```
$ dotnet --info            # .NET SDK 10.0.401 (commit e34a38d2ae), MSBuild 18.9.11, RID osx-arm64
$ sw_vers; sysctl -n machdep.cpu.brand_string
macOS 26.5.2 · Apple M4
```

Pinned packages, each published at least 14 days before 2026-10-09 (dates from the NuGet registration API):

| Package | Version | Published |
|---|---|---|
| Microsoft.Data.Sqlite, Microsoft.EntityFrameworkCore.{Sqlite,Design,Tasks}, Microsoft.Extensions.Hosting | 10.0.12 | 2026-09-08 |
| Dapper | 2.1.89 | 2026-09-23 |
| Dapper.AOT | 1.1.0 | 2026-09-11 |
| Vogen | 8.0.7 | 2026-07-21 |
| ModelContextProtocol, ModelContextProtocol.AspNetCore | 2.2.0 | 2026-08-13 |
| xunit.v3 | 4.0.1 | 2026-09-12 |
| TUnit | 1.69.0 | 2026-09-22 |

Newer versions exist for some of these (for example xunit.v3 4.0.2 and TUnit 1.73.19 on 2026-10-08/09). They were
excluded by the 14-day rule.

Every publish uses `dotnet publish <project> -c Release -r osx-arm64 -p:PublishAot=true`, from `spikes/`. Warning
counts are `grep -oE 'warning (IL|CS)[0-9]{4}' <publish log> | sort | uniq -c`. A diagnostic often appears twice in
a log (once per target), so the counts are occurrences, not distinct sites.

## R8 — data access under NativeAOT

**Scenario** (`spikes/r8-data/Scenario.cs`, shared by all three variants): WAL on, create schema, insert three
rows, update one, delete one, insert inside a transaction and roll it back, insert two in a committed transaction,
then check every state. The binary exits 0 only when every check holds, and 2 when the DB directory doesn't exist.

```
$ bash scripts/run-r8-r9.sh          # runs each binary on a fresh DB, then reads it back with the sqlite3 CLI
## AdoNet
adonet: OK
exit 0
sqlite3: rows [a|alpha|1 b|beta-2|20 d|delta|4 e|epsilon|5 ] journal_mode=wal
error path (missing dir): exit 2
## EfCore
efcore: OK
exit 0
sqlite3: rows [a|alpha|1 b|beta-2|20 d|delta|4 e|epsilon|5 ] journal_mode=wal
error path (missing dir): exit 2
## DapperAot
dapperaot: OK
exit 0
sqlite3: rows [a|alpha|1 b|beta-2|20 d|delta|4 e|epsilon|5 ] journal_mode=wal
error path (missing dir): exit 2
```

| Variant | Publish warnings | Runs correctly | Binary | Startup + scenario, median of 15 (min–max) |
|---|---|---|---|---|
| (a) ADO.NET + `IRowMapper<T>` | **none** | yes | **2.60 MB** | **11 ms** (10–12) |
| (b) EF Core 10, compiled model + precompiled queries | IL2026×3, IL3050×3, IL3002×4, IL2104×3, IL3053×3, CS9270×10, CS8669×3, CS0618×1 | yes | 16.08 MB | 19 ms (18–20) |
| (c) Dapper.AOT 1.1.0 (interceptors) | IL2104×1, IL3053×1 (both from `Dapper.dll`) | yes | 4.15 MB | 12 ms (11–13) |
| JIT reference: (a) as `dotnet AdoNet.dll` | — | yes | — | 46 ms warm (961 ms first run) |

All three binaries are Mach-O arm64 executables (`file`). Each ships `libe_sqlite3.dylib` next to it, so an
installed CLI is two files, not one.

EF Core notes, from `logs/r8-efcore.publish.log`:

```
Microsoft.EntityFrameworkCore.Tasks.targets(98,5): warning : NativeAOT support is experimental and can change in the future.
Microsoft.EntityFrameworkCore.Tasks.targets(98,5): warning : Query precompilation is an experimental feature and should be used with caution.
warning IL3050: Using member 'Microsoft.EntityFrameworkCore.DbContext.DbContext(DbContextOptions)' which has 'RequiresDynamicCodeAttribute' ...
```

- Query precompilation failed until every query lambda captured a local copy instead of a method parameter
  (`logs/r8-efcore.publish.attempt2-param-capture.log`, the failed attempt).
- `EnsureCreated` isn't usable under AOT, so the schema comes from SQL, which removes much of EF's value here.
- The generated interceptors raise CS9270 (an obsolete `InterceptsLocation` form) and CS8669. With
  `TreatWarningsAsErrors` (§4b.6), EF Core can't build without suppressions in the generated code.

**Recommendation:** keep §4b.2 as written. ADO.NET with `IRowMapper<T>` is warning-free and fastest. Dapper.AOT
would cut the mapper boilerplate, but its `Dapper.dll` trim warnings need a per-assembly suppression. Revisit it
once Dapper ships trim-clean.

## R9 — strongly typed ids

Each variant checks `From`/`Value`, equality, rejection of invalid values, a JSON round trip through a
source-generated `JsonSerializerContext` (expected `{"id":"item-1","budget":1200,"name":"alpha"}`) and an ADO.NET
round trip.

| Variant | Publish warnings | JSON correct under AOT | `default(ItemId)` | Binary | Median (15 runs) |
|---|---|---|---|---|---|
| (a) Vogen, `Conversions.SystemTextJson` | IL2026×2, IL3050×2 (Vogen's generated `int` converter calls `JsonSerializer.Serialize<int>(writer, value, options)`) | **no**: `{"id":{},"budget":{},"name":"alpha"}` | compile error VOG009 | 3.09 MB | not timed (superseded by a′) |
| (a′) same, Vogen converters registered at runtime on options wrapping the context | IL2026×2, IL3050×2 | yes | compile error VOG009 | 3.11 MB | 11 ms |
| (b) hand-written `readonly record struct` + hand-written converters | **none** | yes | **allowed** (`Value` is null) | 3.03 MB | 11 ms |
| (c) **hybrid:** Vogen `Conversions.None` + hand-written converters in user code | **none** | yes | compile error VOG009 | 3.05 MB | 11 ms |

Variant (a) fails silently: no exception, just wrong JSON. Raw output from the first run of (a):

```
vogen: json {"id":{},"budget":{},"name":"alpha"}
vogen: db item-1 1200
vogen: FAIL json shape
vogen: FAIL json round trip
vogen: FAILED 2
exit=1
```

Why: the System.Text.Json source generator and Vogen are both source generators, and generators don't see each
other's output. STJ sees `ItemId` as a struct with no public members and no `[JsonConverter]`, and writes `{}`.
Listing Vogen's converters in `[JsonSourceGenerationOptions(Converters = ...)]` doesn't help, because those types
only exist in Vogen's output (tried; same `{}`). The hybrid works because its `[JsonConverter]` attribute and
converter classes are written in user code, which the STJ generator can see. The converters may still call
Vogen-generated members (`From`, `Value`), because the compiler sees all generator output.

The `default()` ban, a probe line `var probe = default(ItemId);` added to the hybrid and built:

```
$ dotnet build r9-ids/VogenHybrid/VogenHybrid.csproj -c Release
error VOG009: Type 'ItemId' cannot be constructed with default as it is prohibited
exit=1
```

The probe was then removed. Cross-check: `sqlite3 <db> "select id||'|'||budget from ids"` returns
`item-1|1200` for (b), (a′) and (c) (`scripts/run-r8-r9.sh`).

**Recommendation:** use the hybrid. Vogen (`Conversions.None`) generates validation, equality and the
`default()` analyzer, and each id type gets a ~6-line hand-written STJ converter next to it. The `/new-slice` and
`/new-package` scaffolds should generate that converter with the id. Also, a JSON round-trip test per id type
belongs in the unit lane, since the `{}` failure is silent and also happens under JIT.

## R7 — xUnit v3 vs TUnit

The same 30 tests are in `spikes/r7-tests/{XunitV3,TUnit}.Tests`, sharing `Shared/Subject.cs`: two 10-case
theories, 9 facts, and 1 integration test on a real temp SQLite file. Lanes are `[Trait("Lane", …)]` in xUnit and
`[Property("Lane", …)]` in TUnit. Both run on Microsoft.Testing.Platform (`spikes/global.json`:
`"test": { "runner": "Microsoft.Testing.Platform" }`).

**Lane filters** (raw summary lines):

```
$ dotnet test --project r7-tests/XunitV3.Tests/XunitV3.Tests.csproj --no-build --filter-trait "Lane=unit"         → total: 29, succeeded: 29
$ dotnet test --project r7-tests/XunitV3.Tests/XunitV3.Tests.csproj --no-build --filter-trait "Lane=integration"  → total: 1,  succeeded: 1
$ dotnet test --project r7-tests/XunitV3.Tests/XunitV3.Tests.csproj --no-build --filter-not-trait "Lane=integration" → total: 29
$ dotnet test --project r7-tests/TUnit.Tests/TUnit.Tests.csproj --no-build --treenode-filter "/*/*/*/*[Lane=unit]"        → total: 29, succeeded: 29
$ dotnet test --project r7-tests/TUnit.Tests/TUnit.Tests.csproj --no-build --treenode-filter "/*/*/*/*[Lane=integration]" → total: 1,  succeeded: 1
$ dotnet test --project r7-tests/TUnit.Tests/TUnit.Tests.csproj --no-build --treenode-filter "/*/*/*/*[Lane!=integration]" → total: 29
```

**Wall time** (`bash scripts/r7-timings.sh`, run under the heavy-job lock):

| | xUnit v3 | TUnit |
|---|---|---|
| cold `dotnet test` (obj/bin deleted, build server shut down) | 2080 ms | 2078 ms |
| warm `dotnet test` (no changes), 3 runs | 886 / 867 / 844 ms | 1735 / 918 / 809 ms |
| `dotnet test --no-build`, 3 runs | 497 / 471 / 485 ms | 466 / 512 / 474 ms |
| test app run directly (JIT), 3 runs | 247 / 236 / 236 ms | 223 / 230 / 226 ms |
| **NativeAOT-published test app** | **crashes** (exit 134) | **30/30 pass, 23–24 ms** |
| AOT publish warnings | IL3000×8, IL2104×3, IL3053×2 | none |

xUnit v3 under NativeAOT, from `r7-tests/XunitV3.Tests/bin/Release/net10.0/osx-arm64/publish/XunitV3.Tests`:

```
Unhandled exception. System.ArgumentException: The value cannot be an empty string. (Parameter 'path')
   at System.IO.Path.GetFullPath(String) + 0x4c
   at Xunit.MicrosoftTestingPlatform.TestPlatformTestFramework.<>c__DisplayClass24_0.<RunAsync>b__2(...)
```

(`Assembly.Location` is empty in a single-file native app; IL3000 predicted it.)

**Recommendation:** xUnit v3. §9 R7's rule is "xUnit v3 unless lane budgets fail", and they don't: both
frameworks are an order of magnitude inside the 10 s unit budget for this suite, and neither is faster under JIT.
TUnit's edge is running tests as a native app, which §4b.6 doesn't need, because tests run JIT and the AOT
binaries are checked black-box by the golden harness (§4c). Reconsider TUnit only if a future AOT-only test lane
is wanted.

## MCP C# SDK 2.2.0 under NativeAOT

Three servers in `spikes/mcp/`, one tool (`upper`):

- **StdioLow**: `WithListToolsHandler` / `WithCallToolHandler`, no reflection-based binding.
- **StdioAttr**: `[McpServerToolType]` + `[McpServerTool]` registered with `WithTools<UpperTools>(options)`, where
  `options` is `McpJsonUtilities.DefaultOptions` with a source-generated context inserted first in the resolver chain.
- **HttpSlim**: Streamable HTTP (`WithHttpTransport(o => o.Stateless = true)`, `MapMcp("/mcp")`) on
  `WebApplication.CreateSlimBuilder`, bound with `Listen(IPAddress.Loopback, port)` in code.

| Server | Publish warnings | Binary | Time to first response, 5 runs |
|---|---|---|---|
| StdioLow | none | 8.14 MB | initialize answered in 25 / 10 / 15 / 10 / 11 ms |
| StdioAttr | none | 9.38 MB | 31 / 10 / 17 / 10 / 10 ms |
| HttpSlim | none | 15.88 MB | listening in 145 / 78 / 74 / 75 / 140 ms (50 ms poll granularity) |

Stdio transcript (`python3 scripts/drive-stdio.py mcp/StdioLow/bin/Release/net10.0/osx-arm64/publish/StdioLow`):

```
<< {"result":{"protocolVersion":"2025-06-18","capabilities":{"logging":{},"tools":{}},"serverInfo":{"name":"spike-stdio-low","version":"0.0.1"}},"id":1,"jsonrpc":"2.0"}
<< {"result":{"tools":[{"name":"upper","description":"Uppercases text.","inputSchema":{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}}]},"id":2,"jsonrpc":"2.0"}
<< {"result":{"content":[{"type":"text","text":"HELLO AOT"}]},"id":3,"jsonrpc":"2.0"}
<< {"result":{"content":[{"type":"text","text":"argument 'text' (string) is required"}],"isError":true},"id":4,"jsonrpc":"2.0"}
<< {"result":{"content":[{"type":"text","text":"unknown tool: nope"}],"isError":true},"id":5,"jsonrpc":"2.0"}
time-to-initialize-response: 362 ms; exit code 0     # first run after publish
OK
```

StdioAttr returns the same results. For bad arguments it reports `An error occurred invoking 'upper'.` as an
`isError` result, and for an unknown tool a JSON-RPC error (`-32602 Unknown tool: 'nope'`). Both servers exit 0
when stdin closes.

HTTP (`bash scripts/drive-http.sh mcp/HttpSlim/bin/Release/net10.0/osx-arm64/publish/HttpSlim 5199`, which starts
the server with `ASPNETCORE_URLS=http://0.0.0.0:5200` in its environment):

```
## listening sockets (ASPNETCORE_URLS=http://0.0.0.0:5200 in env)
127.0.0.1:5199
## initialize
data: {"result":{"protocolVersion":"2025-06-18","capabilities":{"logging":{},"tools":{}},"serverInfo":{"name":"spike-http-slim","version":"0.0.1"}},"id":1,"jsonrpc":"2.0"}
## tools/call upper
data: {"result":{"content":[{"type":"text","text":"HELLO AOT"}]},"id":3,"jsonrpc":"2.0"}
## env-configured port 5200
refused (curl exit 7)
OK
```

In a manual run with `ASPNETCORE_URLS=http://0.0.0.0:6011`, Kestrel logged `Overriding address(es) 'http://0.0.0.0:6011'. Binding to endpoints defined via IConfiguration and/or
UseKestrel() instead.` That's the loopback-in-code rule of §4d working, and the start of the test it asks for.

**Recommendation:** build the Bridge host NativeAOT (§4b.6 table: "NativeAOT if the MCP C# SDK passes the
spike"). The attribute style is AOT-clean when the tool's JSON options include a source-generated context, so use
it for readability. The low-level handlers are the fallback for tools with dynamic schemas.

## Notes for Track C and the scaffolds

- **Kernel.Data:** ADO.NET + `IRowMapper<T>`, SQL in `const string`s. Ship `libe_sqlite3.dylib` (and the Linux `.so`)
  next to each AOT binary; `setup.sh` and `binaries.lock` must list it.
- **Kernel.Testing:** xUnit v3 on MTP. Lane traits as `[Trait("Lane", "unit")]`, filtered with `--filter-trait`.
- **Typed ids:** the Vogen hybrid. Every id gets a JSON round-trip test, because the `{}` failure is silent.
- **JSON escaping:** the MCP SDK's default options escape `'` as `\u0027` (observed above; STJ's default encoder also escapes HTML-sensitive characters such as `<`, `>`, `&`). That's fine
  inside JSON-RPC, but §4c byte-compat writers must use the Kernel's Node-compatible options, never the SDK's.

## Reproduce

```
cd spikes
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
for p in r8-data/AdoNet r8-data/EfCore r8-data/DapperAot r9-ids/HandIds r9-ids/VogenIds r9-ids/VogenHybrid \
         mcp/StdioLow mcp/StdioAttr mcp/HttpSlim; do
  dotnet publish "$p/$(basename "$p").csproj" -c Release -r osx-arm64 -p:PublishAot=true
done
bash scripts/run-r8-r9.sh                # behavior + sqlite3 cross-checks
bash scripts/startup.sh                  # sizes and startup medians
python3 scripts/drive-stdio.py mcp/StdioLow/bin/Release/net10.0/osx-arm64/publish/StdioLow
bash scripts/drive-http.sh mcp/HttpSlim/bin/Release/net10.0/osx-arm64/publish/HttpSlim 5199
bash scripts/r7-timings.sh               # R7 wall times
dotnet publish r7-tests/TUnit.Tests/TUnit.Tests.csproj -c Release -r osx-arm64 -p:PublishAot=true
r7-tests/TUnit.Tests/bin/Release/net10.0/osx-arm64/publish/TUnit.Tests
```

Out of scope: Linux publishes (this box is macOS arm64; the CI AOT job comes with the ports), load testing, and
EF Core migrations under AOT.
