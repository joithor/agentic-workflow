using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    /// <summary>The 'park' row of a root instance's park or escalation (API spec section 3.6). Stub until API P11 U3.</summary>
    /// <param name="item">The item's row, for its title, repo and id.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="suspension">The suspension the ledger row shows.</param>
    /// <param name="since">When the row appeared.</param>
    /// <param name="scrubber">Scrubs the text the row stores.</param>
    /// <returns>The row.</returns>
    public static NeedsYouRow ForPark(ItemRow item, InstanceAddress address, SuspensionSummary suspension, DateTimeOffset since, ITextScrubber scrubber) =>
        throw new NotSupportedException("NeedsYouRows.ForPark is built in API P11 U3.");

    /// <summary>The 'park' row of an open desk task. Stub until API P11 U3.</summary>
    /// <param name="item">The item's row.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="since">When the row appeared.</param>
    /// <param name="scrubber">Scrubs the text the row stores.</param>
    /// <returns>The row.</returns>
    public static NeedsYouRow ForDeskTask(ItemRow item, InstanceAddress address, DateTimeOffset since, ITextScrubber scrubber) =>
        throw new NotSupportedException("NeedsYouRows.ForDeskTask is built in API P11 U3.");

    /// <summary>The 'park' row of a takeover that went idle. Stub until API P11 U3.</summary>
    /// <param name="item">The item's row.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="since">When the row appeared.</param>
    /// <param name="scrubber">Scrubs the text the row stores.</param>
    /// <returns>The row.</returns>
    public static NeedsYouRow ForIdleTakeover(ItemRow item, InstanceAddress address, DateTimeOffset since, ITextScrubber scrubber) =>
        throw new NotSupportedException("NeedsYouRows.ForIdleTakeover is built in API P11 U3.");
}
