using System.Diagnostics;
using System.Globalization;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>A throwaway git repo with one commit holding the given files; Dispose deletes it.</summary>
internal sealed class TempGitRepo : IDisposable
{
    private static readonly TimeSpan _gitTimeout = TimeSpan.FromSeconds(30);

    public TempGitRepo(IReadOnlyDictionary<string, string> files)
    {
        try
        {
            Init(files);
            Head = Run("rev-parse", "HEAD");
            Repo = new ContextRepo(ContextRepoName.From("aw-split-panes"), Path);
        }
        catch (Exception)
        {
            try
            {
                Directory.Delete(Path, recursive: true);   // a git that timed out must not leave its folder behind
            }
            catch (IOException)
            {
                // the cleanup failing must not replace the failure that got us here
            }

            throw;
        }
    }

    private void Init(IReadOnlyDictionary<string, string> files)
    {
        _ = Run("init", "-q", "-b", "main");
        foreach (var (path, text) in files)
        {
            var full = System.IO.Path.Combine(Path, path);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        _ = Run("add", "-A");
        _ = Run("commit", "-q", "-m", "first");
    }

    public string Path { get; } = Directory.CreateTempSubdirectory("aw-ctx-git-").FullName;

    public string Head { get; }

    public ContextRepo Repo { get; }

    public string Run(params string[] args) => RunAt(at: null, args);

    /// <summary>Runs git with the committer and author date set, so a commit lands at an exact instant.</summary>
    /// <param name="at">The instant, or null for git's own.</param>
    /// <param name="args">The git arguments.</param>
    /// <returns>Git's trimmed output.</returns>
    public string RunAt(DateTimeOffset? at, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        if (at is { } instant)
        {
            start.Environment["GIT_AUTHOR_DATE"] = instant.ToString("O", CultureInfo.InvariantCulture);
            start.Environment["GIT_COMMITTER_DATE"] = instant.ToString("O", CultureInfo.InvariantCulture);
        }

        foreach (var arg in (string[])["-C", Path, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;   // why: git is on the test machine's PATH
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(_gitTimeout);
        var stdout = git.StandardOutput.ReadToEndAsync(limit.Token);
        var stderr = git.StandardError.ReadToEndAsync(limit.Token);
        var exited = git.WaitForExitAsync(limit.Token);
        try
        {
            Task.WaitAll([exited, stdout, stderr], limit.Token);
        }
        catch (Exception e) when (!TestContext.Current.CancellationToken.IsCancellationRequested && e is OperationCanceledException or AggregateException { InnerException: OperationCanceledException })
        {
            git.Kill(entireProcessTree: true);
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"git {string.Join(' ', args)} did not finish in {_gitTimeout.TotalSeconds} s: a stuck git, or a pipe a concurrent fork still holds"), e);
        }

        return git.ExitCode == 0 ? stdout.Result.Trim() : throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"git {string.Join(' ', args)} exited {git.ExitCode}: {stderr.Result}"));
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
