using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class FakeTrayContractTests : TrayContract
{
    protected override ITray Create() => new FakeTray();

    protected override IReadOnlyList<string> MenuLabels(ITray tray) => ((FakeTray)tray).MenuLabels;

    protected override void Pick(ITray tray, int index) => ((FakeTray)tray).PickRow(index);
}
