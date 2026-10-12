using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Item;

namespace Aw.Sindri.Ui.Views.Item;

/// <summary>The item detail inside the shell's sheet; Load more asks for the timeline's next page.</summary>
internal sealed partial class ItemSheet : UserControl
{
    public ItemSheet()
    {
        InitializeComponent();
    }

    private void OnLoadMore(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ItemSheetViewModel sheet)
        {
            _ = sheet.LoadMore(CancellationToken.None);
        }
    }
}
