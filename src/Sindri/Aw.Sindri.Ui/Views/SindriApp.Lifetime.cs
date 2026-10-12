using Avalonia.Controls.ApplicationLifetimes;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Views;

/// <summary>The platform wiring (UI spec §5, §15): close-to-tray, the tray's state and the single instance's messages.</summary>
public sealed partial class SindriApp
{
    partial void ShellOpened(IClassicDesktopStyleApplicationLifetime desktop, ShellWindow window, ShellViewModel shell)
    {
        if (Platform is not { } platform)
        {
            return;
        }

        var lifetime = AppLifetime.Start(desktop, window, platform, shell, () => End(desktop, 0));
        desktop.Exit += (_, _) => lifetime.Dispose();
    }
}
