using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
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

    // review run 1, test-6: a row whose served content changed is replaced and draws the new values.
    [Fact]
    public void A_row_whose_now_sentence_changes_is_replaced_and_shows_the_new_one()
    {
        var strip = new WorkflowsStripViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon));
        strip.Update(FixtureInstances.Root);
        strip.SetNeedsYouShown(0);
        var kept = strip.Rows[1];
        var changed = FixtureInstances.Root with { Instances = [.. FixtureInstances.Root.Instances.Select(i => string.Equals(i.Address, kept.Address, StringComparison.Ordinal) ? i with { Now = "waiting on the desk since the last round" } : i)] };

        strip.Update(changed);

        var now = strip.Rows.First(r => string.Equals(r.Address, kept.Address, StringComparison.Ordinal));
        Assert.NotSame(kept, now);
        Assert.Equal("waiting on the desk since the last round", now.Now);
    }

    // A newer `since` alone (the instance entered its state again, nothing else differs) shows the new age: the row is replaced.
    [Fact]
    public void A_newer_since_alone_replaces_the_row_and_shows_the_new_age()
    {
        var clock = new FakeTimeProvider(A.Noon);
        var strip = new WorkflowsStripViewModel(new FakeClipboard(), clock);
        strip.Update(FixtureInstances.Root);
        strip.SetNeedsYouShown(0);
        var kept = strip.Rows[1];
        var newer = FixtureInstances.Root with { Instances = [.. FixtureInstances.Root.Instances.Select(i => string.Equals(i.Address, kept.Address, StringComparison.Ordinal) ? i with { Since = clock.GetUtcNow().AddMinutes(-1) } : i)] };

        strip.Update(newer);

        var now = strip.Rows.First(r => string.Equals(r.Address, kept.Address, StringComparison.Ordinal));
        Assert.NotSame(kept, now);
        Assert.Equal(Labels.RowAge(TimeSpan.FromMinutes(1)), now.Age);
    }
}
