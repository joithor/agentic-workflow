using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The stage tag of a strip row (screens §4.1): the current state with its round, desk mark, children and loop partner, read from the served state cell.</summary>
[Lane(TestLane.Unit)]
public sealed class StageTagTests
{
    private static InstanceSummary Row(Func<StateCell, bool> cell) =>
        FixtureInstances.Rows.First(r => cell(Current(r)));

    private static StateCell Current(InstanceSummary row) => row.States.First(s => string.Equals(s.State, row.State, StringComparison.Ordinal));

    [Fact]
    public void Round_reads_r_n_slash_max()
    {
        var row = Row(c => c.Round is not null);

        var round = Current(row).Round!; // why: the row was chosen by its round

        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"r{round.N}/{round.Max}"), StageTag.For(row), StringComparison.Ordinal);
    }

    [Fact]
    public void A_desk_stage_reads_waiting_for_you()
    {
        var row = Row(c => c.Desk && c.Badge is null);

        Assert.EndsWith(" · waiting for you", StageTag.For(row), StringComparison.Ordinal);
    }

    [Fact]
    public void Children_read_done_slash_total()
    {
        var row = Row(c => c.Children is not null);

        var children = Current(row).Children!; // why: the row was chosen by its children

        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"{children.Done}/{children.Total}"), StageTag.For(row), StringComparison.Ordinal);
    }

    [Fact]
    public void Child_reads_child()
    {
        var withChildren = Row(c => c.Child && c.Children is not null);
        var row = withChildren with { States = [.. withChildren.States.Select(c => c.Child ? c with { Children = null } : c)] }; // a child with no tally yet
        var plain = FixtureInstances.Rows.First(r => !Current(r).Child && !Current(r).Desk && Current(r).Round is null && Current(r).Children is null);

        Assert.EndsWith(" · child", StageTag.For(row), StringComparison.Ordinal);
        Assert.Equal(plain.State, StageTag.For(plain));
    }

    [Fact]
    public void Loop_pair_reads_state_arrows_with()
    {
        var row = Row(c => c.Loops.Count > 0);

        var pair = Current(row).Loops[0];

        Assert.EndsWith($" ⇄ {pair.With}", StageTag.For(row), StringComparison.Ordinal);
    }
}
