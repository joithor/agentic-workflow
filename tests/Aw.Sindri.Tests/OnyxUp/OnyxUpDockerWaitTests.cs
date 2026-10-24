using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// `onyx up` is the human's verb at a terminal: a Docker that is starting, or not running yet, is waited for (a line naming the runtime, a dot per poll)
// up to OnyxDockerWait.Window before the preflight judges it. The clock is a fake that moves by each delay, so nothing here sleeps.
public sealed class OnyxUpDockerWaitTests
{
    private const string OrbStackSocket = "unix:///Users/u/.orbstack/run/docker.sock";
    private const string DesktopSocket = "unix:///Users/u/.docker/run/docker.sock";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContainerRuntimeFacts OrbStackStarting { get; } = FakeContainerRuntime.Starting with { Endpoint = OrbStackSocket };

    private static ContainerRuntimeFacts OrbStackUp { get; } = FakeContainerRuntime.Healthy with { Endpoint = OrbStackSocket, OperatingSystem = "OrbStack" };

    private static OnyxUpRig Rig(params ContainerRuntimeFacts[] answers)
    {
        var rig = new OnyxUpRig();
        rig.Clock.AdvanceOnDelay = true;
        foreach (var answer in answers)
        {
            rig.Runtime.Later.Enqueue(answer);
        }

        return rig;
    }

    private static int Inspections(OnyxUpRig rig) => rig.Runtime.Calls.Count(static c => c == "Inspect");

    [Fact, Trait("Lane", "unit")]
    public async Task Up_waits_for_an_OrbStack_that_is_slow_to_answer_and_then_sets_Onyx_up()
    {
        var rig = Rig(OrbStackStarting, OrbStackStarting, OrbStackUp);

        var result = await rig.Up(Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Contains("OrbStack isn't answering yet; waiting up to 3 minutes for it (open OrbStack, or run `orb restart` if it says Running; Ctrl-C stops)", rig.Progress.Lines, StringComparer.Ordinal);
        Assert.Contains("OrbStack answered", rig.Progress.Lines, StringComparer.Ordinal);
        Assert.Equal(3, Inspections(rig));
        Assert.Contains("Pull", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_waits_for_a_Docker_Desktop_that_is_not_running_yet()
    {
        var down = FakeContainerRuntime.DaemonDown with { Endpoint = DesktopSocket };
        var rig = Rig(down, down, FakeContainerRuntime.Healthy with { Endpoint = DesktopSocket });

        var result = await rig.Up(Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Contains("Docker Desktop isn't running yet; waiting up to 3 minutes for it to start (open Docker Desktop; Ctrl-C stops)", rig.Progress.Lines, StringComparer.Ordinal);
        Assert.Contains("Docker Desktop answered", rig.Progress.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_ticks_once_per_poll_while_it_waits()
    {
        var rig = Rig(OrbStackStarting, OrbStackStarting, OrbStackStarting, OrbStackUp);
        rig.Progress.Forward = new TickCounter(rig.Progress);

        _ = await rig.Up(Token);

        Assert.Equal(3, ((TickCounter)rig.Progress.Forward).TicksBeforeAnswer);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_fails_001_naming_OrbStack_and_orb_restart_once_the_window_runs_out()
    {
        var rig = Rig(OrbStackStarting);
        var begun = rig.Clock.GetUtcNow();

        var result = await rig.Up(Token);

        Assert.Equal("SND-ONYX-001", result.Error.Code);
        Assert.Contains("OrbStack isn't answering (no reply within 180 s): open OrbStack, or run `orb restart` if it says Running, then run this again", result.Error.Message, StringComparison.Ordinal);
        Assert.True(rig.Clock.GetUtcNow() - begun >= OnyxDockerWait.Window);
        Assert.Equal(1 + (int)(OnyxDockerWait.Window / OnyxDockerWait.Interval), Inspections(rig));
        Assert.DoesNotContain("Pull", rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.Empty(rig.Workspace.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_fails_001_naming_Docker_Desktop_when_it_never_starts()
    {
        var rig = Rig(FakeContainerRuntime.DaemonDown with { Endpoint = DesktopSocket });

        var result = await rig.Up(Token);

        Assert.Equal("SND-ONYX-001", result.Error.Code);
        Assert.Contains("Docker Desktop isn't running: open Docker Desktop and wait until it is ready, then run this again", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_does_not_wait_for_a_docker_that_answers()
    {
        var rig = Rig();

        var result = await rig.Up(Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Inspections(rig));
        Assert.DoesNotContain(rig.Progress.Lines, static l => l.Contains("waiting up to", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("no cli", "SND-ONYX-001")]
    [InlineData("permission denied", "SND-ONYX-001")]
    [InlineData("context missing", "SND-ONYX-001")]
    [InlineData("remote", "SND-ONYX-010")]
    public async Task Up_does_not_wait_when_waiting_cannot_help(string box, string code)
    {
        var rig = Rig(box switch
        {
            "no cli" => FakeContainerRuntime.NoCli,
            "permission denied" => FakeContainerRuntime.PermissionDenied,
            "context missing" => FakeContainerRuntime.ContextMissing,
            _ => FakeContainerRuntime.Starting with { Endpoint = "tcp://10.0.0.5:2375" },
        });
        var begun = rig.Clock.GetUtcNow();

        var result = await rig.Up(Token);

        Assert.Equal(code, result.Error.Code);
        Assert.Equal(1, Inspections(rig));
        Assert.Equal(begun, rig.Clock.GetUtcNow());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancelling_the_wait_stops_up_and_releases_the_lock()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.Facts = OrbStackStarting;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var up = rig.Up(cancel.Token);
        Assert.False(up.IsCompleted);   // waiting on the fake clock, which only moves when told
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => up);
        rig.Runtime.Facts = OrbStackUp;
        Assert.True((await rig.Up(Token)).IsSuccess);   // the lock was released on the way out
    }

    // Counts the dots that come before the "answered" line: one per inspection after the first.
    private sealed class TickCounter(CapturingOnyxProgress capture) : IOnyxProgress
    {
        public int TicksBeforeAnswer { get; private set; }

        public void Line(string text)
        {
        }

        public void Tick()
        {
            if (!capture.Lines.Any(static l => l.EndsWith(" answered", StringComparison.Ordinal)))
            {
                TicksBeforeAnswer++;
            }
        }
    }
}
