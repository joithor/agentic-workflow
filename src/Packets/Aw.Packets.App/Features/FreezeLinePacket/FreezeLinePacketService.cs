using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.FreezeLinePacket;

internal sealed class FreezeLinePacketService(LinePacketInspector inspector, IFreezeLog log, TimeProvider clock)
    : IService<FreezeLinePacketRequest, FreezeLinePacketResponse>
{
    public async ValueTask<Result<FreezeLinePacketResponse>> Handle(FreezeLinePacketRequest request, CancellationToken cancellationToken)
    {
        var kind = PacketKinds.Find(request.Kind)!;   // why: the validator accepted the kind
        var inspection = await inspector.Inspect(kind, request.PacketDirectory, request.RepoDirectory ?? request.PacketDirectory, sizes: null, cancellationToken)
            .ConfigureAwait(false);
        if (inspection.IsError)
        {
            return inspection.Error;
        }

        var inspected = inspection.Value;
        var notFrozen = new FreezeLinePacketResponse
        {
            SchemaVersion = 1,
            Kind = kind.Name,
            Packet = inspected.Manifest?.Id,
            By = request.By,
            Frozen = false,
            DigestVersion = PacketDigest.Version,
            AlreadyFrozen = false,
            Checks = inspected.Checks,
        };
        if (!inspected.Passed || inspected.Manifest is not { } manifest)
        {
            return notFrozen;
        }

        var hash = PacketDigest.Compute(kind, inspected.Files);
        if (hash.IsError)
        {
            return hash.Error;
        }

        var receipt = await log.Record(Record(kind, manifest, inspected.Files, hash.Value, request.By), cancellationToken).ConfigureAwait(false);
        return receipt.Map(r => notFrozen with { Frozen = true, Hash = hash.Value, Location = r.Location, AlreadyFrozen = r.AlreadyFrozen });
    }

    private FreezeRecord Record(PacketKind kind, LineManifest manifest, LinePacketFiles files, PacketHash hash, FreezeBy by) => new()
    {
        SchemaVersion = 1,
        Kind = kind.Name,
        Repo = manifest.Repo,
        Packet = manifest.Id,
        Hash = hash,
        DigestVersion = PacketDigest.Version,
        BaseSha = CommitSha.From(manifest.BaseSha),   // the checks passed, so PKT-BASE-001 accepted it
        By = by,
        FrozenAt = clock.GetUtcNow(),
        Files = [.. files.Present.Keys.Where(k => kind.Files.Any(f => f.Name == k) || kind.Directories.Any(d => k.StartsWith(d.Prefix, StringComparison.Ordinal))).Order(StringComparer.Ordinal)],
    };
}
