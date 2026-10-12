using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Testing.Engine;
using Aw.Redaction;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "unit")]
public sealed class RedactionScrubberContractTests : ScrubberContract
{
    protected override string Secret => Planted.Secret;

    protected override (IEgressScrubber Egress, IIngestFilter Ingest) Create()
    {
        var scrubber = new RedactionScrubber(Redactor.Default);
        return (scrubber, scrubber);
    }
}
