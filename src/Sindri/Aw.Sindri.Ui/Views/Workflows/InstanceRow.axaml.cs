using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>One row of the Workflows list: it passes a title click up to the page as a routed event.</summary>
internal sealed partial class InstanceRow : UserControl
{
    /// <summary>A title click: open the instance.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenEvent = RoutedEvent.Register<InstanceRow, RoutedEventArgs>(nameof(OpenEvent), RoutingStrategies.Bubble);

    public InstanceRow()
    {
        InitializeComponent();
    }

    /// <summary>The row's instance address.</summary>
    internal string Address => (DataContext as InstanceRowViewModel)?.Address ?? string.Empty;

    private void OnOpen(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(OpenEvent, this));
}
