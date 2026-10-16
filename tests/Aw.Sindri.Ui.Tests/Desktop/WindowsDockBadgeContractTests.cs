using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The Windows taskbar overlay: skipped everywhere else; with no window handle it still counts, drawing nothing.</summary>
[Lane(TestLane.Integration)]
public sealed class WindowsDockBadgeContractTests : DockBadgeContract
{
    protected override IDockBadge Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsDockBadge(static () => 0);
        }

        Assert.Skip("the taskbar overlay is Windows only");
        return new NullDockBadge(); // why: Skip throws; this keeps the Windows-only constructor behind its guard
    }
}
