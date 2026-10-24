using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CodeIndexJobs;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Testing;
using ProfileEmbeddings = Aw.Sindri.Core.Profile.EmbeddingSettings;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>
/// The code index scope is the approved profile's repos plus every live repo it doesn't name (owner q102, CIX-L59): the typed approval stays the consent for the deny globs and the embeddings address only.
/// Both scope copies (reads and jobs) are held to the same answers.
/// </summary>
public sealed class CodeIndexLiveScopeTests
{
    private static readonly IndexSettings _top = new(DenyPaths: ["private/**"], Embeddings: new ProfileEmbeddings(Enabled: true, Url: "http://127.0.0.1:11500", Model: "mxbai-embed-large"));

    public static TheoryData<string> Copies => new() { "reads", "jobs" };

    private static SindriProfile Approved() => new(1, "thor", Index: _top, Repos: [new RepoEntry("demo", "/work/demo", new IndexSettings(DenyPaths: ["gen/**"]))]);

    private static Result<CodeIndexScope> Scope(string copy, SindriProfile approved, SindriProfile? live, CallerContext? caller = null)
        => copy == "reads" ? CodeIndexScopes.For(approved, live, caller) : CodeIndexJobScopes.For(approved, live, caller);

    private static CodeIndexRepo Repo(CodeIndexScope scope, string name) => scope.Repos.Single(r => r.Name.Value == name);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Copies))]
    public void A_live_only_repo_is_in_scope_with_the_approved_top_level_settings(string copy)
    {
        var live = Approved() with { Repos = [new RepoEntry("demo", "/work/demo"), new RepoEntry("fresh", "/work/fresh")] };

        var scope = Scope(copy, Approved(), live).Value;

        Assert.Equal(["demo", "fresh"], scope.Repos.Select(static r => r.Name.Value));
        var fresh = Repo(scope, "fresh").Settings;
        Assert.Equal(["private/**"], fresh.DenyPaths);
        Assert.Equal("http://127.0.0.1:11500", fresh.Embeddings.Url);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Copies))]
    public void A_live_edit_to_an_approved_repos_deny_paths_does_not_take_effect(string copy)
    {
        var live = Approved() with { Repos = [new RepoEntry("demo", "/work/demo", new IndexSettings(DenyPaths: ["other/**"]))] };

        var demo = Repo(Scope(copy, Approved(), live).Value, "demo").Settings;

        Assert.Equal(["private/**", "gen/**"], demo.DenyPaths);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Copies))]
    public void A_live_edit_to_the_top_level_deny_paths_or_embeddings_address_does_not_take_effect(string copy)
    {
        var edited = new IndexSettings(DenyPaths: [], Embeddings: new ProfileEmbeddings(Url: "http://evil.example:1"));
        var live = Approved() with { Index = edited, Repos = [new RepoEntry("fresh", "/work/fresh")] };

        var fresh = Repo(Scope(copy, Approved(), live).Value, "fresh").Settings;

        Assert.Equal(["private/**"], fresh.DenyPaths);
        Assert.Equal("http://127.0.0.1:11500", fresh.Embeddings.Url);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Copies))]
    public void A_live_only_repos_own_deny_paths_add_to_the_approved_ones(string copy)
    {
        var live = Approved() with { Repos = [new RepoEntry("fresh", "/work/fresh", new IndexSettings(DenyPaths: ["secrets/**"]))] };

        var fresh = Repo(Scope(copy, Approved(), live).Value, "fresh").Settings;

        Assert.Equal(["private/**", "secrets/**"], fresh.DenyPaths);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Copies))]
    public void A_live_only_repos_own_entry_never_changes_the_embeddings_address(string copy)
    {
        var own = new IndexSettings(Embeddings: new ProfileEmbeddings(Url: "http://evil.example:1"));
        var live = Approved() with { Repos = [new RepoEntry("fresh", "/work/fresh", own)] };

        var fresh = Repo(Scope(copy, Approved(), live).Value, "fresh").Settings;

        Assert.Equal("http://127.0.0.1:11500", fresh.Embeddings.Url);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Copies))]
    public void A_session_sees_only_its_own_live_only_repo(string copy)
    {
        var live = Approved() with { Repos = [new RepoEntry("demo", "/work/demo"), new RepoEntry("fresh", "/work/fresh")] };
        var caller = A.Caller().Session().InRepo("fresh").Build();

        var scope = Scope(copy, Approved(), live, caller).Value;

        Assert.Equal(["fresh"], scope.Repos.Select(static r => r.Name.Value));
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Copies))]
    public void No_live_profile_gives_the_approved_scope(string copy)
    {
        var scope = Scope(copy, Approved(), live: null).Value;

        Assert.Equal(["demo"], scope.Repos.Select(static r => r.Name.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_approved_profile_nothing_is_indexed_even_when_the_live_file_has_repos()
    {
        var harness = new SliceHarness();
        harness.Profiles.Live = new LoadedProfile(Approved(), "hash", "/home/profile.yaml");

        var read = await new CodeIndexScopes(harness.Profiles, harness.Profiles, harness.Callers).Current(TestContext.Current.CancellationToken);
        var job = await new CodeIndexJobScopes(harness.Profiles, harness.Profiles, harness.Callers).Current(TestContext.Current.CancellationToken);

        Assert.Equal(["SND-PROFILE-005", "SND-PROFILE-005"], new[] { read.Error.Code, job.Error.Code });
    }
}
