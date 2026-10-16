using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>The head counts of the instance list (API spec section 3.8, E-20) from each row's chip.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceCountsTests
{
    [Theory]
    [InlineData("desk-task", 1, 1, 0, 0, 0)]
    [InlineData("parked-rounds", 1, 1, 0, 0, 0)]
    [InlineData("escalated-gate", 1, 1, 0, 0, 0)]
    [InlineData("taken-over", 1, 0, 1, 0, 0)]
    [InlineData("done", 0, 0, 0, 0, 1)]
    public void One_row_counts_by_its_chip(string name, int open, int needsYou, int withYou, int running, int done) =>
        Assert.Equal(new ListCounts(open, needsYou, withYou, running, done), InstanceCounts.Of([StatusCases.View(name)]));

    [Fact]
    public void Rows_add_up_and_a_finished_one_is_in_no_open_count()
    {
        var counts = InstanceCounts.Of([StatusCases.View("desk-task"), StatusCases.View("parked-rounds"), StatusCases.View("taken-over"), StatusCases.View("done")]);

        Assert.Equal(new ListCounts(Open: 3, NeedsYou: 2, WithYou: 1, Running: 0, Done: 1), counts);
    }

    [Fact]
    public void The_chip_words_are_the_ones_the_rows_and_the_counts_share()
    {
        var chip = StateChip.For(engineOwned: null, deskOpen: true, terminal: false);

        Assert.Equal(StateChip.Decision, chip.Word);
        Assert.Equal(StateChip.Running, StateChip.For(engineOwned: null, deskOpen: false, terminal: false).Word);
        Assert.Equal(StateChip.Queued, StateChip.For(engineOwned: null, deskOpen: false, terminal: false, notStarted: true).Word);
        Assert.Equal(StateChip.Done, StateChip.For(engineOwned: null, deskOpen: false, terminal: true).Word);
    }
}
