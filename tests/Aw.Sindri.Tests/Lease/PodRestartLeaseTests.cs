using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure.Lease;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Lease;

/// <summary>
/// A restarted pod keeps its host name (a StatefulSet pod's stable name), so the lease needs no new host setting
/// (deployable-context spec A2b): the new process takes it at once when the old holder is provably dead.
/// </summary>
public sealed class PodRestartLeaseTests
{
    private static readonly DateTimeOffset _now = KnownInstants.Epoch;

    [Fact, Trait("Lane", "integration")]
    public void The_lease_records_the_machine_name_as_the_host()
        => Assert.Equal(Environment.MachineName, new PosixProcessProbe().Self().Host.Value);

    [Fact, Trait("Lane", "unit")]
    public void A_restart_with_the_same_host_and_boot_takes_the_unexpired_lease_at_once_when_the_old_pid_is_gone()
    {
        var old = new LeaseHolder(HostId.From("sindri-0"), 7, _now.AddMinutes(-5), BootId.From("node-boot"));
        var restarted = new LeaseHolder(HostId.From("sindri-0"), 7, _now, BootId.From("node-boot"));
        var row = new LeaseRow(old, 3, _now.AddMinutes(-5), _now.AddSeconds(40), Released: false);

        // the pid repeats in the new container but started later: a different process
        var liveness = LeaseDecision.Liveness(old, restarted, new ProcessFacts(Running: true, StartedAt: restarted.PidStart));
        var grant = LeaseDecision.Acquire(row, restarted, _now, liveness);

        Assert.Equal(HolderLiveness.ProvenDead, liveness);
        Assert.Equal(4, grant.Value.Epoch);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_restart_on_another_node_with_the_same_host_name_is_proven_dead_by_the_boot_id()
    {
        var old = new LeaseHolder(HostId.From("sindri-0"), 7, _now.AddMinutes(-5), BootId.From("node-a"));
        var restarted = new LeaseHolder(HostId.From("sindri-0"), 7, _now, BootId.From("node-b"));

        Assert.Equal(HolderLiveness.ProvenDead, LeaseDecision.Liveness(old, restarted, new ProcessFacts(Running: true, StartedAt: _now.AddMinutes(-5))));
    }
}
