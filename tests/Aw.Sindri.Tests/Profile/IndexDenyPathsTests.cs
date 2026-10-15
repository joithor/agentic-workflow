using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'index.denyPaths' (docs/sindri/profile.md): globs map extraction never reads, the profile's and a repo's own, added to the built-in secret globs.</summary>
public sealed class IndexDenyPathsTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_yaml_profile_reads_index_deny_paths_at_the_top_and_on_a_repo()
    {
        _files.Write(Base + "index:\n  denyPaths: [\"vendor/**\"]\nrepos:\n  - { name: web, path: /work/web, index: { denyPaths: [\"generated/**\", \"**/*.snap\"] } }\n");

        var profile = (await _files.Source.Load(Token)).Value.Profile;

        Assert.Equal(["vendor/**"], profile.Index!.DenyPaths);
        Assert.Equal(["generated/**", "**/*.snap"], profile.Repos![0].Index!.DenyPaths);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_profile_without_an_index_section_keeps_its_hash()
    {
        _files.Write(Base + "repos:\n  - { name: web, path: /work/web }\n");
        var plain = (await _files.Source.Load(Token)).Value.Hash;
        _files.Write(Base + "repos:\n  - { name: web, path: /work/web }\nindex:\n  denyPaths: [\"vendor/**\"]\n");
        var withIndex = (await _files.Source.Load(Token)).Value.Hash;

        Assert.NotEqual(plain, withIndex);
        _files.Write(Base + "repos:\n  - { name: web, path: /work/web }\n");
        Assert.Equal(plain, (await _files.Source.Load(Token)).Value.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_context_scope_gives_each_repo_the_profiles_globs_and_its_own()
    {
        var profile = new SindriProfile(1, "thor",
            Repos: [new RepoEntry("web", "/work/web", new IndexSettings(["generated/**"])), new RepoEntry("api", "/work/api")],
            Index: new IndexSettings(["vendor/**"]));

        var entries = ContextScopes.Entries(profile);

        Assert.Equal(["vendor/**", "generated/**"], entries.Single(e => e.Name == "web").DenyPaths);
        Assert.Equal(["vendor/**"], entries.Single(e => e.Name == "api").DenyPaths);
        Assert.Null(ContextScopes.Entries(new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web")]))[0].DenyPaths);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", "index.denyPaths[0]: must be a non-empty glob")]
    [InlineData("/etc/**", "index.denyPaths[0]: must be relative to the repo root")]
    [InlineData("a/../b", "index.denyPaths[0]: must not contain '..'")]
    [InlineData("a\\b", "index.denyPaths[0]: must use '/' separators")]
    public void A_glob_that_cant_be_one_names_its_key_path(string glob, string message)
    {
        var profile = new SindriProfile(1, "thor", Index: new IndexSettings([glob]));

        var error = ProfileRules.Check(profile).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repos_own_bad_glob_names_the_repo_and_an_over_long_one_is_refused()
    {
        var onRepo = new SindriProfile(1, "thor", Repos: [new RepoEntry("web", "/work/web", new IndexSettings(["ok/**", "../x"]))]);
        var tooLong = new SindriProfile(1, "thor", Index: new IndexSettings([new string('a', 301)]));

        Assert.Contains("repos.web.index.denyPaths[1]: must not contain '..'", ProfileRules.Check(onRepo).Error.Message, StringComparison.Ordinal);
        Assert.Contains("index.denyPaths[0]: is over 300 characters", ProfileRules.Check(tooLong).Error.Message, StringComparison.Ordinal);
        Assert.True(ProfileRules.Check(new SindriProfile(1, "thor", Index: new IndexSettings(["vendor/**", "**/*.snap"]))).IsSuccess);
    }
}
