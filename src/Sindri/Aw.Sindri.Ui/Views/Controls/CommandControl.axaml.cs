using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>A catalog row's control (UI spec §7): the view over <see cref="ControlViewModel"/>; a click only calls it.</summary>
internal sealed partial class CommandControl : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public CommandControl()
    {
        InitializeComponent();
    }

    private void OnRun(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ControlViewModel control)
        {
            _ = control.Press(CancellationToken.None);
            if (control.IsConfirming)
            {
                // The question replaces nothing: focus moves to Keep, the safe answer (Esc keeps too).
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = this.FindControl<Button>("KeepButton")?.Focus(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ControlViewModel control)
        {
            _ = control.Confirm(CancellationToken.None);
            _ = this.FindControl<Button>("RunButton")?.Focus();
        }
    }

    private void OnKeep(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ControlViewModel control)
        {
            control.Keep();
            _ = this.FindControl<Button>("RunButton")?.Focus();
        }
    }

    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ControlViewModel control)
        {
            _ = control.Copy(CancellationToken.None);
        }
    }
}
