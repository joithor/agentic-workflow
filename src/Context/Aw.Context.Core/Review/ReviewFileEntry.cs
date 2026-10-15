using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Review;

/// <summary>One entry of a review file. A person sets 'decision' (and 'label' or 'other' for a rename, move or merge) and leaves the rest.</summary>
/// <param name="Label">The entry's label (a rename's new label when the decision is rename).</param>
/// <param name="Group">Where it sat in the queue.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Node">The proposed node, or null for a link.</param>
/// <param name="Link">The proposed link, or null for a node.</param>
/// <param name="Decision">What to do; null leaves the entry undecided.</param>
/// <param name="Other">The node to move under or merge into.</param>
// The optional parameters come last so a file may leave them out: the JSON reader requires every parameter without a default.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReviewFileEntry(
    string Label,
    ReviewGroup Group,
    double Confidence,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NodeKey? Node = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LinkId? Link = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewVerb? Decision = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NodeKey? Other = null);
