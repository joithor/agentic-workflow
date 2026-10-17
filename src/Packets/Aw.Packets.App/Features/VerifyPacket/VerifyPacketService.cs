using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Packets.Core;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.VerifyPacket;

/// <summary>
/// Recomputes the packet's hash with the one canonicalization (<see cref="PacketHasher"/>, what 'hash' prints and
/// 'freeze' records), compares it with the expected one and, unless it is a candidate, finds the freeze record where
/// 'freeze' wrote it: &lt;owner&gt;/&lt;name&gt;/&lt;packet id&gt;/frozen/&lt;hex&gt;.json, the repository and id from packet.json (a scope
/// packet has none: the request names them).
/// </summary>
internal sealed class VerifyPacketService(PacketHasher hasher, IFreezeLog log) : IService<VerifyPacketRequest, VerifyPacketResponse>
{
    public async ValueTask<Result<VerifyPacketResponse>> Handle(VerifyPacketRequest request, CancellationToken cancellationToken)
    {
        var kind = PacketKinds.Find(request.Kind)!;   // why: the validator accepted the kind
        var hashed = await hasher.Hash(kind, request.PacketDirectory, cancellationToken).ConfigureAwait(false);
        if (hashed.IsError)
        {
            return hashed.Error;
        }

        var identity = hashed.Value.Identity ?? Named(request);
        var verdict = new VerifyPacketResponse
        {
            SchemaVersion = 1,
            Kind = kind.Name,
            Packet = identity?.Packet,
            Verified = false,
            Candidate = request.Candidate,
            Expected = request.Hash,
            Computed = hashed.Value.Hash,
            DigestVersion = PacketDigest.Version,
        };
        if (hashed.Value.Hash != request.Hash)
        {
            return verdict with { Reason = $"the packet hashes to {hashed.Value.Hash.Value}, not {request.Hash.Value}" };
        }

        if (request.Candidate)
        {
            return verdict with { Verified = true };
        }

        if (identity is null)
        {
            return verdict with { Reason = "packet.json can't be read, so the packet's freeze record can't be located" };
        }

        return await Lookup(verdict, identity, cancellationToken).ConfigureAwait(false);
    }

    private static PacketIdentity? Named(VerifyPacketRequest request) =>
        request.RepoSlug is { } slug && request.Instance is { } instance && RepoSlug.TryFrom(slug, out var repo) && PacketId.TryFrom(instance, out var id)
            ? new PacketIdentity(repo, id)
            : null;

    private async ValueTask<Result<VerifyPacketResponse>> Lookup(VerifyPacketResponse verdict, PacketIdentity identity, CancellationToken cancellationToken)
    {
        var location = FreezeLocation.Of(identity.Repo, identity.Packet, verdict.Expected);
        var found = await log.Find(identity, verdict.Expected, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } record)
        {
            return verdict with { Frozen = false, Location = location, Reason = $"no freeze record for {verdict.Expected.Value} (looked at packets/{location})" };
        }

        return record.Hash != verdict.Expected
            ? verdict with { Frozen = false, Location = location, Reason = $"the freeze record at packets/{location} says {record.Hash.Value}, so it doesn't belong there" }
            : verdict with { Verified = true, Frozen = true, By = record.FrozenBy, FrozenAt = record.FrozenAt, Location = location };
    }
}
