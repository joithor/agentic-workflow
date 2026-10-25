using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The stage tag of a strip row (screens §4.1): the current state, then what its served cell says: the round ("r1/3"), the
/// children ("4/9"), the desk mark, the child mark, and the loop partner ("⇄ verify"). The cell is the one the API serves;
/// the app never works the recipe out itself.
/// </summary>
internal static class StageTag
{
    /// <summary>The tag of a row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The words, such as "mitigate r1/3 ⇄ verify", "classify · desk", "build 4/9".</returns>
    internal static string For(InstanceSummary row)
    {
        var cell = row.States.FirstOrDefault(s => string.Equals(s.State, row.State, StringComparison.Ordinal));
        return cell is null ? row.State : string.Concat(row.State, Parts(cell, row.State));
    }

    private static string Parts(StateCell cell, string state) => string.Concat(
        cell.Round is { } r ? string.Create(CultureInfo.InvariantCulture, $" r{r.N}/{r.Max}") : string.Empty,
        cell.Children is { } c ? string.Create(CultureInfo.InvariantCulture, $" {c.Done}/{c.Total}") : string.Empty,
        cell.Desk ? $" · {Labels.StageDesk}" : string.Empty,
        cell.Child && cell.Children is null ? " · child" : string.Empty,
        cell.Loops.FirstOrDefault(l => !string.Equals(l.With, state, StringComparison.Ordinal)) is { } loop ? $" ⇄ {loop.With}" : string.Empty);
}
