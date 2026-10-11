using Aw.Evidence.Core.Images;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeImageCodecContractTests : ImageCodecContract
{
    protected override IImageCodec Create() => new FakeImageCodec();
}
