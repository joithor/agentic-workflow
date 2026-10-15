using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The running app's tray icons (Avalonia's <see cref="TrayIcon.IconsProperty"/> on the Application). Avalonia creates a
/// TrayIcon's native icon only when it is added to that list, and removes it when it leaves the list or the app shuts
/// down, so a head's tray adapter registers its icon here: a head references no Application itself (ARCH-UI-001).
/// Called on the UI thread once the app has started.
/// </summary>
public static class AppTrayIcons
{
    /// <summary>Adds an icon to the app's tray icons (once), which shows it on the platform.</summary>
    /// <param name="icon">The icon, with its image, tooltip and menu already set.</param>
    public static void Add(TrayIcon icon)
    {
        var app = Application.Current ?? throw new InvalidOperationException("The app hasn't started.");
        var icons = TrayIcon.GetIcons(app);
        if (icons is null)
        {
            icons = [];
            TrayIcon.SetIcons(app, icons);
        }

        if (!icons.Contains(icon))
        {
            icons.Add(icon);
        }
    }

    /// <summary>Removes an icon from the app's tray icons, which removes it from the platform.</summary>
    /// <param name="icon">The icon.</param>
    public static void Remove(TrayIcon icon)
    {
        if (Application.Current is { } app)
        {
            _ = TrayIcon.GetIcons(app)?.Remove(icon);
        }
    }
}
