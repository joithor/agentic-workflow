using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>One group of sessions: its header collapses the rows or shows them again.</summary>
internal sealed partial class SessionGroup : UserControl
{
    /// <summary>The header was pressed: collapse the rows or show them again.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ToggleEvent = RoutedEvent.Register<SessionGroup, RoutedEventArgs>(nameof(ToggleEvent), RoutingStrategies.Bubble);

    public SessionGroup()
    {
        InitializeComponent();
    }

    /// <summary>The group's view model; null before it has a data context.</summary>
    internal SessionGroupViewModel? Group => DataContext as SessionGroupViewModel;

    private void OnToggle(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ToggleEvent, this));
}
