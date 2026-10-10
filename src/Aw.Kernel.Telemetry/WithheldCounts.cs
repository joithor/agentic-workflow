using System.Runtime.InteropServices;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// What the scrubber withheld so far: log lines whose text was replaced because a scrub was blocked (a match timeout),
/// and spans and metric streams dropped before export because they carried a secret the processor can't rewrite or a
/// scrub was blocked. The same numbers are published as the counter 'aw.telemetry.scrub.withheld' (tag 'path').
/// </summary>
/// <param name="Logs">Log lines written with withheld text.</param>
/// <param name="Spans">Spans dropped before export.</param>
/// <param name="Metrics">Metric streams dropped before export.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct WithheldCounts(long Logs, long Spans, long Metrics);
