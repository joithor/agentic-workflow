namespace Aw.Packets.Core.Kinds.Line;

/// <summary>A line packet directory as read: the listed files and the files of the kind's linked directories, and the entries the kind doesn't list.</summary>
/// <param name="Present">Each listed file that exists, and each file under a linked directory, by path relative to the packet directory ('/' separators), with its bytes.</param>
/// <param name="Unlisted">The directory's other top-level entries (a sub-directory ends in '/'), sorted by ordinal.</param>
public sealed record LinePacketFiles(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Present, IReadOnlyList<string> Unlisted);
