using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.Registry;

/// <summary>The registry fakes keep their contracts (engine spec §4.1, §4.2); the SQLite adapters join in P2 unit (a).</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryRecipeStoreContractTests : RecipeStoreContract
{
    protected override IRecipeStore Create() => new InMemoryRecipeStore();
}
