using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.Views.Queue;

/// <summary>One item row; a click on its title asks the page to open the item's sheet.</summary>
internal sealed partial class ItemRow : UserControl
{
    /// <summary>Raised when the row asks to open its item's sheet (a title click).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent =
        RoutedEvent.Register<ItemRow, RoutedEventArgs>(nameof(Open), RoutingStrategies.Bubble);

    public ItemRow()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the row asks to open its item's sheet.</summary>
    public event EventHandler<RoutedEventArgs> Open
    {
        add => AddHandler(OpenEvent, value);
        remove => RemoveHandler(OpenEvent, value);
    }

    /// <summary>The item id of the row a routed event came from (a table or compact row), or empty.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The id, or empty.</returns>
    internal static string ItemIdOf(RoutedEventArgs e) => ((e.Source as Control)?.DataContext as ItemRowViewModel)?.Id ?? string.Empty;

    /// <summary>The row's item id, or empty.</summary>
    internal string ItemId => (DataContext as ItemRowViewModel)?.Id ?? string.Empty;

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
