using Aw.Meta.Core.Config;
using Aw.Meta.Core.Usage;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Usage;

public sealed class UsageWatcherTests
{
    private readonly UsageWorld _w = new();

    private async Task<IReadOnlyList<string>> Tick(UsageConfig? config = null)
    {
        var team = (await _w.World.Config.Load(CancellationToken.None)).Value with { Usage = config ?? new UsageConfig() };
        var readings = await _w.Reader.Read(team, CancellationToken.None);
        return await _w.Watcher.Tick(team, readings.Value, CancellationToken.None);
    }

    private void PastTheReset() => _w.World.Clock.Advance(TimeSpan.FromMinutes(40));

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_is_announced_once_across_ticks_and_logged()
    {
        await _w.Ready();
        _w.LimitLane("api");

        var first = await Tick();
        var second = await Tick();

        Assert.Equal(["USAGE LIMITED api session resets 2026-10-15 05:30:00Z"], first);
        Assert.Empty(second);
        Assert.Contains("NOTE", Assert.Single(_w.World.Log.Lines), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_idle_lane_whose_window_is_back_gets_the_resume_line_typed_and_submitted()
    {
        await _w.Ready();
        _w.LimitLane("api");
        _ = await Tick();
        PastTheReset();

        var lines = await Tick();

        Assert.Equal(["USAGE OK api"], lines);
        Assert.Equal([("t1-api", UsageWatch.ResumeLine)], _w.World.Panes.Woken);
        Assert.Contains(_w.World.Log.Lines, l => l.Contains("resumed", StringComparison.Ordinal) && l.Contains("api", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_enter_the_pane_swallows_is_pressed_again_and_the_line_is_verified()
    {
        await _w.Ready();
        _w.LimitLane("api");
        _ = await Tick();
        PastTheReset();
        _w.World.Panes.SwallowEnters = 2;

        _ = await Tick();

        Assert.Equal(3, _w.World.Panes.Enters);
        Assert.Equal([("t1-api", UsageWatch.ResumeLine)], _w.World.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_resume_that_cannot_be_verified_is_reported_and_not_logged_as_resumed()
    {
        await _w.Ready();
        _w.LimitLane("api");
        _ = await Tick();
        PastTheReset();
        _w.World.Panes.SwallowEnters = 10;

        var lines = await Tick();

        Assert.Equal(2, lines.Count);
        Assert.StartsWith("USAGE RESUME FAILED api: ", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain(_w.World.Log.Lines, l => l.Contains("resumed", StringComparison.Ordinal));
        Assert.Equal(4, _w.World.Panes.Enters);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_lane_is_never_typed_into()
    {
        await _w.Ready();
        _w.LimitLane("api", busy: true);
        _ = await Tick();
        PastTheReset();

        var lines = await Tick();

        Assert.Equal(["USAGE OK api"], lines);
        Assert.Empty(_w.World.Panes.Woken);
        Assert.Equal(0, _w.World.Panes.Enters);

        _w.LimitLane("api");
        _ = await Tick();

        Assert.Equal([("t1-api", UsageWatch.ResumeLine)], _w.World.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_that_is_limited_only_in_its_transcript_is_announced_but_not_typed_into()
    {
        await _w.Ready();
        _w.LaneSession("ui", "s-ui");
        _w.World.ClaudeTranscripts.Put("s-ui", UsageWorld.LimitLine + "\n", A.At(58));
        _ = await Tick();
        PastTheReset();

        var lines = await Tick();

        Assert.Equal(["USAGE OK ui"], lines);
        Assert.Empty(_w.World.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Auto_resume_off_types_nothing()
    {
        await _w.Ready();
        _w.LimitLane("api");
        var off = new UsageConfig(AutoResume: false);
        _ = await Tick(off);
        PastTheReset();

        var lines = await Tick(off);

        Assert.Equal(["USAGE OK api"], lines);
        Assert.Empty(_w.World.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_meta_is_woken_by_the_output_and_nothing_is_typed_into_its_pane()
    {
        await _w.Ready();
        _w.World.ClaudeTranscripts.Put(MetaWorld.MetaSessionId, UsageWorld.LimitLine + "\n", A.At(58));
        _ = await Tick();
        PastTheReset();

        var lines = await Tick();

        Assert.Equal(["USAGE OK meta", "USAGE OK meta: resume your subagents (SendMessage them) and re-arm the watch"], lines);
        Assert.Empty(_w.World.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_memory_is_kept_in_the_store_so_another_watcher_does_not_repeat_the_announcement()
    {
        await _w.Ready();
        _w.LimitLane("api");
        _ = await Tick();

        var again = await Tick();

        Assert.Empty(again);
        Assert.NotNull((await _w.World.Values.Get("usage.watch", CancellationToken.None)).Value);
    }
}
