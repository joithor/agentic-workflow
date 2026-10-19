using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>A Step input with no runtime is refused instead of dropped (SND-WF-016; engine spec §5.1, E1).</summary>
public sealed class StepInputRegistrationTests
{
    private static readonly BlockId _echo = BlockId.From(FakeStepInput.Id);

    [Fact, Trait("Lane", "unit")]
    public async Task AddStepInput_registers_keyed_by_id_and_marks_the_runtime()
    {
        await using var provider = new ServiceCollection().AddStepInput<FakeStepInput>().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        _ = Assert.IsType<FakeStepInput>(scope.ServiceProvider.GetRequiredKeyedService<IStepInput>(FakeStepInput.Id));
        _ = Assert.IsType<FakeStepInput>(Assert.Single(scope.ServiceProvider.GetServices<IStepInput>()));
        Assert.True(new StepInputRuntimes(provider.GetServices<StepInputRegistration>()).Has(_echo));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_descriptor_registered_alone_has_no_runtime()
    {
        await using var provider = new ServiceCollection().AddBlockDescriptor(FakeStepInput.Descriptor).BuildServiceProvider();

        Assert.False(new StepInputRuntimes(provider.GetServices<StepInputRegistration>()).Has(_echo));
    }

    [Fact, Trait("Lane", "unit")]
    public void StepInputRefusals_names_the_alphabetically_first_state_whatever_order_the_recipe_declares_them()
    {
        var one = ARecipe.WithStepInputs(ARecipe.Minimal(RecipeId.From("workflow.inputs")), FakeStepInput.Id);
        var (name, state) = one.States.First(static s => s.Value.Step is not null);
        var reversed = one with
        {
            States = new Dictionary<string, Core.Workflow.Recipes.RecipeState>(StringComparer.Ordinal) { ["zeta"] = state, ["alpha"] = state, [name] = state },
        };

        var refused = StepInputRefusals.Check(reversed, new StepInputRuntimes([]));

        Assert.NotNull(refused);
        Assert.Contains("states.alpha.step.inputs[0]", refused.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void StepInputRefusals_names_the_path_and_id_of_the_first_input_with_no_runtime()
    {
        var recipe = ARecipe.WithStepInputs(ARecipe.Minimal(RecipeId.From("workflow.inputs")), FakeStepInput.Id, "input.test.other");
        var runtimes = new StepInputRuntimes([new StepInputRegistration(_echo)]);

        var refused = StepInputRefusals.Check(recipe, runtimes);

        Assert.NotNull(refused);
        Assert.Equal(WorkflowErrors.StepInputNoRuntime.Code, refused.Code);
        Assert.Contains("states.produce.step.inputs[1]", refused.Message, StringComparison.Ordinal);
        Assert.Contains("input.test.other", refused.Message, StringComparison.Ordinal);
        Assert.Contains("fix:", refused.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void StepInputRefusals_passes_a_recipe_whose_inputs_all_run_and_one_with_none()
    {
        var runtimes = new StepInputRuntimes([new StepInputRegistration(_echo)]);

        Assert.Null(StepInputRefusals.Check(ARecipe.WithStepInputs(ARecipe.Minimal(RecipeId.From("workflow.inputs")), FakeStepInput.Id), runtimes));
        Assert.Null(StepInputRefusals.Check(ARecipe.Minimal(RecipeId.From("workflow.inputs")), runtimes));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_workflow_error_catalogue_lists_SND_WF_016_as_unavailable() =>
        Assert.Equal(Kernel.ErrorKind.Unavailable, Assert.Single(WorkflowErrors.All, e => e.Code == "SND-WF-016").Kind);
}
