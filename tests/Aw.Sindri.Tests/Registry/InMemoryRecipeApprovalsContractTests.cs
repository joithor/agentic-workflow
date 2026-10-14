using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.Registry;

/// <summary>The in-memory approvals keep the approvals contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryRecipeApprovalsContractTests : RecipeApprovalsContract
{
    protected override IRecipeApprovals Create() => new InMemoryRecipeApprovals();
}
