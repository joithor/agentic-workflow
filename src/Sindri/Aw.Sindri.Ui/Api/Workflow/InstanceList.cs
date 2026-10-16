namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /workflow/instances: the instance rows with the root-wide counts.</summary>
/// <param name="Instances">Instances, as served.</param>
/// <param name="Counts">Counts, as served.</param>
/// <param name="Empty">Empty, as served.</param>
/// <param name="AsOf">AsOf, as served.</param>
internal sealed record InstanceList(
    IReadOnlyList<InstanceSummary> Instances,
    ListCounts Counts,
    string? Empty,
    DateTimeOffset AsOf);
