using Avalonia.Controls;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop adapter of <see cref="ITray"/>: Avalonia's TrayIcon, created on the first <see cref="Show"/> (on the UI thread).</summary>
internal sealed class AvaloniaTray : ITray
{
    public TrayView? Shown => throw new NotSupportedException();

    /// <summary>The icon, once shown.</summary>
    internal TrayIcon? Icon { get; }

    public event EventHandler? OpenRequested
    {
        add => throw new NotSupportedException();
        remove => throw new NotSupportedException();
    }

    public event EventHandler? QuitRequested
    {
        add => throw new NotSupportedException();
        remove => throw new NotSupportedException();
    }

    public void Show(TrayView view) => throw new NotSupportedException($"{TrayMarks.Draw(view.State)}");
}
