using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.Registry;

/// <summary>The recording wake keeps the wake contract.</summary>
[Lane(TestLane.Unit)]
public sealed class RecordingRecipeWakeContractTests : RecipeWakeContract
{
    protected override IRecipeWake Create() => new RecordingRecipeWake();
}
