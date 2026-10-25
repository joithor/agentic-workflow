using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri repo status' as Program runs it, over an in-memory profile and a fake locator, with no control plane: the SessionStart hook's contract (one line or nothing, exit 0 for --nudge, nothing written).</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class RepoStatusCommandTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private readonly InMemoryProfiles _profiles = new();

    public RepoStatusCommandTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(params string[] args)
    {
        var locator = new FakeRepoLocator().In(Path.GetFullPath("/work/demo/src"), "/work/demo");
        return CommandTable.Run(args, Environment, TimeProvider.System, services => services
            .AddSingleton<IRepoLocator>(locator)
            .AddSingleton<IApprovedProfile>(_profiles)
            .AddSingleton<IProfileSource>(_profiles));
    }

    private static LoadedProfile Profile(params RepoEntry[] repos)
    {
        var profile = new SindriProfile(1, "thor", Repos: repos);
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private async Task Approve(params RepoEntry[] repos) => _ = await _profiles.Save(Profile(repos), TestContext.Current.CancellationToken);

    [Fact, Trait("Lane", "integration")]
    public async Task Nudge_prints_one_line_for_a_repo_that_is_not_in_the_approved_profile_and_exits_0()
    {
        await Approve(new RepoEntry("other", "/work/other"));

        var code = await Run("repo", "status", "/work/demo/src", "--nudge");

        Assert.Equal(0, code);
        Assert.Equal("sindri: demo is not onboarded (no secret scan or index); run: sindri repo onboard\n", _out.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Nudge_is_silent_for_an_onboarded_repo_with_no_approved_profile_and_outside_git()
    {
        Assert.Equal(0, await Run("repo", "status", "/work/demo/src", "--nudge"));
        await Approve(new RepoEntry("demo", "/work/demo"));
        Assert.Equal(0, await Run("repo", "status", "/work/demo/src", "--nudge"));
        Assert.Equal(0, await Run("repo", "status", "/elsewhere", "--nudge"));

        Assert.Equal(string.Empty, _out.ToString());
        Assert.Equal(string.Empty, _error.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Plain_status_exits_0_when_onboarded_and_1_when_not_and_says_which()
    {
        await Approve(new RepoEntry("demo", "/work/demo"));
        Assert.Equal(0, await Run("repo", "status", "/work/demo/src"));
        Assert.Equal(1, await Run("repo", "status", "/elsewhere"));

        Assert.Equal("demo is onboarded.\nNot inside a git repo.\n", _out.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_status_names_the_state()
    {
        await Approve(new RepoEntry("demo", "/work/demo"));

        Assert.Equal(0, await Run("repo", "status", "/work/demo/src", "--json"));

        Assert.Contains("\"state\":\"onboarded\"", _out.ToString().Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Status_writes_nothing_under_the_home()
    {
        _ = await Run("repo", "status", "/work/demo/src", "--nudge");
        _ = await Run("repo", "status", "/work/demo/src");

        Assert.Empty(Directory.GetFileSystemEntries(_home.Path));
    }
}
