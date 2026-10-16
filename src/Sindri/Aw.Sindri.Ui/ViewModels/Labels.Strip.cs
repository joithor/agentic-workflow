using System.Globalization;
namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Home Workflows strip (screens §4.1).</summary>
internal static partial class Labels
{
    /// <summary>The strip row's desk answer button.</summary>
    internal const string Answer = "Answer";

    /// <summary>What the strip says while the workflow list route is not served yet (screens §4.8).</summary>
    internal const string ArrivesWorkflowStrip = "arrives with API P11";

    /// <summary>The strip's overflow row: "… 3 more".</summary>
    /// <param name="count">How many open rows are not shown.</param>
    /// <returns>The words.</returns>
    internal static string MoreRows(int count) => string.Create(CultureInfo.InvariantCulture, $"… {count} more");
}
