using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Review;

/// <summary>The override log as a person reads it (S-4).</summary>
public static class ReviewHistory
{
    /// <summary>Builds the history from every override, newest first.</summary>
    /// <param name="overrides">Every override.</param>
    /// <returns>The history.</returns>
    public static ReviewHistoryView Of(IReadOnlyList<MapOverride> overrides) => throw new NotSupportedException();
}
