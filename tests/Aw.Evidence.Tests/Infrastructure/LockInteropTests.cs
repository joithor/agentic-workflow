using System.Diagnostics;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The C# locks and config/lib/locks.sh exclude each other, both ways, on the same directory.</summary>
[Lane(TestLane.Integration)]
public sealed class LockInteropTests : IDisposable
{
    private readonly TempDirectory _root = new();

    private string Lock => LockTestEnvironment.Settings(_root).QaStack;

    [Fact]
    public async Task A_lock_bash_holds_is_busy_for_csharp_until_bash_releases_it()
    {
        Assert.Equal(0, await Bash("acquire_lock \"$1\" 0"));

        var busy = await LockTestEnvironment.Locks(_root).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);

        Assert.Equal(0, await Bash("release_lock \"$1\""));
        await using var taken = (await LockTestEnvironment.Locks(_root).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;
        Assert.Equal([LockKind.QaStack], taken.Held);
    }

    [Fact]
    public async Task A_lock_csharp_holds_makes_bash_time_out_until_csharp_releases_it()
    {
        var lease = (await LockTestEnvironment.Locks(_root).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(1, await Bash("acquire_lock \"$1\" 1"));

        await lease.DisposeAsync();
        Assert.False(Directory.Exists(Lock), "the C# release leaves no directory");
        Assert.Equal(0, await Bash("acquire_lock \"$1\" 0 && release_lock \"$1\""));
        Assert.False(Directory.Exists(Lock), "the bash release leaves no directory");
    }

    public void Dispose() => _root.Dispose();

    private async Task<int> Bash(string body)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(Lock)!);   // why: the lock path always has a parent
        var start = new ProcessStartInfo("bash")
        {
            ArgumentList = { "-c", "source \"$0\"; " + body, RepoFile("config/lib/locks.sh"), Lock },
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var bash = Process.Start(start)!;   // why: Process.Start returns null only for shell-executed documents
        _ = await bash.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await bash.WaitForExitAsync(TestContext.Current.CancellationToken);
        return bash.ExitCode;
    }

    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(relative);
    }
}
