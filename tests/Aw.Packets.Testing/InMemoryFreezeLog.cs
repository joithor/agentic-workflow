using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Testing;

/// <summary>The in-memory <see cref="IFreezeLog"/>: the one fake, kept honest by <see cref="FreezeLogContract"/>.</summary>
internal sealed class InMemoryFreezeLog : IFreezeLog
{
    private readonly SortedDictionary<string, FreezeRecord> _records = new(StringComparer.Ordinal);

    /// <summary>When true, every Record fails with AW-PACKETS-007.</summary>
    public bool Unwritable { get; init; }

    /// <summary>The records, by location.</summary>
    public IReadOnlyDictionary<string, FreezeRecord> Records => _records;

    /// <inheritdoc />
    public ValueTask<Result<FreezeReceipt>> Record(FreezeRecord record, CancellationToken cancellationToken)
    {
        if (Unwritable)
        {
            return ValueTask.FromResult<Result<FreezeReceipt>>(PacketsErrors.FreezeLogUnavailable.With("the in-memory log is unwritable"));
        }

        var location = FreezeLocation.Of(record);
        var added = _records.TryAdd(location, record);
        return ValueTask.FromResult<Result<FreezeReceipt>>(new FreezeReceipt(location, AlreadyFrozen: !added));
    }

    /// <inheritdoc />
    public ValueTask<Result<FreezeRecord?>> Find(PacketIdentity packet, PacketHash hash, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<FreezeRecord?>(_records.GetValueOrDefault(FreezeLocation.Of(packet.Repo, packet.Packet, hash))));
}
