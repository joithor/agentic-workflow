using System.Diagnostics.Metrics;

namespace Aw.Kernel.Telemetry;

/// <summary>Counts what the scrubber withheld, per path, and publishes it as 'aw.telemetry.scrub.withheld'.</summary>
internal sealed class Withheld : IDisposable
{
    public const string MeterName = "Aw.Kernel.Telemetry";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _counter;
    private long _logs;
    private long _spans;
    private long _metrics;

    public Withheld()
    {
        _counter = _meter.CreateCounter<long>("aw.telemetry.scrub.withheld", unit: "{record}", "Records the scrubber withheld, by path");
    }

    public WithheldCounts Counts => new(Interlocked.Read(ref _logs), Interlocked.Read(ref _spans), Interlocked.Read(ref _metrics));

    public void Log() => Count(ref _logs, "log");

    public void Span() => Count(ref _spans, "span");

    public void Metric() => Count(ref _metrics, "metric");

    public void Dispose() => _meter.Dispose();

    private void Count(ref long field, string path)
    {
        _ = Interlocked.Increment(ref field);
        _counter.Add(1, new KeyValuePair<string, object?>("path", path));
    }
}
