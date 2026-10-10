using System.Diagnostics.Metrics;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// Counts what one runtime withheld, per path (<see cref="Counts"/>), and adds each to the process-wide counter
/// 'aw.telemetry.scrub.withheld' (tag 'path'). The counter's meter is one static instance that is never disposed:
/// OpenTelemetry keys a metric stream by meter and instrument name, so a second meter of the same name being
/// disposed (another runtime ending) would end the stream for every runtime.
/// </summary>
internal sealed class Withheld
{
    public const string MeterName = "Aw.Kernel.Telemetry";

    private static readonly Meter _meter = new(MeterName);
    private static readonly Counter<long> _counter =
        _meter.CreateCounter<long>("aw.telemetry.scrub.withheld", unit: "{record}", "Records telemetry withheld, by path");

    private long _logs;
    private long _spans;
    private long _metrics;
    private long _failures;

    public WithheldCounts Counts => new(Interlocked.Read(ref _logs), Interlocked.Read(ref _spans), Interlocked.Read(ref _metrics), Interlocked.Read(ref _failures));

    public void Log() => Count(ref _logs, "log");

    public void Span() => Count(ref _spans, "span");

    public void Metric() => Count(ref _metrics, "metric");

    public void Failure() => Count(ref _failures, "failure");

    private static void Count(ref long field, string path)
    {
        _ = Interlocked.Increment(ref field);
        _counter.Add(1, new KeyValuePair<string, object?>("path", path));
    }
}
