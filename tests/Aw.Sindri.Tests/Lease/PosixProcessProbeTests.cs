using System.Diagnostics;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure.Lease;

namespace Aw.Sindri.Tests.Lease;

public sealed class PosixProcessProbeTests
{
    private readonly PosixProcessProbe _probe = new();

    [Fact, Trait("Lane", "integration")]
    public void This_process_is_alive_with_its_own_start_time()
    {
        var self = _probe.Self();

        Assert.Equal(Environment.ProcessId, self.Pid);
        Assert.Equal(HolderLiveness.Alive, LeaseDecision.Liveness(self, self, _probe.Facts(self.Pid)));
        Assert.False(string.IsNullOrWhiteSpace(self.BootId.Value));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_exited_child_is_proven_dead()
    {
        using var child = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", "exit 0"]) { UseShellExecute = false })!;   // why: Start returns a process for a file that exists
        var startedChild = _probe.Facts(child.Id);
        await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        var holder = _probe.Self() with { Pid = child.Id, PidStart = startedChild.StartedAt ?? _probe.Self().PidStart };

        Assert.Equal(HolderLiveness.ProvenDead, LeaseDecision.Liveness(holder, _probe.Self(), _probe.Facts(child.Id)));
    }
}
