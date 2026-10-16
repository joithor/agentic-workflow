using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The strip's selection (screens §4.1): kept by address across refreshes, walked with J / K, cleared and announced when a row finishes.</summary>
[Lane(TestLane.Unit)]
public sealed class StripSelectionTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private WorkflowsStripViewModel Strip()
    {
        var strip = new WorkflowsStripViewModel(new FakeClipboard(), _clock);
        strip.SetNeedsYouShown(0);
        strip.Update(FixtureInstances.Root);
        return strip;
    }

    private static string Selected(WorkflowsStripViewModel strip) => Assert.Single(strip.Rows, r => r.IsSelected).Address;

    [Fact]
    public void Move_walks_the_shown_rows_and_says_when_it_runs_off_either_end()
    {
        var strip = Strip();

        Assert.True(strip.Move(1));
        Assert.Equal(strip.Rows[0].Address, Selected(strip));
        Assert.True(strip.Move(1));
        Assert.Equal(strip.Rows[1].Address, Selected(strip));
        Assert.False(strip.Move(-1) && strip.Move(-1));
        Assert.Equal(strip.Rows[0].Address, Selected(strip));

        _ = strip.Select(strip.Rows[^1].Address);
        Assert.False(strip.Move(1));
        Assert.Equal(strip.Rows[^1].Address, Selected(strip));
    }

    [Fact]
    public void Select_ignores_an_address_that_is_not_shown_and_Deselect_clears()
    {
        var strip = Strip();

        Assert.False(strip.Select("not-an-address"));
        Assert.Null(strip.SelectedAddress);

        Assert.True(strip.Select(strip.Rows[2].Address));
        strip.Deselect();
        Assert.Null(strip.SelectedAddress);
        Assert.DoesNotContain(strip.Rows, r => r.IsSelected);
    }

    [Fact]
    public void Selection_is_kept_by_address_across_refreshes()
    {
        var strip = Strip();
        var address = strip.Rows[2].Address;
        _ = strip.Select(address);

        // The API re-serves the list with the first two rows swapped: the selection follows the address, not the place.
        var swapped = FixtureInstances.Root with { Instances = [FixtureInstances.Rows[1], FixtureInstances.Rows[0], .. FixtureInstances.Rows.Skip(2)] };
        strip.Update(swapped);

        Assert.Equal(address, Selected(strip));
    }

    [Fact]
    public void A_row_that_turns_terminal_leaves_and_announces_address_done()
    {
        var strip = Strip();
        var row = strip.Rows[1];
        var source = FixtureInstances.Rows.Single(r => string.Equals(r.Address, row.Address, StringComparison.Ordinal));
        _ = strip.Select(row.Address);

        var finished = source with { States = [.. source.States.Select(s => string.Equals(s.State, source.State, StringComparison.Ordinal) ? s with { Terminal = true } : s)] };
        strip.Update(FixtureInstances.Root with { Instances = [.. FixtureInstances.Rows.Select(r => ReferenceEquals(r, source) ? finished : r)] });

        Assert.Null(strip.SelectedAddress);
        Assert.DoesNotContain(strip.Rows, r => string.Equals(r.Address, row.Address, StringComparison.Ordinal));
        Assert.Equal($"{row.Address} done", strip.Announcement);
    }
}
