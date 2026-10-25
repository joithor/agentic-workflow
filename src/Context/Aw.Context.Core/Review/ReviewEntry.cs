using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Review;

/// <summary>One thing to decide in the latest draft.</summary>
/// <param name="Node">The proposed node (for a link: its 'from' end).</param>
/// <param name="Link">The proposed link when the entry is a link, else null.</param>
/// <param name="Group">Where it sits in the queue.</param>
/// <param name="Label">Its label (scrubbed, one line).</param>
/// <param name="Provenance">Who produced it.</param>
/// <param name="Confidence">0 to 1.</param>
/// <param name="Impact">How many links and decisions hang on it; the model group is ordered by it.</param>
/// <param name="Files">The files that implement it ('code:' keys).</param>
/// <param name="Parent">The feature it is under, or null.</param>
/// <param name="Override">For an orphaned entry: the override whose target is gone (the id a 'drop' names), else null.</param>
/// <param name="Trust">Whether its label may be read as instructions: a model's proposal is untrusted until a person accepts it.</param>
public sealed record ReviewEntry(
    NodeKey Node,
    LinkId? Link,
    ReviewGroup Group,
    string Label,
    Provenance Provenance,
    double Confidence,
    int Impact,
    IReadOnlyList<NodeKey> Files,
    NodeKey? Parent,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OverrideId? Override = null,
    Results.Trust Trust = Results.Trust.Trusted)
{
    /// <summary>The repo the proposed node belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo, such as a decision that spans repos or an orphaned override; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }
}
