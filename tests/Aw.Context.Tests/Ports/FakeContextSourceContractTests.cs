using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeContextSourceContractTests : ContextSourceContract<FakeContextSource>
{
    protected override Task<FakeContextSource> Create() => Task.FromResult(FakeContextSource.FixtureDocs());
}
