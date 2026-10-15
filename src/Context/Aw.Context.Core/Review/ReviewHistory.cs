using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Review;

/// <summary>The override log as a person reads it (S-4).</summary>
public static class ReviewHistory
{
    /// <summary>Builds the history from every override, newest first.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The history.</returns>
    public static ReviewHistoryView Of(IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        var undone = OverrideLog.Undone(overrides);
        return new ReviewHistoryView([.. OverrideLog.Ordered(overrides).Reverse().Select(o =>
        {
            var id = o.ToId();
            return new ReviewHistoryEntry(id, o.Action, o.Target, o.Link, o.Label, o.Who, o.At, undone.Contains(id.Value), o.Undoes);
        })]);
    }
}
