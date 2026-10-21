using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Views.Setup;

/// <summary>A step's action: the button only calls the view model, which asks the shell to open the action.</summary>
internal sealed partial class SetupActionView : UserControl
{
    public SetupActionView()
    {
        InitializeComponent();
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => (DataContext as SetupActionViewModel)?.Open();
}
