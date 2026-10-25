using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Testing.Workflow.ApproveAll;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>The fake shipped set honours the port's contract.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeShippedRecipesContractTests : ShippedRecipesContract
{
    protected override IShippedRecipes Create(IReadOnlyList<ShippedSeed> seeds) => new FakeShippedRecipes(seeds);
}
