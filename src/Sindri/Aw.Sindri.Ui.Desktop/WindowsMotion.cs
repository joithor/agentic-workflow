using System.Runtime.Versioning;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>Windows' reduce-motion setting: SystemParametersInfo(SPI_GETCLIENTAREAANIMATION), off meaning reduce.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsMotion
{
    /// <summary>Reads the setting.</summary>
    /// <returns>True when client-area animations are off.</returns>
    internal static bool Read() => throw new NotSupportedException();
}
