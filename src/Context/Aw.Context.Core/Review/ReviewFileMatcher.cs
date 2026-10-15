using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>Matches a review file against the latest draft's queue (S-4).</summary>
public static class ReviewFileMatcher
{
    /// <summary>Matches the decided entries.</summary>
    /// <param name="file">The file.</param>
    /// <param name="queue">The latest draft's queue.</param>
    /// <returns>The decisions to apply and the entries skipped, or SND-CONTEXT-043 when no decided entry matches.</returns>
    public static Result<ReviewFileMatch> Match(ReviewFile file, ReviewQueueView queue)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(queue);
        throw new NotSupportedException("S-4: ReviewFileMatcher.Match is not built yet");
    }
}
