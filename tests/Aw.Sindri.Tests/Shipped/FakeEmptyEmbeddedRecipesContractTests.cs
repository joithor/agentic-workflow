using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Shipped;

/// <summary>The empty set is legal for the fake.</summary>
public sealed class FakeEmptyEmbeddedRecipesContractTests : EmbeddedRecipesContract
{
    protected override IEmbeddedRecipes Subject { get; } = FakeEmbeddedRecipes.Empty;

    protected override IReadOnlyList<string> ExpectedPaths { get; } = [];
}
