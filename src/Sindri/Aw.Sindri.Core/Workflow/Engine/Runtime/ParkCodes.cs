using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The typed code of a park, so a reader never parses its words.</summary>
public static class ParkCodes
{
    /// <summary>The code of a Step or resource with no runtime in this build (W5).</summary>
    public const string NoRuntime = "SND-WF-010";

    /// <summary>The code of <paramref name="suspension"/>: the refusal's own where the park carries one, 'SND-WF-010' for a Step with no runtime, else null. An escalation or a takeover has none.</summary>
    /// <param name="suspension">The suspension.</param>
    /// <returns>The code, or null.</returns>
    public static string? Of(Suspension suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        return suspension.Kind != SuspensionKind.Park ? null : suspension.Code ?? (suspension.Reason == ParkReason.NoRuntime ? NoRuntime : null);
    }
}
