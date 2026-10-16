using Avalonia.Controls;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The desktop adapter of <see cref="ITray"/>: Avalonia's TrayIcon (on GNOME it needs the AppIndicator extension, UI spec
/// §18.2). It is created on the first <see cref="Show"/>, on the UI thread, and registered with the app
/// (<see cref="AppTrayIcons"/>): Avalonia makes the native icon only for a TrayIcon in the app's list, never for one
/// that is merely created. It is removed when Quit is picked, before the app hears it, so no icon outlives the app. Menu labels escape '_' (an access-key marker in native menus); the tooltip
/// is shown as given.
/// </summary>
internal sealed class AvaloniaTray : ITray
{
    private NativeMenuItem? _open;
    private NativeMenuItem? _quit;
    private NativeMenuItem? _running;

    public event EventHandler? OpenRequested;

    public event EventHandler? QuitRequested;

    public event EventHandler<TrayRowEventArgs>? RowRequested;

    public TrayView? Shown { get; private set; }

    /// <summary>The icon, once shown.</summary>
    internal TrayIcon? Icon { get; private set; }

    /// <summary>The icon's menu (Open Sindri, a separator, Quit), once shown.</summary>
    internal NativeMenu? Menu { get; private set; }

    public void Show(TrayView view)
    {
        var icon = Icon ??= Build();
        icon.Icon = TrayMarks.For(view.State, view.Count);
        icon.ToolTipText = view.Tooltip;
        _open!.Header = Escape(view.OpenLabel); // why: Build sets both items before the icon exists
        _quit!.Header = Escape(view.QuitLabel); // why: as above
        Fill(view);
        MacOSProperties.SetIsTemplateIcon(icon, value: view.State != TrayState.Attention); // why: a template image is tinted by the menu bar, so the attention colour would be lost
        icon.IsVisible = true;

        // Registered once it is complete: Avalonia then makes the native icon with this image, tooltip and menu.
        AppTrayIcons.Add(icon);
        Shown = view;
    }

    /// <summary>A label as a native menu shows it literally: each '_' doubled.</summary>
    /// <param name="label">The app's label.</param>
    /// <returns>The escaped label.</returns>
    internal static string Escape(string label) => label.Replace("_", "__", StringComparison.Ordinal);

    // The menu: the needs-you rows (at most five), Open Sindri, the disabled Running item, Quit. Rows are rebuilt on each
    // show (their ids change); the other items persist. Picking a row only raises RowRequested: no action runs from the tray.
    private void Fill(TrayView view)
    {
        _running!.Header = Escape(view.RunningLabel); // why: Build sets it before the icon exists
        var items = new List<NativeMenuItemBase>();
        foreach (var row in view.MenuRows)
        {
            var id = row.Id;
            items.Add(new NativeMenuItem { Header = Escape(row.Label), Command = new ActionCommand(() => RowRequested?.Invoke(this, new TrayRowEventArgs(id))) });
        }

        if (items.Count > 0)
        {
            items.Add(new NativeMenuItemSeparator());
        }

        items.Add(_open!); // why: Build sets it before the icon exists
        if (view.RunningLabel.Length > 0)
        {
            items.Add(_running);
        }

        items.Add(new NativeMenuItemSeparator());
        items.Add(_quit!); // why: as above
        Menu!.Items.Clear(); // why: Build sets the menu before the icon exists
        foreach (var item in items)
        {
            Menu.Items.Add(item);
        }
    }

    private TrayIcon Build()
    {
        _open = new NativeMenuItem { Command = new ActionCommand(() => OpenRequested?.Invoke(this, EventArgs.Empty)) };
        _quit = new NativeMenuItem { Command = new ActionCommand(Quit) };
        _running = new NativeMenuItem { IsEnabled = false };
        Menu = [_open, new NativeMenuItemSeparator(), _quit];
        var icon = new TrayIcon { Menu = Menu };
        icon.Clicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        MacOSProperties.SetIsTemplateIcon(icon, value: true); // why: Show turns it off for the coloured Attention mark
        return icon;
    }

    private void Quit()
    {
        if (Icon is { } icon)
        {
            AppTrayIcons.Remove(icon);
            icon.Dispose();
            Icon = null;
        }

        QuitRequested?.Invoke(this, EventArgs.Empty);
    }
}
