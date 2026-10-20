using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ports;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetSetup;

[Lane(TestLane.Unit)]
public sealed class InMemorySetupFactsContractTests : SetupFactsContract
{
    protected override ISetupFacts Create() => new InMemorySetupFacts();
}
