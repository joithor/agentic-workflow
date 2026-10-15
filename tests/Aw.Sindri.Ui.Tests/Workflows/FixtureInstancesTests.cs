using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// The one instance table (critique run 5 P1: open counts, strip head and children counts from one source). The table is
/// the API's golden of the root set; every figure the app prints is held to its rows.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class FixtureInstancesTests
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

        Assert.True(matched >= 4, string.Create(CultureInfo.InvariantCulture, $"only {matched} instance goldens are rows of the table"));
    }

    [Fact]
    public void The_tables_counts_derive_from_its_rows()
    {
        var rows = FixtureInstances.Rows;

        Assert.Equal(
            (rows.Count(r => !IsTerminal(r)), rows.Count(r => _needsYou.Contains(r.Chip.Word, StringComparer.Ordinal)), rows.Count(r => r.Chip.Word == "WITH YOU"), rows.Count(r => r.Chip.Word == "RUNNING"), rows.Count(IsTerminal)),
            (FixtureInstances.Counts.Open, FixtureInstances.Counts.NeedsYou, FixtureInstances.Counts.WithYou, FixtureInstances.Counts.Running, FixtureInstances.Counts.Done));
    }

    [Fact]
    public void Nav_count_strip_head_and_workflows_head_equal_the_open_rows()
    {
        var open = FixtureInstances.Rows.Count(FixtureInstances.IsOpen);
        var counts = FixtureInstances.Counts;

        Assert.Equal(8, open);
        Assert.Equal(open, counts.Open);
        Assert.Equal("8", InstanceHeads.Nav(counts));
        Assert.Equal("8 open", InstanceHeads.Strip(counts));
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{open} open · {counts.NeedsYou} need you · {counts.WithYou} with you"), InstanceHeads.Workflows(counts));
    }

    [Fact]
    public void The_finished_line_is_the_done_today_extra_until_P11_serves_a_per_day_figure()
    {
        Assert.Equal(FixtureInstances.DoneToday, FixtureInstances.Counts.Done);
        Assert.Equal("4 done today", InstanceHeads.DoneLine(FixtureInstances.DoneToday));
    }

    [Fact]
    public void Open_is_not_terminal_and_a_terminal_state_is_never_open()
    {
        foreach (var row in FixtureInstances.Rows)
        {
            Assert.Equal(!IsTerminal(row), FixtureInstances.IsOpen(row));
        }

        Assert.Contains(FixtureInstances.Rows, r => !FixtureInstances.IsOpen(r));
    }

    [Fact]
    public void Children_counts_equal_the_child_rows()
    {
        var groups = FixtureInstances.Details.SelectMany(d => d.Detail.Children.Select(g => (d.Scenario, d.Detail, Group: g))).ToList();
        Assert.True(groups.Count >= 2, "the project goldens have child groups");

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

    // The critique's JD6 drift: one item, one recipe, in the strip, the head sentence and the children list.
    [Fact]
    public void ITM_01JD6_has_one_recipe_everywhere()
    {
        const string Item = "ITM-01JD6";
        var rows = FixtureInstances.Details
            .SelectMany(d => d.Detail.Children.SelectMany(g => FixtureInstances.ChildRows(g).Where(r => r.ItemId == Item).Select(r => (d.Detail, Group: g, Row: r))))
            .ToList();

        var (parent, group, row) = Assert.Single(rows);
        Assert.Equal("workflow.step", group.Recipe);
        Assert.Equal((StateGlyph.Active, "checks"), (row.Glyph, row.State));
        Assert.Equal(group.State, parent.State);
        // The root row's one-line "now" names the same item in the same state, and the same recipe runs the parent everywhere.
        var summary = Assert.Single(FixtureInstances.Rows, r => r.Id == parent.Id);
        Assert.Contains($"{Item} {row.State}", summary.Now, StringComparison.Ordinal);
        Assert.Equal(parent.Recipe, summary.Recipe);
        // A step of the shipped recipes runs at the desk until engine P8: a decision, taken over from the desk, never an agent.
        Assert.Equal("DECISION", row.Chip.Word);
        Assert.Null(row.Agent);
    }

    private static IEnumerable<(string State, StateGlyph Glyph, bool Terminal)> Cells(IEnumerable<StateCell> cells) => cells.Select(c => (c.State, c.Glyph, c.Terminal));

    private static bool IsTerminal(InstanceSummary row) => !FixtureInstances.IsOpen(row);
}
