using Aw.Kernel;

namespace Aw.Context.Core.Overrides;

/// <summary>When an override may be undone (S-4).</summary>
public static class UndoRules
{
    /// <summary>Finds the override an undo would reverse.</summary>
    /// <param name="overrides">Every override.</param>
    /// <param name="target">The id to reverse.</param>
    /// <returns>The row, or SND-CONTEXT-047 when the id is unknown or already undone.</returns>
    public static Result<MapOverride> Check(IReadOnlyList<MapOverride> overrides, OverrideId target)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        _ = target;
        throw new NotSupportedException("S-4: UndoRules.Check is not built yet");
    }
}
