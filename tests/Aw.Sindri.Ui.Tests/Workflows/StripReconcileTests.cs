using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A re-read of the strip keeps the rows it already has (UI spec §3.3: a refresh never moves focus).</summary>
[Lane(TestLane.Unit)]
public sealed class StripReconcileTests
{
    private static InstanceList Reread(InstanceList list) => list with { Instances = [.. list.Instances.Select(i => i with { })] };

    [Fact]
    public void A_re_read_with_the_same_rows_keeps_the_row_instances_and_the_list()
    {
        var strip = new WorkflowsStripViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon));
        strip.Update(FixtureInstances.Root);
        strip.SetNeedsYouShown(0);
        var list = strip.Rows;
        var before = strip.Rows.ToList();

        strip.Update(Reread(FixtureInstances.Root));

        Assert.Same(list, strip.Rows);
        Assert.Equal(before.Count, strip.Rows.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Same(before[i], strip.Rows[i]);
        }
    }
}
