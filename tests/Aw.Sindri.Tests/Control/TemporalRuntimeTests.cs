using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Control;

public sealed class TemporalRuntimeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Stop_holds_the_dev_server_until_the_worker_wait_runs_out()
    {
        var server = new FakeTemporalServer();
        _ = await server.Ensure(Token);
        var clock = new FakeTimeProvider();
        var runtime = new TemporalRuntime(server, clock);
        runtime.Track(new TaskCompletionSource().Task);   // a worker that never stops

        var stop = runtime.Stop(Token).AsTask();
        await Task.Delay(50, Token);
        var runningBeforeTheWait = server.Running is not null;
        clock.Advance(TemporalRuntime.WorkerStopWait);
        var stopped = await stop.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

        Assert.True(runningBeforeTheWait);   // why: the server goes after the worker, not beside it
        Assert.True(stopped.IsSuccess);
        Assert.Null(server.Running);         // why: a worker that hangs must not keep the stop (and the lease) forever
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_wait_for_a_worker_that_outlived_the_first_returns_at_once()
    {
        var clock = new FakeTimeProvider();
        var runtime = new TemporalRuntime(new FakeTemporalServer(), clock);
        runtime.Track(new TaskCompletionSource().Task);   // a worker that never stops
        var first = runtime.WaitForWorker(Token).AsTask();
        await Task.Delay(50, Token);
        clock.Advance(TemporalRuntime.WorkerStopWait);
        await first.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

        var second = runtime.WaitForWorker(Token).AsTask();
        await Task.Delay(50, Token);

        Assert.True(second.IsCompleted);   // why: serve's wait and then StopProcess's must not add up to twice the bound
    }
}
