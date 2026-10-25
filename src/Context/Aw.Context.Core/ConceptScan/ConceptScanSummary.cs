using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.ConceptScan;

/// <summary>The compact text of a draft map the first model pass reads.</summary>
public static class ConceptScanSummary
{
    /// <summary>The keys of the graph's features, in key order.</summary>
    /// <param name="graph">The draft's graph.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlyList<string> FeatureKeys(GraphSnapshot graph) => throw new NotSupportedException();

    /// <summary>Writes the summary, cut at <paramref name="maxChars"/> at a line end.</summary>
    /// <param name="graph">The draft's graph.</param>
    /// <param name="glossary">The glossary, or null.</param>
    /// <param name="maxChars">The most characters the text may have.</param>
    /// <returns>The text.</returns>
    public static string Build(GraphSnapshot graph, MapGlossary? glossary, int maxChars) => throw new NotSupportedException();
}
