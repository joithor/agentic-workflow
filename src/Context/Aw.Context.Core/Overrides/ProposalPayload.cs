using System.Text.Json.Serialization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>What a 'propose' override carries (S-4): the scoping run's proposal for a node the map doesn't have yet.</summary>
/// <param name="Run">The scoping run that proposed it.</param>
/// <param name="Repo">The repo the surface was found in.</param>
/// <param name="Kind">The proposed node's kind.</param>
/// <param name="Parent">The feature it belongs under, or null.</param>
/// <param name="Evidence">The records it rests on, or null.</param>
/// <param name="Quote">The brief's quote that names it, or null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProposalPayload(
    string Run,
    ContextRepoName Repo,
    NodeKind Kind,
    NodeKey? Parent = null,
    IReadOnlyList<EvidenceRef>? Evidence = null,
    string? Quote = null)
{
    /// <summary>The proposal as the lines that join its override's id hash (a method: it is never read from a file).</summary>
    /// <returns>The text.</returns>
    public string ToHashText() => $"{Run}\n{Repo.Value}\n{Kind}\n{Parent?.Value}\n{Quote}";
}
