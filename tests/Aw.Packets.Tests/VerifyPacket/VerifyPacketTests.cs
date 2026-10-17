using Aw.Packets.App.Features.FreezeLinePacket;
using Aw.Packets.App.Features.FreezePacket;
using Aw.Packets.App.Features.FreezeScopingPacket;
using Aw.Packets.App.Features.HashPacket;
using Aw.Packets.App.Features.VerifyPacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Hashing;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;
using Aw.Packets.Testing;
using Aw.Packets.Tests.Line;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Packets.Tests.VerifyPacket;

/// <summary>LN-PKT2: verify recomputes the hash with the one canonicalization and finds the freeze record freeze wrote.</summary>
public sealed class VerifyPacketTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryFreezeLog _log = new();
    private readonly InMemoryPacketSource _builds = new();
    private readonly InMemoryScopingPacketSource _scoping = new();
    private readonly InMemoryLinePacketSource _lines = new();

    private PacketHasher Hasher() => new(_builds, _scoping, _lines, PacketFiles.Json);

    private VerifyPacketService Verifier() => new(Hasher(), _log);

    private static InMemoryRepoSnapshot Repo() => new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles);

    private async Task<PacketHash> FreezeBuild(FreezeBy by = FreezeBy.Machine)
    {
        _ = _builds.With("pkt", PacketFiles.Of(A.Packet().Build()));
        var freeze = new FreezePacketService(new PacketInspector(_builds, Repo()), _log, new FakeTimeProvider(_now));
        var result = await freeze.Handle(new FreezePacketRequest("pkt", RepoDirectory: null, By: by), CancellationToken.None);
        return result.Value.Hash!.Value;
    }

    private async Task<VerifyPacketResponse> Verify(string kind, PacketHash hash, bool candidate = false, string? slug = null, string? instance = null)
    {
        var result = await Verifier().Handle(new VerifyPacketRequest(kind, "pkt", hash, candidate, slug, instance), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_frozen_packet_with_its_hash_is_verified_and_says_who_froze_it()
    {
        var hash = await FreezeBuild(FreezeBy.Owner);

        var verified = await Verify("build", hash);

        Assert.True(verified.Verified);
        Assert.Equal((true, FreezeBy.Owner, _now, "demo-packet"), (verified.Frozen, verified.By, verified.FrozenAt, verified.Packet?.Value));
        Assert.Equal($"example/repo/demo-packet/frozen/{hash.Hex}.json", verified.Location);
        Assert.Equal((hash, hash), (verified.Expected, verified.Computed));
        Assert.Null(verified.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_without_by_reads_as_a_machine_freeze()
    {
        var hash = await FreezeBuild();
        var record = Assert.Single(_log.Records).Value;
        var old = new InMemoryFreezeLog();
        _ = await old.Record(record with { By = null }, CancellationToken.None);

        var result = await new VerifyPacketService(Hasher(), old)
            .Handle(new VerifyPacketRequest("build", "pkt", hash), CancellationToken.None);

        Assert.Equal((true, FreezeBy.Machine), (result.Value.Verified, result.Value.By));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_packet_that_hashes_to_something_else_is_not_verified_and_no_record_is_looked_for()
    {
        var frozen = await FreezeBuild();
        _ = _builds.With("pkt", PacketFiles.Of(A.Packet().With(A.Task("t2")).Build()));

        var verified = await Verify("build", frozen);

        Assert.False(verified.Verified);
        Assert.NotEqual(frozen, verified.Computed);
        Assert.Contains(verified.Computed.Value, verified.Reason, StringComparison.Ordinal);
        Assert.Contains(frozen.Value, verified.Reason, StringComparison.Ordinal);
        Assert.Null(verified.Frozen);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_packet_with_its_hash_that_was_never_frozen_is_not_verified()
    {
        _ = _builds.With("pkt", PacketFiles.Of(A.Packet().Build()));
        var hash = (await new HashPacketService(Hasher()).Handle(new HashPacketRequest("build", "pkt"), CancellationToken.None)).Value.Hash;

        var verified = await Verify("build", hash);

        Assert.False(verified.Verified);
        Assert.Equal((false, null), (verified.Frozen, verified.By));
        Assert.Contains("no freeze record", verified.Reason, StringComparison.Ordinal);
        Assert.Equal($"example/repo/demo-packet/frozen/{hash.Hex}.json", verified.Location);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_candidate_only_compares_the_hash_and_looks_for_no_record()
    {
        _ = _builds.With("pkt", PacketFiles.Of(A.Packet().Build()));
        var hash = (await new HashPacketService(Hasher()).Handle(new HashPacketRequest("build", "pkt"), CancellationToken.None)).Value.Hash;

        var match = await Verify("build", hash, candidate: true);
        var other = await Verify("build", PacketHash.From("sha256:" + new string('0', 64)), candidate: true);

        Assert.Equal((true, true, null), (match.Verified, match.Candidate, match.Frozen));
        Assert.False(other.Verified);
        Assert.Null(other.Frozen);
        Assert.Empty(_log.Records);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(LineChecksTests.Kinds), MemberType = typeof(LineChecksTests))]
    public async Task Every_line_kind_verifies_what_freeze_recorded(string name)
    {
        var kind = PacketKinds.Find(name)!;
        _ = _lines.With("pkt", A.LinePacket(kind).Build());
        var freeze = new FreezeLinePacketService(new LinePacketInspector(_lines, Repo(), PacketFiles.Json), _log, new FakeTimeProvider(_now));
        var frozen = await freeze.Handle(new FreezeLinePacketRequest(name, "pkt", RepoDirectory: null, By: FreezeBy.Owner), CancellationToken.None);

        var verified = await Verify(name, frozen.Value.Hash!.Value);

        Assert.True(verified.Verified, verified.Reason);
        Assert.Equal((FreezeBy.Owner, name), (verified.By, verified.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_packet_is_found_under_the_repository_and_instance_it_was_frozen_under()
    {
        _ = _scoping.With("pkt", A.ScopingPacket().Build());
        var freeze = new FreezeScopingPacketService(_scoping, _log, new FakeTimeProvider(_now), PacketFiles.Json);
        var frozen = await freeze.Handle(new FreezeScopingPacketRequest("pkt", "example/repo", "idea-1"), CancellationToken.None);
        var hash = frozen.Value.Hash!.Value;

        var verified = await Verify("scoping", hash, slug: "example/repo", instance: "idea-1");
        var elsewhere = await Verify("scoping", hash, slug: "example/repo", instance: "idea-2");

        Assert.True(verified.Verified);
        Assert.Equal("idea-1", verified.Packet?.Value);
        Assert.False(elsewhere.Verified);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_packet_without_a_repository_and_instance_is_refused_unless_it_is_a_candidate()
    {
        var hash = PacketHash.From("sha256:" + new string('0', 64));
        var validator = new VerifyPacketValidator();

        Assert.True(validator.Validate(new VerifyPacketRequest("scoping", "pkt", hash)).IsError);
        Assert.True(validator.Validate(new VerifyPacketRequest("scoping", "pkt", hash, Candidate: false, RepoSlug: "example/repo", Instance: "idea-1")).IsSuccess);
        Assert.True(validator.Validate(new VerifyPacketRequest("scoping", "pkt", hash, Candidate: true)).IsSuccess);
        Assert.True(validator.Validate(new VerifyPacketRequest("build", "pkt", hash)).IsSuccess);
        Assert.True(validator.Validate(new VerifyPacketRequest("nonsense", "pkt", hash)).IsError);
        await Task.CompletedTask;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_packet_is_AW_PACKETS_002()
    {
        var result = await Verifier().Handle(new VerifyPacketRequest("build", "nowhere", PacketHash.From("sha256:" + new string('0', 64))), CancellationToken.None);

        Assert.Equal(PacketsErrors.NotFound.Code, result.Error.Code);
    }
}
