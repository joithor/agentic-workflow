using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class FakeSingleInstanceContractTests : SingleInstanceContract
{
    private readonly FakeTimeProvider _clock = new();
    private readonly FakeInstanceChannel _channel;

    public FakeSingleInstanceContractTests()
    {
        _channel = new FakeInstanceChannel(_clock);
    }

    protected override ISingleInstance Launch() => new FakeSingleInstance(_channel);

    protected override void Elapse(TimeSpan by) => _clock.Advance(by);
}
