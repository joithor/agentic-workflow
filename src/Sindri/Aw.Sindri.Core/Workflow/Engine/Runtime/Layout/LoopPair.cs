namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>One loop a state belongs to, 'mitigate and verify' (E-20).</summary>
/// <param name="With">The other state of the pair.</param>
/// <param name="N">The current round.</param>
/// <param name="Max">The most rounds the loop allows.</param>
public sealed record LoopPair(
    string With,
    int N,
    int Max);
