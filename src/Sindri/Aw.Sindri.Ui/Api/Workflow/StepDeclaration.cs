namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The declaration of the step behind a state.</summary>
/// <param name="Kind">Kind, as served.</param>
/// <param name="Ref">Ref, as served.</param>
/// <param name="Declaration">Declaration, as served.</param>
internal sealed record StepDeclaration(
    string Kind,
    string Ref,
    string Declaration);
