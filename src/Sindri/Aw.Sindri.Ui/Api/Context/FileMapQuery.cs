namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/filemap is asked: a directory of a repo, all of it or only what is unmapped, on the map or its draft.</summary>
/// <param name="Repo">The repo's name.</param>
/// <param name="Path">The directory, or null for the root.</param>
/// <param name="UnmappedOnly">True to list only what is not mapped.</param>
/// <param name="Draft">True to read the draft map.</param>
internal sealed record FileMapQuery(string Repo, string? Path, bool UnmappedOnly, bool Draft);
