using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The Workflows page: a row's title opens its instance, and the filters narrow the list.</summary>
internal sealed partial class WorkflowsPage : UserControl
{
    public WorkflowsPage()
    {
        InitializeComponent();
        AddHandler(InstanceRow.OpenEvent, OnOpen);
        AddHandler(RecipeGroup.ToggleEvent, OnToggle);
    }

    private WorkflowsViewModel? List => DataContext as WorkflowsViewModel;

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is InstanceRow { Address.Length: > 0 } row)
        {
            List?.Open(row.Address);
        }
    }

    private void OnToggle(object? sender, RoutedEventArgs e) => (e.Source as RecipeGroup)?.Group?.Toggle();

    private void OnRecipeChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (List is { } list && sender is ComboBox { SelectedItem: string chosen })
        {
            var recipe = string.Equals(chosen, Labels.AllRecipes, StringComparison.Ordinal) ? null : chosen;
            if (!string.Equals(recipe, list.RecipeFilter, StringComparison.Ordinal))
            {
                _ = list.FilterRecipe(recipe, CancellationToken.None);
            }
        }
    }

    // The subject filter applies on Enter, so typing never re-reads the list under the human's hands.
    private void OnSubjectKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && List is { } list && sender is TextBox box)
        {
            _ = list.FilterSubject(box.Text, CancellationToken.None);
            e.Handled = true;
        }
    }

    private void OnDoneToggle(object? sender, RoutedEventArgs e) => _ = List?.ToggleDone(CancellationToken.None);
}
