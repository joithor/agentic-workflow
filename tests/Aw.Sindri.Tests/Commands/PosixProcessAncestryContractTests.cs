using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The real ancestry (proc_pidinfo on macOS, /proc on Linux): this test process up to pid 1; a child that exited is the gone pid.</summary>
[Lane(TestLane.Integration)]
public sealed class PosixProcessAncestryContractTests : ProcessAncestryContract
{
    protected override IProcessAncestry Create() => new PosixProcessAncestry();

    /// <summary>
    /// A parent is named as it was invoked (argv[0], as ps shows it), not by its executable's file: Claude Code runs as
    /// '…/claude/versions/2.1.296', invoked through a 'claude' link. Here a shell runs through a link named 'aw-fake-harness'.
    /// The executable path is the file the OS runs, the link's target (engine spec §6.2: the probe records both).
    /// </summary>
    [Fact(Timeout = 20_000)]
    public async Task A_parent_is_named_as_it_was_invoked_not_by_its_executable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var temp = new TempHome();
        var link = Path.Combine(temp.Path, "aw-fake-harness");
        _ = File.CreateSymbolicLink(link, "/bin/sh");
        using var harness = Process.Start(new ProcessStartInfo(link, ["-c", "sleep 30 & echo $!; wait"]) { UseShellExecute = false, RedirectStandardOutput = true })!;   // why: Start returns a process for a file that exists
        try
        {
            var child = int.Parse((await harness.StandardOutput.ReadLineAsync(ct))!, CultureInfo.InvariantCulture);   // why: the shell prints the child's pid first

            var parent = new PosixProcessAncestry().Ancestors(child, 1);

            var found = Assert.Single(parent);
            Assert.Equal((harness.Id, "aw-fake-harness"), (found.Pid, found.Name));
            var executable = Assert.IsType<string>(found.Executable);
            Assert.NotEqual(link, executable, StringComparer.Ordinal);   // never the link
            Assert.Equal(RealPath("/bin/sh"), RealPath(executable), StringComparer.Ordinal);   // the file the OS runs: /bin/sh itself (bash on macOS), or its target (/usr/bin/dash where /bin is a link to /usr/bin)
        }
        finally
        {
            harness.Kill(entireProcessTree: true);
            await harness.WaitForExitAsync(ct);
        }
    }

    /// <summary>The file a path names with its directory link and its own link followed ('/bin/sh' is '/usr/bin/dash' on usrmerge Linux).</summary>
    private static string RealPath(string path)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(path)!);
        var file = new FileInfo(Path.Combine((dir.ResolveLinkTarget(returnFinalTarget: true) ?? dir).FullName, Path.GetFileName(path)));
        return (file.ResolveLinkTarget(returnFinalTarget: true) ?? file).FullName;
    }

    protected override async ValueTask<int> GonePid()
    {
        using var child = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", "exit 0"]) { UseShellExecute = false })!;   // why: Start returns a process for a file that exists
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await child.WaitForExitAsync(deadline.Token);
        return child.Id;
    }
}
