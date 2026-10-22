using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Infrastructure.Embedding;
using Aw.CodeIndex.Testing;
using Aw.CodeIndex.Tests.Build;

namespace Aw.CodeIndex.Tests.Embedding;

// The real Ollama on loopback with the pinned model (spec §11, CI-8). Skips with a reason when this box has none: the
// service never installs or pulls, so a missing server or model is not a failure of the code under test.
[Trait("Lane", "heavy")]
public sealed class OllamaHeavyTests(ITestOutputHelper output) : EmbedderContract
{
    private static readonly EmbeddingsSettings _settings = new(Enabled: true, CodeIndexSettings.DefaultEmbeddingsUrl, Model: null);

    protected override EmbeddingsSettings Settings => _settings;

    protected override async Task<IEmbedder> Arrange() => await Available();

    [Fact]
    public async Task Neighbors_ranks_a_known_near_duplicate_first_over_the_real_model()
    {
        var embedder = await Available();
        using var git = A.GitRepo()
            .WithFile("src/Pricing.cs", "namespace Shop;\n\npublic class Pricing\n{\n    public decimal TotalWithTax(decimal net, decimal rate)\n    {\n        var tax = net * rate;\n        return net + tax;\n    }\n}\n")
            .WithFile("src/Invoice.cs", "namespace Shop;\n\npublic class Invoice\n{\n    public decimal GrossAmount(decimal netAmount, decimal taxRate)\n    {\n        var taxAmount = netAmount * taxRate;\n        return netAmount + taxAmount;\n    }\n}\n")
            .WithFile("src/Files.cs", "namespace Shop;\n\npublic class Files\n{\n    public string[] ListLogs(string directory)\n    {\n        return Directory.GetFiles(directory, \"*.log\");\n    }\n}\n")
            .Build();
        using var index = new RealIndex(embedder: embedder);
        var repo = git.AsRepo("shop");
        var scope = RealIndex.ScopeOf(repo);
        var lines = new Lines();

        var report = (await index.Service.Handle(new Features.BuildCodeIndex.BuildCodeIndexRequest(repo, git.Head, lines), TestContext.Current.CancellationToken)).Value;
        var neighbors = (await index.Facade.Neighbors(scope, repo.Name, "decimal TotalWithTax(decimal net, decimal rate) { var tax = net * rate; return net + tax; }", 3, TestContext.Current.CancellationToken)).Value;

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"embeddings: {report.Embedded} of {report.Embeddable} embedded; layers: {string.Join(", ", report.Layers.Select(l => l.Layer + "=" + l.State))}"));
        output.WriteLine("nearest: " + string.Join(", ", neighbors.Neighbors.Select(n => n.Symbol.Name + " " + n.Score.ToString("F3", CultureInfo.InvariantCulture))));
        Assert.Equal((3, 3), (report.Embedded, report.Embeddable));
        Assert.Equal(["TotalWithTax", "GrossAmount", "ListLogs"], neighbors.Neighbors.Select(n => n.Symbol.Name), StringComparer.Ordinal);
    }

    private static async Task<OllamaEmbedder> Available()
    {
        var embedder = new OllamaEmbedder();
        var probe = await embedder.Probe(_settings, TestContext.Current.CancellationToken);
        Assert.SkipWhen(probe.IsError, probe.IsError ? $"no Ollama with the pinned model on this box ({probe.Error.Code}); run scripts/install-deps.sh" : string.Empty);
        return embedder;
    }
}
