using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The scoping recipe's gather Step names three context inputs; every one needs a runtime in the shipped build or the recipe cannot be approved (SND-WF-016).</summary>
public sealed class ScopingStepInputRuntimesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_Step_input_of_workflow_scoping_has_a_runtime_in_the_shipped_build()
    {
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.scoping.yaml"), "workflow.scoping.yaml").Recipe;
        var provider = ShippedComposition.Compose();

        var refused = StepInputRefusals.Check(recipe, new StepInputRuntimes(provider.GetServices<StepInputRegistration>()));

        Assert.Null(refused);
    }
}
