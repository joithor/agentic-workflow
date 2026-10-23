using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What changed between two digests of one pull request (PR babysitter spec §6.1): the outcome of the 'pr.changed' event is the first change in the order
/// merged, closed, updated (the head moved), review (a review, a comment added or edited) and ci (a check changed state); 'changes' lists all of them.
/// </summary>
internal static class PrChanges
{
    /// <summary>The outcomes in priority order.</summary>
    internal static readonly string[] Order = ["merged", "closed", "updated", "review", "ci"];

    /// <summary>The changes <paramref name="now"/> shows over <paramref name="before"/>, in <see cref="Order"/>; empty when nothing changed.</summary>
    /// <param name="before">The digest the watcher recorded last.</param>
    /// <param name="now">The digest it just read.</param>
    /// <returns>The change names.</returns>
    internal static IReadOnlyList<string> Between(WatchedDigest before, PullDigest now)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(now);
        var named = new HashSet<string>(StringComparer.Ordinal);
        if (now.State == PullState.Merged && before.State != PullState.Merged)
        {
            _ = named.Add("merged");
        }

        if (now.State == PullState.Closed && before.State != PullState.Closed)
        {
            _ = named.Add("closed");
        }

        if (!string.Equals(now.HeadSha, before.HeadSha, StringComparison.Ordinal))
        {
            _ = named.Add("updated");
        }

        if (now.CommentCount != before.CommentCount)
        {
            _ = named.Add("review");
        }

        if (now.Checks != before.Checks)
        {
            _ = named.Add("ci");
        }

        if (named.Count == 0 && !string.Equals(now.Version, before.Version, StringComparison.Ordinal))
        {
            _ = named.Add("review");   // why: the version moved with no field the digest names: a comment was edited
        }

        return [.. Order.Where(named.Contains)];
    }
}
