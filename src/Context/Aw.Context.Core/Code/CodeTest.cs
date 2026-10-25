using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Code;

/// <summary>A test file that covers an implementing file, by a stored edge or by a lexical rule.</summary>
/// <param name="Path">The path in the repo.</param>
/// <param name="Label">The label as the caller reads it (the path for a lexical test).</param>
/// <param name="Kind">'code'.</param>
/// <param name="Basis">How it was reached.</param>
/// <param name="Tests">The scope nodes it covers by a 'tests' edge (empty for a lexical test).</param>
/// <param name="Named">The implementing files it covers.</param>
/// <param name="Confirmed">True when a 'tests' edge of it is confirmed.</param>
/// <param name="Suggested">True when it is not a confirmed link.</param>
/// <param name="LinkId">The unconfirmed edge, to accept or reject it; null for a confirmed edge and for a lexical test (it is not a map link).</param>
/// <param name="Trust">Whether its label may be read as instructions.</param>
/// <param name="Anchors">The anchors it covers.</param>
public sealed record CodeTest(
    string Path,
    string Label,
    NodeKind Kind,
    CodeBasis Basis,
    IReadOnlyList<NodeKey> Tests,
    IReadOnlyList<string> Named,
    bool Confirmed,
    bool Suggested,
    LinkId? LinkId,
    Results.Trust Trust,
    IReadOnlyList<NodeKey> Anchors)
{
    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }
}
