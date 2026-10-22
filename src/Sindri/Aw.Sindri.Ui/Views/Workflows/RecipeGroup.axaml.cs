using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>One workflow's group: its header passes a click up to the page as a routed event, and the page collapses or expands it.</summary>
internal sealed partial class RecipeGroup : UserControl
{
    /// <summary>The header was pressed: collapse the rows or show them again.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ToggleEvent = RoutedEvent.Register<RecipeGroup, RoutedEventArgs>(nameof(ToggleEvent), RoutingStrategies.Bubble);

    public RecipeGroup()
    {
        InitializeComponent();
    }

    /// <summary>The group's view model; null before it has a data context.</summary>
    internal RecipeGroupViewModel? Group => DataContext as RecipeGroupViewModel;

    private void OnRecipe(object? sender, RoutedEventArgs e) => Group?.OpenRecipe();

    private void OnToggle(object? sender, RoutedEventArgs e) => RaiseEvent(new RoutedEventArgs(ToggleEvent, this));
}
