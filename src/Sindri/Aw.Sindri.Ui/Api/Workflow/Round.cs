namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A round of a looping state.</summary>
/// <param name="N">N, as served.</param>
/// <param name="Max">Max, as served.</param>
internal sealed record Round(
    int N,
    int Max);
