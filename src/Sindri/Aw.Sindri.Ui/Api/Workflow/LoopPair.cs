namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A loop between two states.</summary>
/// <param name="With">With, as served.</param>
/// <param name="N">N, as served.</param>
/// <param name="Max">Max, as served.</param>
internal sealed record LoopPair(
    string With,
    int N,
    int Max);
