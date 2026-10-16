using Aw.Evidence.Core.Data;
using Aw.Evidence.Infrastructure.Redaction;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;
using Aw.Redaction;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Unit)]
public sealed class RedactionScrubberContractTests : TextScrubberContract
{
    protected override ITextScrubber Create() => new RedactionScrubber(Redactor.Default);
}
