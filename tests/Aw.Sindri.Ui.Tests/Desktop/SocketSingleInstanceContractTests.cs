using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Integration)]
public sealed class SocketSingleInstanceContractTests : SingleInstanceContract, IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;
    private readonly FakeTimeProvider _clock = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    protected override ISingleInstance Launch() => new SocketSingleInstance(UiStatePaths.In(Path.Combine(_dir, "ui")), _clock);

    protected override void Elapse(TimeSpan by) => _clock.Advance(by);
}
