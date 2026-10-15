using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>Whether a draft may become the current map (SND-CONTEXT-046, owner answer 2026-10-14): only when no proposal is undecided.</summary>
public static class PromoteReadiness
{
    /// <summary>Checks the draft's queue.</summary>
    /// <param name="queue">The latest draft's queue, or null when there is no draft.</param>
    /// <returns>The queue when ready, else SND-CONTEXT-046 naming what is left.</returns>
    public static Result<ReviewQueueView> Check(ReviewQueueView? queue)
    {
        _ = queue;
        throw new NotSupportedException("S-4: PromoteReadiness.Check is not built yet");
    }
}
