using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Resolution;
using Aw.Kernel;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

public sealed partial class BuildCodeIndexService
{
    private const int EdgeBatch = 5_000;

    private static async ValueTask<Result<EdgeShares>> WriteEdges(IIndexBuild build, BuildCodeIndexRequest request, IReadOnlySet<string> projectDirectories, CancellationToken cancellationToken)
    {
        request.Progress.Report("resolving calls");
        var symbols = await build.ReadSymbols(cancellationToken).ConfigureAwait(false);
        if (symbols.IsError)
        {
            return symbols.Error;
        }

        var rows = new List<CallSiteRow>();
        await foreach (var row in build.ReadCallSites(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(row);
        }

        var edges = new CallResolver(symbols.Value, projectDirectories).Resolve(rows);
        var cleared = await build.ClearEdges(EdgeBasis.Syntax, cancellationToken).ConfigureAwait(false);
        if (cleared.IsError)
        {
            return cleared.Error;
        }

        foreach (var batch in edges.Chunk(EdgeBatch))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var added = await build.AddEdges(batch, cancellationToken).ConfigureAwait(false);
            if (added.IsError)
            {
                return added.Error;
            }
        }

        return EdgeShares.Of(edges);
    }

    /// <summary>Of the call sites, the share that resolved to nothing and the share that stayed ambiguous.</summary>
    private sealed record EdgeShares(double UnresolvedShare, double AmbiguousShare)
    {
        public static EdgeShares Of(IReadOnlyList<EdgeRecord> edges)
        {
            var sites = edges.GroupBy(e => (e.Source, e.TargetName, e.Kind, e.Line)).ToList();
            return sites.Count == 0
                ? new EdgeShares(0, 0)
                : new EdgeShares(
                    sites.Count(g => g.All(e => e.Confidence == Confidence.Unresolved)) / (double)sites.Count,
                    sites.Count(g => g.Any(e => e.Confidence == Confidence.Ambiguous)) / (double)sites.Count);
        }
    }
}
