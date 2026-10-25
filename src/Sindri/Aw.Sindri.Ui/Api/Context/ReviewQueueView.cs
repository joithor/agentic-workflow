using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>GET /sindri/context/map/review: the draft's undecided entries.</summary>
/// <param name="Draft">Draft, as served.</param>
/// <param name="Version">Version, as served.</param>
/// <param name="Entries">Entries, as served.</param>
/// <param name="Decided">Decided, as served.</param>
/// <param name="Proposals">Proposals, as served.</param>
/// <param name="Counts">Counts, as served.</param>
/// <param name="ByKind">ByKind, as served.</param>
/// <param name="Truncated">Truncated, as served.</param>
/// <param name="Undecided">Undecided, as served.</param>
internal sealed record ReviewQueueView(
    string Draft,
    ReviewVersion Version,
    IReadOnlyList<ReviewEntry> Entries,
    int Decided,
    int Proposals,
    ReviewCounts Counts,
    JsonElement ByKind,
    ReviewTruncated Truncated,
    int Undecided);
