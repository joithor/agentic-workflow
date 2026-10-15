using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

#pragma warning disable MA0182 // until the U8 pages (T1, T4) place it
/// <summary>
/// The one place the open figure is read (rev 8, critique run 5 P1: "open counts from one source"). The nav tab, the Home
/// strip head and the Workflows head all print the list's "counts.open", which the API computes over the root rows
/// that are not terminal (owner, 2026-10-14); no view counts rows of its own. <see cref="IsOpen"/> is that same rule on one
/// row, for the tests that hold the figure to the rows.
/// </summary>
internal static class InstanceHeads
{
    /// <summary>True when the row's current state is not terminal.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Whether it is open.</returns>
    internal static bool IsOpen(InstanceSummary row) => !row.States.Any(s => s.Terminal && string.Equals(s.State, row.State, StringComparison.Ordinal));

    /// <summary>The nav tab's count: the open figure alone.</summary>
    /// <param name="counts">The list's counts.</param>
    /// <returns>The text.</returns>
    internal static string Nav(ListCounts counts) => Number(counts.Open);

    /// <summary>The Home strip's head: "8 open".</summary>
    /// <param name="counts">The list's counts.</param>
    /// <returns>The text.</returns>
    internal static string Strip(ListCounts counts) => $"{Number(counts.Open)} open";

    /// <summary>The Workflows page's head: "8 open · 2 need you · 1 with you".</summary>
    /// <param name="counts">The list's counts.</param>
    /// <returns>The text.</returns>
    internal static string Workflows(ListCounts counts) => $"{Number(counts.Open)} open · {Number(counts.NeedsYou)} need you · {Number(counts.WithYou)} with you";

    /// <summary>The collapsed finished line: "4 done today".</summary>
    /// <param name="done">The finished figure.</param>
    /// <returns>The text.</returns>
    internal static string DoneLine(int done) => $"{Number(done)} done today";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
#pragma warning restore MA0182
