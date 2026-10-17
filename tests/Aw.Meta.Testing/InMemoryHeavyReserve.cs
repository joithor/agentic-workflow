using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IHeavyReserve: the one fake, kept honest by HeavyReserveContract.</summary>
public sealed class InMemoryHeavyReserve : IHeavyReserve
{
    /// <summary>The kept computation; null until one is written.</summary>
    public HeavyReserveRecord? Record { get; set; }

    /// <summary>The kept reservation (0 when none).</summary>
    public int Reserve => Record?.Reserve ?? 0;

    /// <summary>Whether writes fail (an unwritable state directory).</summary>
    public bool FailWrites { get; set; }

    /// <summary>How many times a computation was written.</summary>
    public int Writes { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<HeavyReserveRecord?>> Read(CancellationToken cancellationToken) => Done.Of(Record);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Write(HeavyReserveRecord record, CancellationToken cancellationToken)
    {
        if (FailWrites)
        {
            return Done.Failed<Unit>(MetaErrors.HostUnreadable.With("the state directory is not writable"));
        }

        Record = record;
        Writes++;
        return Done.Unit();
    }
}
