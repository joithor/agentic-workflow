using Avalonia.Controls.ApplicationLifetimes;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Views;

/// <summary>The platform wiring (UI spec §5, §15): close-to-tray, the tray's state and the single instance's messages.</summary>
public sealed partial class SindriApp
{
    partial void ShellOpened(IClassicDesktopStyleApplicationLifetime desktop, ShellWindow window, ShellViewModel shell)
    {
        if (Platform is not { } platform || _preferences is not { } preferences || _clock is not { } clock)
        {
            return;
        }

#pragma warning disable CA2000 // disposed by the desktop's Exit handler below
        var lifetime = new AppLifetime(window, platform.Tray, shell.Navigate, () => End(desktop, 0), () => Settle(window, preferences), clock, platform.DockBadge);
#pragma warning restore CA2000
        lifetime.Quitting += (_, _) => platform.Instance.Retire();
        lifetime.Start(desktop, shell);
        _listener?.Deliver(lifetime.Receive);
        desktop.Exit += (_, _) => lifetime.Dispose();
    }

    // Before the app ends: the window's bounds go into the preferences while the window is still open, and the writes
    // still queued (Settings' too) are what the app waits for.
    private static Task Settle(ShellWindow window, Preferences preferences)
    {
        window.RememberBounds();
        return preferences.Flush();
    }
}
