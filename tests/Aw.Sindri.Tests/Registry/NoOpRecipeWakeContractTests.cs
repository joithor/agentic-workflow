using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.Registry;

/// <summary>The no-op wake (until P4's signal sender) keeps the wake contract.</summary>
[Lane(TestLane.Unit)]
public sealed class NoOpRecipeWakeContractTests : RecipeWakeContract
{
    protected override IRecipeWake Create() => new NoOpRecipeWake();
}
