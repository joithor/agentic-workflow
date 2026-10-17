using System.Globalization;
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
        if (queue is null)
        {
            return ContextErrors.PromoteRefused.With("there is no draft to promote (fix: sindri context map build, sindri context map propose <file>, or name context-rebuild under services: in the profile)");
        }

        return queue.Undecided > 0
            ? ContextErrors.PromoteRefused.With($"draft {queue.Draft} has {queue.Undecided.ToString(CultureInfo.InvariantCulture)} undecided review items (fix: decide them with 'sindri context map review'; skipping doesn't decide)")
            : queue;
    }
}
