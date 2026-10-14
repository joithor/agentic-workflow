using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FixedSourceCatalogContractTests : SourceCatalogContract<FixedSourceCatalog>
{
    protected override FixedSourceCatalog Create() => new(FakeContextSource.FixtureDocs());
}
