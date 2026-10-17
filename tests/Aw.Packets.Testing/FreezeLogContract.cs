using Aw.Packets.Core;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;
using Xunit;

namespace Aw.Packets.Testing;

/// <summary>
/// The IFreezeLog contract: the same tests run against the in-memory fake and every real adapter
/// (.agents/rules/csharp.md, Seams). A derived class supplies the log and a way to read back what it stored.
/// </summary>
public abstract class FreezeLogContract
{
    private const string HashA = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    /// <summary>A record with deterministic fields.</summary>
    /// <param name="hash">The hash.</param>
    /// <returns>The record.</returns>
    protected static FreezeRecord ARecord(string hash = HashA) => new()
    {
        SchemaVersion = 1,
        Repo = RepoSlug.From("example/repo"),
        Packet = PacketId.From("demo-packet"),
        Hash = PacketHash.From(hash),
        DigestVersion = 1,
        BaseSha = CommitSha.From(PacketBuilder.BaseSha),
        FrozenAt = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero),
        Files = ["packet.json", "tasks.json"],
    };

    /// <summary>A record lands at '&lt;owner&gt;/&lt;name&gt;/&lt;packet&gt;/frozen/&lt;hex&gt;.json' and reads back unchanged.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_record_lands_at_its_location_and_reads_back()
    {
        var log = Create();

        var receipt = await log.Record(ARecord(), CancellationToken.None);

        Expect.That(receipt.IsSuccess && receipt.Value == new FreezeReceipt("example/repo/demo-packet/frozen/" + HashA[7..] + ".json", AlreadyFrozen: false),
            "a first record lands at <owner>/<name>/<packet>/frozen/<hex>.json and isn't already frozen");
        var stored = ReadBack(log);
        Expect.That(stored.Count == 1 && Same(ARecord(), stored[0]), "the stored record reads back unchanged");
    }

    /// <summary>Recording the same hash again writes nothing and says so.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_same_hash_again_is_already_frozen_and_changes_nothing()
    {
        var log = Create();
        _ = await log.Record(ARecord(), CancellationToken.None);

        var again = await log.Record(ARecord() with { FrozenAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero) }, CancellationToken.None);

        Expect.That(again.IsSuccess && again.Value.AlreadyFrozen, "the same hash again is already frozen");
        var stored = ReadBack(log);
        Expect.That(stored.Count == 1 && stored[0].FrozenAt == ARecord().FrozenAt, "the first record is kept unchanged");
    }

    /// <summary>A second hash is a second record.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_new_hash_is_a_new_record()
    {
        var log = Create();
        _ = await log.Record(ARecord(), CancellationToken.None);

        var second = await log.Record(ARecord(HashB), CancellationToken.None);

        Expect.That(second.IsSuccess && !second.Value.AlreadyFrozen && ReadBack(log).Count == 2, "a second hash is a second record");
    }

    /// <summary>The same packet id and hash in two repositories are two records.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_same_packet_in_two_repos_is_two_records()
    {
        var log = Create();
        _ = await log.Record(ARecord(), CancellationToken.None);

        var other = await log.Record(ARecord() with { Repo = RepoSlug.From("other/repo") }, CancellationToken.None);

        Expect.That(other.IsSuccess && !other.Value.AlreadyFrozen && ReadBack(log).Count == 2, "another repo's packet with the same id and hash is its own record");
    }

    /// <summary>Find returns the record by repository, packet and hash.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Find_returns_the_record_of_that_hash_and_nothing_for_another()
    {
        var log = Create();
        _ = await log.Record(ARecord() with { By = FreezeBy.Owner }, CancellationToken.None);
        var identity = new PacketIdentity(RepoSlug.From("example/repo"), PacketId.From("demo-packet"));

        var found = await log.Find(identity, PacketHash.From(HashA), CancellationToken.None);
        var otherHash = await log.Find(identity, PacketHash.From(HashB), CancellationToken.None);
        var otherRepo = await log.Find(identity with { Repo = RepoSlug.From("other/repo") }, PacketHash.From(HashA), CancellationToken.None);
        var otherPacket = await log.Find(identity with { Packet = PacketId.From("other-packet") }, PacketHash.From(HashA), CancellationToken.None);

        Expect.That(found.IsSuccess && found.Value is { } record && Same(ARecord(), record) && record.By == FreezeBy.Owner, "the recorded freeze is found, with who froze it");
        Expect.That(otherHash.IsSuccess && otherHash.Value is null, "another hash of the packet was never frozen");
        Expect.That(otherRepo.IsSuccess && otherRepo.Value is null, "the same packet id in another repository is another packet");
        Expect.That(otherPacket.IsSuccess && otherPacket.Value is null, "another packet is not found");
    }

    /// <summary>A log that can't write fails with AW-PACKETS-007.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unwritable_log_is_AW_PACKETS_007()
    {
        var result = await CreateUnwritable().Record(ARecord(), CancellationToken.None);

        Expect.That(result.IsError && result.Error.Code == PacketsErrors.FreezeLogUnavailable.Code, "an unwritable log is AW-PACKETS-007");
    }

    /// <summary>Whether two records hold the same values (record equality compares the file lists by reference).</summary>
    /// <param name="x">One record.</param>
    /// <param name="y">The other.</param>
    /// <returns>True when every field matches.</returns>
    protected static bool Same(FreezeRecord x, FreezeRecord y) =>
        x.SchemaVersion == y.SchemaVersion && x.Repo == y.Repo && x.Packet == y.Packet && x.Hash == y.Hash && x.DigestVersion == y.DigestVersion
        && x.BaseSha == y.BaseSha && x.FrozenAt == y.FrozenAt && x.Files.SequenceEqual(y.Files, StringComparer.Ordinal);

    /// <summary>Creates an empty log.</summary>
    /// <returns>The log under test.</returns>
    protected abstract IFreezeLog Create();

    /// <summary>Creates a log whose writes fail.</summary>
    /// <returns>The log under test.</returns>
    protected abstract IFreezeLog CreateUnwritable();

    /// <summary>Reads back every record the log stored.</summary>
    /// <param name="log">A log from <see cref="Create"/>.</param>
    /// <returns>The records.</returns>
    protected abstract IReadOnlyList<FreezeRecord> ReadBack(IFreezeLog log);
}
