using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;

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
            new ReviewFileEntry(e.Link is null ? e.Node : null, e.Link, e.Label, e.Group, e.Confidence))]);
    }
}

/// <summary>One entry of a review file. A person sets 'decision' (and 'label' or 'other' for a rename, move or merge) and leaves the rest.</summary>
/// <param name="Node">The proposed node, or null for a link.</param>
/// <param name="Link">The proposed link, or null for a node.</param>
/// <param name="Label">The entry's label (a rename's new label when the decision is rename).</param>
/// <param name="Group">Where it sat in the queue.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Decision">What to do; null leaves the entry undecided.</param>
/// <param name="Other">The node to move under or merge into.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewFileEntry(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NodeKey? Node,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LinkId? Link,
    string Label,
    ReviewGroup Group,
    double Confidence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewVerb? Decision = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NodeKey? Other = null);
