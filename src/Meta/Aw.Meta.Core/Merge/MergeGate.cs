namespace Aw.Meta.Core.Merge;

/// <summary>The merge preconditions.</summary>
public static class MergeGate
{
    /// <summary>Evaluates every precondition: gate, gate-result, head, main, order, draft.</summary>
    /// <param name="facts">What is known about the pull request and main.</param>
    /// <returns>One check per precondition, in that order.</returns>
    public static IReadOnlyList<MergeCheck> Evaluate(MergeFacts facts) => throw new NotSupportedException("MergeGate: make MergeGateTests pass");
}
