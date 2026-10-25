using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Infrastructure.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The git change source honours the change contract over a repo built in a temp directory.</summary>
[Lane(TestLane.Integration)]
public sealed class GitChangeSourceContractTests : ChangeSourceContract, IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "aw-change-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (!Directory.Exists(_base))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_base, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);   // git objects are read-only
        }

        Directory.Delete(_base, recursive: true);
    }

    protected override IChangeSource Create(IReadOnlyDictionary<string, string> baseFiles, IReadOnlyList<ChangeSeedCommit> commits, out string checkout)
    {
        checkout = Path.Combine(_base, "repo");
        _ = Directory.CreateDirectory(checkout);
        Git(checkout, "init", "-q", "-b", "main");
        Commit(checkout, "Start", baseFiles);
        Git(checkout, "checkout", "-q", "-b", "feat/expiry");
        foreach (var commit in commits)
        {
            Commit(checkout, commit.Message, commit.Files);
        }

        return new GitChangeSource();
    }

    private static void Commit(string repo, string message, IReadOnlyDictionary<string, string> files)
    {
        foreach (var (path, text) in files)
        {
            var full = Path.Combine(repo, path);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);   // why: every path has a directory part under the repo
            File.WriteAllText(full, text);
        }

        Git(repo, "add", "-A");
        Git(repo, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-q", "-m", message);
    }

    private static void Git(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(repo);
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        _ = git.StandardOutput.ReadToEnd();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();
        Assert.True(git.ExitCode == 0, $"git {string.Join(' ', args)} failed: {error}");
    }
}
