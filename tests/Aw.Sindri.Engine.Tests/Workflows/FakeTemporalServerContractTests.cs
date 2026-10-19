using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflows;

[Lane(TestLane.Unit)]
public sealed class FakeTemporalServerContractTests : TemporalServerContract
{
    protected override ITemporalServer Create() => new FakeTemporalServer();
}
