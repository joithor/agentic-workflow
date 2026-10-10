# Aw.Kernel.Telemetry

Logging, spans and metrics for every C# host, with `Aw.Redaction`'s scrubber on every log and export path (spec §4b.1,
§4d "runtime hardening", §5 W7). Tier 0, sublevel 1 in `deps.yaml` (edges to `Kernel` and `Redaction`); its one
external dependency is `OpenTelemetry`. Its closure (see `src/Aw.Kernel.Telemetry/packages.lock.json`) also brings
`Microsoft.Extensions.Logging`, `.Logging.Configuration`, `.Configuration` (with `.Binder` and the
`.EnvironmentVariables` provider), `.Options` and `.DependencyInjection` into every host: nothing in this repo may use
`IConfiguration` (csharp.md DI rules), and no exporter, OTLP, gRPC or HTTP package is in it.

## Start it in the Host

```csharp
// Composition.Compose(services, environment, clock) — the signature every Host has (csharp.md DI rules):
services.AddKernelTelemetry(TelemetryOptions.FromEnvironment("aw-readiness", environment, clock));
// A host that exports picks the adapter here too: … with { TraceExporter = <adapter from Infrastructure> }.
```

`FromEnvironment` reads `AW_LOG_LEVEL` and takes the host's clock (a `FakeTimeProvider` in the DI validation test). It
never fails the host: an unknown level stays at information and sets `TelemetryOptions.Problem`
(`AW-TELEMETRY-001`, without the value, which could be anything pasted into the variable); the runtime writes it once
as its first line, a warning whose `eventName` is the code. Without DI, `using var telemetry =
TelemetryRuntime.Start(options)` and `telemetry.LoggerFactory`. `AddKernelTelemetry` starts the runtime right away (so
spans are recorded and the problem line is written while Compose runs, before anything resolves a logger) and
registers it as a singleton the container disposes; disposing the runtime flushes it. Run **one runtime per process**: the withheld counter is process-wide.

OpenTelemetry's SDK reads some standard `OTEL_*` variables. The resource is exactly `service.name` from `ServiceName`
(`ResourceBuilder.CreateEmpty()`, so `OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` are ignored), and the sampler
is set in code (always on, so `OTEL_TRACES_SAMPLER` is ignored). `OTEL_SDK_DISABLED=true` still turns the providers
off: spans aren't recorded and log lines then carry null ids. None of them can turn exporting on: only an exporter
the host passes exports.

| Option | Default | Notes |
|---|---|---|
| `ServiceName` | required | On spans and metrics (the OpenTelemetry resource) |
| `MinimumLevel` | information | `AW_LOG_LEVEL`: trace, debug, information, warning, error, critical, none (any case); anything else is `AW-TELEMETRY-001` |
| `Sink` | standard error | An `ILogSink` (below) |
| `Redactor` | `Redactor.Default` | The scrubber on every path |
| `Clock` | `TimeProvider.System` | Log timestamps (`FakeTimeProvider` in tests) |
| `SourcePattern` | `Aw.*` | Activity sources and meters listened to (OpenTelemetry wildcard) |
| `TraceExporter`, `MetricExporter` | null | **Exporting is off by default.** An exporter is an adapter the host opts into (an OTLP exporter for a local collector is added with the first host that needs it); the runtime owns and disposes it |

## Log lines

One JSON object per line, written with `Utf8JsonWriter` (no reflection), relaxed escaping, '\n' terminated:

```json
{"timestamp":"2026-10-10T06:00:00.0000000Z","level":"warning","category":"Aw.Smoke","eventId":3,"eventName":"Call","message":"calling with Authorization: Bearer [REDACTED:bearer]","traceId":"…32 hex…","spanId":"…16 hex…","actor":"host:smoke","tenant":"personal","ring":0,"correlationId":"0f8fad5b-…","fields":{"Item":"item-1"}}
```

- `timestamp` in the AwJson `O` form (UTC, seven fractional digits, `Z`); `level` lowercase; `eventId` always,
  `eventName` and `exception` (the exception's `ToString()`) only when present.
- `traceId`/`spanId`: the current `Activity`, or `null` outside a span. The tracer provider records spans even with
  exporting off (unless `OTEL_SDK_DISABLED=true`), so log lines inside a span carry ids.
- `actor`, `tenant`, `ring`, `correlationId`, `causationId`: from the innermost `EventContext` scope
  (`logger.BeginScope(context)`). Other scope states are not written.
- `fields`: the structured values of a `[LoggerMessage]` call, as invariant text (a list as its elements joined with
  ", "). A name the template repeats gets a numbered key (`A`, `A_2`), so no JSON property repeats.
- Log through `[LoggerMessage]` source-generated methods (`CA1848`); hand finished text to `ILogger.Log` only when the
  text is built at run time.

## Scrubbing on every path

A name counts as a secret's when `IRedactor.IsSecretName` says so after a leading `@` or `$` (template destructuring
prefixes) is removed.

| Path | What happens |
|---|---|
| Log line | category, event name, message, exception (`ToString()`, inner exceptions included), every field key and value scrubbed; a field with a secret's **name** is withheld whole (`[REDACTED:secret-field]`), and so is a dictionary's or key-value list's entry with a secret's name (`[password, [REDACTED:secret-field]]`); a `char[]` is scrubbed as one string. The message is rendered again from its template when it holds such a value, as the formatter does (alignment and format applied), each hole taking the field of its own name: a source-generated `[LoggerMessage]` lists fields in parameter order, so position would put a value in the wrong hole |
| Span (only with a trace exporter) | the scrubbing processor runs before the export processor: display name, tag values and status description are rewritten in place. A tag with a secret's name is withheld whatever its type; numbers and flags are kept; a list is scrubbed element by element, with secret-named dictionary entries withheld (the tag becomes a string array); any other object is scrubbed as its text. The operation name, trace state, tag keys, events and links can't be rewritten, so a span with a secret in any of them is **dropped** (marked not recorded). Without an exporter nothing leaves the process, so spans aren't touched |
| Metric | the exporter is wrapped: an instrument whose meter tags or any point's tags carry a secret (in a key, a value, a list element, or as a secret's name) is **withheld** from every export for the life of the process (cumulative points come back each time) and counted once. Metric tags should be small fixed sets |
| Blocked scrub (a match timeout) | never passes raw text: a log line is written with `[REDACTED:blocked]` in place of the text; a span or metric instrument is dropped |
| Failure (the sink or a value's `ToString()` throws) | never thrown into the caller: the log line or span is dropped and counted as `failure`; `Flush` and `Dispose` never throw (a writer the host already closed is counted too) |

Withheld records are counted: `TelemetryRuntime.Withheld` (logs, spans, metrics, failures, for this runtime) and the
process-wide counter `aw.telemetry.scrub.withheld` with tag `path` (`log`, `span`, `metric`, `failure`), never with
the payload.

**Limit:** the wrapped metric exporter's `ParentProvider` stays unset (OpenTelemetry's setter is internal), so an
exporter that reads its resource from the provider sees none, and metric exports carry no `service.name`. The OTLP
adapter, when a host adds one, has to take the resource explicitly.

## The sink port

`ILogSink.WriteLine(line)` and `Flush()`; one call is one line, a line with a line break is refused
(`ArgumentException`), concurrent writers never interleave. `TextWriterLogSink` is the adapter over any `TextWriter`
(standard error, or a file the host opened; the host owns the writer). The in-memory fake (`InMemoryLogSink`) and every
real sink pass `LogSinkContract` in `tests/Aw.Kernel.Telemetry.Testing`; that project also has
`CollectingActivityExporter` and `CollectingMetricExporter` for tests that opt into exporting.

## NativeAOT

`src/Aw.Kernel.Telemetry.AotSmoke/run.sh` publishes a NativeAOT binary (0 IL2xxx/IL3xxx warnings required), runs it and
compares its output with `expected.txt`; the CI .NET job runs it with the other smokes. `stdin-mode` logs each
standard-input line and prints the JSON lines (for a hand test).
