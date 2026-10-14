using Aw.Context.Core.Trust;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeScrubberFactoryContractTests : ScrubberFactoryContract<IScrubberFactory>
{
    protected override IScrubberFactory Create() => new FakeScrubberFactory();
}
