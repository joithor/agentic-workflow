using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The UI thread stand-in itself: work posted after a run ends is dropped, never thrown.</summary>
[Lane(TestLane.Unit)]
public sealed class UiThreadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_post_after_the_run_ends_is_dropped()
    {
        SynchronizationContext? captured = null;
        await UiThread.Run(() =>
        {
            captured = SynchronizationContext.Current;
            return Task.CompletedTask;
        }, Token);

        // why: a timer's finalizer can post to the captured context after the run; a throw there kills the test host
        var thrown = Record.Exception(() => captured!.Post(static _ => { }, state: null));

        Assert.Null(thrown);
    }
}
