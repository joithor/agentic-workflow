using System.Runtime.InteropServices;
using Aw.Redaction;

namespace Aw.Kernel.Telemetry;

/// <summary>What scrubbing one record found: whether anything was redacted and whether any scrub was blocked.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ScrubTally(bool Redacted, bool Blocked)
{
    public bool Clean => !Redacted && !Blocked;

    public ScrubTally With(ScrubOutcome outcome) => outcome switch
    {
        ScrubOutcome.Blocked => this with { Blocked = true },
        ScrubOutcome.Redacted => this with { Redacted = true },
        ScrubOutcome.Clean => this,
        _ => this,
    };
}
