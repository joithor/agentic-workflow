using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Host.Shipped;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Shipped;

/// <summary>The Host adapter over an assembly that embeds a recipe set under 'aw-recipes/sindri/' (this test assembly does, with a README beside the recipes that it must skip).</summary>
public sealed class ManifestEmbeddedRecipesContractTests : EmbeddedRecipesContract
{
    protected override IEmbeddedRecipes Subject { get; } = new ManifestEmbeddedRecipes(typeof(ManifestEmbeddedRecipesContractTests).Assembly);

    protected override IReadOnlyList<string> ExpectedPaths { get; } =
        ["evals/workflow.other/happy.jsonl", "evals/workflow.sample/happy.jsonl", "workflow.other.yaml", "workflow.sample.yaml"];

    [Fact, Trait("Lane", "unit")]
    public void A_files_bytes_are_the_embedded_resources_bytes()
    {
        var file = Subject.Files().Single(f => string.Equals(f.RelativePath, "workflow.sample.yaml", StringComparison.Ordinal));

        Assert.StartsWith("schema: 1\nid: workflow.sample\n", System.Text.Encoding.UTF8.GetString(file.Bytes.Span).ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }
}
