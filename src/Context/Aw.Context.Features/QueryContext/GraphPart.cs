using Aw.Context.Core.Packing;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the graph adds to an answer: its items, the map's citation, the anchors, the version read and the suggestions.</summary>
/// <param name="Candidates">The graph, affects, decisions and code items, in order.</param>
/// <param name="Cites">The map's citation ('C1'), or none.</param>
/// <param name="Anchors">The anchors' keys.</param>
/// <param name="MapVersion">The map version read ('v1'), or null without a map.</param>
/// <param name="Suggested">Suggested anchors.</param>
internal sealed record GraphPart(IReadOnlyList<PackCandidate> Candidates, IReadOnlyDictionary<string, Citation> Cites, IReadOnlyList<string> Anchors,
    string? MapVersion, IReadOnlyList<string> Suggested)
{
    internal static GraphPart None { get; } = new([], new Dictionary<string, Citation>(StringComparer.Ordinal), [], MapVersion: null, []);
}
