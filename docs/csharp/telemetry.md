# Aw.Kernel.Telemetry

Logging, spans and metrics for every C# host, with `Aw.Redaction`'s scrubber on every log and export path (spec §4b.1,
§4d "runtime hardening", §5 W7). Tier 0, sublevel 1 in `deps.yaml` (edges to `Kernel` and `Redaction`); its one
external dependency is `OpenTelemetry` (which brings `Microsoft.Extensions.Logging`).

## Start it in the Host

```csharp
// Program.cs (a CLI host): level from AW_LOG_LEVEL, log lines to standard error, nothing exported.
var options = TelemetryOptions.FromEnvironment("aw-readiness", Environment.GetEnvironmentVariable);
if (options.IsError) { Console.Error.WriteLine(options.Error.Message); return 2; }
services.AddKernelTelemetry(options.Value);          // in Compose: TelemetryRuntime, ILoggerFactory, ILogger<T>
```

OpenTelemetry's SDK also reads its standard `OTEL_*` environment variables (sampler, resource attributes, limits).
None of them can turn exporting on: no exporter package is referenced, so only an exporter the host passes exports.

Without DI, `using var telemetry = TelemetryRuntime.Start(options)` and `telemetry.LoggerFactory`. Disposing the
runtime flushes it; the DI container disposes the singleton for you.

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
  exporting off, so log lines inside a span always carry ids.
- `actor`, `tenant`, `ring`, `correlationId`, `causationId`: from the innermost `EventContext` scope
  (`logger.BeginScope(context)`). Other scope states are not written.
- `fields`: the structured values of a `[LoggerMessage]` call, as invariant text.
- Log through `[LoggerMessage]` source-generated methods (`CA1848`); hand finished text to `ILogger.Log` only when the
  text is built at run time.

## Scrubbing on every path

| Path | What happens |
|---|---|
| Log line | category, event name, message, exception and every field scrubbed; a field whose **name** is a secret's (`IsSecretName`: password, token, api_key, Authorization, …) is withheld whole (`[REDACTED:secret-field]`), and so is its hole in the message (the message is re-rendered from its template) |
| Span | the scrubbing processor runs first, before any export processor: name, tag values (secret-named tags withheld) and status description are rewritten in place. Events and links can't be rewritten, so a span with a secret in an event or link is **dropped** (marked not recorded) |
| Metric | the exporter is wrapped: a metric stream with a secret tag value or a secret-named tag is **dropped** (metric tags should be small fixed sets) |
| Blocked scrub (a match timeout) | never passes raw text: a log line is written with `[REDACTED:blocked]` in place of the text; a span or metric stream is dropped |

Withheld records are counted: `TelemetryRuntime.Withheld` (logs, spans, metrics) and the counter
`aw.telemetry.scrub.withheld` with tag `path` (`log`, `span`, `metric`), never with the payload.

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
