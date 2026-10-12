using System.Text.Json;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Items;

/// <summary>The one line an item's timeline shows for an event (API spec §3.1).</summary>
public static class TimelineLine
{
    /// <summary>The line for <paramref name="recorded"/>: 'Created: &lt;title&gt;', 'new → done', 'Linked to &lt;record&gt; (&lt;alias&gt;)', or the event type.</summary>
    /// <param name="recorded">The ledger event.</param>
    /// <param name="json">The options its payload reads with.</param>
    /// <returns>The line, before scrubbing.</returns>
    public static string For(LedgerEvent recorded, JsonSerializerOptions json)
        => throw new NotSupportedException("TimelineLine.For: P3 unit D makes it pass");
}
