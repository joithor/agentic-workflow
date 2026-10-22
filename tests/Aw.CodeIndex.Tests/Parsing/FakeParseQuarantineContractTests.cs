using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Parsing;

[Trait("Lane", "unit")]
public sealed class FakeParseQuarantineContractTests : ParseQuarantineContract
{
    protected override IParseQuarantine Create() => new FakeParseQuarantine();

    protected override IParseQuarantine Restart(IParseQuarantine previous) => ((FakeParseQuarantine)previous).Restarted();
}
