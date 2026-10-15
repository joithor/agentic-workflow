using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

[Lane(TestLane.Unit)]
public sealed class FakeSindriApiContractTests : SindriApiContract
{
    private protected override ISindriApi Reach(FakeSindriApi server) => server;
}
