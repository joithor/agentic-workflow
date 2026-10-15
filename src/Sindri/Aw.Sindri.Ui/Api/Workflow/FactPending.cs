namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What a pending fact still needs.</summary>
/// <param name="Required">Required, as served.</param>
/// <param name="Checks">Checks, as served.</param>
/// <param name="CompleteAction">CompleteAction, as served.</param>
internal sealed record FactPending(
    bool Required,
    IReadOnlyList<string> Checks,
    string CompleteAction);
