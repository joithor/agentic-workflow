namespace Aw.Context.Features.PreviewMap;

/// <summary>Reads and checks a map file without writing: what the owner confirms by its hash.</summary>
/// <param name="Path">The map file.</param>
internal sealed record PreviewMapRequest(string Path);
