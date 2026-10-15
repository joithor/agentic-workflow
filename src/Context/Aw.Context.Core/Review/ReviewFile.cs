using System.Text.Json.Serialization;

namespace Aw.Context.Core.Review;

/// <summary>The review file 'map review --export' writes and '--apply' reads (S-4): the queue as entries a person fills in with a decision.</summary>
/// <param name="Draft">The draft the file was exported from ('v14@7c3e1a2').</param>
/// <param name="Entries">One per undecided entry.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewFile(string Draft, IReadOnlyList<ReviewFileEntry> Entries)
{
    /// <summary>The file for a queue: every entry undecided.</summary>
    /// <param name="queue">The queue.</param>
    /// <returns>The file.</returns>
    public static ReviewFile Of(ReviewQueueView queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return new ReviewFile(queue.Draft, [.. queue.Entries.Where(e => e.Group != ReviewGroup.Orphaned).Select(e =>
            new ReviewFileEntry(e.Label, e.Group, e.Confidence, e.Link is null ? e.Node : null, e.Link))]);
    }
}
