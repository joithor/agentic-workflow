using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.GetInstance;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;
using RecipeUrgency = Aw.Sindri.Core.Workflow.Recipes.RecipeUrgency;

namespace Aw.Sindri.Tests.Layout;

/// <summary>InstanceViews.Detail and InstanceSummaries.Of are pure functions of (view, context): a hand-built view in, the API's records out, no clock and no I/O.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceComposersTests
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-9/step/1");
    private static readonly DateTimeOffset _since = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _asOf = new(2026, 10, 12, 9, 30, 0, TimeSpan.Zero);

    private static InstanceView Built()
    {
        var recipe = new RecipeVersion(RecipeId.From("workflow.step"), 3, RecipeHash.From("sha256:" + new string('a', 64)));
        var cancel = new StatusCommand(StatusVerb.Cancel, ActionId.From("ApplyCommand:cancel"), ActionClass.Desk, "sindri workflow cancel ITM-9/step/1", ConfirmText.Cancel(_address, 0, 0));
        var status = new StatusFields(
            new StatusHeader(_address, recipe.Id, "aaaaaaaaaaaa", 5, "produce", Detail: null, _since, 1800),
            "wait", Answer: null, Child: null, Drift: null, [], Timeout: null, Budget: null, Paused: null, [cancel], "text");
        return new InstanceView(_address, recipe, "produce", Detail: null, _since, "wait", [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
        {
            Id = WorkflowInstanceId.Of(_address),
            ItemId = ItemKey.From("ITM-9"),
            Urgency = RecipeUrgency.Triage,
            Status = status,
            Step = new StepDeclaration("active", "step.produce", "step.produce"),
        };
    }

    [Fact]
    public void Detail_takes_title_parent_counts_and_as_of_from_its_context_and_the_rest_from_the_view()
    {
        var view = Built();
        var parent = new ParentRef(WorkflowInstanceId.Of(InstanceAddress.From("ITM-1/item/1")), InstanceAddress.From("ITM-1/item/1"), "Parent", "build", Workstream: null);
        var counts = new ViewCounts(7, 1, 2, Context: null);

        var detail = InstanceViews.Detail(view, new InstanceDetailContext("A title", parent, counts, _asOf));

        Assert.Equal(("A title", parent, counts, _asOf), (detail.Title, detail.Parent, detail.Counts, detail.AsOf));
        Assert.Equal((view.Address, view.Id, view.ItemId, view.State, view.Since), (detail.Address, detail.Id, detail.ItemId, detail.State, detail.Since));
        Assert.Equal(new InstanceRecipe(view.Recipe.Id, view.Recipe.Hash, 3, RecipeUrgency.Triage), detail.Recipe);
        Assert.Equal(new CurrentState("produce", view.Step, [], Desk: null, view.Status!), detail.Current);
        Assert.Equal([ActionId.From("ApplyCommand:cancel")], detail.Actions);
        Assert.Equal("item", detail.Subject.Kind);   // a recipe with no input is subject-less: the item stands in
    }

    [Fact]
    public void Detail_is_the_same_for_the_same_view_and_context()
    {
        var context = new InstanceDetailContext("t", Parent: null, new ViewCounts(0, 0, 0, Context: null), _asOf);

        Assert.Equal(JsonSerializer.Serialize(InstanceViews.Detail(Built(), context), _options), JsonSerializer.Serialize(InstanceViews.Detail(Built(), context), _options));
    }

    [Fact]
    public void A_summary_has_the_id_the_chip_the_answer_and_the_action_ids_of_the_view()
    {
        var view = Built();

        var summary = InstanceSummaries.Of(view);

        Assert.Equal((view.Id, new StateChip("RUNNING", ChipTone.Ok)), (summary.Id, summary.Chip));
        Assert.Equal([ActionId.From("ApplyCommand:cancel")], summary.Actions);
        Assert.Null(summary.Answer);
        Assert.Equal(new StateChip("QUEUED", ChipTone.Neutral), InstanceSummaries.Of(view, new InstanceSummaryContext(NotStarted: true)).Chip);
    }

    [Fact]
    public void A_view_with_no_status_record_is_refused_not_guessed()
    {
        _ = Assert.Throws<ArgumentException>(() => InstanceSummaries.Of(Built() with { Status = null }));
        _ = Assert.Throws<ArgumentException>(() => InstanceViews.Detail(Built() with { Status = null }, new InstanceDetailContext("t", Parent: null, new ViewCounts(0, 0, 0, Context: null), _asOf)));
    }

    [Fact]
    public void A_budget_with_no_cap_prints_used_and_no_limit_and_serves_a_null_limit()
    {
        var view = StatusCases.View("desk-task") with { Budget = new BudgetView(84_000, StateCap: null, 120_000, InstanceCap: null) };

        var text = StatusLayout.Render(view, _asOf);

        Assert.Contains("tokens 84k used (state), 120k used (instance)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("84k /", text, StringComparison.Ordinal);
        var line = new StatusBudget("state", 84_000, Limit: null, new BudgetFigures(120_000, Limit: null));
        Assert.Null(line.Limit);
    }
}
