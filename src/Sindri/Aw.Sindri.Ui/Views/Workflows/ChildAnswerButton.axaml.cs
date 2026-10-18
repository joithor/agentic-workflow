using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The "Answer in the child" button of a row: pressed, it passes <see cref="FollowEvent"/> up to the page, which selects the row, follows the pointer and keeps the row as the opener (Esc returns to it).</summary>
internal sealed partial class ChildAnswerButton : UserControl
{
    /// <summary>The button was pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> FollowEvent = RoutedEvent.Register<ChildAnswerButton, RoutedEventArgs>(nameof(FollowEvent), RoutingStrategies.Bubble);

    public ChildAnswerButton()
    {
        InitializeComponent();
    }

    /// <summary>The pointer this button follows.</summary>
    internal ChildAnswerViewModel? Answer => DataContext as ChildAnswerViewModel;

    private void OnFollow(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(FollowEvent, this));
}
