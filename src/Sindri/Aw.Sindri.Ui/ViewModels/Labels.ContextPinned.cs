using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page's pinned "Approval on" choice (F3 context spec 1 §2 row 1b, §9 item 1).</summary>
internal static partial class Labels
{
    /// <summary>The pinned choice's words once the item's frozen scope is known.</summary>
    /// <param name="when">When the scope was approved and frozen.</param>
    /// <returns>"Approval on 3 Jan 2026".</returns>
    internal static string ContextMapAsOfPinnedOn(DateTimeOffset when) =>
        "Approval on " + when.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Why the pinned choice is off when the starting points were read at different times.</summary>
    internal const string ContextMapPinnedMixed = "These starting points were read at different times.";

    /// <summary>The line shown when the approved map version is no longer kept and the live map is shown instead.</summary>
    internal const string ContextMapPinnedPruned = "That version of the map is no longer kept. Showing the live map.";
}
