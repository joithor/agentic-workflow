using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>Every view that names a recipe carries its display name beside the id (setup-once spec §7.7); with none known the id is made readable, never served raw.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeDisplayNamesTests
{
    private static readonly InstanceView _view = StatusCases.View("done");

    [Theory]
    [InlineData("workflow.step", "Step")]
    [InlineData("workflow.dev-desk-round-trip", "Dev desk round trip")]
    [InlineData("workflow.ship", "Ship")]
    public void An_unnamed_recipe_is_its_id_made_readable(string id, string expected) =>
        Assert.Equal(expected, new RecipeIdDisplayNames().Of(RecipeId.From(id)), StringComparer.Ordinal);

    [Fact]
    public void The_detail_and_the_list_row_carry_the_name_the_caller_knows()
    {
        var counts = new ViewCounts(0, 0, 0, Context: null);

        var detail = InstanceViews.Detail(_view, new InstanceDetailContext("t", Parent: null, counts, StatusCases.Now, RecipeDisplayName: "One step"));
        var row = InstanceSummaries.Of(_view, new InstanceSummaryContext(StatusCases.Now, RecipeDisplayName: "One step"));

        Assert.Equal(("One step", "One step"), (detail.Recipe.DisplayName, row.Recipe.DisplayName));
    }

    [Fact]
    public void The_detail_and_the_list_row_fall_back_to_the_readable_id()
    {
        var detail = InstanceViews.Detail(_view, new InstanceDetailContext("t", Parent: null, new ViewCounts(0, 0, 0, Context: null), StatusCases.Now));
        var row = InstanceSummaries.Of(_view, new InstanceSummaryContext(StatusCases.Now));

        Assert.Equal(RecipeIdDisplayNames.Readable(_view.Recipe.Id), detail.Recipe.DisplayName, StringComparer.Ordinal);
        Assert.Equal(detail.Recipe.DisplayName, row.Recipe.DisplayName, StringComparer.Ordinal);
    }

    [Fact]
    public void The_recipe_view_carries_the_name()
    {
        var recipe = Testing.Views.WorkflowViewFixtures.NoOutputsRecipe();

        Assert.Equal("No outputs", recipe.DisplayName, StringComparer.Ordinal);
    }
}
