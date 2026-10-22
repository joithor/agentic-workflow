using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Host.Shipped;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Shipped;

/// <summary>A build that embeds nothing yields the empty set, which is legal (until the publish embeds HEAD's recipes, SO-A7a).</summary>
public sealed class ManifestEmbeddedRecipesEmptyContractTests : EmbeddedRecipesContract
{
    protected override IEmbeddedRecipes Subject { get; } = new ManifestEmbeddedRecipes(typeof(object).Assembly);

    protected override IReadOnlyList<string> ExpectedPaths { get; } = [];
}
