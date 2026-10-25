using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "unit")]
public sealed class FakeMapPublishStoreContractTests : MapPublishStoreContract
{
    protected override IGraphStore CreateStore() => new FakeGraphStore();
}
