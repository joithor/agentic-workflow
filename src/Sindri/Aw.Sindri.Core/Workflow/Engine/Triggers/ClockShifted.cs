using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>Sleep or downtime (spec W18, §5.6): every armed deadline moves later by the unaccounted gap, which counts as time when nothing ran.</summary>
/// <param name="Actor">'service:workflow.timers'.</param>
/// <param name="Tick">The timers service's tick that measured the gap.</param>
/// <param name="Gap">Wall-clock time minus monotonic time since the last tick, at least 60 s.</param>
public sealed record ClockShifted(string Actor, long Tick, TimeSpan Gap) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.Shift;

    /// <inheritdoc/>
    public override string KeyPart => string.Create(CultureInfo.InvariantCulture, $"shift:{Tick}");
}
