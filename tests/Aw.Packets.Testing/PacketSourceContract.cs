using Aw.Packets.Core;
using Xunit;

namespace Aw.Packets.Testing;

/// <summary>The IPacketSource contract, run against the in-memory fake and the disk adapter.</summary>
public abstract class PacketSourceContract
{
    /// <summary>A packet directory loads with every linked file and the parsed model.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_packet_directory_loads_with_every_linked_file()
    {
        var packet = A.Packet().Build();
        var files = PacketFiles.Of(packet);

        var loaded = await Create(files).Load(Directory, CancellationToken.None);

        Expect.That(loaded.IsSuccess, "a well-formed packet loads");
        Expect.That(loaded.Value.Files.Keys.Order(StringComparer.Ordinal).SequenceEqual(files.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal),
            "every linked file is loaded, keyed by its relative path");
        Expect.That(files.All(f => loaded.Value.Files[f.Key].Span.SequenceEqual(f.Value)), "each file's bytes are as written");
        Expect.That(loaded.Value.Packet.Manifest.Id == packet.Manifest.Id && loaded.Value.Packet.Tasks.Tasks.Count == packet.Tasks.Tasks.Count,
            "the parsed model matches");
    }

    /// <summary>A directory with no packet.json is AW-PACKETS-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task No_packet_json_is_AW_PACKETS_002()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        _ = files.Remove("packet.json");

        var loaded = await Create(files).Load(Directory, CancellationToken.None);

        Expect.That(loaded.IsError && loaded.Error.Code == PacketsErrors.NotFound.Code, "no packet.json is AW-PACKETS-002");
    }

    /// <summary>A directory that doesn't exist is AW-PACKETS-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_directory_is_AW_PACKETS_002()
    {
        var loaded = await Create(PacketFiles.Of(A.Packet().Build())).Load(MissingDirectory, CancellationToken.None);

        Expect.That(loaded.IsError && loaded.Error.Code == PacketsErrors.NotFound.Code, "a missing directory is AW-PACKETS-002");
    }

    /// <summary>A missing linked file is AW-PACKETS-003.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_linked_file_is_AW_PACKETS_003()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        _ = files.Remove("tasks.json");

        var loaded = await Create(files).Load(Directory, CancellationToken.None);

        Expect.That(loaded.IsError && loaded.Error.Code == PacketsErrors.Malformed.Code, "a missing linked file is AW-PACKETS-003");
    }

    /// <summary>A link that leaves the packet directory is AW-PACKETS-003, whatever reads the files.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_link_leaving_the_directory_is_AW_PACKETS_003()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        files["packet.json"] = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Encoding.UTF8.GetString(files["packet.json"]).Replace("\"decisions.json\"", "\"../decisions.json\"", StringComparison.Ordinal));

        var loaded = await Create(files).Load(Directory, CancellationToken.None);

        Expect.That(loaded.IsError && loaded.Error.Code == PacketsErrors.Malformed.Code, "a link outside the directory is AW-PACKETS-003");
    }

    /// <summary>A file at a schemaVersion this build doesn't read is AW-PACKETS-004.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unsupported_schema_version_is_AW_PACKETS_004()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        files["tasks.json"] = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Encoding.UTF8.GetString(files["tasks.json"]).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));

        var loaded = await Create(files).Load(Directory, CancellationToken.None);

        Expect.That(loaded.IsError && loaded.Error.Code == PacketsErrors.UnsupportedSchema.Code, "schemaVersion 2 is AW-PACKETS-004");
    }

    /// <summary>The packet directory this contract loads.</summary>
    protected abstract string Directory { get; }

    /// <summary>A packet directory that doesn't exist.</summary>
    protected abstract string MissingDirectory { get; }

    /// <summary>Creates the source with <paramref name="files"/> at <see cref="Directory"/>.</summary>
    /// <param name="files">The files by relative path.</param>
    /// <returns>The source under test.</returns>
    protected abstract IPacketSource Create(IReadOnlyDictionary<string, byte[]> files);
}
