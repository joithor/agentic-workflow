using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>Fact files in a git worktree: the Step's 'git add' skips them, and a file git tracks is never written over.</summary>
[Lane(TestLane.Integration)]
public sealed class StepFactWriterGitTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("aw-facts-git-").FullName;

    public StepFactWriterGitTests()
    {
        Assert.NotNull(ViewerProcess.Output("git", ["-C", _repo, .. IsolatedGit.Init()], TestContext.Current.CancellationToken).AsTask().Result);
    }

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private static Dictionary<string, string> Files(params string[] names) => names.ToDictionary(static n => n, static _ => "\"x\"", StringComparer.Ordinal);

    private async Task<string?> Git(params string[] arguments) => await ViewerProcess.Output("git", ["-C", _repo, .. IsolatedGit.NoHooks, "-c", "user.name=t", "-c", "user.email=t@example.com", .. arguments], TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_written_names_are_excluded_locally_so_git_add_leaves_them_out_and_the_gitignore_is_untouched()
    {
        var problem = await StepFactWriter.Write(_repo, Files("workspace.json", "brief.json"), TestContext.Current.CancellationToken);

        Assert.Null(problem);
        _ = await Git("add", "-A");
        Assert.Equal(string.Empty, await Git("status", "--porcelain"));
        Assert.False(File.Exists(Path.Combine(_repo, ".gitignore")));
    }

    [Fact]
    public async Task A_second_launch_rewrites_its_own_untracked_file_and_adds_no_second_exclude_line()
    {
        _ = await StepFactWriter.Write(_repo, Files("brief.json"), TestContext.Current.CancellationToken);

        var problem = await StepFactWriter.Write(_repo, new Dictionary<string, string>(StringComparer.Ordinal) { ["brief.json"] = "\"newer\"" }, TestContext.Current.CancellationToken);

        Assert.Null(problem);
        Assert.Equal("\"newer\"", await File.ReadAllTextAsync(Path.Combine(_repo, "brief.json"), TestContext.Current.CancellationToken));
        var exclude = await File.ReadAllLinesAsync(Path.Combine(_repo, ".git", "info", "exclude"), TestContext.Current.CancellationToken);
        Assert.Single(exclude, "/brief.json");
        Assert.Single(exclude, "# sindri step fact files (written by the launcher)");
    }

    [Fact]
    public async Task A_name_git_tracks_fails_naming_the_file_and_the_fact_and_nothing_is_written_or_changed()
    {
        await File.WriteAllTextAsync(Path.Combine(_repo, "brief.json"), "{\"mine\":true}", TestContext.Current.CancellationToken);
        _ = await Git("add", "brief.json");
        _ = await Git("commit", "-q", "-m", "tracked");

        var problem = await StepFactWriter.Write(_repo, Files("workspace.json", "brief.json"), TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal(SindriErrors.LaunchFactFileTracked.Code, problem.Code);
        Assert.Contains("brief.json", problem.Message, StringComparison.Ordinal);
        Assert.Contains("fact:brief", problem.Message, StringComparison.Ordinal);
        Assert.Equal("{\"mine\":true}", await File.ReadAllTextAsync(Path.Combine(_repo, "brief.json"), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(_repo, "workspace.json")));
    }
}
