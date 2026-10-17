using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.FreezePacket;

internal sealed class FreezePacketService(PacketInspector inspector, IFreezeLog log, TimeProvider clock)
    : IService<FreezePacketRequest, FreezePacketResponse>
{
    public async ValueTask<Result<FreezePacketResponse>> Handle(FreezePacketRequest request, CancellationToken cancellationToken)
    {
        var inspection = await inspector.Inspect(request.PacketDirectory, request.RepoDirectory ?? request.PacketDirectory, sizes: null, cancellationToken)
            .ConfigureAwait(false);
        if (inspection.IsError)
        {
            return inspection.Error;
        }

        var checkedPacket = inspection.Value;
        var notFrozen = new FreezePacketResponse
        {
            SchemaVersion = 1,
            Packet = checkedPacket.Loaded.Packet.Manifest.Id,
            By = request.By,
            Frozen = false,
            DigestVersion = PacketDigest.Version,
            AlreadyFrozen = false,
            Checks = checkedPacket.Checks,
        };
        if (!checkedPacket.Passed)
        {
            return notFrozen;
        }

        var hash = PacketDigest.Compute(checkedPacket.Loaded);
        if (hash.IsError)
        {
            return hash.Error;
        }

        var receipt = await log.Record(Record(checkedPacket.Loaded, hash.Value, request.By), cancellationToken).ConfigureAwait(false);
        return receipt.Map(r => notFrozen with { Frozen = true, Hash = hash.Value, Location = r.Location, AlreadyFrozen = r.AlreadyFrozen });
    }

    private FreezeRecord Record(LoadedPacket loaded, PacketHash hash, FreezeBy by) => new()
    {
        SchemaVersion = 1,
        Repo = loaded.Packet.Manifest.Repo,
        Packet = loaded.Packet.Manifest.Id,
        Hash = hash,
        DigestVersion = PacketDigest.Version,
        BaseSha = CommitSha.From(loaded.Packet.Manifest.BaseSha),   // the checks passed, so PKT-BASE-001 accepted it
        By = by,
        FrozenAt = clock.GetUtcNow(),
        Files = [.. loaded.Files.Keys.Order(StringComparer.Ordinal)],
    };
}
