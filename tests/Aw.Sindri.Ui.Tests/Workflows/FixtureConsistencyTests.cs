using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// Fixture-consistency checks, not UI behaviour: the API's goldens that the fake serves agree with each other (the root
/// list, the instance goldens, the children counts). They call no app code, so a failure here is a golden or the fixture
/// drifting, and the fix is there. They stay in this project until the API golden tests can hold them (U8 moves them).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class FixtureConsistencyTests
{
    // The rows the needs-you panel counts (API §3.8): PARKED, ESCALATED and DECISION. A WITH YOU row is neither.
    private static readonly string[] _needsYou = ["PARKED", "ESCALATED", "DECISION"];

    [Fact]
    public void Every_instance_golden_of_a_root_row_equals_the_one_tables_row()
    {
        var matched = 0;

        foreach (var (scenario, detail) in FixtureInstances.Details.Where(d => d.Detail.Parent is null))
        {
            var row = FixtureInstances.Rows.SingleOrDefault(r => r.Id == detail.Id);
            if (row is null)
            {
                continue;
            }

            Assert.Equal((detail.Address, detail.State, detail.Recipe, detail.Subject, detail.Since), (row.Address, row.State, row.Recipe, row.Subject, row.Since));
            Assert.True(Cells(detail.States).SequenceEqual(Cells(row.States)), $"{scenario}: the states differ from the table's row");
            matched++;
        }

        Assert.NotEqual(0, matched);
    }

    [Fact]
    public void The_tables_counts_derive_from_its_rows()
    {
        var rows = FixtureInstances.Rows;
        var counts = FixtureInstances.Counts;

        Assert.Equal(
            (rows.Count(FixtureInstances.IsOpen), rows.Count(r => _needsYou.Contains(r.Chip.Word, StringComparer.Ordinal)), rows.Count(r => r.Chip.Word == "WITH YOU"), rows.Count(r => r.Chip.Word == "RUNNING"), rows.Count(r => !FixtureInstances.IsOpen(r))),
            (counts.Open, counts.NeedsYou, counts.WithYou, counts.Running, counts.Done));
    }

    [Fact]
    public void The_done_today_extra_equals_the_served_done_figure_until_P11_serves_a_per_day_figure() =>
        Assert.Equal(FixtureInstances.DoneToday, FixtureInstances.Counts.Done);

    [Fact]
    public void Some_row_is_open_and_some_is_not()
    {
        Assert.Contains(FixtureInstances.Rows, FixtureInstances.IsOpen);
        Assert.Contains(FixtureInstances.Rows, r => !FixtureInstances.IsOpen(r));
    }

    [Fact]
    public void Children_counts_equal_the_child_rows()
    {
        var groups = FixtureInstances.Details.SelectMany(d => d.Detail.Children.Select(g => (d.Scenario, d.Detail, Group: g))).ToList();
        Assert.NotEmpty(groups);

        foreach (var (scenario, detail, group) in groups)
        {
            var rows = FixtureInstances.ChildRows(group).ToList();
            var tally = ChildTally.Of(group);

            Assert.Equal((group.Done, group.Total, group.Total - group.More), (tally.Done, tally.Total, rows.Count));
            Assert.Equal(rows.Count, tally.ByWord.Sum(w => w.Rows));
            Assert.Equal(rows.Count(r => r.Glyph == StateGlyph.Done), tally.Done);
            var cell = detail.States.Single(s => s.State == group.State);
            Assert.True(cell.Children is { } progress && (progress.Done, progress.Total) == (group.Done, group.Total), $"{scenario}: the {group.State} cell's progress is not the group's counts");
        }
    }

    // The critique's JD6 drift: one item runs one recipe in the strip, the head sentence and the children list. The
    // rendering of it waits for the U8 pages (T4); here only that the goldens agree with each other.
    [Fact]
    public void An_item_that_is_a_child_row_is_named_the_same_in_its_parents_row()
    {
        foreach (var (detail, _, row) in ChildrenWithAnItem())
        {
            if (FixtureInstances.Rows.SingleOrDefault(r => r.Id == detail.Id) is not { } summary)
            {
                continue;
            }

            Assert.Equal(detail.Recipe, summary.Recipe);
            if (summary.Now.Contains(row.ItemId!, StringComparison.Ordinal))
            {
                Assert.Contains($"{row.ItemId} {row.State}", summary.Now, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void A_child_row_with_an_item_is_one_row_across_the_goldens()
    {
        var items = ChildrenWithAnItem().Select(c => c.Row.ItemId).ToList();

        Assert.NotEmpty(items);
        Assert.Equal(items.Count, items.Distinct(StringComparer.Ordinal).Count());
    }

    private static IEnumerable<(InstanceDetail Detail, ChildGroup Group, ChildRow Row)> ChildrenWithAnItem() =>
        FixtureInstances.Details
            .Where(d => d.Detail.Parent is null)
            .SelectMany(d => d.Detail.Children.SelectMany(g => FixtureInstances.ChildRows(g).Where(r => r.ItemId is not null).Select(r => (d.Detail, g, r))));

    private static IEnumerable<(string State, StateGlyph Glyph, bool Terminal)> Cells(IEnumerable<StateCell> cells) => cells.Select(c => (c.State, c.Glyph, c.Terminal));
}
