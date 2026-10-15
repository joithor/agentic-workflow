using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>Recorded events for fold tests: the harness's event factory builds the envelope, the test picks the sequence.</summary>
internal static class Recorded
{
    /// <summary>A recorded <paramref name="type"/> event carrying <paramref name="payload"/> at <paramref name="sequence"/>.</summary>
    public static LedgerEvent Of<TPayload>(EventFactory events, string type, TPayload payload, long sequence)
    {
        var entry = events.Entry(type, payload, string.Create(CultureInfo.InvariantCulture, $"{type}:{sequence}"));
        return new LedgerEvent(sequence, Epoch: 1, entry.Envelope, entry.IdempotencyKey, entry.Item);
    }
}
