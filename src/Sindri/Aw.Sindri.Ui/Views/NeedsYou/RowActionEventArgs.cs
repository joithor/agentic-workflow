using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Views.NeedsYou;

/// <summary>A Needs-you row's control was pressed: the action it stands for.</summary>
/// <param name="action">The action.</param>
internal sealed class RowActionEventArgs(NeedsYouAction action) : RoutedEventArgs(NeedsYouRow.ActionEvent)
{
    /// <summary>The pressed control's action.</summary>
    internal NeedsYouAction Action { get; } = action;
}
