namespace Aw.Sindri.Ui.Api.Context;

/// <summary>An unmapped path with its file count.</summary>
/// <param name="Path">Path, as served.</param>
/// <param name="Files">Files, as served.</param>
internal sealed record UnmappedPath(
    string Path,
    int Files);
