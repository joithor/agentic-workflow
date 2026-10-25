using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>Every view that names a recipe carries its display name beside the id (setup-once spec §7.7); with none known it serves null and the app makes the id readable.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeDisplayNamesTests
{
    private static readonly InstanceView _view = StatusCases.View("done");

    [Fact]
    public void Until_the_engine_knows_names_none_is_served() =>
        Assert.Null(new NoRecipeDisplayNames().Of(RecipeId.From("workflow.step")));

    [Fact]
    public void The_detail_and_the_list_row_carry_the_name_the_caller_knows()
    {
        var detail = InstanceViews.Detail(_view, new InstanceDetailContext("t", Parent: null, new ViewCounts(0, 0, 0, Context: null), StatusCases.Now, RecipeDisplayName: "One step"));
        var row = InstanceSummaries.Of(_view, new InstanceSummaryContext(StatusCases.Now, RecipeDisplayName: "One step"));

        Assert.Equal(("One step", "One step"), (detail.Recipe.DisplayName, row.Recipe.DisplayName));
    }

    [Fact]
    public void With_no_name_known_the_detail_and_the_row_serve_null_not_the_id()
    {
        var detail = InstanceViews.Detail(_view, new InstanceDetailContext("t", Parent: null, new ViewCounts(0, 0, 0, Context: null), StatusCases.Now));
        var row = InstanceSummaries.Of(_view, new InstanceSummaryContext(StatusCases.Now));

        Assert.Null(detail.Recipe.DisplayName);
        Assert.Null(row.Recipe.DisplayName);
        Assert.DoesNotContain("displayName", System.Text.Json.JsonSerializer.Serialize(detail.Recipe), StringComparison.Ordinal);
    }

    [Fact]
    public void The_recipe_view_carries_the_name() =>
        Assert.Equal("No outputs", Testing.Views.WorkflowViewFixtures.NoOutputsRecipe().DisplayName, StringComparer.Ordinal);
}
