using Aw.Context.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "integration")]
public sealed class FakeDocIndexContractTests : DocIndexContract<FakeDocIndex>
{
    protected override FakeDocIndex Create() => new(new FakeTimeProvider());
}
