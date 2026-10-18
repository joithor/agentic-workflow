using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Views.Facts;

/// <summary>A scope-map fact's viewer: a table, with a toggle to the raw tree.</summary>
internal sealed partial class ScopeMapFactView : UserControl
{
    public ScopeMapFactView()
    {
        InitializeComponent();
    }

    private void OnToggle(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ScopeMapViewer viewer)
        {
            viewer.ToggleRaw();
        }
    }
}
