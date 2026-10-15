using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryMetaConfigContractTests : MetaConfigContract
{
    protected override IMetaConfig Create() => new InMemoryMetaConfig();
}
