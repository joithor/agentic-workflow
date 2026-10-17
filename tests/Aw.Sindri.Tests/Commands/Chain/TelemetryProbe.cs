using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>
/// Listens to the 'Aw.Sindri' spans and the 'sindri.commands' counter while it lives, keeping only what ran under its own root
/// span: the listeners are process-wide, so a parallel test class running the same command would otherwise leak in.
/// </summary>
internal sealed class TelemetryProbe : IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<Activity> _spans = [];
    private readonly List<(long Value, Dictionary<string, object?> Tags)> _counted = [];
    private readonly ActivityListener _activities;
    private readonly MeterListener _meters = new();
    private readonly ActivitySource _source = new("Aw.Sindri");
    private readonly Activity _root;
    private readonly ActivityTraceId _trace;

    public TelemetryProbe()
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = static s => s.Name == "Aw.Sindri",
            Sample = static (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => { if (a.TraceId == _trace) { lock (_gate) { _spans.Add(a); } } },
        };
        ActivitySource.AddActivityListener(_activities);
        _root = _source.StartActivity("sindri.test.probe") ?? throw new InvalidOperationException("The probe's root span was not sampled.");
        _trace = _root.TraceId;
        _meters.InstrumentPublished = static (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Aw.Sindri" && instrument.Name == "sindri.commands")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meters.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            if (Activity.Current?.TraceId != _trace)
            {
                return;
            }

            var named = tags.ToArray().ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal);
            lock (_gate) { _counted.Add((value, named)); }
        });
        _meters.Start();
    }

    public IReadOnlyList<Activity> Spans { get { lock (_gate) { return [.. _spans]; } } }

    public IReadOnlyList<(long Value, Dictionary<string, object?> Tags)> Counted { get { lock (_gate) { return [.. _counted]; } } }

    public void Dispose()
    {
        _root.Dispose();
        _source.Dispose();
        _activities.Dispose();
        _meters.Dispose();
    }
}
