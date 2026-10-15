namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One fact of an instance.</summary>
/// <param name="Key">Key, as served.</param>
/// <param name="Label">Label, as served.</param>
/// <param name="Type">Type, as served.</param>
/// <param name="Direction">Direction, as served.</param>
/// <param name="From">From, as served.</param>
/// <param name="Status">Status, as served.</param>
/// <param name="Summary">Summary, as served.</param>
/// <param name="At">At, as served.</param>
/// <param name="By">By, as served.</param>
/// <param name="Ref">Ref, as served.</param>
/// <param name="Progress">Progress, as served.</param>
/// <param name="Pending">Pending, as served.</param>
internal sealed record FactRow(
    string Key,
    string Label,
    FactType Type,
    FactDirection Direction,
    string? From,
    FactStatus Status,
    string Summary,
    DateTimeOffset? At,
    string? By,
    FactRef? Ref,
    Progress? Progress,
    FactPending? Pending);
