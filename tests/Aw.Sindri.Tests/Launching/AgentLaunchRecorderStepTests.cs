using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launching;

/// <summary>The agent runtime records its own launch events: they carry the instance, Step state and attempt of the request (API follow-up to P10a, engine P8).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentLaunchRecorderStepTests
{
    private readonly SliceHarness _slice = new();

    private async Task<AgentLaunchRecorder> Recorder()
    {
        _ = await _slice.HoldLease();
        return new AgentLaunchRecorder(_slice.Store, _slice.Held, _slice.Events, new LaunchLiveness(), _slice.Clock);
    }

    private static LaunchRequest Request(LaunchId launch) =>
        A.LaunchRequest().WithLaunch(launch).Unattended(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent")))
            .OnInstance(InstanceAddress.From("ITM-00000000000000000000000000000001/spec/1"), RunToken.For(2, 3)).Build();

    [Fact]
    public async Task Started_and_failed_engine_launches_record_instance_state_and_attempt()
    {
        var recorder = await Recorder();
        var second = LaunchId.From("LCH-" + new string('0', 31) + "2");

        await recorder.Started(Request(A.FirstLaunchId), new StartedLaunch(new HostRef(HostKind.Tmux, "s"), ViewerOutcome.Skipped, ["tmux"], CatalogSource.Builtin, "sha256:abc"), TestContext.Current.CancellationToken);
        await recorder.Failed(Request(second), new FailedLaunch(SindriErrors.LaunchTimedOut.With("late"), LaunchStage.Host), TestContext.Current.CancellationToken);

        var types = _slice.Store.Events.ToList();
        var started = _slice.Events.Payload<LaunchStarted>(types.Single(e => e.Envelope.Type == EventTypes.LaunchStarted)).Value;
        var failed = _slice.Events.Payload<LaunchFailed>(types.Single(e => e.Envelope.Type == EventTypes.LaunchFailed)).Value;
        Expect.Equal(("ITM-00000000000000000000000000000001/spec/1", "build", 3), (started.Instance!.Value.Value, started.State, started.Attempt));
        Expect.Equal(("ITM-00000000000000000000000000000001/spec/1", "build", 3), (failed.Instance!.Value.Value, failed.State, failed.Attempt));
    }
}
