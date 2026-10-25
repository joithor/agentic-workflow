namespace Aw.Context.Core.ConceptScan;

/// <summary>Reads the two model replies; a reply that does not read finds nothing.</summary>
public static class ConceptScanReplies
{
    /// <summary>The concepts the first pass proposed.</summary>
    /// <param name="reply">The model's text.</param>
    /// <returns>The candidates.</returns>
    public static IReadOnlyList<ConceptCandidate> Candidates(string reply) => throw new NotSupportedException();

    /// <summary>The concept hints the second pass returned.</summary>
    /// <param name="reply">The model's text.</param>
    /// <param name="featureKeys">The keys of the draft's features.</param>
    /// <returns>The hints.</returns>
    public static IReadOnlyList<ConceptHint> Hints(string reply, IReadOnlySet<string> featureKeys) => throw new NotSupportedException();
}
