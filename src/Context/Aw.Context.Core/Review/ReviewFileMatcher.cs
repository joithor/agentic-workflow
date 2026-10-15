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
        var decisions = new List<ReviewDecision>();
        var skipped = new List<string>();
        foreach (var entry in file.Entries.Where(e => e.Decision is not null))
        {
            var decision = Decide(entry, queue);
            if (decision.IsSuccess)
            {
                decisions.Add(decision.Value);
            }
            else
            {
                skipped.Add(decision.Error.Message);
            }
        }

        return decisions.Count == 0
            ? ContextErrors.ReviewFileNoMatch.With($"no decided entry of the file is in draft {queue.Draft} (fix: export the queue again with 'sindri context map review --export')")
            : new ReviewFileMatch(decisions, skipped);
    }

    // The error's message is the skipped line; it never leaves this class as an error.
    private static Result<ReviewDecision> Decide(ReviewFileEntry entry, ReviewQueueView queue)
    {
        var name = NameOf(entry);
        if (!queue.Entries.Any(q => q.Group != ReviewGroup.Orphaned && Names(entry, q)))
        {
            return ContextErrors.ReviewFileNoMatch.With($"{name}: not in the latest draft's queue");
        }

        if (entry.Decision is not { } verb || !Carried(verb, entry))
        {
            return ContextErrors.ReviewFileNoMatch.With($"{name}: a file can't carry '{entry.Decision}' here");
        }

        return new ReviewDecision(verb, entry.Link is null ? entry.Node : null, entry.Link, Label: verb == ReviewVerb.Rename ? entry.Label : null, Other: entry.Other);
    }

    private static string NameOf(ReviewFileEntry entry) => entry.Link is { } link ? link.Value : entry.Node is { } node ? node.Value : entry.Label;

    private static bool Names(ReviewFileEntry entry, ReviewEntry queued) =>
        entry.Link is { } link ? queued.Link?.Value == link.Value : queued.Link is null && entry.Node is { } node && queued.Node.Value == node.Value;

    // accept and reject for nodes and links; rename, move and merge need a node.
    private static bool Carried(ReviewVerb verb, ReviewFileEntry entry) => verb switch
    {
        ReviewVerb.Accept or ReviewVerb.Reject => true,
        ReviewVerb.Rename => entry.Link is null && !string.IsNullOrWhiteSpace(entry.Label),
        ReviewVerb.Move or ReviewVerb.Merge => entry.Link is null && entry.Other is not null,
        ReviewVerb.AcceptDeterministic or ReviewVerb.Undo or ReviewVerb.Drop or ReviewVerb.Add or ReviewVerb.Delete => false,
        _ => false,
    };
}
