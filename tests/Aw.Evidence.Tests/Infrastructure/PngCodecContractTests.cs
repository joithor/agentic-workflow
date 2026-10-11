using Aw.Evidence.Core.Images;
using Aw.Evidence.Infrastructure.Images;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Unit)]
public sealed class PngCodecContractTests : ImageCodecContract
{
    protected override IImageCodec Create() => new PngCodec();
}
