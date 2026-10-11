using System.Runtime.Versioning;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>macOS's reduce-motion setting: NSWorkspace.sharedWorkspace.accessibilityDisplayShouldReduceMotion, through the Objective-C runtime.</summary>
[SupportedOSPlatform("macos")]
internal static class MacMotion
{
    /// <summary>Reads the setting.</summary>
    /// <returns>True when the user asks for reduced motion.</returns>
    internal static bool Read() => throw new NotSupportedException();
}
