using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Review;

/// <summary>Builds what is left to decide in a draft (spec §8.7, S-4): deterministic proposals, model proposals and suggested file links by impact, then orphaned overrides.</summary>
public static class ReviewQueue
{
    /// <summary>Builds the queue.</summary>
    /// <param name="label">The draft read ('v14@7c3e1a2').</param>
    /// <param name="draft">The draft as built (before overrides).</param>
    /// <param name="overrides">Every override.</param>
    /// <returns>The undecided entries and how many proposals already have a decision.</returns>
    public static ReviewQueueView Build(string label, GraphSnapshot draft, IReadOnlyList<MapOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(overrides);
        throw new NotSupportedException("S-4: ReviewQueue.Build is not built yet");
    }
}
