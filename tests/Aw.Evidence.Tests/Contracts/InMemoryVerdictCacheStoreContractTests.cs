using Aw.Evidence.Core.Visual;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class InMemoryVerdictCacheStoreContractTests : VerdictCacheStoreContract
{
    protected override IVerdictCacheStore Create() => new InMemoryVerdictCacheStore();

    protected override string Root => "/runs";
}
