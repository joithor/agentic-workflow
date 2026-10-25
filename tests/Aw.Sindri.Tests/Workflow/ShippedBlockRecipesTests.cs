using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The shipped block recipes (decision-tree recipes DT-R2): how they nest, and the two rules every parent and every route follows.</summary>
public sealed class ShippedBlockRecipesTests
{
    private static readonly string[] _sindri = [.. ShippedRecipes.Recipes.Where(r => r.StartsWith("sindri/", StringComparison.Ordinal))];

    public static TheoryData<string> Recipes => [.. _sindri];

    private static Recipe Read(string path) => RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path)).Recipe;

    private static int Depth(string id)
    {
        var recipe = Read($"sindri/{id}.yaml");
        return recipe.States.Values.Select(s => s.WaitFor?.Child?.Recipe.Value).OfType<string>().Select(c => 1 + Depth(c)).DefaultIfEmpty(0).Max();
    }

    [Fact, Trait("Lane", "unit")]
    public void Block_recipes_nest_within_RCP_CHILD_003()
    {
        Assert.Equal(2, Depth("workflow.evidence"));
        Assert.Equal(1, Depth("workflow.evidence-all"));
        Assert.Equal(0, Depth("workflow.evidence-ui"));
        Assert.Equal(0, Depth("workflow.evidence-desktop"));
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Every_child_wait_routes_cancelled_on_its_own_transition_and_records_nothing(string path)
    {
        var recipe = Read(path);

        foreach (var (name, state) in recipe.States.Where(s => s.Value.WaitFor?.Child is not null))
        {
            var cancelled = recipe.Transitions.Where(t => t.From.Contains(name, StringComparer.Ordinal) && t.When?.Outcome?.Value == "cancelled").ToList();

            var line = Assert.Single(cancelled);
            Assert.Equal([name], line.From);
            Assert.Empty(line.Produces);
        }
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Every_route_state_has_its_desk_twin(string path)
    {
        var recipe = Read(path);

        foreach (var (name, state) in recipe.States.Where(s => s.Value.Step?.Use.Value == "step.judge.choose"))
        {
            var twin = Assert.Single(recipe.States, s => s.Value.Step?.Use.Value == "step.desk.choose" && s.Value.Step.With.TryGetValue("from", out var from) && from.Literal?.GetString() == name);
            var failed = Assert.Single(recipe.Transitions, t => t.From.Contains(name, StringComparer.Ordinal) && t.On == "step.failed");
            Assert.Equal(twin.Key, failed.To);
        }
    }
}
