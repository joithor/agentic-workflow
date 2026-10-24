using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.SweepLostSessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>The silence sweep (API spec §8.1): a session with no heartbeat for 120 s ends as lost, once.</summary>
[Lane(TestLane.Unit)]
public sealed class LostSessionsTests
{
    private readonly SessionHarness _s = new();
    private readonly FakeHostedSessions _hosted = new();
    private readonly InstanceReads _instances = new();
    private static readonly InstanceAddress _step = InstanceAddress.From("ITM-1/step/1");

    /// <summary>The instance a Step launch belongs to, as the engine reads it: taken over or not, or unreadable.</summary>
    private sealed class InstanceReads : IInstanceReader
    {
        public string? TakenOverBy { get; set; }

        public bool Fail { get; set; }

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                return ValueTask.FromResult<Result<InstanceView>>(WorkflowErrors.InstanceNotFound.With(address.Value));
            }

            var recipe = new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
            return ValueTask.FromResult<Result<InstanceView>>(new InstanceView(address, recipe, "produce", Detail: null, KnownInstants.Epoch, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(address),
                TakenOverBy = TakenOverBy,
            });
        }
    }

    private async Task<int> SweepAfterTurnEnded(string? takenOverBy = null, bool unreadable = false)
    {
        await _s.Started(instance: _step);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);
        (_instances.TakenOverBy, _instances.Fail) = (takenOverBy, unreadable);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(121));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        return await Sweeper().Sweep(Token);
    }

    [Fact]
    public async Task A_Step_not_taken_over_ends_as_idle_when_its_instance_is_read()
    {
        Expect.Equal(1, await SweepAfterTurnEnded());
    }

    [Fact]
    public async Task A_Step_a_person_took_over_is_never_ended_as_idle()
    {
        Expect.Equal(0, await SweepAfterTurnEnded(takenOverBy: "human:desk"));
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
        Expect.Equal(0, await Sweeper().Sweep(Token));   // still held on the next sweep

        _instances.TakenOverBy = null;   // handed back: the turn has been over all along
        Expect.Equal(1, await Sweeper().Sweep(Token));
    }

    [Fact]
    public async Task A_Step_whose_instance_cannot_be_read_is_not_ended_as_idle()
    {
        Expect.Equal(0, await SweepAfterTurnEnded(unreadable: true));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LostSessions Sweeper() => new(_s.Liveness, _s.Events(), _s.Views.Slice.Clock, _s.Files, new LaunchViews(_s.Views.Views), _s.Views.Progress, _hosted, _instances);

    [Fact]
    public async Task Silence_120s_writes_lost()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);

        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(119));
        var early = await Sweeper().Sweep(Token);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(1));
        var swept = await Sweeper().Sweep(Token);

        Expect.Equal((0, 1), (early, swept));
        var recorded = Assert.Single(_s.Views.Slice.Store.Events);
        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(recorded).Value;
        Expect.Equal(($"session.ended:{A.FirstLaunchId.Value}", SessionEndReason.Lost, null), (recorded.IdempotencyKey, payload.Reason, payload.ExitCode));
        Expect.Equal(A.FirstLaunchId, Assert.Single(_s.Files.Forgotten));
    }

    [Fact]
    public async Task A_turn_that_ended_120s_ago_with_no_report_ends_the_session_as_idle_while_it_still_beats()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);

        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(119));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        var early = await Sweeper().Sweep(Token);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(1));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        var swept = await Sweeper().Sweep(Token);

        Expect.Equal((0, 1), (early, swept));
        var recorded = Assert.Single(_s.Views.Slice.Store.Events);
        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(recorded).Value;
        Expect.Equal(($"session.ended:{A.FirstLaunchId.Value}", SessionEndReason.Idle, null), (recorded.IdempotencyKey, payload.Reason, payload.ExitCode));
        Expect.Equal(A.FirstLaunchId, Assert.Single(_hosted.Forgotten));
    }

    [Fact]
    public async Task A_new_turn_starts_the_idle_clock_over()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.StartTurn(A.FirstLaunchId);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Files.EndTurn(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);

        Expect.Equal(0, await Sweeper().Sweep(Token));
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
    }

    [Fact]
    public async Task A_session_whose_turn_never_ended_is_never_idle()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        for (var minute = 0; minute < 5; minute++)
        {
            _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));
            _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
            Expect.Equal(0, await Sweeper().Sweep(Token));
        }

        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
    }

    [Fact]
    public async Task A_lost_session_is_ended_once()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(5));

        _ = await Sweeper().Sweep(Token);
        var again = await Sweeper().Sweep(Token);

        Expect.Equal(0, again);
        _ = Assert.Single(_s.Views.Slice.Store.Events);
    }

    [Fact]
    public async Task A_hosted_session_that_is_alive_is_never_lost()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _hosted.Add(A.FirstLaunchId);

        for (var minute = 0; minute < 5; minute++)
        {
            _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));
            Expect.Equal(0, await Sweeper().Sweep(Token));
        }

        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
    }

    [Fact]
    public async Task A_hosted_session_that_is_gone_ends_as_an_exit_and_is_forgotten()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _hosted.Add(A.FirstLaunchId);
        _hosted.MarkGone(A.FirstLaunchId);

        var lost = await Sweeper().Sweep(Token);

        Expect.Equal(0, lost);
        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal(SessionEndReason.Exit, payload.Reason);
        Expect.True(payload.ExitCode is null);
        Expect.Equal(A.FirstLaunchId, Assert.Single(_hosted.Forgotten));
    }

    [Fact]
    public async Task A_hosted_session_Sindri_no_longer_tracks_is_not_revived()
    {
        await _s.Started();
        _hosted.Add(A.FirstLaunchId);   // its launch ended before; the host's map still has it

        _ = await Sweeper().Sweep(Token);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(5));
        _ = await Sweeper().Sweep(Token);

        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
        Expect.Equal(0, _s.Liveness.Active().Count);
    }

    [Fact]
    public async Task A_lost_session_is_forgotten_by_its_host()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(3));

        _ = await Sweeper().Sweep(Token);

        Expect.Equal(A.FirstLaunchId, Assert.Single(_hosted.Forgotten));
    }

    [Fact]
    public async Task A_heartbeat_keeps_the_session()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));

        var swept = await Sweeper().Sweep(Token);

        Expect.Equal(0, swept);
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
    }

    [Fact]
    public async Task The_start_sweep_keeps_the_files_of_a_live_session_and_removes_a_dead_ones()
    {
        await _s.Started();   // A.FirstLaunchId has a live session
        var dead = LaunchId.From("LCH-" + new string('0', 31) + "9");
        _s.Files.Open(A.FirstLaunchId);
        _s.Files.Open(dead);

        var removed = await Sweeper().SweepFiles(TimeSpan.FromSeconds(60), Token);

        Expect.Equal(1, removed);
        Expect.Equal(dead, Assert.Single(_s.Files.Swept));
    }

    [Fact]
    public async Task The_start_sweep_waits_for_the_views_to_catch_up()
    {
        await _s.Started();
        _s.Files.Open(LaunchId.From("LCH-" + new string('0', 31) + "9"));
        _s.Views.Publish(stalledAt: 1);

        Assert.Null(await Sweeper().SweepFiles(TimeSpan.FromSeconds(60), Token));
        Assert.Empty(_s.Files.Swept);
    }
}
