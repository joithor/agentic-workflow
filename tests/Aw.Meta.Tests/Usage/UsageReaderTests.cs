using Aw.Meta.Core.Usage;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Usage;

public sealed class UsageReaderTests
{
    private readonly UsageWorld _w = new();

    private async Task<IReadOnlyList<UsageReading>> Read()
    {
        var team = (await _w.World.Config.Load(CancellationToken.None)).Value;
        var rows = await _w.Reader.Read(team, CancellationToken.None);
        Assert.True(rows.IsSuccess, rows.IsError ? rows.Error.Message : string.Empty);
        return rows.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_lane_but_the_meta_lane_is_read_then_the_meta_and_a_quiet_team_is_ok()
    {
        await _w.Ready();

        var rows = await Read();

        Assert.Equal(["api", "ui", "meta"], rows.Select(r => r.Subject), StringComparer.Ordinal);
        Assert.All(rows, r => Assert.Equal(UsageState.Ok, r.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_in_the_lanes_pane_is_limited_and_idle_when_the_prompt_is_empty()
    {
        await _w.Ready();
        _w.LimitLane("api");

        var api = (await Read())[0];

        Assert.Equal(UsageState.Limited, api.State);
        Assert.Equal(UsageKind.Session, api.Stop?.Kind);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 5, 30, 0, TimeSpan.Zero), api.Stop?.ResetsAt);
        Assert.True(api.Idle);
        Assert.Equal("pane", api.Source);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_pane_is_limited_but_not_idle()
    {
        await _w.Ready();
        _w.LimitLane("api", busy: true);

        var api = (await Read())[0];

        Assert.Equal(UsageState.Limited, api.State);
        Assert.False(api.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_at_the_end_of_the_lanes_transcript_is_limited_and_never_idle_because_only_the_pane_says_the_lane_is_at_a_prompt()
    {
        await _w.Ready();
        _w.LaneSession("ui", "s-ui");
        _w.World.ClaudeTranscripts.Put("s-ui", LimitLineWithNewline(), A.At(58));

        var ui = (await Read())[1];

        Assert.Equal(UsageState.Limited, ui.State);
        Assert.False(ui.Idle);
        Assert.Equal("transcript", ui.Source);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_meta_is_limited_when_its_own_transcript_ends_in_the_message()
    {
        await _w.Ready();
        _w.World.ClaudeTranscripts.Put(MetaWorld.MetaSessionId, LimitLineWithNewline(), A.At(58));

        var meta = (await Read())[2];

        Assert.Equal(("meta", UsageState.Limited), (meta.Subject, meta.State));
        Assert.Equal(UsageKind.Session, meta.Stop?.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_whose_pane_and_transcript_cannot_be_read_is_unknown_with_the_reason()
    {
        await _w.Ready();
        _w.World.Panes.Remove("t1-ui");

        var ui = (await Read())[1];

        Assert.Equal(UsageState.Unknown, ui.State);
        Assert.False(string.IsNullOrEmpty(ui.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_non_Claude_lane_is_read_from_its_pane_only()
    {
        await _w.Ready();
        _ = _w.World.PutLane("ui");
        _w.World.Panes.Show("t1-ui", UsageWorld.Idle(UsageWorld.Limit));

        var ui = (await Read())[1];

        Assert.Equal(UsageState.Limited, ui.State);
        Assert.Equal("pane", ui.Source);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pane_and_transcript_agreeing_say_both()
    {
        await _w.Ready();
        _w.LaneSession("api", "s-api");
        _w.LimitLane("api");
        _w.World.ClaudeTranscripts.Put("s-api", LimitLineWithNewline(), A.At(58));

        var api = (await Read())[0];

        Assert.Equal("pane+transcript", api.Source);
        Assert.True(api.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_limit_further_back_than_the_tail_window_is_not_read()
    {
        await _w.Ready();
        _w.LaneSession("api", "s-api");
        _w.World.ClaudeTranscripts.Put("s-api", LimitLineWithNewline() + new string('x', UsageReader.TailBytes + 1000) + "\n", A.At(58));

        var api = (await Read())[0];

        Assert.NotEqual(UsageState.Limited, api.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_limit_inside_the_tail_window_is_read()
    {
        await _w.Ready();
        _w.LaneSession("api", "s-api");
        _w.World.ClaudeTranscripts.Put("s-api", new string('x', UsageReader.TailBytes + 1000) + "\n" + LimitLineWithNewline(), A.At(58));

        var api = (await Read())[0];

        Assert.Equal(UsageState.Limited, api.State);
    }

    private static string LimitLineWithNewline() => UsageWorld.LimitLine + "\n";
}
