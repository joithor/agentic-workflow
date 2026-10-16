using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Text;

[Lane(TestLane.Unit)]
public sealed class RedactingScrubberContractTests : TextScrubberContract
{
    protected override ITextScrubber Create() => new RedactingScrubber(Redactor.Default);
}
