using System.Runtime.InteropServices;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// What telemetry withheld so far: log lines whose text was replaced because a scrub was blocked (a match timeout);
/// spans dropped before export because they carried a secret the processor can't rewrite or a scrub was blocked;
/// metric instruments withheld from export for the same reasons (counted once each); and failures, records dropped
/// because writing or scrubbing them threw (a full disk, a closed writer, a value whose text throws). The same numbers
/// are published as the counter 'aw.telemetry.scrub.withheld' (tag 'path': log, span, metric, failure).
/// </summary>
/// <param name="Logs">Log lines written with withheld text.</param>
/// <param name="Spans">Spans dropped before export.</param>
/// <param name="Metrics">Metric instruments withheld from export.</param>
/// <param name="Failures">Records dropped because writing or scrubbing them threw.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct WithheldCounts(long Logs, long Spans, long Metrics, long Failures);
