namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What the map covers.</summary>
/// <param name="Paths">Paths, as served.</param>
/// <param name="Truncated">Truncated, as served.</param>
/// <param name="Files">Files, as served.</param>
/// <param name="Unmapped">Unmapped, as served.</param>
/// <param name="Suggested">Suggested, as served.</param>
/// <param name="Created">Created, as served.</param>
/// <param name="AgeDays">AgeDays, as served.</param>
internal sealed record CoverageMap(
    IReadOnlyList<string> Paths,
    bool Truncated,
    int Files,
    int Unmapped,
    int Suggested,
    DateTimeOffset Created,
    int AgeDays);
