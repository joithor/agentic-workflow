using Aw.Evidence.Infrastructure.Ids;
using Aw.Evidence.Testing;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Unit)]
public sealed class RandomIdsContractTests : IdGeneratorContract
{
    protected override IIdGenerator Create() => new RandomIds();
}
