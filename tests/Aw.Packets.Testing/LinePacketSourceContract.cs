using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Xunit;

namespace Aw.Packets.Testing;

/// <summary>The ILinePacketSource contract, run against the in-memory fake and the disk adapter.</summary>
public abstract class LinePacketSourceContract
{
    /// <summary>Every listed file is read with its bytes, and nothing is unlisted.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_phase_packet_reads_all_its_files()
    {
        var files = A.LinePacket(PacketKinds.Phase).Build();

        var read = await Create(files).Read(PacketKinds.Phase, Directory, CancellationToken.None);

        Expect.That(read.IsSuccess, "a well-formed directory reads");
        Expect.That(read.Value.Present.Count == PacketKinds.Phase.Files.Count, "every listed file is present");
        Expect.That(files.All(f => read.Value.Present[f.Key].Span.SequenceEqual(f.Value)), "each file's bytes are as written");
        Expect.That(read.Value.Unlisted.Count == 0, "nothing else is in the directory");
    }

    /// <summary>A proof pack's files under app-run/ and evidence/ are read, keyed by relative path, and are not unlisted.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_files_of_a_linked_directory_are_read_by_relative_path()
    {
        var files = A.LinePacket(PacketKinds.Proof).With("evidence/shots/a.png", "png").Build();

        var read = await Create(files).Read(PacketKinds.Proof, Directory, CancellationToken.None);

        Expect.That(read.IsSuccess, "the directory reads");
        Expect.That(read.Value.Present.ContainsKey("app-run/run.txt") && read.Value.Present.ContainsKey("evidence/shots/a.png"), "linked files are read by their path");
        Expect.That(read.Value.Unlisted.Count == 0, "a linked directory is not unlisted");
    }

    /// <summary>A kind without linked directories reads no sub-directory's files.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_sub_directory_of_a_kind_without_directories_is_unlisted_and_not_read()
    {
        var files = A.LinePacket(PacketKinds.Review).With("evidence/a.png", "png").Build();

        var read = await Create(files).Read(PacketKinds.Review, Directory, CancellationToken.None);

        Expect.That(read.IsSuccess && read.Value.Unlisted.SequenceEqual(["evidence/"], StringComparer.Ordinal) && !read.Value.Present.ContainsKey("evidence/a.png"),
            "the sub-directory is named and not read");
    }

    /// <summary>A missing listed file is absent from the files, not an error: validate reports it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_file_is_absent_not_an_error()
    {
        var files = A.LinePacket(PacketKinds.Design).Without("spec.md").Build();

        var read = await Create(files).Read(PacketKinds.Design, Directory, CancellationToken.None);

        Expect.That(read.IsSuccess && !read.Value.Present.ContainsKey("spec.md") && read.Value.Present.Count == PacketKinds.Design.Files.Count - 1, "the other files are read");
    }

    /// <summary>Other entries are named, sorted, with a sub-directory ending in '/'.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Other_entries_are_unlisted_sorted_and_a_directory_ends_in_a_slash()
    {
        var files = A.LinePacket(PacketKinds.Handoff).With("notes.txt", "x").With("zeta.md", "x").With("attachments/a.png", "x").Build();

        var read = await Create(files).Read(PacketKinds.Handoff, Directory, CancellationToken.None);

        Expect.That(read.IsSuccess && read.Value.Unlisted.SequenceEqual(["attachments/", "notes.txt", "zeta.md"], StringComparer.Ordinal),
            "unlisted entries are the other top-level names, sorted by ordinal");
    }

    /// <summary>A directory that doesn't exist is AW-PACKETS-002.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_directory_is_AW_PACKETS_002()
    {
        var read = await Create(A.LinePacket(PacketKinds.Plan).Build()).Read(PacketKinds.Plan, MissingDirectory, CancellationToken.None);

        Expect.That(read.IsError && read.Error.Code == PacketsErrors.NotFound.Code, "a missing directory is AW-PACKETS-002");
    }

    /// <summary>The packet directory this contract reads.</summary>
    protected abstract string Directory { get; }

    /// <summary>A packet directory that doesn't exist.</summary>
    protected abstract string MissingDirectory { get; }

    /// <summary>Creates the source with <paramref name="files"/> at <see cref="Directory"/>.</summary>
    /// <param name="files">The files by relative path.</param>
    /// <returns>The source under test.</returns>
    protected abstract ILinePacketSource Create(IReadOnlyDictionary<string, byte[]> files);
}
