using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeModelPortContractTests : ModelPortContract<FakeModelPort>
{
    protected override FakeModelPort Create() => new();
}
