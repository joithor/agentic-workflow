using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// The three protected actions the app may confirm itself (setup spec §2): the shared confirmation controls draw and answer for these and for
/// no other, so a control can never be pointed at another protected action. Until the catalog row is ready the controls are the terminal copy.
/// </summary>
internal static class AppConfirm
{
    private static readonly string[] _actions = ["ApproveProfile", "ApproveRecipe", "ApproveAllRecipes"];

    /// <summary>The action ids the app confirms.</summary>
    internal static IReadOnlyList<string> Actions => _actions;

    /// <summary>Checks that an action is one of the three.</summary>
    /// <param name="action">The catalog action id.</param>
    /// <returns>The same id.</returns>
    /// <exception cref="ArgumentException">The id is not one the app confirms.</exception>
    internal static string Require(string action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _actions.Contains(action, StringComparer.Ordinal) ? action : throw new ArgumentException($"The app does not confirm '{action}' itself.", nameof(action));
    }

    /// <summary>True when the catalog says the app may confirm the action (a cli-only, pending or missing row is not).</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="action">The action id.</param>
    /// <returns>True when the row is ready.</returns>
    internal static bool IsReady(CatalogViewModel catalog, string action) =>
        catalog.Row(action) is { } row && CatalogReadiness.Parse(row.Ui).Kind == ControlKind.Ready;

    /// <summary>The terminal copy that stands in while the row is not ready: the existing lock button over the served command.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="action">The action id.</param>
    /// <param name="verb">The verb the lock label shows ("Approve settings").</param>
    /// <param name="subject">What it acts on, for the accessible name.</param>
    /// <param name="clock">The clock for "Copied".</param>
    /// <returns>The lock button's model, or null when the row is ready.</returns>
    internal static ProtectedActionViewModel? Terminal(CatalogViewModel catalog, string action, string verb, string subject, TimeProvider clock) =>
        IsReady(catalog, action) ? null : new ProtectedActionViewModel(new ProtectedAction(verb, catalog.Row(action)?.Cli ?? string.Empty, subject), catalog.Clipboard, clock);
}
