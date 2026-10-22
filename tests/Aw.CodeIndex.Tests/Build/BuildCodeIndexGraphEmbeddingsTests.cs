using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Build;

// Regression: symbols that only the graph layer produces (TypeScript, kind graph-symbol) were never embedded, because they carried no body and the
// embed candidates are the symbols with a stored body. A build embeds them with the source text read at their file and line span.
public sealed class BuildCodeIndexGraphEmbeddingsTests
{
    private const string FileA = "export function placeOrder() {\n  return computeTotal();\n}\n";
    private const string FileB = "export function computeTotal() {\n  return 42;\n}\n";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly GraphSnapshot _graph = new(
        [new GraphNode("src_a_place", "src/a.ts", "placeOrder", 1, 0), new GraphNode("src_b_total", "src/b.ts", "computeTotal", 1, 0)],
        [new GraphEdge("src_a_place", "src_b_total", "calls", "EXTRACTED", 2)]);

    [Fact, Trait("Lane", "integration")]
    public async Task A_build_embeds_graph_symbols_with_their_source_text()
    {
        using var git = A.GitRepo().WithFile("src/a.ts", FileA).WithFile("src/b.ts", FileB).Build();
        var embedder = new FakeEmbedder();
        using var index = new RealIndex(embedder: embedder, graph: new FakeGraphRunner { Graph = _graph });
        var repo = git.AsRepo("web");

        var report = (await index.Service.Handle(new BuildCodeIndexRequest(repo, git.Head, new Progress<string>()), Token)).Value;

        var layer = report.Layers.Single(l => l.Layer == LayerName.Embeddings);
        Assert.Equal((2, 2, LayerState.Ok), (report.Embedded, report.Embeddable, layer.State));
        var texts = embedder.Batches.SelectMany(b => b).ToList();
        Assert.Contains(texts, t => t.Contains("return computeTotal();", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("return 42;", StringComparison.Ordinal));
    }
}
