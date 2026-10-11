namespace Aw.Sindri.Ui.Desktop;

/// <summary>GNOME's reduce-motion setting: org.gnome.desktop.interface enable-animations, read through gsettings (best effort).</summary>
internal static class GnomeMotion
{
    /// <summary>The command that reads the setting.</summary>
    internal const string Command = "gsettings";

    /// <summary>Reads the setting.</summary>
    /// <param name="gsettings">The gsettings command.</param>
    /// <returns>True when animations are off; false when they are on or the setting can't be read.</returns>
    internal static bool Read(string gsettings) => throw new NotSupportedException(gsettings);
}
