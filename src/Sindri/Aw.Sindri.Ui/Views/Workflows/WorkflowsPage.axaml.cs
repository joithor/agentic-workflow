using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
        AddHandler(InstanceRow.SelectEvent, OnSelect);
        AddHandler(RecipeGroup.ToggleEvent, OnToggle);
    }

    private WorkflowsViewModel? List => DataContext as WorkflowsViewModel;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        List?.PropertyChanged -= OnListChanged;
        base.OnDataContextChanged(e);
        List?.PropertyChanged += OnListChanged;
    }

    // J / K moved the selection onto a row: its frame takes focus, on the keyboard's ring, and scrolls into view. A selection that
    // cleared (the row went) leaves focus where it is; the window keeps it somewhere usable.
    private void OnListChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkflowsViewModel.SelectedAddress), StringComparison.Ordinal) && List?.SelectedAddress is { } address)
        {
            Dispatcher.UIThread.Post(() => FocusRow(address), DispatcherPriority.Loaded);
        }
    }

    private void FocusRow(string address)
    {
        var row = this.GetVisualDescendants().OfType<InstanceRow>().FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.Address, address, StringComparison.Ordinal));
        if (row is null || row.IsKeyboardFocusWithin)
        {
            return;   // not drawn, or focus is already in the row (a click or its control): it stays where it is
        }

        if (row.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "WorkflowRowFrame", StringComparison.Ordinal)) is { } frame)
        {
            _ = frame.Focus(NavigationMethod.Tab);
            frame.BringIntoView();
        }
    }

    // Focus arriving on a row, or a control in it, selects it: the keys act on the row the human is on.
    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        if (e.Source is InstanceRow { Address.Length: > 0 } row)
        {
            _ = List?.Select(row.Address);
        }
    }

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
