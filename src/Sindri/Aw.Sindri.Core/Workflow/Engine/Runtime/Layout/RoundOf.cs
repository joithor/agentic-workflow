namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>A loop round counter, 'r2/3' (E-20).</summary>
/// <param name="N">The current round.</param>
/// <param name="Max">The most rounds the loop allows.</param>
public sealed record RoundOf(
    int N,
    int Max);
