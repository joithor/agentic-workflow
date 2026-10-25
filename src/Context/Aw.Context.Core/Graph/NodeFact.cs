using System.Text.Json.Serialization;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Graph;

/// <summary>A node as an answer shows it: its label scrubbed (and fenced when untrusted), who produced it, whether a human confirmed it and whether its text can be trusted.</summary>
/// <param name="Key">The key.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Label">The label, scrubbed, one line, at most 200 characters; inside the untrusted fence when <paramref name="Trust"/> is untrusted.</param>
/// <param name="Provenance">Who produced it (after overrides: an accepted node is human).</param>
/// <param name="Confirmed">True when a human confirmed it.</param>
/// <param name="Trust">Whether its text can be trusted.</param>
/// <param name="Confidence">0 to 1.</param>
public sealed record NodeFact(NodeKey Key, NodeKind Kind, string Label, Provenance Provenance, bool Confirmed, Results.Trust Trust, double Confidence)
{
    /// <summary>The repo it belongs to (see <see cref="NodeRepos"/>), or null when it belongs to no single repo or the answer doesn't say; left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repo { get; init; }
}
