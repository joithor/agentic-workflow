using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Views.Instance;

#pragma warning disable MA0182 // created by the children panel's data templates, which the analyzer does not read
/// <summary>One child of the children panel: its title opens the child's frame, Open opens its item's sheet.</summary>
internal sealed partial class ChildRowView : UserControl
{
    public ChildRowView()
    {
        InitializeComponent();
    }

    private void OnOpenFrame(object? sender, RoutedEventArgs e) => (DataContext as ChildRowViewModel)?.OpenFrame();

    private void OnOpenItem(object? sender, RoutedEventArgs e) => (DataContext as ChildRowViewModel)?.OpenItem();
}
#pragma warning restore MA0182
