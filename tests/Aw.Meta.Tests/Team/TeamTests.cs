#pragma warning disable CA1822, IDE0052 // red stage: removed when the service takes its ports
using Aw.Meta.Core;
using Aw.Meta.Features.Team;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Team;

public sealed class TeamTests
{
    private readonly InMemoryMetaConfig _config = new();

    private TeamInitService Init() => new();

    private TeamShowService Show() => new();

    private static TeamInitRequest Request(string team = "sindri", string[]? lanes = null, bool force = false) =>
        new(team, "/work/repo", lanes ?? ["api", "ui"], "meta", Log: null, force);

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_writes_a_starter_config_with_the_meta_lane_and_a_repo_scope()
    {
        var made = await Init().Handle(Request(), CancellationToken.None);
        var loaded = await _config.Load(CancellationToken.None);

        Assert.True(made.IsSuccess);
        Assert.Equal(["meta", "api", "ui"], loaded.Value.Lanes);
        Assert.Equal(("sindri", "/work/repo", "meta"), (loaded.Value.Team, loaded.Value.Repo, loaded.Value.MetaLane));
        Assert.Equal(["repo"], loaded.Value.Gate.Always);
        Assert.Equal(["**"], loaded.Value.Gate.Scopes["repo"].Paths);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_keeps_a_meta_lane_the_caller_already_listed()
    {
        _ = await Init().Handle(Request(lanes: ["api", "meta"]), CancellationToken.None);

        Assert.Equal(["api", "meta"], (await _config.Load(CancellationToken.None)).Value.Lanes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamInit_refuses_to_replace_a_config_unless_forced()
    {
        _ = await Init().Handle(Request(), CancellationToken.None);

        var refused = await Init().Handle(Request(team: "other"), CancellationToken.None);
        var forced = await Init().Handle(Request(team: "other", force: true), CancellationToken.None);

        Assert.Equal(MetaErrors.ConfigExists.Code, refused.Error.Code);
        Assert.Equal("other", forced.Value.Team);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "api")]
    [InlineData("sindri", "")]
    [InlineData("sindri", "api,api")]
    [InlineData("sindri", "a b")]
    public void TeamInit_input_that_cannot_name_sessions_is_refused(string team, string lanes)
    {
        var lane = lanes.Length == 0 ? [] : lanes.Split(',');

        var result = new TeamInitValidator().Validate(Request(team, lane));

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TeamShow_returns_the_config_or_AW_META_002()
    {
        var none = await Show().Handle(new TeamShowRequest(), CancellationToken.None);
        _ = await Init().Handle(Request(), CancellationToken.None);
        var some = await Show().Handle(new TeamShowRequest(), CancellationToken.None);

        Assert.Equal(MetaErrors.NoTeamConfig.Code, none.Error.Code);
        Assert.Equal("sindri", some.Value.Team);
    }
}
