using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeContextEngineContractTests : ContextEngineContract<FakeContextEngine>
{
    protected override FakeContextEngine CreateDown() => FakeContextEngine.Instance;
}
