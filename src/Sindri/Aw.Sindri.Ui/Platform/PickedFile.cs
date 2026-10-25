namespace Aw.Sindri.Ui.Platform;

/// <summary>A file the human chose.</summary>
/// <param name="Name">The file's name, without its folder.</param>
/// <param name="Size">Its size in bytes.</param>
/// <param name="Content">Its bytes; empty when the file is larger than the limit that was asked for.</param>
public sealed record PickedFile(string Name, long Size, ReadOnlyMemory<byte> Content);
