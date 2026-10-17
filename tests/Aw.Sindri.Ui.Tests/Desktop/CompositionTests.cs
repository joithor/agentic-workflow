using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The head's composition hands the app a dock badge for this OS.</summary>
[Lane(TestLane.Integration)]
public sealed class CompositionTests
{
    [Fact]
    public void Composition_builds_with_a_dock_badge()
    {
        var home = Path.Combine(Path.GetTempPath(), "aw-composition-" + RealTime.NewGuid().ToString("N"));

        var platform = Composition.Platform(static _ => null, home, new FakeTimeProvider(A.Noon));

        Assert.NotNull(platform.DockBadge);
        Assert.Equal(0, platform.DockBadge.Shown);
        Assert.Equal(OperatingSystem.IsMacOS() || OperatingSystem.IsWindows(), platform.DockBadge is not NullDockBadge);
    }
}
