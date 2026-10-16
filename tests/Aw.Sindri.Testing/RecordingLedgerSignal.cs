using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Testing;

/// <summary>A ledger signal that records each pulse and never makes a waiter wait.</summary>
public sealed class RecordingLedgerSignal : ILedgerSignal
{
    private readonly Lock _gate = new();
    private readonly List<long> _pulses = [];

    /// <summary>The sequences pulsed so far, in order.</summary>
    public IReadOnlyList<long> Pulses
    {
        get
        {
            lock (_gate)
            {
                return [.. _pulses];
            }
        }
    }

    /// <inheritdoc/>
    public void Pulse(long sequence)
    {
        lock (_gate)
        {
            _pulses.Add(sequence);
        }
    }

    /// <inheritdoc/>
    public ValueTask<long> WaitAfter(long sequence, TimeSpan limit, CancellationToken cancellationToken) => ValueTask.FromResult(sequence);
}
