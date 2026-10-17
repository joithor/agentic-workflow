using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>The file-level links of a graph ('code:&lt;path&gt;' nodes with 'implements' and 'tests' edges), by file path, with the intent on features.</summary>
internal sealed class FileLinks
{
    private const string CodePrefix = "code:";
    private readonly Dictionary<string, List<GraphEdge>> _byFile;
    private readonly Dictionary<string, HashSet<string>> _records = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _decisions = new(StringComparer.Ordinal);

    /// <param name="graph">The graph.</param>
    /// <param name="repo">The repo whose links are read, or null for every link: an edge whose file evidence ('repo:&lt;repo&gt;/&lt;path&gt;') names only other repos is left out; one with no repo evidence counts for any repo.</param>
    internal FileLinks(GraphSnapshot graph, Engine.ContextRepoName? repo = null)
    {
        _byFile = graph.Edges.Where(e => e.Type is EdgeType.Implements or EdgeType.Tests && BelongsTo(e, repo) && e.From.Value.StartsWith(CodePrefix, StringComparison.Ordinal) && !e.From.Value.Contains('#', StringComparison.Ordinal))
            .GroupBy(e => e.From.Value[CodePrefix.Length..], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        foreach (var edge in graph.Edges.Where(e => e.Type is EdgeType.Mentions or EdgeType.Decides))
        {
            var index = edge.Type == EdgeType.Mentions ? _records : _decisions;
            if (!index.TryGetValue(edge.To.Value, out var from))
            {
                index[edge.To.Value] = from = new HashSet<string>(StringComparer.Ordinal);
            }

            _ = from.Add(edge.From.Value);
        }
    }

    private static bool BelongsTo(GraphEdge edge, Engine.ContextRepoName? repo)
    {
        if (repo is not { } wanted)
        {
            return true;
        }

        var files = edge.EvidenceOrEmpty.Select(r => r.Key.Value).Where(k => k.StartsWith("repo:", StringComparison.Ordinal)).ToList();
        return files.Count == 0 || files.Exists(k => k.StartsWith($"repo:{wanted.Value}/", StringComparison.Ordinal));
    }

    /// <summary>Mapped (a confirmed 'implements' edge), suggested (only unconfirmed ones) or unmapped.</summary>
    internal FileMapStatus StatusOf(string file)
    {
        var own = (_byFile.GetValueOrDefault(file) ?? []).Where(e => e.Type == EdgeType.Implements).ToList();
        return own.Exists(e => Confirmation.Of(e.Provenance)) ? FileMapStatus.Mapped : own.Count > 0 ? FileMapStatus.Suggested : FileMapStatus.Unmapped;
    }

    /// <summary>A file's links as features: a suggestion keeps its link id.</summary>
    internal IEnumerable<FileFeature> FeaturesOf(string file, bool confirmedOnly) =>
        (_byFile.GetValueOrDefault(file) ?? []).Where(e => !confirmedOnly || Confirmation.Of(e.Provenance))
            .Select(e => new FileFeature(e.To, e.Provenance, e.Provenance == Provenance.Model ? e.Confidence : null, Confirmation.Of(e.Provenance),
                Confirmation.Of(e.Provenance) ? null : LinkIds.For(e), e.Type));

    /// <summary>The unconfirmed 'implements' links of a file, to accept or reject.</summary>
    internal IEnumerable<FileMapSuggestion> Suggestions(string file) =>
        (_byFile.GetValueOrDefault(file) ?? []).Where(e => e.Type == EdgeType.Implements && !Confirmation.Of(e.Provenance))
            .OrderBy(e => e.To.Value, StringComparer.Ordinal).Select(e => new FileMapSuggestion(LinkIds.For(e), file, e.To, e.Confidence, [.. e.EvidenceOrEmpty.Select(r => r.Key.Value)]));

    /// <summary>The distinct records and decisions with an edge onto any of the features.</summary>
    internal FileIntent IntentOn(IEnumerable<NodeKey> features)
    {
        var keys = features.Select(k => k.Value).ToList();
        return new FileIntent(Count(_records, keys), Count(_decisions, keys));
    }

    private static int Count(Dictionary<string, HashSet<string>> index, List<string> features) =>
        features.SelectMany(f => index.GetValueOrDefault(f) ?? []).Distinct(StringComparer.Ordinal).Count();
}
