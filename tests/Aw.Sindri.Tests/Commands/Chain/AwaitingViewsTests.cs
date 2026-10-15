using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>
/// A command that reads views waits, inside the gate, for the views to reach the ledger head; it is refused at once while
/// they rebuild (SND-API-010) or stall (SND-VIEW-001), and after 2 s behind (SND-API-011). Commands that read no views never wait (API spec §2.1).
/// </summary>
public sealed class AwaitingViewsTests : IAsyncDisposable
{
    private readonly ChainHarness _c = new(fixtureReadsViews: true);

    public ValueTask DisposeAsync() => _c.DisposeAsync();

    private static CallerContext Human => A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_that_reads_views_during_a_rebuild_is_SND_API_010_and_not_run()
    {
        _ = await _c.H.HoldLease();
        _c.Progress.RequestRebuild();

        var refused = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), Human);

        Assert.Equal(SindriErrors.ViewsRebuilding.Code, refused.Error.Code);
        Assert.Equal(0, _c.Latch.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_that_reads_views_while_they_stall_is_SND_VIEW_001_at_once_and_not_run()
    {
        _ = await _c.H.HoldLease();
        _c.Progress.Publish(new ViewsStatus(0, 5, ProjectedAt: null, Rebuilding: false, Stalled: true, StalledAt: 1, StalledType: "service.ran"));

        var refused = await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), Human);

        Assert.Equal(SindriErrors.ViewsStalled.Code, refused.Error.Code);
        Assert.Contains("service.ran", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, _c.Latch.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_that_reads_views_runs_once_the_views_reach_the_ledger_head()
    {
        await AnEarlierCommand();
        var head = (await _c.Ledger.Head(TestContext.Current.CancellationToken)).Value;

        var running = _c.Start(new FixtureCommand("accept"), Human);
        Assert.Equal(1, _c.Latch.Started);
        _c.Progress.Publish(new ViewsStatus(head, head, _c.H.Clock.GetUtcNow(), Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null));

        Assert.True((await running).IsSuccess);
        Assert.Equal(2, _c.Latch.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_that_reads_views_behind_the_head_for_2_s_is_SND_API_011_and_not_run()
    {
        await AnEarlierCommand();

        var running = _c.Start(new FixtureCommand("accept"), Human);
        while (!running.IsCompleted)
        {
            _c.H.Clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(SindriErrors.ViewsBehind.Code, (await running).Error.Code);
        Assert.Equal(1, _c.Latch.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stall_that_starts_during_the_wait_is_SND_VIEW_001()
    {
        await AnEarlierCommand();
        var running = _c.Start(new FixtureCommand("accept"), Human);
        await Task.Delay(50, TestContext.Current.CancellationToken);   // it is waiting for the cursor

        _c.Progress.Publish(_c.Progress.Current with { Stalled = true, StalledAt = 1, StalledType = "service.ran" });
        _c.H.Clock.Advance(TimeSpan.FromMilliseconds(500));

        Assert.Equal(SindriErrors.ViewsStalled.Code, (await running).Error.Code);
        Assert.Equal(1, _c.Latch.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_that_starts_during_the_wait_is_SND_API_010()
    {
        await AnEarlierCommand();
        var running = _c.Start(new FixtureCommand("accept"), Human);
        await Task.Delay(50, TestContext.Current.CancellationToken);   // it is waiting for the cursor

        _c.Progress.RequestRebuild();
        while (!running.IsCompleted)
        {
            _c.H.Clock.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(SindriErrors.ViewsRebuilding.Code, (await running).Error.Code);
        Assert.Equal(1, _c.Latch.Started);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_reads_no_views_and_runs_during_a_rebuild()
    {
        _ = await _c.H.HoldLease();
        _c.Progress.RequestRebuild();

        var stopped = await _c.Run<StopProcessRequest, StopProcessResponse>(new StopProcessRequest(StopReason.Stop), Human);

        Assert.True(stopped.IsSuccess);
    }

    /// <summary>Holds the lease and runs one command while the views are at the (empty) head, so the head moves past them.</summary>
    private async Task AnEarlierCommand()
    {
        _ = await _c.H.HoldLease();
        Assert.True((await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), Human)).IsSuccess);
        Assert.True((await _c.Ledger.Head(TestContext.Current.CancellationToken)).Value > _c.Progress.Current.Cursor);
    }
}
