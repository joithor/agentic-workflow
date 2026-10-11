using Aw.Evidence.Testing;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class SequentialIdsContractTests : IdGeneratorContract
{
    protected override IIdGenerator Create() => new SequentialIds();
}
