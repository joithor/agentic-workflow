using Aw.Context.Core.Trust;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeScrubberContractTests : ScrubberContract
{
    protected override string Secret => FakeScrubber.Secret;

    protected override (IEgressScrubber Egress, IIngestFilter Ingest) Create()
    {
        var fake = new FakeScrubber();
        return (fake, fake);
    }
}
