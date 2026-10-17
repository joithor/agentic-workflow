using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IHeavyReserve: the one fake, kept honest by HeavyReserveContract.</summary>
public sealed class InMemoryHeavyReserve : IHeavyReserve
{
    /// <summary>The box-wide slot count; change it to simulate another cap file.</summary>
    public int Cap { get; set; } = IHeavyReserve.DefaultCap;

    /// <summary>The reservation file's value.</summary>
    public int Reserve { get; set; }

    /// <summary>Whether writes fail (an unwritable state root).</summary>
    public bool FailWrites { get; set; }

    /// <summary>How many times the reservation was written.</summary>
    public int Writes { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<HeavySlots>> Read(CancellationToken cancellationToken) => Done.Of(new HeavySlots(Cap, Math.Min(Reserve, Cap)));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Write(int slots, CancellationToken cancellationToken)
    {
        if (FailWrites)
        {
            return Done.Failed<Unit>(MetaErrors.HostUnreadable.With("the state root is not writable"));
        }

        Reserve = slots;
        Writes++;
        return Done.Unit();
    }
}
