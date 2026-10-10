# R1a AOT spike: test framework, data access, typed ids and the MCP SDK under NativeAOT

Spike for spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4b.2, §4b.3, §4b.5, §4b.6, §4c and
§4d, deciding R7, R8 and R9 and the Bridge host's build. The spec carries the decisions; this doc carries the
evidence.

**Where the code is.** The spike code, its lock files, scripts and every log cited here are on the tag
[`spike/r1a-aot-2026-10`](https://github.com/jthorare/agentic-workflow/tree/spike/r1a-aot-2026-10/spikes), not on
`main`. It's throwaway measurement code (one project fails to build on purpose; another fails warnings-as-errors
on purpose), and keeping it out of the tree keeps it out of the repo-wide .NET checks: the B1 package verification
(#89) and A2's .NET CI job (#93) need no exclusions. Log paths below (`logs/…`) are relative to `spikes/` on that tag.

## Decisions

| # | Question | Decision | Confidence |
|---|---|---|---|
| R7 | Test framework | **xUnit v3 on Microsoft.Testing.Platform.** Under JIT both frameworks run the same suites in about the same time (30 tests: 238 vs 222 ms per test-app run; 2,000 tests: 359 vs 369 ms; 0.06–0.07 ms per added test), far inside the 10 s unit budget, and lane filters work in both. Tests run JIT (§4b.6), so TUnit's working NativeAOT test runner (xUnit v3's crashes) isn't needed. Lane budgets are enforced from xUnit's CTRF report (`scripts/lane-budget.py`, proven to fail on an over-budget run) | High |
| R8 | Data access under NativeAOT | **Hand-written ADO.NET with `IRowMapper<T>`, over the OS `libsqlite3`.** 0 warnings and one file per tool. EF Core 10 and Dapper.AOT both run but neither builds warnings-as-errors: EF fails on IL2026/IL3050 in the `DbContext` constructor and calls itself experimental; Dapper.AOT fails only on `Dapper.dll`'s assembly-level IL2104/IL3053, and suppressing those hides calls its generator can't intercept (a generic `Query<T>` then built clean and threw `PlatformNotSupportedException` at runtime). Startup doesn't separate ADO.NET and Dapper.AOT (6.3 vs 6.8 ms median, overlapping ranges); EF is about twice as slow (13.5 ms) | High |
| R9 | Strongly typed ids | **Vogen with its STJ and parsing generation turned off, plus a user-code `JsonConverter` (via `TryFrom`, throwing `JsonException`) and `IParsable<T>` per id.** Source generators can't see each other's output, so Vogen's generated converter is invisible to the STJ generator (ids serialize as `{}`) and its generated `TryParse` is invisible to the request-delegate generator (route ids fall back to JSON-body binding: every request a 500). With both in user code: 0 warnings, JSON and route binding work under AOT, the VOG009 `default` ban holds, and bad input is a 400 | High |
| MCP | MCP C# SDK 2.2.0 for the Bridge | **NativeAOT, two entry points as today (REST minimal API, stdio MCP), MCP tools on the SDK's low-level handlers.** All three spike servers publish with 0 warnings and pass every call. The low-level handlers can reproduce the TS bridge's schemas and `isError` replies exactly, which the attribute style can't. The HTTP host is loopback-only against 7 override vectors once no args or environment reach its configuration, and it rejects a foreign `Host` (400) and `Origin` (403) | High |

## Environment and pins

```
.NET SDK 10.0.401 (MSBuild 18.9.11), RID osx-arm64 · macOS 26.5.2 · Apple M4
```

- Every package is pinned exactly (`[x.y.z]` in `spikes/Directory.Packages.props`) and locked
  (`RestorePackagesWithLockFile`, one committed `packages.lock.json` per project).
- `python3 scripts/nuget-dates.py --lock $(find . -name packages.lock.json)` dates every resolved package
  (direct and transitive) from the nuget.org registration API: 104 packages, the newest Dapper 2.1.89 (2026-09-23),
  all before the 14-day cutoff (`logs/nuget-dates.txt`).
- Newer releases were excluded by that rule, and the newest xUnit v3 (4.0.2, 2026-10-09) wasn't retested for the
  AOT crash.
- `spikes/nuget.config` clears sources and maps everything to nuget.org.
- MSBuild uses the nearest `Directory.Build.props`/`.targets`/`Directory.Packages.props`, so the repo root's (from
  #89) don't apply under `spikes/`. `nuget.config` merges up the tree, so the root's signature validation does.
- All numbers assume **`InvariantGlobalization=true`** (set in `spikes/Directory.Build.props`). See R8 for the cost
  of turning it off.

**How the numbers were made.**
- `scripts/publish-all.sh` publishes everything with one log per project (`logs/<area>-<project>.publish.log`) and
  prints the warning table below. Counts are occurrences of `warning (IL|CS)NNNN` in the log; a diagnostic usually
  appears more than once (per target), so they're not distinct sites.
- `scripts/timeit.py <commands.tsv>` times each command with `perf_counter_ns` around `subprocess.run`. It runs the
  commands round-robin in a shuffled order, uses a fresh temp DB per run, and measures `/usr/bin/true` the same way
  (2.0 ms) as the spawn floor.
- The machine was busy with other build jobs (1-minute load average about 8), so absolute numbers are high. Read
  them as comparisons within one table.

## R8 — data access under NativeAOT

**Scenario** (`r8-data/Scenario.cs`, shared by every variant):
1. turn on WAL and create the schema;
2. insert three rows, update one, delete one;
3. insert inside a transaction and roll it back, then insert two rows in a committed transaction;
4. check every state.

The binary exits 0 only when every check holds, and 2 when the DB directory doesn't exist.
`scripts/run-r8-r9.sh` runs each binary on a fresh DB, then reads the DB back with the `sqlite3` CLI:

```
## AdoNet              sqlite3: rows [a|alpha|1 b|beta-2|20 d|delta|4 e|epsilon|5 ] journal_mode=wal   error path (missing dir): exit 2
## AdoNetSystemSqlite  sqlite3: rows [a|alpha|1 b|beta-2|20 d|delta|4 e|epsilon|5 ] journal_mode=wal   error path (missing dir): exit 2
## EfCore              sqlite3: rows [a|alpha|1 b|beta-2|20 d|delta|4 e|epsilon|5 ] journal_mode=wal   error path (missing dir): exit 2
## DapperAot           sqlite3: rows [a|alpha|1 b|beta-2|20 d|delta|4 e|epsilon|5 ] journal_mode=wal   error path (missing dir): exit 2
OK                                                                     (logs/r8-r9-run.txt)
```

| Variant | Publish warnings (`logs/publish-all.txt`) | `-p:TreatWarningsAsErrors=true` | Binary + `libe_sqlite3.dylib` = on disk | Startup + scenario, median (min–max), 15 runs |
|---|---|---|---|---|
| (a) ADO.NET + `IRowMapper<T>`, bundled e_sqlite3 | none | — | 2.60 + 1.62 = 4.22 MB | 6.3 ms (5.9–7.4) |
| (a′) same over the **OS `libsqlite3`** (`SQLitePCLRaw.provider.sqlite3`, SQLite 3.51.0) | none | — | **2.60 MB, one file** | 10.0 ms (8.3–17.3) |
| (a″) same with **ICU on** (`InvariantGlobalization=false`) | none | — | 2.79 + 1.62 = 4.41 MB | 6.4 ms (6.1–7.4) |
| (b) EF Core 10, compiled model + precompiled queries | CS0618×1 CS8669×3 CS9270×10 IL2026×3 IL2104×3 IL3002×4 IL3050×3 IL3053×3 | **fails**: IL2026, IL3050 at `DbContext(DbContextOptions)` | 16.08 + 1.62 = 17.70 MB | 13.5 ms (13.0–14.8) |
| (c) Dapper.AOT 1.1.0 | IL2104×1 IL3053×1 (`Assembly 'Dapper' produced …`) | **fails**: IL2104, IL3053 | 4.15 + 1.62 = 5.77 MB | 6.8 ms (6.2–8.4) |
| JIT reference: (a) as `dotnet AdoNet.dll` | — | — | — | 42.4 ms (41.5–45.9) |

(`logs/startup.txt`, `logs/sizes.txt`, `logs/r8-*.warnaserror.publish.log`. Every binary is a Mach-O arm64
executable with no CoreCLR/hostfxr/clrjit link: `logs/native-check.txt`.)

**Dapper.AOT's suppression hides a runtime failure.** The narrow fix for (c) is
`<NoWarn>IL2104;IL3053</NoWarn>`. `r8-data/DapperFallback` does exactly that with `TreatWarningsAsErrors=true`,
then issues one query the generator intercepts and one through a generic helper (`c.Query<T>(sql)` with an open
`T`) that it can't. The build reports no diagnostics at all; the run (`logs/r8-DapperFallback.run.txt`):

```
dapper-fallback: intercepted Query<Row>: 1 row(s), first alpha
dapper-fallback: generic Query<T> threw PlatformNotSupportedException: Dynamic code generation is not supported on this platform.
```

**EF Core notes** (`logs/r8-EfCore.publish.log`; query precompilation failed until each query captured a local
instead of a method parameter, in `logs/r8-EfCore.attempt2-param-capture.publish.log`):

```
Microsoft.EntityFrameworkCore.Tasks.targets(98,5): warning : NativeAOT support is experimental and can change in the future.
Microsoft.EntityFrameworkCore.Tasks.targets(98,5): warning : Query precompilation is an experimental feature and should be used with caution.
```

`EnsureCreated` isn't usable under AOT either, so the schema came from SQL anyway.

**Native SQLite.** The bundled `e_sqlite3` makes every tool two files (1.62 MB of dylib next to each binary), each
needing macOS signing and a Linux `.so`. (a′) uses the OS library through SQLitePCLRaw's `sqlite3` provider, one
line at startup (`SQLitePCL.raw.SetProvider(new SQLite3Provider_sqlite3())`). It passes the same scenario and
cross-check in one file. It starts slower here (10.0 vs 6.3 ms, wider range), which matters little for a CLI.
The OS version varies by host, so `Aw.Kernel.Data` checks a minimum version at startup and `sindri doctor`
reports it, with the bundled library as the fallback (listed in `binaries.lock` if used). Linux wasn't built (this
box is macOS).

**Invariant globalization.** On macOS, turning ICU on costs 0.19 MB and no measurable startup. Both builds link
the system `libicucore`, so there's no difference to see here. The reason for invariant mode is Linux, where an
ICU build needs `libicu` installed and fails at startup without it. Invariant mode also matches §4c's
culture-invariant rules. The spec now sets it on every NativeAOT entry point.

**Decision:** keep §4b.2's ADO.NET + `IRowMapper<T>`, over the OS `libsqlite3`. Neither alternative passes the
warnings-as-errors build §4b.6 requires. Dapper.AOT's mapper savings would cost a suppression that hides exactly
the failure AOT analysis exists to catch.

## R9 — strongly typed ids

Each variant checks `From`/`Value`, equality, rejection of invalid values, a JSON round trip through a
source-generated `JsonSerializerContext` (expected `{"id":"item-1","budget":1200,"name":"alpha"}`), and an
ADO.NET round trip that `sqlite3` reads back as `item-1|1200` (`logs/r8-r9-run.txt`).

| Variant | Vogen settings | Publish warnings | JSON under AOT | Route binding (RDG) | `default(ItemId)` | Binary | Median |
|---|---|---|---|---|---|---|---|
| (a) Vogen defaults | `[ValueObject<string>(conversions: Conversions.SystemTextJson)]`, the Vogen default for STJ | IL2026×2 IL3050×2 (its generated `int` converter calls `JsonSerializer.Serialize<int>(writer, value, options)`) | **`{"id":{},"budget":{},…}`**, no error (`logs/r9-VogenIds.run1-no-converters.txt`, first run) | not tested | VOG009 | 3.09 MB (first run) | — |
| (a′) (a) + Vogen's converters registered at runtime on options wrapping the context | same | IL2026×2 IL3050×2 | correct | not tested | VOG009 | 3.11 MB | 5.9 ms |
| (b) hand-written `readonly record struct` + converters | — | none | correct | not tested | **allowed** (`Value` is null) | 3.03 MB | 5.8 ms |
| (c) **hybrid**: `[assembly: VogenDefaults(conversions: None, parsableForStrings: GenerateNothing, parsableForPrimitives: GenerateNothing)]` + user-code converter (`TryFrom` → `JsonException`) + user-code `IParsable<T>` | as stated | **none** | correct; bad input is `JsonException` | **works**: 200 / 400 | VOG009 (build fails) | 3.06 MB | 6.0 ms |

(a) was published three times while finding the cause, in `logs/r9-VogenIds.publish*.log`:
1. the first run as above;
2. `publish2`: Vogen's converter types listed in `[JsonSourceGenerationOptions(Converters = …)]`. Still `{}`,
   because those types exist only in Vogen's output, which the STJ generator can't see;
3. `publish3`, the current log: the runtime registration, (a′).

**Why the hybrid.** System.Text.Json's source generator and the request-delegate generator are source generators
like Vogen, and generators don't see each other's output:
- STJ sees `ItemId` as a struct with no public members and no `[JsonConverter]`, so it writes `{}`. For a string
  id there's no warning at all; only the `int` id's converter raised IL2026/IL3050.
- The RDG doesn't see Vogen's `IParsable` either. Its generated code says
  `// Endpoint Parameter: id (Type = Spike.R9.ItemId, IsParsable = False, … Source = JsonBodyOrService)`.
  Every request then fails with `JsonTypeInfo metadata for type 'Spike.R9.ItemId' was not provided`
  (`logs/r9-RouteBind.run1.log`), under AOT and under JIT with the RDG on (`logs/r9-RouteBind.jit-rdg-before-fix.txt`).

Turning both Vogen generators off and writing the converter and `TryParse` next to each id fixes both, and the
user code may call Vogen's members (`TryFrom`, `Value`), because the compiler sees all generator output.

**Boundary behavior of the hybrid** (`VogenHybrid`, AOT):

```
hybrid: missing id: JsonException        hybrid: null id: JsonException       hybrid: empty id: JsonException
hybrid: numeric id: JsonException        hybrid: negative budget: JsonException   hybrid: missing name: JsonException
hybrid: missing id under STJ defaults: accepted, Id.IsInitialized()=False
hybrid: OK
```

The last line is the counterfactual. Without `RespectRequiredConstructorParameters` (and
`RespectNullableAnnotations`) on the context, a missing property deserializes to an uninitialized id and the
converter never runs. The Kernel's JSON profile turns both on.

**Route binding of the hybrid** (`r9-ids/RouteBind`, `CreateSlimBuilder`, RDG, AOT, 0 warnings;
`bash scripts/drive-routebind.sh …/RouteBind`):

```
/items/item-1          -> 200 item item-1 initialized=True
/items/%20             -> 400
/budget/1200           -> 200 budget 1200
/budget/-1             -> 400
/budget/abc            -> 400
/query?id=q-1          -> 200 query q-1
/query?id=             -> 400
/query                 -> 400
/qbudget?b=            -> 400
/qbudget?b=5           -> 200 qbudget 5
OK
```

`/query?id=` was first a **500**. For an empty query value the RDG skips `TryParse` and passes an uninitialized id
to the handler, and Vogen throws on first use (`logs/r9-RouteBind.emptyquery.log`). So VOG009 only stops a
`default` literal at compile time; uninitialized ids can still appear at runtime (JSON without the options above,
RDG empty values, arrays, unassigned fields). The probe's Host maps `Vogen.ValueObjectValidationException` to 400
(Host is where §4b.3 allows a catch), which gives the 400 above.

**Guards** (both on the tag, both run by the spike):
- **The `default` ban is a regression check, not a transcript.** `r9-ids/VogenDefaultProbe` links the hybrid's
  `Ids.cs` and contains `var probe = default(ItemId);`. `scripts/check-default-probe.sh` asserts that the build
  fails with `error VOG009: Type 'ItemId' cannot be constructed with default as it is prohibited`
  (`logs/r9-default-probe.txt`).
- **The silent `{}` has a structural guard.** `r9-ids/IdGuard.Tests` (xUnit, unit lane) reflects over an assembly
  and fails when a value object lacks a top-level user-code `[JsonConverter]` or has a nested Vogen
  `…SystemTextJsonConverter`. It passes on the hybrid and reports four violations on (a)
  (`logs/r9-IdGuard.test.log`). In the product it belongs in `Aw.Architecture.Tests`, over every assembly.

**Decision:** the hybrid (c). The spec's §4b.3 now states the whole pattern. The scaffolds generate each id with
its converter, `TryParse` and a JSON round-trip test covering missing and null values.

## R7 — xUnit v3 vs TUnit

The same suites are in `r7-tests/{XunitV3,TUnit}.Tests`, sharing `Shared/Subject.cs`. Each has 30 tests: two
10-case theories, 9 facts and 1 integration test on a real temp SQLite file. `r7-tests/{XunitV3,TUnit}.Bulk` add
2,000 generated unit tests per framework (`scripts/gen-r7-bulk.py 2000`) to show how run time grows with test
count. Lanes are `[Trait("Lane", …)]` (xUnit) and `[Property("Lane", …)]` (TUnit), on Microsoft.Testing.Platform
(`spikes/global.json`: `"test": { "runner": "Microsoft.Testing.Platform" }`).

**Lane filters** (both select 29 unit and 1 integration):

```
dotnet test --project r7-tests/XunitV3.Tests/XunitV3.Tests.csproj --filter-trait "Lane=unit"               → total: 29
dotnet test --project r7-tests/XunitV3.Tests/XunitV3.Tests.csproj --filter-trait "Lane=integration"        → total: 1
dotnet test --project r7-tests/TUnit.Tests/TUnit.Tests.csproj --treenode-filter "/*/*/*/*[Lane=unit]"        → total: 29
dotnet test --project r7-tests/TUnit.Tests/TUnit.Tests.csproj --treenode-filter "/*/*/*/*[Lane=integration]" → total: 1
```

**Wall time**: Release throughout, `python3 scripts/timeit.py scripts/r7.tsv --rounds 5`, median (min–max) in ms,
from `logs/r7-timings.txt`:

| | xUnit v3 | TUnit |
|---|---|---|
| test app (JIT), 30 tests | 237.6 (229.3–242.3) | 222.3 (218.2–235.5) |
| test app (JIT), 2,000 tests | 359.1 (344.0–368.6) | 368.8 (348.0–416.5) |
| `dotnet test --no-build`, 30 tests | 502.2 (467.2–510.1) | 475.6 (465.3–490.7) |
| `dotnet test` (incremental, no changes), 30 tests | 837.9 (797.0–880.1) | 822.8 (798.8–849.3) |
| NativeAOT test app, 30 / 2,000 tests | **crashes** (exit 134) | 17.3 (16.3–17.4) / 79.7 (77.5–97.8) |
| AOT publish warnings | IL3000×8, IL2104×3, IL3053×2 | none |

The cost is a fixed startup of about 0.22–0.24 s per test app plus 0.06 (xUnit v3) to 0.07 (TUnit) ms per test for these
trivial tests. The first run's single cold `dotnet test` samples (2,080 vs 2,078 ms) weren't
re-measured: CI builds anyway, and the decision doesn't depend on them.

xUnit v3 as a NativeAOT app (`logs/r7-XunitV3.aot-run.txt`; IL3000 predicted it, because `Assembly.Location` is
empty in a single-file native app):

```
Unhandled exception. System.ArgumentException: The value cannot be an empty string. (Parameter 'path')
   at System.IO.Path.GetFullPath(String) + 0x4c
exit=134
```

**Lane budgets.** §4b.5 says CI fails a test that runs over its lane's time. xUnit v3 has no per-trait budget, so
the spike runs each lane with its CTRF reporter
(`--report-xunit-ctrf --report-xunit-ctrf-filename <lane>.json`), and `scripts/lane-budget.py` fails when the
lane's run time or any single test is over budget (`logs/r7-lane-budget.txt`):

```
lane unit: 29 tests, run 0.056 s (budget 10.0 s) … OK                       exit 0
lane integration: 1 tests, run 0.065 s (budget 60.0 s) … OK                 exit 0
lane unit: 2000 tests, run 0.147 s (budget 10.0 s) … OK                     exit 0
lane unit: 29 tests … over per-test budget 0.0001 s … FAIL: over budget     exit 1
lane unit: 2000 tests, run 0.147 s (budget 0.01 s) FAIL: over budget        exit 1
```

**Decision:** xUnit v3. §9's rule was "xUnit v3 unless lane budgets fail", and they don't. TUnit's real advantage
is running tests as a native app, which §4b.6 doesn't use. `Aw.Kernel.Testing` ships the CTRF budget check.

## MCP C# SDK 2.2.0 under NativeAOT

| Server (`spikes/mcp/`) | What | Publish warnings | Binary | Time to initialize response, 5 runs |
|---|---|---|---|---|
| StdioLow | `WithListToolsHandler` / `WithCallToolHandler`, no reflection binding | none | 8.14 MB | 9–10 ms |
| StdioAttr | `[McpServerTool]` + `WithTools<T>(options)` with a source-generated context first in the resolver chain | none | 9.38 MB | 10–11 ms |
| HttpSlim | Streamable HTTP (`MapMcp`) on `CreateSlimBuilder`, hardened as below | none | 15.88 MB | — |

(`logs/mcp-stdio-startup.txt`. Both stdio servers pass `scripts/drive-stdio.py`: initialize, tools/list, a good
call, bad arguments, an unknown tool, and exit 0 when stdin closes.)

**Loopback in code, against every override vector.** `bash scripts/drive-http.sh …/HttpSlim 5199` starts the
server once per vector, pointed at `0.0.0.0:5200`, and lists its listening sockets with `lsof`. The baseline column
is the first spike's setup (`CreateSlimBuilder(args)`, configuration untouched):

```
vector                               baseline                           hardened
ASPNETCORE_URLS                      127.0.0.1:5199                     127.0.0.1:5199
DOTNET_URLS                          127.0.0.1:5199                     127.0.0.1:5199
ASPNETCORE_HTTP_PORTS                127.0.0.1:5199                     127.0.0.1:5199
ASPNETCORE_PREFERHOSTINGURLS+URLS    *:5200                             127.0.0.1:5199
env Kestrel__Endpoints               *:5200 127.0.0.1:5199              127.0.0.1:5199
arg --Kestrel:Endpoints              127.0.0.1:5199                     127.0.0.1:5199
arg --urls                           127.0.0.1:5199                     127.0.0.1:5199
```

So `Listen(IPAddress.Loopback, port)` alone isn't enough: two vectors bound **all interfaces** in the baseline. The
first spike claimed the §4d rule worked when it had only tested `ASPNETCORE_URLS`. The hardened host:
- passes no args to `CreateSlimBuilder` and clears the configuration sources (adding an empty in-memory one, since
  setting a value needs a source);
- pins `PreferHostingUrls(false)` and empty `UseUrls()`. `ASPNETCORE_`-prefixed variables are host settings read
  when the builder is created, so clearing the sources afterwards was too late for `PREFERHOSTINGURLS`: a first
  hardened version without these two pins (commit `bcca7bc`) still bound `*:5200` for that vector.

**Host and Origin** (an initialize request; status codes):

```
case                                     baseline  hardened
no Origin, Host 127.0.0.1                200       200
Host: localhost                          200       200
Host: evil.example (DNS rebinding)       200       400
Origin: http://127.0.0.1                 200       200
Origin: http://evil.example              200       403
Origin: null                             200       403
```

The 400 comes from `UseHostFiltering` with `AllowedHosts = 127.0.0.1, localhost, [::1]`, which
`CreateSlimBuilder` doesn't add by default. The 403 comes from a few lines of middleware that allow a missing
`Origin` (non-browser clients) or a loopback one. Both are AOT-clean.

**Parity with the TS bridge.** Today the bridge is two entry points: Fastify REST (`src/index.ts`) and stdio MCP
(`src/mcp.ts`). `scripts/capture-ts-bridge-mcp.py` records the TS MCP surface (`logs/ts-bridge-mcp-capture.txt`):
- schemas carry `$schema`, `additionalProperties: false`, `format: "uuid"` and `minLength`;
- bad arguments are an `isError` result with the zod text (`MCP error -32602: Input validation error: …`);
- an unknown tool is an `isError` result too (`MCP error -32602: Tool nope not found`).

The SDK's attribute style differs on all three:
- it generates `inputSchema` from the C# signature;
- bad arguments get a generic `An error occurred invoking 'upper'.`;
- an unknown tool is a JSON-RPC **error** (`-32602 Unknown tool: 'nope'`), not a result.

The low-level handlers (StdioLow) return whatever schema and result the code builds.

**Decision:**
- **Builds:** keep two NativeAOT entry points, REST and stdio MCP. A single `WebApplication` with stdio would need
  console logging forced to stderr, because stdout carries JSON-RPC.
- **Tools:** use the low-level handlers, with the schemas copied from the golden recording, so tools/list and
  error replies match. Streamable HTTP MCP isn't part of the port. If it's added later, it comes with the Host and
  Origin checks above.
- **Declared divergences** for the Bridge's golden contract: no `HOST` variable or `ALLOW_REMOTE` escape hatch
  (the TS server honors both); no wildcard CORS (`@fastify/cors` with `origin: true` reflects any origin today);
  `Host`/`Origin` rejections are new.

## JSON escaping (§4c)

The MCP SDK's serializer escapes `'` as `'`. That's harmless in JSON-RPC, but §4c writers must not use STJ's
defaults. `bash scripts/check-escaping.sh` writes 18 strings through STJ's default encoder and through
`UnsafeRelaxedJsonEscaping`, and compares the bytes with Node's `JSON.stringify` (`logs/json-escaping.txt`;
excerpt, with Node's raw invisible characters described in parentheses):

```
input (JSON literal)         node JSON.stringify            STJ default                        STJ relaxed
"it's"                       "it's"                         "it's"                        =
"a<b>c&d"                    "a<b>c&d"                      "a<b>c&d"           =
"1+1"                        "1+1"                          "1+1"                         =
"café"                  "café"                         "café"                        =
"emoji 😀"         "emoji 😀"                      "emoji 😀"               "emoji 😀"
"line sep "        (raw U+2028/2029)              "line sep "              "line sep "
"ctl\u0001\u001f"            "ctl\u0001\u001f"              "ctl\u0001\u001F"                  "ctl\u0001\u001F"
"del\u007f"                  (raw U+007F)                   "del\u007F"                        "del\u007F"
"lone \ud800 surrogate"      "lone \ud800 surrogate"        "lone � surrogate"            "lone � surrogate"
"bom﻿"                  (raw U+FEFF)                   "bom﻿"                        "bom﻿"
differences from node: default 16/18, relaxed 7/18
STJ reader rejects "lone \ud800 surrogate": Cannot read incomplete UTF-16 JSON text as string with missing low surrogate.
```

So neither encoder works: relaxed still escapes emoji, U+2028/2029, U+007F, NBSP and BOM, writes upper-case hex,
and **replaces a lone surrogate with U+FFFD** (data loss). STJ's reader also rejects a lone surrogate that Node
writes. `Aw.Kernel` (B2, `feat/readiness-b2-kernel-core`) already has a custom `NodeJsonEncoder`/`NodeJsonEscaping`
with a byte-compat corpus. This corpus belongs in it, especially the lone-surrogate read and write cases. MCP and
HTTP wire JSON, whose serializer the Kernel doesn't control, is compared parsed and normalized in the golden
harness (spec §4c).

## Notes for Track C

- **`Aw.Kernel.Data` (C1):**
  - ADO.NET + `IRowMapper<T>`, SQL in `const string`s, over the OS `libsqlite3` via
    `SQLitePCLRaw.provider.sqlite3`, with a minimum-version check (WAL and the JSON functions the schemas use) and a
    `sindri doctor` line;
  - per-connection pragmas `journal_mode=wal`, `busy_timeout` and `foreign_keys=on`;
  - typed ids bind through one helper (`AddId(name, id)` writing `id.Value`), so repositories never call `.Value`
    by hand.
- **`Aw.Kernel.Testing` (C2):** xUnit v3 on MTP; lanes as `[Trait("Lane", …)]`, filtered with `--filter-trait`;
  the CTRF lane-budget check (`lane-budget.py`'s logic) as a CI step per lane; `[assembly: AssemblyTrait]` or an
  architecture test so no test is left without a lane.
- **Typed ids (C3):**
  - the hybrid exactly as in R9: `VogenDefaults` set once per package (or injected by props), and each id with its
    user-code converter (`TryFrom` → `JsonException`), `IParsable<T>` (`TryParse` through `TryFrom`, because VOG009
    also bans `result = default`) and a round-trip test covering missing and null values;
  - the `IdGuard` rule in `Aw.Architecture.Tests`, and the Host mapping Vogen's validation exception to 400;
  - `EnableRequestDelegateGenerator=true` in every host build, so JIT endpoint tests bind like AOT.
- **Kernel JSON (B2):** `RespectRequiredConstructorParameters` and `RespectNullableAnnotations` on, and the escaping
  corpus above in its golden tests.
- **Bridge (R2):** as in the MCP decision above, with the 7-vector loopback test, the Host/Origin tests and the
  declared divergences in its golden contract.

## Reproduce

```
git checkout spike/r1a-aot-2026-10 && cd spikes
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
bash scripts/publish-all.sh                       # every publish, one log each, and the warning table
python3 scripts/timeit.py scripts/startup.tsv     # R8/R9 startup (15 rounds)
python3 scripts/timeit.py scripts/r7.tsv --rounds 5
bash scripts/run-r8-r9.sh                         # behavior + sqlite3 cross-checks + error path
bash scripts/drive-routebind.sh r9-ids/RouteBind/bin/Release/net10.0/osx-arm64/publish/RouteBind
bash scripts/check-default-probe.sh               # VOG009 must fail the build
dotnet test --project r9-ids/IdGuard.Tests/IdGuard.Tests.csproj
python3 scripts/drive-stdio.py mcp/StdioLow/bin/Release/net10.0/osx-arm64/publish/StdioLow
bash scripts/drive-http.sh mcp/HttpSlim/bin/Release/net10.0/osx-arm64/publish/HttpSlim 5199
bash scripts/check-escaping.sh
python3 scripts/capture-ts-bridge-mcp.py <repo root>   # needs mcp-bridge built
python3 scripts/nuget-dates.py --lock $(find . -name packages.lock.json)
```

Out of scope: Linux publishes (this box is macOS arm64; the CI AOT job comes with the ports), load testing, and
EF Core migrations under AOT.
