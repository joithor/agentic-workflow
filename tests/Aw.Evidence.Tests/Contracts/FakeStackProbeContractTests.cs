using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeStackProbeContractTests : StackProbeContract
{
    protected override Task<StackUnderTest> ArrangeAsync(StackState state)
    {
        var probe = new FakeStackProbe { Title = state.LoginTitle, DatabaseReady = state.DatabaseReady };
        probe.OpenPorts.Clear();
        if (state.PortOpen)
        {
            _ = probe.OpenPorts.Add("localhost:5002");
        }

        return Task.FromResult(new StackUnderTest(probe, new Uri("http://localhost:3000/login"), "localhost", 5002));
    }
}
