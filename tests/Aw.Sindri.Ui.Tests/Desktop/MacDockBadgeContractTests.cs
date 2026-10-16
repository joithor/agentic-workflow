using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The macOS dock tile: skipped everywhere else, since NSApplication's dock tile exists only there.</summary>
[Lane(TestLane.Integration)]
public sealed class MacDockBadgeContractTests : DockBadgeContract
{
    protected override IDockBadge Create()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new MacDockBadge();
        }

        Assert.Skip("the dock tile is macOS only");
        return new NullDockBadge(); // why: Skip throws; this keeps the macOS-only constructor behind its guard
    }
}
