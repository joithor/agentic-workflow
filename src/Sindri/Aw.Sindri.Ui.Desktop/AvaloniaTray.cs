using Avalonia.Controls;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The desktop adapter of <see cref="ITray"/>: Avalonia's TrayIcon (on GNOME it needs the AppIndicator extension, UI spec
/// §18.2). It is created on the first <see cref="Show"/>, on the UI thread, and removed when Quit is picked, before the
/// app hears it, so no icon outlives the app. Menu labels escape '_' (an access-key marker in native menus); the tooltip
/// is shown as given.
/// </summary>
internal sealed class AvaloniaTray : ITray
{
    private NativeMenuItem? _open;
    private NativeMenuItem? _quit;

    public event EventHandler? OpenRequested;

    public event EventHandler? QuitRequested;

    public TrayView? Shown { get; private set; }

    /// <summary>The icon, once shown.</summary>
    internal TrayIcon? Icon { get; private set; }

    /// <summary>The icon's menu (Open Sindri, a separator, Quit), once shown.</summary>
    internal NativeMenu? Menu { get; private set; }

    public void Show(TrayView view)
    {
        var icon = Icon ??= Build();
        icon.Icon = TrayMarks.For(view.State);
        icon.ToolTipText = view.Tooltip;
        _open!.Header = Escape(view.OpenLabel); // why: Build sets both items before the icon exists
        _quit!.Header = Escape(view.QuitLabel); // why: as above
        icon.IsVisible = true;
        Shown = view;
    }

    /// <summary>A label as a native menu shows it literally: each '_' doubled.</summary>
    /// <param name="label">The app's label.</param>
    /// <returns>The escaped label.</returns>
    internal static string Escape(string label) => label.Replace("_", "__", StringComparison.Ordinal);

    private TrayIcon Build()
    {
        _open = new NativeMenuItem { Command = new ActionCommand(() => OpenRequested?.Invoke(this, EventArgs.Empty)) };
        _quit = new NativeMenuItem { Command = new ActionCommand(Quit) };
        Menu = [_open, new NativeMenuItemSeparator(), _quit];
        var icon = new TrayIcon { Menu = Menu };
        icon.Clicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        MacOSProperties.SetIsTemplateIcon(icon, value: true);
        return icon;
    }

    private void Quit()
    {
        Icon?.Dispose();
        Icon = null;
        QuitRequested?.Invoke(this, EventArgs.Empty);
    }
}
