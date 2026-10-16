using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>The item states a root instance's ledger rows give an item (API spec section 3.6, API P11). Stub until U2 is built.</summary>
public static class EngineItemStates
{
    /// <summary>The part after an event, or null when the event leaves the item alone.</summary>
    /// <param name="prior">The part so far, or null before the first row.</param>
    /// <param name="eventType">The ledger event type.</param>
    /// <param name="payload">The event's payload.</param>
    /// <returns>The new part, or null.</returns>
    public static EngineItemPart? Next(EngineItemPart? prior, string eventType, WorkflowEventPayload payload) =>
        throw new NotSupportedException("EngineItemStates is built in API P11 U2.");

    /// <summary>The item state a part reads as.</summary>
    /// <param name="part">The part.</param>
    /// <returns>The state.</returns>
    public static ItemState StateOf(EngineItemPart part) =>
        throw new NotSupportedException("EngineItemStates is built in API P11 U2.");
}
