using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Reads graphify's graph.json (an untrusted output of a third-party tool) into clusters: one per community, with the files its nodes come from.</summary>
internal static class GraphifyGraph
{
    /// <summary>The fewest files a community needs to be a candidate feature (provisional owner default 2026-10-14 night).</summary>
    public const int MinimumFiles = 2;

    /// <summary>Parses the graph.</summary>
    /// <param name="json">graph.json's text.</param>
    /// <param name="roots">The snapshot directory's names (as given and resolved): graphify writes absolute paths into it.</param>
    /// <returns>The clusters, in community order, or an error when the text isn't a graph.</returns>
    public static Result<IReadOnlyList<FileCluster>> Clusters(string json, IReadOnlyList<string> roots) =>
        throw Pending.Because(json, roots);
}
