using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Shipped;

/// <summary>The fake <see cref="IEmbeddedRecipes"/> keeps the port's promises.</summary>
public sealed class FakeEmbeddedRecipesContractTests : EmbeddedRecipesContract
{
    protected override IEmbeddedRecipes Subject { get; } = FakeEmbeddedRecipes.Of(
        ("workflow.sample.yaml", "id: workflow.sample"), ("evals/workflow.sample/happy.jsonl", "{}"), ("workflow.other.yaml", "id: workflow.other"));

    protected override IReadOnlyList<string> ExpectedPaths { get; } = ["evals/workflow.sample/happy.jsonl", "workflow.other.yaml", "workflow.sample.yaml"];
}
