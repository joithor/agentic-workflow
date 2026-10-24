using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A run the state timeout parks (engine P8 U3): the park keeps the session, and a cancel stops it. The state timeout is at least a minute and time skipping
/// doesn't advance while the watch runs, so these take about a minute of real time each: the heavy lane, out of the integration lane's 120 s budget.
/// </summary>
[Lane(TestLane.Heavy)]
[Collection(nameof(WallClockAlone))]
public sealed class AgentParkedRunTests(SindriTimeSkippingServer skipping) : AgentRuntimeTestBase, IClassFixture<SindriTimeSkippingServer>
{
    [Fact(Timeout = 120_000)]
    public async Task The_state_timeout_parks_and_the_session_stays()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct, skipping.Environment);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);

            await WorkflowsHarness.Skip(skipping.Environment, TimeSpan.FromMinutes(3), ct);
            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Equal(ParkReason.Timeout, parked.Question?.Reason);
            Assert.Equal((0, 1), (_launcher.Stops.Count, _launcher.SessionCount));   // a park keeps the session
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_cancel_of_a_parked_run_stops_the_session_the_park_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct, skipping.Environment);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            await WorkflowsHarness.Skip(skipping.Environment, TimeSpan.FromMinutes(3), ct);
            _ = await WorkflowsHarness.Until(handle, static v => v.Question is not null);
            Assert.Empty(_launcher.Stops);

            _ = await Cancel(handle, 'd');

            await WaitFor(() => _launcher.Stops.Contains(rig.Launch), ct);
            Assert.Equal(0, _launcher.SessionCount);
        });
    }
}
