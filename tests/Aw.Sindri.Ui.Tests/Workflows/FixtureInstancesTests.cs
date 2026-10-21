using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// The app's heads over the one instance table (critique run 5 P1: open counts and strip head from one source): the text
/// the app builds from the API's counts, held to figures derived from the table, never to numbers written here. The
/// table's own consistency is <see cref="FixtureConsistencyTests"/>.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class FixtureInstancesTests
{
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void Nav_count_strip_head_and_workflows_head_print_the_served_open_figure_which_equals_the_open_rows()
    {
        var open = Number(FixtureInstances.Rows.Count(FixtureInstances.IsOpen));
        var counts = FixtureInstances.Counts;

        Assert.Equal(open, Number(counts.Open));
        Assert.Equal(open, InstanceHeads.Nav(counts));
        Assert.Equal($"{open} open", InstanceHeads.Strip(counts));
        Assert.Equal($"{open} open · {Number(counts.NeedsYou)} need you · {Number(counts.WithYou)} with you", InstanceHeads.Workflows(counts));
    }

    [Fact]
    public void The_finished_line_prints_the_served_done_figure() =>
        Assert.Equal($"{Number(FixtureInstances.Counts.Done)} finished", InstanceHeads.DoneLine(FixtureInstances.Counts.Done));
}
