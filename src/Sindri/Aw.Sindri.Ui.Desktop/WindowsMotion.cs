using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>Windows' reduce-motion setting: SystemParametersInfo(SPI_GETCLIENTAREAANIMATION), off meaning reduce.</summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsMotion
{
    private const uint GetClientAreaAnimation = 0x1042;

    /// <summary>Reads the setting.</summary>
    /// <returns>True when client-area animations are off; false when they are on or the call fails.</returns>
    internal static bool Read() => SystemParametersInfo(GetClientAreaAnimation, 0, out var animate, 0) && animate == 0;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint parameter, out int value, uint update);
}
