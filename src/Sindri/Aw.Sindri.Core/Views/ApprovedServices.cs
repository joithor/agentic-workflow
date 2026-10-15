using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// The 'services' view rows that still count: those of services the approved profile names (plan G5). A service an approval
/// dropped keeps its row (the views are a pure fold of the ledger) but leaves the list, and no failure of it degrades health.
/// One filter for GET /services and the health inputs, so the two can't diverge.
/// </summary>
public static class ApprovedServices
{
    /// <summary>The rows of <paramref name="rows"/> whose service <paramref name="approved"/> names (none when nothing is approved).</summary>
    /// <param name="rows">The services view's rows.</param>
    /// <param name="approved">The approved profile, or null before the first approval.</param>
    /// <returns>The rows to show and to judge health by.</returns>
    public static IReadOnlyList<ServiceRow> Of(IReadOnlyList<ServiceRow> rows, LoadedProfile? approved)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var named = approved?.Profile.Services ?? SindriProfile.NoServices;
        return [.. rows.Where(r => named.ContainsKey(r.Name.Value))];
    }
}
