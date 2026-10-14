namespace Aw.Sindri.Core.Events;

/// <summary>The payload of 'context.map.imported': which version the import made current, from which confirmed file hash (counts only, never map text).</summary>
/// <param name="Version">The new current map version.</param>
/// <param name="Repo">The repo the map describes.</param>
/// <param name="Hash">The map file's content hash the human confirmed.</param>
/// <param name="Nodes">Nodes imported.</param>
/// <param name="Edges">Edges imported.</param>
/// <param name="Overrides">Overrides the file carried.</param>
public sealed record ContextMapImported(int Version, string Repo, string Hash, int Nodes, int Edges, int Overrides);
