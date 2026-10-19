using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>One ended session: focus arriving passes up to the page as a routed event.</summary>
internal sealed partial class EndedSessionRow : UserControl
{
    /// <summary>Focus arrived on the row or a control in it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SelectEvent = RoutedEvent.Register<EndedSessionRow, RoutedEventArgs>(nameof(SelectEvent), RoutingStrategies.Bubble);

    public EndedSessionRow()
    {
        InitializeComponent();
        AddHandler(GotFocusEvent, (_, _) => RaiseEvent(new RoutedEventArgs(SelectEvent, this)));
    }

    /// <summary>The row's key.</summary>
    internal string Key => (DataContext as EndedSessionViewModel)?.Key ?? string.Empty;

    private void OnCopy(object? sender, RoutedEventArgs e) => _ = (DataContext as EndedSessionViewModel)?.Copy(CancellationToken.None);
}
