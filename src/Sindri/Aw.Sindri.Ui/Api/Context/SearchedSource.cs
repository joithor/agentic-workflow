namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One source the answer searched.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="State">State, as served.</param>
/// <param name="Reason">Reason, as served.</param>
/// <param name="Code">Code, as served.</param>
/// <param name="AsOf">AsOf, as served.</param>
internal sealed record SearchedSource(
    string Id,
    string State,
    string? Reason,
    string? Code,
    DateTimeOffset? AsOf);
