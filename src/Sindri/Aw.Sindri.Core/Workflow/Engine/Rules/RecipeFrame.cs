using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>Where an instance stands in its recipe while engine-owned frames sit on top (spec §5.1, W16).</summary>
internal static class RecipeFrame
{
    /// <summary>The recipe state the instance is in: the current frame's, or, while suspended, the frame the stack suspended; null once cancelled.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The recipe state, or null.</returns>
    public static string? State(WorkflowInstance instance)
    {
        if (!EngineStates.IsEngineOwned(instance.Frame.State))
        {
            return instance.Frame.State;
        }

        return instance.Suspended.Select(s => s.Frame.State).FirstOrDefault(s => !EngineStates.IsEngineOwned(s));
    }

    /// <summary>The park the instance is in now (its current frame is 'parked'), with the frame it suspended; null otherwise.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The top suspended frame of a park, or null.</returns>
    public static SuspendedFrame? Park(WorkflowInstance instance) =>
        instance.Frame.State == EngineStates.Parked && instance.Suspended.Count > 0 && instance.Suspended[^1].Suspension.Kind == SuspensionKind.Park
            ? instance.Suspended[^1]
            : null;

    /// <summary>A park reason as status spells it.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>Its name.</returns>
    public static string Name(ParkReason reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.NoRuntime or _ => "SND-WF-010",
    };
}
