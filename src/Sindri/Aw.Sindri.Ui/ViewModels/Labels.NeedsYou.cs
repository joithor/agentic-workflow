using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Needs-you panel's seen state and motion words.</summary>
internal static partial class Labels
{
    /// <summary>The word next to an unseen pulse row under reduced motion (UI spec §5).</summary>
    internal const string New = "new";

    /// <summary>The Needs-you panel's Mark all seen button.</summary>
    internal const string MarkAllSeen = "Mark all seen";

    /// <summary>The Undo button after Mark all seen.</summary>
    internal const string Undo = "Undo";

    /// <summary>What Mark all seen did, before its Undo button (mockup comment, provisional owner default 2026-10-14 night).</summary>
    /// <param name="count">How many rows it marked.</param>
    /// <returns>The line.</returns>
    internal static string MarkedSeen(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} marked seen");

    /// <summary>The tooltip of a protected action's control (screens fixture, "The protected-action label").</summary>
    internal const string ProtectedTooltip = "Needs a typed confirmation at a terminal";

    /// <summary>The tooltip of an action that reads the views while they rebuild (UI spec §4.1, SND-API-010).</summary>
    internal const string RetryAfterRebuild = "Retry after the rebuild";

    /// <summary>The Needs-you panel's overflow control; it expands the list in place (D8: the "all N →" form of the mockups).</summary>
    /// <param name="count">How many rows there are.</param>
    /// <returns>The words.</returns>
    internal static string AllRows(int count) => string.Create(CultureInfo.InvariantCulture, $"all {count} →");

    /// <summary>The control that shows the rows held back while the pointer is over the panel.</summary>
    /// <param name="count">How many rows are held.</param>
    /// <returns>The words.</returns>
    internal static string NewRows(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} new");

    /// <summary>The live region's line when a row leaves the list.</summary>
    /// <param name="id">The row's shown id.</param>
    /// <returns>The words.</returns>
    internal static string Cleared(string id) => id + " cleared";

    /// <summary>A row's age: minutes under an hour, then hours, then days.</summary>
    /// <param name="age">How long the row has waited.</param>
    /// <returns>The words, such as "12m", "2h", "3d".</returns>
    internal static string RowAge(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "<1m",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes}m"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours}h"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays}d"),
    };
}
