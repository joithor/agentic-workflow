using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the System overview saying what its cards say (overdue services, the Code index, working agents).</summary>
internal static partial class Labels
{
    /// <summary>The Agents card's row for sessions working now: the Agents page's word.</summary>
    internal const string AgentsWorking = "Working";

    /// <summary>A service whose next run is long past.</summary>
    internal const string ServiceOverdue = "Overdue";

    /// <summary>The Code index, named in the headline when its card needs a look.</summary>
    internal const string CodeIndexLookName = "code index";

    /// <summary>The headline's part for services that missed their runs: "2 services overdue".</summary>
    /// <param name="count">How many are overdue.</param>
    /// <returns>The words.</returns>
    internal static string ServicesOverdueHeadline(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "service" : "services")} overdue");

    /// <summary>The Services card's summary when some are overdue: "8 overdue · 1 scheduled", or "9 overdue" with none scheduled.</summary>
    /// <param name="overdue">Services past their next run.</param>
    /// <param name="scheduled">The others.</param>
    /// <returns>The words.</returns>
    internal static string ServicesOverdueSummary(int overdue, int scheduled) =>
        scheduled == 0 ? string.Create(CultureInfo.InvariantCulture, $"{overdue} overdue") : string.Create(CultureInfo.InvariantCulture, $"{overdue} overdue · {scheduled} scheduled");
}
