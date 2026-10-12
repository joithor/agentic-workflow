using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeScrubberContractTests : ScrubberContract<FakeScrubber>
{
    protected override string Secret => FakeScrubber.Secret;

    protected override FakeScrubber Create() => new();
}
