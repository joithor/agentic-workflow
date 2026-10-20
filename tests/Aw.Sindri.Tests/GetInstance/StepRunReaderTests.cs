using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Tests.Workflow.Engine;
using static Aw.Sindri.Tests.GetInstance.StepRunFixtures;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The Step run read joins an instance's ledger events with the loop caps of its approved recipe.</summary>
[Lane(TestLane.Unit)]
public sealed class StepRunReaderTests
{
    private static readonly Recipe _loop = LoopOver("produce", "checks", "verify");

    private static readonly StepRunEvents _inTheLoop = new(
        Started("produce", "1.1"),
        Completed("produce", "checks", "1.1", "done", minutes: 5, next: "2.1"),
        Completed("checks", "verify", "2.1", "done", minutes: 10, next: "3.1"),
        Completed("verify", "done", "3.1", "done", minutes: 15, next: "4.1"));

    [Fact]
    public async Task A_looped_step_has_the_recipes_cap_as_max_attempts_including_a_member_the_instance_is_not_in()
    {
        var reader = new StepRunReader(_inTheLoop, new StepRunRecipes(_loop));

        var runs = (await reader.Read(StatusCases.View("done") with { States = [Cell("done", round: null)] }, CancellationToken.None)).Value;

        Assert.Equal([("produce", 3), ("checks", 3), ("verify", 3)], runs.Take(3).Select(static r => (r.Step, r.MaxAttempts ?? -1)));
    }

    [Fact]
    public async Task While_the_instance_is_in_the_loop_with_a_grant_the_cap_is_the_active_cells_allowance()
    {
        var reader = new StepRunReader(_inTheLoop, new StepRunRecipes(_loop));
        var view = StatusCases.View("done") with { States = [Cell("produce", round: null), Cell("checks", new Round(3, 5)), Cell("verify", round: null)] };

        var runs = (await reader.Read(view, CancellationToken.None)).Value;

        Assert.Equal([5, 5, 5], runs.Take(3).Select(static r => r.MaxAttempts ?? -1));
    }

    [Fact]
    public async Task A_step_in_no_loop_has_no_max_attempts()
    {
        var reader = new StepRunReader(new StepRunEvents(Started("done", "1.1")), new StepRunRecipes(_loop));

        var run = Assert.Single((await reader.Read(StatusCases.View("done"), CancellationToken.None)).Value);

        Assert.Null(run.MaxAttempts);
    }

    [Fact]
    public async Task A_recipe_that_cannot_be_read_leaves_max_attempts_null_on_every_run_and_the_read_succeeds()
    {
        var reader = new StepRunReader(_inTheLoop, new StepRunRecipes());

        var result = await reader.Read(StatusCases.View("done"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["produce", "checks", "verify", "done"], result.Value.Select(static r => r.Step), StringComparer.Ordinal);
        Assert.All(result.Value, static r => Assert.Null(r.MaxAttempts));
    }

    private static Recipe LoopOver(params string[] members)
    {
        var step = EngineFixture.Step;
        var states = step.States.ToDictionary(static s => s.Key, static s => s.Value, StringComparer.Ordinal);
        foreach (var member in members)
        {
            states[member] = states[member] with { Loop = "revisions" };
        }

        return step with { States = states, Loops = new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["revisions"] = new(3, LoopSpec.DefaultNoProgress) } };
    }

    private static StateCell Cell(string state, Round? round) =>
        new(state, Order: 1, BranchOf: null, StateGlyph.Pending, Badge: null, round, [], Desk: false, Child: false, Children: null, Outcome: null, Terminal: false);
}
