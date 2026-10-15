using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Views;
using Aw.Sindri.Tests.Layout;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>The same invariants over what InstanceViews.Of and Detail produce for the shipped dev recipes, not over the hand-written fixtures.</summary>
public sealed partial class WorkflowEngineInvariantsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_invariant_holds_for_the_detail_the_engine_produces_for_the_dev_recipes()
    {
        var produced = DevInstances.All.Select(p =>
            new View(p.Detail.Address.Value, p.Detail.Recipe, p.Detail.State, p.Detail.Since, p.Detail.EngineOwned, p.Detail.Subject, p.Detail.States,
                InstanceSummaries.Of(p.View, new InstanceSummaryContext(DevInstances.AsOf)).Chip, p.Detail, [p.Detail.Current.Status])).ToList();

        foreach (var check in new Func<View, RecipeShape, IEnumerable<string>>[] { Children, Timeouts, Declared, Answers, Tokens, DeskTasks, OneCurrent, Subjects })
        {
            Walk(check, produced);
        }
    }
}
