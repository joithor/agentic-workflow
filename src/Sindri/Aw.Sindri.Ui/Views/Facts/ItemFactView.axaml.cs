using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Views.Facts;

/// <summary>An item fact's viewer: Open asks the frame to open the item.</summary>
internal sealed partial class ItemFactView : UserControl
{
    public ItemFactView()
    {
        InitializeComponent();
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => (DataContext as ItemFactViewer)?.Open();
}
