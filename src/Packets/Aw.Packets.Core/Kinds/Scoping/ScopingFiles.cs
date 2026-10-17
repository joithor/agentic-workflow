namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>A scope packet directory as read: the listed files that exist, and the entries the kind doesn't list.</summary>
/// <param name="Present">Each listed file that exists, by name, with its bytes.</param>
/// <param name="Unlisted">The directory's other entries (a sub-directory ends in '/'), sorted by ordinal.</param>
public sealed record ScopingFiles(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Present, IReadOnlyList<string> Unlisted);
