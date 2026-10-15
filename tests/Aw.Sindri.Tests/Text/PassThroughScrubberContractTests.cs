using Aw.Kernel.Testing;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Text;

[Lane(TestLane.Unit)]
public sealed class PassThroughScrubberContractTests : TextScrubberContract
{
    protected override void SkipUnlessItRecognisesSecrets() =>
        Assert.Skip("the pass-through scrubber recognises no secret; the Aw.Redaction adapter's binding runs this case");

    protected override ITextScrubber Create() => new PassThroughScrubber();
}
