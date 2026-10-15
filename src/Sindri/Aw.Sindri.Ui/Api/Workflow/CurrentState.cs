namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The state an instance is in.</summary>
/// <param name="State">State, as served.</param>
/// <param name="Step">Step, as served.</param>
/// <param name="Gates">Gates, as served.</param>
/// <param name="Desk">Desk, as served.</param>
/// <param name="Status">Status, as served.</param>
internal sealed record CurrentState(
    string State,
    StepDeclaration? Step,
    IReadOnlyList<GateLine> Gates,
    DeskForm? Desk,
    StatusFields Status);
