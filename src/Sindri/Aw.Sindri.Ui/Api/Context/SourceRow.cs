using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One source of the Sources panel.</summary>
/// <param name="Source">Source, as served.</param>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Repo">Repo, as served.</param>
/// <param name="State">State, as served.</param>
/// <param name="Detail">Detail, as served.</param>
/// <param name="Code">Code, as served.</param>
/// <param name="LastSync">LastSync, as served.</param>
/// <param name="Records">Records, as served.</param>
/// <param name="Sync">Sync, as served.</param>
internal sealed record SourceRow(
    string Source,
    string Kind,
    string? Repo,
    string State,
    string Detail,
    string? Code,
    DateTimeOffset? LastSync,
    int Records,
    JsonElement? Sync = null);
