using Aw.Kernel.Testing;
using Aw.Readiness.App.Infrastructure;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The gh adapter's own safety: which binary runs, and what happens to one that hangs (integration lane:
/// real files and processes, no network).</summary>
[Lane(TestLane.Integration)]
public sealed class GhCliTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private string Script(string name, string body)
    {
        var path = Path.Combine(_dir.Path, name);
        return ExecutableScript.Write(path, body + "\n");
    }

    [Fact]
    public void A_bare_gh_resolves_only_through_absolute_path_entries()
    {
        var bin = Directory.CreateDirectory(Path.Combine(_dir.Path, "bin")).FullName;
        _ = Script("bin/gh", "echo real");
        _ = Script("gh", "echo planted");   // a checkout's own gh, in a relative PATH entry and the current directory

        var found = GhCli.Resolve("gh", string.Join(Path.PathSeparator, ".", "", "relative/dir", bin));

        Assert.Equal(Path.Combine(bin, "gh"), found);
    }

    [Fact]
    public void The_search_path_comes_from_the_environment_composition_passes()
    {
        var bin = Directory.CreateDirectory(Path.Combine(_dir.Path, "envbin")).FullName;
        _ = Script("envbin/gh", "echo env");

        var settings = SettingsFromEnvironment.Read(name => name == "PATH" ? bin : null);

        Assert.Equal(bin, settings.GhSearchPath);
        Assert.Equal(Path.Combine(bin, "gh"), GhCli.Resolve(settings.GhBinary, settings.GhSearchPath));
    }

    [Theory]
    [InlineData("gh")]
    [InlineData("relative/gh")]
    public void No_absolute_gh_on_path_or_a_relative_override_resolves_to_nothing(string binary) =>
        Assert.Null(GhCli.Resolve(binary, string.Join(Path.PathSeparator, ".", "relative")));

    [Fact]
    public async Task A_gh_that_hangs_times_out_and_is_killed()
    {
        var marker = Path.Combine(_dir.Path, "still-running");
        var gh = Script("gh-hangs", $"sleep 5; touch '{marker}'");
        var cli = new GhCli(new ReadinessSettings(_dir.Path, gh, GhSearchPath: "", TimeSpan.FromMilliseconds(300)));

        var reply = await cli.Run(_dir.Path, ["repo", "view"], CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken);

        Assert.False(reply.Ran);
        Assert.Contains("timed out", reply.Reason, StringComparison.Ordinal);
        Assert.False(File.Exists(marker), "the hung gh kept running after the timeout");
    }
}
