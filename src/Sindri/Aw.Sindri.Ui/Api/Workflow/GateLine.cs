namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One gate of a state.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="On">On, as served.</param>
/// <param name="Threshold">Threshold, as served.</param>
/// <param name="Description">Description, as served.</param>
internal sealed record GateLine(
    string Id,
    string On,
    double? Threshold,
    string Description);
