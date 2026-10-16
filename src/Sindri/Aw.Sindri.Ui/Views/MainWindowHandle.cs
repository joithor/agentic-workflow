using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Aw.Sindri.Ui.Views;

/// <summary>The app's main window's native handle, for the head's adapters (the Windows taskbar badge). The head holds no Avalonia application types (ARCH-UI-001), so the lookup lives here.</summary>
public static class MainWindowHandle
{
    /// <summary>The handle once the main window exists.</summary>
    /// <returns>The native handle; 0 before the window exists or when it has none.</returns>
    public static nint Current() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.TryGetPlatformHandle()?.Handle ?? 0;
}
