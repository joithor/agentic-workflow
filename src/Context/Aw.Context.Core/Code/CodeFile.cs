using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Code;

/// <summary>A file that implements a node of the anchors' own scope, with the tests that cover it.</summary>
/// <param name="Path">The path in the repo.</param>
/// <param name="Label">The node's label as the caller reads it.</param>
/// <param name="Kind">'code'.</param>
/// <param name="Implements">The scope nodes it implements.</param>
/// <param name="Tests">The scope nodes it tests.</param>
/// <param name="Provenance">Of its best link.</param>
/// <param name="Confidence">Of its best link.</param>
/// <param name="Confirmed">True when some link of it is confirmed.</param>
/// <param name="Suggested">True when every link of it is an unconfirmed (model) suggestion.</param>
/// <param name="LinkId">The best unconfirmed link, to accept or reject it; null unless suggested.</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
/// <param name="Anchors">The anchors whose own scope it implements.</param>
/// <param name="TestedBy">The paths in 'tests' that cover it, by any basis.</param>
public sealed record CodeFile(
    string Path,
    string Label,
    NodeKind Kind,
    IReadOnlyList<NodeKey> Implements,
    IReadOnlyList<NodeKey> Tests,
    Provenance Provenance,
    double Confidence,
    bool Confirmed,
    bool Suggested,
    LinkId? LinkId,
    Results.Trust Trust,
    IReadOnlyList<NodeKey> Anchors,
    IReadOnlyList<string> TestedBy)
{
    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }
}
