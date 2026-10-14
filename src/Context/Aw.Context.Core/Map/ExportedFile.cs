namespace Aw.Context.Core.Map;

/// <summary>A file in the exports folder.</summary>
/// <param name="Path">Its absolute path.</param>
/// <param name="Written">When it was last written.</param>
public sealed record ExportedFile(string Path, DateTimeOffset Written);
