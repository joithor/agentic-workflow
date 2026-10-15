using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Tests.ApproveRecipe;

namespace Aw.Sindri.Tests.Registry;

/// <summary>A registry row whose stored text isn't JSON reads as a kernel error, the way a typed read of it would, and never throws.</summary>
public sealed class MalformedRegisteredRecipeTests
{
    private static RegisteredRecipe Broken(string id = "workflow.broken") =>
        new(RecipeId.From(id), 1, RecipeHash.Of(Encoding.UTF8.GetBytes("broken")), "{", Base: null, DateTimeOffset.UnixEpoch);

    [Fact, Trait("Lane", "unit")]
    public void Text_that_is_not_json_reads_as_the_kernel_error_instead_of_throwing()
    {
        var read = Broken().Read(SliceHarness.Json);
        var typed = AwJson.Deserialize<Recipe>(Encoding.UTF8.GetBytes("{"), SliceHarness.Json);

        Assert.True(read.IsError);
        Assert.Equal(typed.Error.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_malformed_row_does_not_stop_the_other_recipes_resolving_as_children()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Recipes.Add(Broken(), TestContext.Current.CancellationToken);

        var children = await r.Children();

        Assert.NotNull(children.Resolve(step.Id));
        Assert.Null(children.Resolve(RecipeId.From("workflow.broken")));
    }
}
