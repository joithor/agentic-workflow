using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.GetProfile;

namespace Aw.Sindri.Tests.GetProfile;

/// <summary>What the live profile asks for beyond the approved one, in words (setup-once spec §5.2 "Preview").</summary>
public sealed class ProfileChangesTests
{
    private static readonly ShippedBlockSentences _sentences = new();

    [Fact, Trait("Lane", "unit")]
    public void With_nothing_approved_every_repo_service_and_effect_is_new()
    {
        var live = Profile(repos: [new RepoEntry("web", "/work/web")], services: new() { ["heartbeat"] = new ServiceSettings("5s") }, effects: ["effect.claim.ref"]);

        var changes = ProfileChanges.Between(approved: null, live, _sentences);

        Assert.Equal(["web"], changes.NewRepos.Select(static r => r.Name));
        Assert.Equal(["Sindri will run the heartbeat service every 5 seconds"], changes.Services);
        Assert.Equal(["Reserves a work item so no other run starts on it"], changes.Effects);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_the_approved_profile_names_is_not_new()
    {
        var approved = Profile(repos: [new RepoEntry("web", "/work/web")]);
        var live = Profile(repos: [new RepoEntry("web", "/work/web"), new RepoEntry("api", "/work/api")]);

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(["api"], changes.NewRepos.Select(static r => r.Name));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_service_run_more_often_is_said_with_both_cadences_and_a_slower_one_is_not_said()
    {
        var approved = Profile(services: new() { ["heartbeat"] = new ServiceSettings("1h"), ["observe"] = new ServiceSettings("5m") });
        var live = Profile(services: new() { ["heartbeat"] = new ServiceSettings("1m"), ["observe"] = new ServiceSettings("1h") });

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(["Sindri will run the heartbeat service more often: every 1 minute instead of every 1 hour"], changes.Services);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_effect_the_approved_profile_allows_is_not_new_and_an_unknown_kind_is_said_by_its_name()
    {
        var approved = Profile(effects: ["effect.claim.ref"]);
        var live = Profile(effects: ["effect.claim.ref", "effect.notify.desk", "effect.deploy.canary-ring"]);

        var changes = ProfileChanges.Between(approved, live, _sentences);

        Assert.Equal(["Puts a question or notice in your Needs you list", "Allows the effect \"deploy canary ring\""], changes.Effects);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_that_asks_for_nothing_new_is_empty()
    {
        var same = Profile(repos: [new RepoEntry("web", "/work/web")], effects: ["effect.claim.ref"]);

        Assert.True(ProfileChanges.Between(same, same, _sentences).IsEmpty);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_has_three_sentences_in_reading_order()
    {
        Assert.Equal(
            ["Sindri will read /work/web and send what it reads to your Onyx and to the map cleanup agent",
             "Sindri will write its MCP settings into web for Claude Code, Codex and Cursor",
             "Sindri will keep web's map up to date on its own"],
            ProfileChanges.RepoSentences("web", "/work/web"));
    }

    private static SindriProfile Profile(IReadOnlyList<RepoEntry>? repos = null, Dictionary<string, ServiceSettings>? services = null, IReadOnlyList<string>? effects = null) =>
        new(1, "t", Repos: repos, Services: services, Workflow: effects is null ? null : new WorkflowSettings(Effects: effects));
}
