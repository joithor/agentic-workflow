using Aw.Evidence.Core.Data;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeTextScrubberContractTests : TextScrubberContract
{
    protected override ITextScrubber Create() => new FakeTextScrubber().Hides(Secret);
}
