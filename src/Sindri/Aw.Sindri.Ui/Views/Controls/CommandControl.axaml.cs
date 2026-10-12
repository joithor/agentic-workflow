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
            _ = control.Run(CancellationToken.None);
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
