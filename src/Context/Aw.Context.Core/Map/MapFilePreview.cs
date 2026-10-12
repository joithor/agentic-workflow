namespace Aw.Context.Core.Map;

/// <summary>A checked map file before import: what the owner confirms by its hash.</summary>
/// <param name="Path">The file.</param>
/// <param name="Repo">The repo it maps.</param>
/// <param name="Kind">Authored or export.</param>
/// <param name="Hash">The content hash to confirm.</param>
/// <param name="Counts">What it holds.</param>
public sealed record MapFilePreview(string Path, string Repo, MapDocumentKind Kind, string Hash, MapCounts Counts);
