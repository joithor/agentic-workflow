namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The child a parked instance points at.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="Address">Address, as served.</param>
/// <param name="State">State, as served.</param>
/// <param name="Reason">Reason, as served.</param>
internal sealed record StatusChild(
    string Id,
    string Address,
    string State,
    string? Reason);
