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
        var row = overrides.FirstOrDefault(o => o.ToId().Value == target.Value);
        if (row is null)
        {
            return ContextErrors.ReviewTargetStale.With($"no override {target.Value} (fix: list the history with 'sindri context map review --history')");
        }

        return OverrideLog.Undone(overrides).Contains(target.Value)
            ? ContextErrors.ReviewTargetStale.With($"override {target.Value} is already undone (fix: nothing to do)")
            : row;
    }
}
