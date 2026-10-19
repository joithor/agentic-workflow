using Aw.Context.Infrastructure.Git;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>
/// 'git archive' when git has exited but something else still holds its stderr open (a stray fork of a parallel process): the call
/// stops waiting for that EOF after the grace instead of holding to the whole archive timeout. A stand-in 'git' script plays git.
/// </summary>
[Trait("Lane", "integration")]
public sealed class GitCliArchiveTests : IDisposable
{
    private static readonly TimeSpan _grace = TimeSpan.FromMilliseconds(300);

    private readonly string _folder = Directory.CreateTempSubdirectory("aw-fake-git-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // Prints no tar (an empty archive), starts a sleeper that keeps the inherited stderr open and gives up its stdout, then exits with the code.
    private GitCli FakeGit(int exit, string stderrText)
    {
        var script = Path.Combine(_folder, "git");
        File.WriteAllLines(script, ["sleep 3 > /dev/null &", $"echo {stderrText} >&2", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"exit {exit}")]);
        return new GitCli(["/bin/sh", script], _grace);
    }

    private static Task Drain(Stream tar, CancellationToken token) => tar.CopyToAsync(Stream.Null, token);

    [Fact]
    public async Task Archive_returns_within_the_grace_when_git_exits_but_its_stderr_never_reaches_EOF()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });

        var result = await GraphifyRun.Archive(FakeGit(exit: 0, "quiet"), repo, Drain);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task Archive_still_reports_a_failing_git_when_its_stderr_never_reaches_EOF()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });

        var result = await GraphifyRun.Archive(FakeGit(exit: 3, "boom"), repo, Drain);

        Assert.True(result.IsError);
        Assert.Contains("exit 3", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Archive_finishes_when_the_consumer_stops_reading_before_the_end_of_the_tar()
    {
        // A TarReader stops at the end-of-archive marker and leaves git's record padding unread. Once the user is over
        // pipe-user-pages-soft (a loaded gate), each new pipe holds one page, git blocks writing that padding and never
        // exits. A file bigger than any pipe makes the same stop-early consumer hang on every box.
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["big.txt"] = new string('x', 1024 * 1024) });

        var result = await GraphifyRun.Archive(new GitCli(), repo, ReadFirstBlock, deadline: TimeSpan.FromSeconds(10));

        Assert.False(result.IsError);
    }

    private static async Task ReadFirstBlock(Stream tar, CancellationToken token) => _ = await tar.ReadAsync(new byte[512], token);
}
