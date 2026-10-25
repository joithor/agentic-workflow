using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.SampleSessionUsage;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>The usage sample (SCR E-28): a live session that knows its agent session id gets a private 'session.usage' when its tokens changed.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionUsageSamplerTests
{
    private static readonly SessionId _agentSession = SessionId.From("11111111-2222-3333-4444-555555555555");
    private readonly SessionHarness _s = new();
    private readonly UsageSamples _samples = new();
    private readonly Dictionary<SessionId, IReadOnlyList<ModelTokens>> _used = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SessionUsageSampler Sampler() => new(_s.Liveness, new LaunchViews(_s.Views.Views), new Used(_used), _samples, _s.Events(), _s.Views.Slice.Clock);

    private async Task Running()
    {
        await _s.Started();
        await _s.Views.Reported(_agentSession);
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
    }

    [Fact]
    public async Task A_live_session_gets_a_private_sample_keyed_by_launch_and_number()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", Input: 10, CacheRead: 5, CacheWrite: 1, Output: 7)];
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));

        var appended = await Sampler().Sample(Token);

        Expect.Equal(1, appended);
        var recorded = Assert.Single(_s.Views.Slice.Store.Events);
        var payload = _s.Views.Slice.Events.Payload<SessionUsage>(recorded).Value;
        Expect.Equal(($"session.usage:{A.FirstLaunchId.Value}:2", 2, 16L, 7L), (recorded.IdempotencyKey, payload.Sample, payload.InputTokens, payload.OutputTokens));
    }

    [Fact]
    public async Task A_sample_names_its_cache_reads_beside_the_input_total()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", Input: 10, CacheRead: 5, CacheWrite: 1, Output: 7), new ModelTokens("n", Input: 1, CacheRead: 20, CacheWrite: 0, Output: 1)];

        _ = await Sampler().Sample(Token);

        var payload = _s.Views.Slice.Events.Payload<SessionUsage>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal((37L, 25L), (payload.InputTokens, payload.CacheReadTokens));
    }

    [Fact]
    public async Task A_sample_with_no_cache_reads_leaves_the_field_out()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];

        _ = await Sampler().Sample(Token);

        var payload = _s.Views.Slice.Events.Payload<SessionUsage>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Assert.Null(payload.CacheReadTokens);
    }

    [Fact]
    public async Task Unchanged_totals_write_nothing_and_changed_ones_write_the_next_sample()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];
        _ = await Sampler().Sample(Token);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));
        var same = await Sampler().Sample(Token);
        _used[_agentSession] = [new ModelTokens("m", 20, 0, 0, 9)];
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));
        var changed = await Sampler().Sample(Token);

        Expect.Equal((0, 1, 2), (same, changed, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task A_session_without_an_agent_id_or_reader_is_not_sampled()
    {
        await _s.Started();   // no 'launch.session-reported', and the started event carries no id
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];

        var withoutId = await Sampler().Sample(Token);
        await _s.Views.Reported(_agentSession);
        _used.Clear();
        var withoutReader = await Sampler().Sample(Token);

        Expect.Equal((0, 0, 0), (withoutId, withoutReader, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task An_ended_session_is_not_sampled()
    {
        await Running();
        await _s.Views.End();
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];

        Expect.Equal(0, await Sampler().Sample(Token));
    }

    [Fact]
    public async Task A_flush_samples_the_launch_before_any_tick()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", Input: 10, CacheRead: 5, CacheWrite: 1, Output: 7)];

        var appended = await Sampler().Flush(A.FirstLaunchId, Token);

        var payload = _s.Views.Slice.Events.Payload<SessionUsage>(Assert.Single(_s.Views.Slice.Store.Events)).Value;
        Expect.Equal((1, 1, 16L, 7L), (appended, payload.Sample, payload.InputTokens, payload.OutputTokens));
    }

    [Fact]
    public async Task A_flush_of_a_launch_with_no_transcript_yet_writes_nothing()
    {
        await Running();

        var appended = await Sampler().Flush(A.FirstLaunchId, Token);

        Expect.Equal((0, 0), (appended, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task A_flush_and_the_tick_share_what_they_have_seen()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];
        _ = await Sampler().Flush(A.FirstLaunchId, Token);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(60));

        var tick = await Sampler().Sample(Token);
        _used[_agentSession] = [new ModelTokens("m", 20, 0, 0, 9)];
        var flush = await Sampler().Flush(A.FirstLaunchId, Token);

        Expect.Equal((0, 1, 2), (tick, flush, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task A_flush_of_a_launch_whose_session_already_ended_still_appends_its_final_totals()
    {
        await Running();
        await _s.Views.End();
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];

        var appended = await Sampler().Flush(A.FirstLaunchId, Token);

        Expect.Equal((1, 1), (appended, _s.Views.Slice.Store.Events.Count));
    }

    [Fact]
    public async Task A_tick_and_a_flush_of_one_launch_leave_the_newer_totals_in_the_ledger()
    {
        await Running();
        var first = new Held();
        var reads = 0;
        var usage = new Sequenced(() => Interlocked.Increment(ref reads) == 1 ? first : null, _agentSession) { Newer = [new ModelTokens("m", 20, 0, 0, 9)] };
        var sampler = new SessionUsageSampler(_s.Liveness, new LaunchViews(_s.Views.Views), usage, _samples, _s.Events(), _s.Views.Slice.Clock);
        var tick = sampler.Sample(Token).AsTask();   // reads the older totals and waits there
        await first.Reached.Task;
        var flush = sampler.Flush(A.FirstLaunchId, Token).AsTask();
        await Task.Yield();
        first.Release.SetResult([new ModelTokens("m", 10, 0, 0, 7)]);

        _ = await Task.WhenAll(tick, flush);

        var latest = _s.Views.Slice.Store.Events.Select(e => _s.Views.Slice.Events.Payload<SessionUsage>(e).Value).MaxBy(p => p.Sample)!;
        Expect.Equal((20L, 9L), (latest.InputTokens, latest.OutputTokens));
    }

    [Fact]
    public async Task A_flush_after_the_session_ended_and_a_tick_pruned_it_still_lands_in_the_same_minute()
    {
        await Running();
        _used[_agentSession] = [new ModelTokens("m", 10, 0, 0, 7)];
        _ = await Sampler().Sample(Token);
        _used[_agentSession] = [new ModelTokens("m", 20, 0, 0, 9)];
        await _s.Views.End();
        _s.Liveness.End(A.FirstLaunchId);
        _ = await Sampler().Sample(Token);   // the launch is no longer tracked: the tick forgets its totals

        var appended = await Sampler().Flush(A.FirstLaunchId, Token);

        var latest = _s.Views.Slice.Store.Events.Select(e => _s.Views.Slice.Events.Payload<SessionUsage>(e).Value).MaxBy(p => p.Sample)!;
        Expect.Equal((1, 20L, 9L), (appended, latest.InputTokens, latest.OutputTokens));
    }

    private sealed class Held
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<IReadOnlyList<ModelTokens>> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>The first read waits for the test and then answers the older totals; every later read answers the newer ones.</summary>
    private sealed class Sequenced(Func<Held?> first, SessionId session) : ILaunchUsage
    {
        public IReadOnlyList<ModelTokens>? Newer { get; set; }

        public async ValueTask<IReadOnlyList<ModelTokens>?> OfSession(AgentId target, SessionId id, CancellationToken cancellationToken)
        {
            if (id != session)
            {
                return null;
            }

            if (first() is { } held)
            {
                held.Reached.SetResult();
                return await held.Release.Task.ConfigureAwait(false);
            }

            return Newer;
        }
    }

    private sealed class Used(Dictionary<SessionId, IReadOnlyList<ModelTokens>> used) : ILaunchUsage
    {
        public ValueTask<IReadOnlyList<ModelTokens>?> OfSession(AgentId target, SessionId session, CancellationToken cancellationToken) =>
            ValueTask.FromResult(used.TryGetValue(session, out var rows) ? rows : null);
    }
}
