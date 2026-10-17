using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Scoping;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.FreezeScopingPacket;

internal sealed class FreezeScopingPacketService(IScopingPacketSource source, IFreezeLog log, TimeProvider clock, JsonSerializerOptions json)
    : IService<FreezeScopingPacketRequest, FreezeScopingPacketResponse>
{
    public async ValueTask<Result<FreezeScopingPacketResponse>> Handle(FreezeScopingPacketRequest request, CancellationToken cancellationToken)
    {
        var files = await source.Read(request.PacketDirectory, cancellationToken).ConfigureAwait(false);
        if (files.IsError)
        {
            return files.Error;
        }

        var instance = PacketId.From(request.Instance);   // the validator accepted it
        var checks = ScopingChecks.Run(files.Value, json);
        var notFrozen = new FreezeScopingPacketResponse
        {
            SchemaVersion = 1,
            Kind = PacketKinds.Scoping.Name,
            Instance = instance,
            By = request.By,
            Frozen = false,
            DigestVersion = PacketDigest.Version,
            AlreadyFrozen = false,
            Checks = checks,
        };
        if (checks.Any(static c => c.State is CheckState.Fail or CheckState.Omitted))
        {
            return notFrozen;
        }

        var hash = PacketDigest.Compute(files.Value);
        if (hash.IsError)
        {
            return hash.Error;
        }

        var record = Record(request, instance, hash.Value);
        return await Complete(request, notFrozen, record, hash.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<FreezeScopingPacketResponse>> Complete(FreezeScopingPacketRequest request, FreezeScopingPacketResponse notFrozen, FreezeRecord recorded, PacketHash hash, CancellationToken cancellationToken)
    {
        var receipt = await log.Record(recorded, cancellationToken).ConfigureAwait(false);
        if (receipt.IsError)
        {
            return receipt.Error;
        }

        var stored = await log.Stored(recorded, receipt.Value, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        return notFrozen with
        {
            Frozen = true,
            Hash = hash,
            Location = receipt.Value.Location,
            AlreadyFrozen = receipt.Value.AlreadyFrozen,
            By = stored.Value.FrozenBy,
            FrozenAt = stored.Value.FrozenAt,
            AlreadyFrozenBy = receipt.Value.AlreadyFrozen && stored.Value.FrozenBy != request.By ? stored.Value.FrozenBy : null,
        };
    }

    private FreezeRecord Record(FreezeScopingPacketRequest request, PacketId instance, PacketHash hash) => new()
    {
        SchemaVersion = 1,
        Kind = PacketKinds.Scoping.Name,
        Repo = RepoSlug.From(request.RepoSlug),
        Packet = instance,
        Hash = hash,
        DigestVersion = PacketDigest.Version,
        By = request.By,
        FrozenAt = clock.GetUtcNow(),
        Files = [.. PacketKinds.Scoping.Files.Select(static f => f.Name).Order(StringComparer.Ordinal)],
    };
}
