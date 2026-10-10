using Aw.Kernel.Testing;
using Aw.Kernel.Telemetry.Testing;

namespace Aw.Kernel.Telemetry.Tests;

[Lane(TestLane.Unit)]
public sealed class InMemoryLogSinkContractTests : LogSinkContract
{
    protected override ILogSink CreateSink() => new InMemoryLogSink();

    protected override string Delivered(ILogSink sink) => ((InMemoryLogSink)sink).Text;
}
