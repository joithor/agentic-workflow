using Avalonia.Controls;
using Avalonia.Input;
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

    /// <summary>Puts focus back on the control's button (after its question is answered or dismissed), on the keyboard's ring.</summary>
    internal bool FocusRun() => this.FindControl<Button>("RunButton")?.Focus(NavigationMethod.Tab) == true;

    private void OnRun(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ControlViewModel control)
        {
            _ = control.Press(CancellationToken.None);
            if (control.IsConfirming)
            {
                // The question replaces nothing: focus moves to Keep, the safe answer (Esc keeps too).
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = this.FindControl<Button>("KeepButton")?.Focus(NavigationMethod.Tab), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ControlViewModel control)
        {
            _ = control.Confirm(CancellationToken.None);
            _ = FocusRun();
        }
    }

    private void OnKeep(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ControlViewModel control)
        {
            control.Keep();
            _ = FocusRun();
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
