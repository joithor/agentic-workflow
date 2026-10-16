namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The open desk task's form.</summary>
/// <param name="Run">Run, as served.</param>
/// <param name="Outputs">Outputs, as served.</param>
/// <param name="Checks">Checks, as served.</param>
/// <param name="CompleteAction">CompleteAction, as served.</param>
internal sealed record DeskForm(
    string Run,
    IReadOnlyList<DeskOutput> Outputs,
    IReadOnlyList<string> Checks,
    string CompleteAction);
