using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Views.Instance;

/// <summary>The children panel: "Open all in Work" asks the frame to list every child of a group in Work.</summary>
internal sealed partial class ChildrenPanel : UserControl
{
    public ChildrenPanel()
    {
        InitializeComponent();
    }

    private void OnOpenAll(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ChildGroupViewModel group })
        {
            group.OpenAllInWork();
        }
    }
}
