using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The real ancestry (proc_pidinfo on macOS, /proc on Linux): this test process up to pid 1; a child that exited is the gone pid.</summary>
[Lane(TestLane.Integration)]
public sealed class PosixProcessAncestryContractTests : ProcessAncestryContract
{
    protected override IProcessAncestry Create() => new PosixProcessAncestry();

    protected override async ValueTask<int> GonePid()
    {
        using var child = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", "exit 0"]) { UseShellExecute = false })!;   // why: Start returns a process for a file that exists
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await child.WaitForExitAsync(deadline.Token);
        return child.Id;
    }
}
