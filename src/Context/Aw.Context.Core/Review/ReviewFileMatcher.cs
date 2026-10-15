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
            var name = entry.Link?.Value ?? entry.Node?.Value ?? entry.Label;
            var found = queue.Entries.Any(q => q.Group != ReviewGroup.Orphaned && (entry.Link is { } l ? q.Link?.Value == l.Value : q.Link is null && entry.Node is { } n && q.Node.Value == n.Value));
            if (!found)
            {
                skipped.Add($"{name}: not in the latest draft's queue");
            }
            else if (entry.Decision is not { } verb || !Carried(verb, entry))
            {
                skipped.Add($"{name}: a file can't carry '{entry.Decision.ToString()?.ToLowerInvariant()}' here");
            }
            else
            {
                decisions.Add(new ReviewDecision(verb, entry.Link is null ? entry.Node : null, entry.Link, Label: verb == ReviewVerb.Rename ? entry.Label : null, Other: entry.Other));
            }
        }

        return decisions.Count == 0
            ? ContextErrors.ReviewFileNoMatch.With($"no decided entry of the file is in draft {queue.Draft} (fix: export the queue again with 'sindri context map review --export')")
            : new ReviewFileMatch(decisions, skipped);
    }

    // accept and reject for nodes and links; rename, move and merge need a node.
    private static bool Carried(ReviewVerb verb, ReviewFileEntry entry) => verb switch
    {
        ReviewVerb.Accept or ReviewVerb.Reject => true,
        ReviewVerb.Rename => entry.Link is null && !string.IsNullOrWhiteSpace(entry.Label),
        ReviewVerb.Move or ReviewVerb.Merge => entry.Link is null && entry.Other is not null,
        _ => false,
    };
}
