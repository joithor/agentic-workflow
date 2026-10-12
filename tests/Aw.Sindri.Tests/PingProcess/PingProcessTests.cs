using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.PingProcess;

namespace Aw.Sindri.Tests.PingProcess;

public sealed class PingProcessTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private PingProcessService Service() => new(_h.Held, _h.Probe);

    [Fact, Trait("Lane", "unit")]
    public async Task PingProcess_answers_the_pid_and_the_held_epoch()
    {
        var grant = await _h.HoldLease();

        var ping = (await Service().Handle(new PingProcessRequest(), Token)).Value;

        Assert.Equal(new Ping(grant.Holder.Pid, grant.Epoch), ping);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task PingProcess_refuses_with_SND_API_001_while_no_lease_is_held()
    {
        var ping = await Service().Handle(new PingProcessRequest(), Token);

        Assert.True(ping.IsError);   // why: a process without the lease (stopping, or not started) isn't the control plane
        Assert.Equal(SindriErrors.NotRunning.Code, ping.Error.Code);
    }
}
