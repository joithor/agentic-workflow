using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>Turns one review decision into override rows (S-4): checks it still applies to the latest draft (SND-CONTEXT-047) and writes nothing itself. Pure.</summary>
public static class ReviewPlanner
{
    /// <summary>Plans the rows for a decision.</summary>
    /// <param name="decision">What the person decided.</param>
    /// <param name="draft">The latest draft as built (before overrides).</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="who">The OS user who decided.</param>
    /// <param name="at">When.</param>
    /// <returns>The rows to append, or SND-CONTEXT-047 when the target is gone or decided already.</returns>
    public static Result<IReadOnlyList<MapOverride>> Plan(ReviewDecision decision, GraphSnapshot draft, IReadOnlyList<MapOverride> overrides, string who, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(overrides);
        _ = (who, at);
        throw new NotSupportedException("S-4: ReviewPlanner.Plan is not built yet");
    }
}
