using System.Globalization;

namespace Aw.Sindri.Core.Views;

/// <summary>The empty, loading and error sentences of the list views (API spec §3.6, the P2 part): the CLI prints them and every list returns one in 'empty'.</summary>
public static class EmptyText
{
    /// <summary>The services list is empty.</summary>
    /// <returns>The sentence.</returns>
    public static string Services() => "No services are approved: run `sindri profile approve`.";

    /// <summary>The views are rebuilding.</summary>
    /// <param name="cursor">The projector's cursor.</param>
    /// <param name="head">The ledger head.</param>
    /// <returns>The sentence.</returns>
    public static string Rebuilding(long cursor, long head) =>
        string.Create(CultureInfo.InvariantCulture, $"Rebuilding views (cursor {cursor} of {head})…");

    /// <summary>The projector stalled.</summary>
    /// <param name="at">The sequence it stalled at.</param>
    /// <returns>The sentence.</returns>
    public static string Stalled(long at) =>
        string.Create(CultureInfo.InvariantCulture, $"Views are stalled at {at}: run `sindri views rebuild`");

    /// <summary>The projector can't run (no event is to blame).</summary>
    /// <param name="detail">Why, with its code.</param>
    /// <returns>The sentence.</returns>
    public static string Failing(string? detail) => $"The views can't be updated ({detail}): run `{FixFor(detail)}`";

    /// <summary>The command that fixes a stall: the doctor when the ledger is what can't be read (a rebuild can't help), else a rebuild.</summary>
    /// <param name="detail">The stall's '&lt;code&gt;: &lt;detail&gt;'.</param>
    /// <returns>The command.</returns>
    public static string FixFor(string? detail) =>
        detail is not null && detail.StartsWith("SND-LEDGER", StringComparison.Ordinal) ? "sindri doctor" : "sindri views rebuild";

    /// <summary>The views can't be read.</summary>
    /// <returns>The sentence.</returns>
    public static string Unreadable() => "The views can't be read: run `sindri views rebuild`";

    /// <summary>The sentence a list answers with: stalled or failing, then rebuilding, then its empty sentence when it has no rows, else null.</summary>
    /// <param name="views">The projector's status.</param>
    /// <param name="empty">True when the list has no rows.</param>
    /// <param name="emptySentence">The list's empty sentence.</param>
    /// <returns>The sentence, or null.</returns>
    public static string? For(ViewsStatus views, bool empty, Func<string> emptySentence)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(emptySentence);
        return views.Text ?? (empty ? emptySentence() : null);
    }
}
