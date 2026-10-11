using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class FakeSingleInstanceContractTests : SingleInstanceContract
{
    private readonly FakeInstanceChannel _channel = new();

    protected override ISingleInstance Launch() => new FakeSingleInstance(_channel);
}
