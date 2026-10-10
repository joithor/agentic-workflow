using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Events;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry.AotSmoke;

/// <summary>The smoke's checks: a scrubbed JSON log line with ids and context, and spans scrubbed or dropped before export.</summary>
internal static class Checks
{
    private const string Service = "aw-telemetry-smoke";
    private static readonly ActivitySource _source = new("Aw.Smoke");

    public static async Task<int> StdinMode()
    {
        var options = TelemetryOptions.FromEnvironment(Service, Environment.GetEnvironmentVariable, TimeProvider.System);
        using var runtime = TelemetryRuntime.Start(options with { Sink = new TextWriterLogSink(Console.Out) });
        var logger = runtime.LoggerFactory.CreateLogger("Aw.Smoke.Stdin");
        while (await Console.In.ReadLineAsync().ConfigureAwait(false) is { } text)
        {
            using var span = _source.StartActivity("stdin-line");
            Emit(logger, LogLevel.Information, default, text);
        }

        return 0;
    }

    public static int Run(TextWriter output)
    {
        var sink = LogCheck(output);
        var collector = SpanCheck(output);
        MetricCheck(output);
        RegistrationCheck(output);
        var spanTexts = collector.Spans.SelectMany(s => s.TagObjects.Select(t => t.Key + "=" + Text(t.Value))
            .Concat([s.OperationName, s.DisplayName, s.StatusDescription ?? string.Empty, s.TraceStateString ?? string.Empty]));
        var leaked = sink.Lines.Concat(spanTexts).Any(l => l.Contains(Samples.Body, StringComparison.Ordinal));
        Write(output, $"secret in any output: {(leaked ? "yes" : "no")}");
        return leaked ? 1 : 0;
    }

    private static MemorySink LogCheck(TextWriter output)
    {
        var sink = new MemorySink();
        using (var runtime = TelemetryRuntime.Start(new TelemetryOptions { ServiceName = Service, Sink = sink, Clock = new FixedClock() }))
        {
            Write(output, $"exporting by default: {(runtime.IsExporting ? "on" : "off")}");
            var logger = runtime.LoggerFactory.CreateLogger("Aw.Smoke");
            var correlation = new Guid(0x0f8fad5b, 0xd9cb, 0x469f, 0xa1, 0x65, 0x70, 0x86, 0x77, 0x28, 0x95, 0x0e);
            var context = EventContext.Create("host:smoke", "personal", ring: 0, correlation).Value;
            using (logger.BeginScope(context))
            using (_source.StartActivity("work"))
            {
                Emit(logger, LogLevel.Warning, new EventId(3, "Call"), "calling with " + Samples.Bearer);
            }
        }

        var line = JsonDocument.Parse(sink.Lines[0]).RootElement;
        Write(output, $"log keys: {string.Join(',', line.EnumerateObject().Select(p => p.Name))}");
        Write(output, $"log timestamp: {line.GetProperty("timestamp").GetString()}");
        Write(output, $"log message: {line.GetProperty("message").GetString()}");
        Write(output, $"log trace id: {line.GetProperty("traceId").GetString()?.Length} hex chars");
        Write(output, $"log actor: {line.GetProperty("actor").GetString()}");
        return sink;
    }

    private static SpanCollector SpanCheck(TextWriter output)
    {
        var collector = new SpanCollector();
        using var runtime = TelemetryRuntime.Start(new TelemetryOptions { ServiceName = Service, Sink = new MemorySink(), TraceExporter = collector });
        using (var span = _source.StartActivity("call"))
        {
            // why: the runtime started above listens to this source, so StartActivity returns an activity.
            span!.DisplayName = "call " + Samples.GithubToken;
            _ = span.SetTag("header", Samples.Bearer).SetTag("count", 2);
        }

        using (var leaky = _source.StartActivity("leaky"))
        {
            _ = leaky?.AddEvent(new ActivityEvent("sent", tags: new ActivityTagsCollection { ["body"] = Samples.Bearer }));
        }

        runtime.Flush();
        Write(output, $"exporting with an exporter: {(runtime.IsExporting ? "on" : "off")}");
        Write(output, $"spans exported: {collector.Spans.Count}");
        Write(output, $"span name: {collector.Spans[0].DisplayName}");
        Write(output, $"span tag header: {Text(collector.Spans[0].GetTagItem("header"))}");
        Write(output, $"span tag count: {Text(collector.Spans[0].GetTagItem("count"))}");
        var withheld = runtime.Withheld;
        Write(output, $"withheld: logs={withheld.Logs} spans={withheld.Spans} metrics={withheld.Metrics}");
        return collector;
    }

    private static void MetricCheck(TextWriter output)
    {
        var metrics = new MetricCollector();
        using var meter = new System.Diagnostics.Metrics.Meter("Aw.Smoke.Metrics");
        using var runtime = TelemetryRuntime.Start(new TelemetryOptions { ServiceName = Service, Sink = new MemorySink(), MetricExporter = metrics });
        var counter = meter.CreateCounter<long>("aw.smoke.calls");
        var leaky = meter.CreateCounter<long>("aw.smoke.leaky");
        counter.Add(2, new KeyValuePair<string, object?>("kind", "check"));
        leaky.Add(1, new KeyValuePair<string, object?>("header", Samples.Bearer));
        runtime.Flush();
        runtime.Flush();
        Write(output, $"metric streams exported: {string.Join(',', metrics.Names.Where(n => n.StartsWith("aw.smoke", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal))}");
        Write(output, $"withheld metrics: {runtime.Withheld.Metrics}");
    }

    private static void RegistrationCheck(TextWriter output)
    {
        var sink = new MemorySink();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddKernelTelemetry(new TelemetryOptions { ServiceName = Service, Sink = sink, SourcePattern = "Aw.Smoke.Registration" });
        using (var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services))
        {
            var logger = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ILogger<MemorySink>>(provider);
            Emit(logger, LogLevel.Information, default, "from DI " + Samples.GithubToken);
        }

        var line = JsonDocument.Parse(sink.Lines[0]).RootElement;
        Write(output, $"DI logger category: {line.GetProperty("category").GetString()}");
        Write(output, $"DI logger message: {line.GetProperty("message").GetString()}");
    }

    private static void Emit(ILogger logger, LogLevel level, EventId eventId, string text) =>
        logger.Log(level, eventId, text, exception: null, static (state, _) => state);

    private static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static void Write(TextWriter output, FormattableString line) => output.Write(line.ToString(CultureInfo.InvariantCulture) + "\n");
}
