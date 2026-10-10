using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class GitRepoSnapshotContractTests : RepoSnapshotContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override string NotARepository
    {
        get
        {
            var plain = Path.Combine(_root.Path, "plain");
            _ = Directory.CreateDirectory(plain);
            return plain;
        }
    }

    protected override (IRepoSnapshot Snapshot, string Repo, string Sha) Create()
    {
        var repo = _root.Write("repo", CommitFiles.ToDictionary(static f => f, static f => "x\n"u8.ToArray(), StringComparer.Ordinal));
        _ = Git(repo, "init", "-q");
        _ = Git(repo, "add", "-A");
        _ = Git(repo, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "c");
        return (new GitRepoSnapshot(TimeSpan.FromSeconds(30)), Path.Combine(repo, "src"), Git(repo, "rev-parse", "HEAD").Trim());
    }

    public void Dispose() => _root.Dispose();

    internal static string Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = directory };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)}: {process.StandardError.ReadToEnd()}");
        return output;
    }
}
