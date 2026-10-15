using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The list row's 'now' text: the view's 'next' cut to 80 characters, ellipsis inside the 80 (UI screens E-20).</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceSummariesTests
{
    private static string Now(string next) => InstanceSummaries.Of(StatusCases.View("desk-task") with { Next = next }).Now;

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair_so_the_text_stays_valid_unicode()
    {
        var next = new string('x', 76) + "\U0001F600" + new string('y', 10);   // the emoji's two UTF-16 units sit at 76 and 77, across the cut at 77

        var now = Now(next);

        Assert.Equal(new string('x', 76) + "...", now);
        Assert.True(now.Length <= InstanceSummaries.NowLength);
    }

    [Fact]
    public void A_pair_that_ends_before_the_cut_is_kept()
    {
        var next = new string('x', 75) + "\U0001F600" + new string('y', 10);   // units at 75 and 76, both before the cut at 77

        Assert.Equal(new string('x', 75) + "\U0001F600...", Now(next));
    }
}
