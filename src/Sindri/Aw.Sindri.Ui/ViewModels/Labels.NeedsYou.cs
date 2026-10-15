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
}
