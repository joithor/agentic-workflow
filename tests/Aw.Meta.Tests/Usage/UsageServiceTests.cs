using Aw.Meta.Core.Usage;
using Aw.Meta.Features.Usage;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Usage;

public sealed class UsageServiceTests
{
    private readonly UsageWorld _w = new();

    private UsageService Service() => new(_w.World.Config, _w.Reader);

    [Fact, Trait("Lane", "unit")]
    public async Task A_quiet_team_is_all_ok_and_not_an_attention()
    {
        await _w.Ready();

        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Attention);
        Assert.Equal(["api", "ui", "meta"], result.Value.Subjects.Select(s => s.Subject), StringComparer.Ordinal);
        Assert.All(result.Value.Subjects, s => Assert.Equal(UsageState.Ok, s.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_limited_lane_has_its_kind_and_reset_and_is_an_attention()
    {
        await _w.Ready();
        _w.LimitLane("ui");
        _w.World.Panes.Remove("t1-meta");
        _w.World.ClaudeTranscripts.FailFor(MetaWorld.MetaSessionId);

        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        Assert.True(result.Value.Attention);
        var ui = result.Value.Subjects[1];
        Assert.Equal((UsageState.Limited, UsageKind.Session, new DateTimeOffset(2026, 10, 15, 5, 30, 0, TimeSpan.Zero)), (ui.State, ui.Kind, ui.ResetsAt));
        Assert.Equal(UsageState.Unknown, result.Value.Subjects[2].State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_subject_alone_is_not_an_attention()
    {
        await _w.Ready();
        _w.World.Panes.Remove("t1-ui");

        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        Assert.False(result.Value.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_the_call_fails()
    {
        var result = await Service().Handle(new UsageRequest(), CancellationToken.None);

        Assert.True(result.IsError);
    }
}
