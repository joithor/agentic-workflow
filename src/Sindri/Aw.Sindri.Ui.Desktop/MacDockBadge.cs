using System.Runtime.Versioning;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>macOS's dock badge: [[NSApplication sharedApplication] dockTile] setBadgeLabel:, through the Objective-C runtime.</summary>
[SupportedOSPlatform("macos")]
internal sealed class MacDockBadge : IDockBadge
{
    public int Shown => 0;

    public void Show(int count) => throw new NotSupportedException();
}
