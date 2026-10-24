using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>A tray that shows nothing: capture mode runs on a display with no tray (a virtual X server) and must not create one.</summary>
internal sealed class NullTray : ITray
{
    public event EventHandler? OpenRequested
    {
        add { }
        remove { }
    }

    public event EventHandler<TrayRowEventArgs>? RowRequested
    {
        add { }
        remove { }
    }

    public event EventHandler? QuitRequested
    {
        add { }
        remove { }
    }

    public TrayView? Shown { get; private set; }

    public void Show(TrayView view) => Shown = view;
}
