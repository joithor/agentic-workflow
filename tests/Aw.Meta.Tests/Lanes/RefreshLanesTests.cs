using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.Lanes;
using Aw.Meta.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Lanes;

public sealed class RefreshLanesTests
{
    private readonly InMemoryMetaConfig _config = new();
    private readonly InMemorySessionDirectory _sessions = new();
    private readonly InMemoryMessageStore _messages = new();
    private readonly InMemoryLaneStore _lanes = new();
    private readonly FakeTimeProvider _clock = new(A.T0);

    private RefreshLanesService Service() => new(_config, new LaneRefresher(_sessions, _messages, _lanes), _clock, MetaWorld.NoNonClaudeLanes());

    private async Task<LanesResponse> Refresh()
    {
        var result = await Service().Handle(new RefreshLanesRequest(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task Configure() =>
        _ = await _config.Save(new("t1", "/work", ["meta", "api", "ui"], "meta"), CancellationToken.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_session_named_team_lane_is_mapped_and_the_map_gets_version_one()
    {
        await Configure();
        _sessions.Add(A.Live("t1-api", "s-api"));
        _sessions.Add(A.Live("t1-meta", "s-meta"));

        var map = await Refresh();

        Assert.Equal((1, true), (map.Version, map.Changed));
        Assert.Equal(["meta", "api"], map.Entries.Select(e => e.Lane), StringComparer.Ordinal);
        Assert.Equal(["ui"], map.Unmapped, StringComparer.Ordinal);
        Assert.Equal(1, (await _lanes.Current(CancellationToken.None)).Value.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Refreshing_again_with_nothing_changed_keeps_the_version()
    {
        await Configure();
        _sessions.Add(A.Live("t1-api", "s-api"));
        _ = await Refresh();

        var again = await Refresh();

        Assert.Equal((1, false), (again.Version, again.Changed));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_session_bumps_the_version()
    {
        await Configure();
        _sessions.Add(A.Live("t1-api", "s-api"));
        _ = await Refresh();
        _sessions.Add(A.Live("t1-ui", "s-ui"));

        var map = await Refresh();

        Assert.Equal((2, true), (map.Version, map.Changed));
        Assert.DoesNotContain("ui", map.Unmapped, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_announcement_from_a_live_session_maps_a_lane_with_no_directly_named_session()
    {
        await Configure();
        _sessions.Add(A.Live("agentic-9a", "s-9"));
        _ = await _messages.RecordInbound("/t", 1, [InboxFrom("m1", "agentic-9a", "ui lane online")], CancellationToken.None);

        var map = await Refresh();

        var ui = Assert.Single(map.Entries);
        Assert.Equal(("ui", "agentic-9a", LaneSource.Announcement), (ui.Lane, ui.SessionName, ui.Source));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_announcement_text_starts_with_the_version_and_names_every_mapped_lane_and_the_unmapped_ones()
    {
        await Configure();
        _sessions.Add(A.Live("t1-api", "s-api"));
        _sessions.Add(A.Live("t1-meta", "s-meta"));

        var map = await Refresh();

        Assert.Equal("LANE MAP v1: meta=t1-meta, api=t1-api (unmapped: ui)", map.Announcement);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_it_fails_with_AW_META_002()
    {
        var result = await Service().Handle(new RefreshLanesRequest(), CancellationToken.None);

        Assert.Equal(MetaErrors.NoTeamConfig.Code, result.Error.Code);
    }

    private static Core.Messages.InboxMessage InboxFrom(string id, string name, string body) =>
        Core.Messages.InboxMessage.Of(new(MessageId.From(id), $"uds:/run/{name}.sock", name, body, A.At(1)));
}
