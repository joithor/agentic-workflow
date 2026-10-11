using Aw.Evidence.Infrastructure.Browser;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Unit)]
public sealed class BrowserCallTests
{
    [Fact]
    public async Task A_cancellation_during_a_call_ends_the_wait_at_once()
    {
        using var cancel = new CancellationTokenSource();
        var never = new TaskCompletionSource<int>();   // a Playwright wait that would run to its own (long) timeout

        var call = BrowserCall.Run("wait for something", () => never.Task, cancel.Token).AsTask();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_failing_call_is_a_browser_failure_with_its_first_line()
    {
        var failed = await BrowserCall.Run<int>("click open", () => throw new InvalidOperationException("element not found\ncall log: …"), TestContext.Current.CancellationToken);

        Assert.Equal("click open: element not found", failed.Error.Message);
    }
}
