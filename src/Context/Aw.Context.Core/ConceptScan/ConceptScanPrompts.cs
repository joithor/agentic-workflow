namespace Aw.Context.Core.ConceptScan;

/// <summary>The two prompts of a scan, each held to <see cref="ConceptScanLimits.MaxInputChars"/>.</summary>
public static class ConceptScanPrompts
{
    /// <summary>How much summary text the first prompt holds.</summary>
    public static int SummaryRoom => throw new NotSupportedException();

    /// <summary>The first pass.</summary>
    /// <param name="summary">The scrubbed summary.</param>
    /// <returns>The prompt.</returns>
    public static string Propose(string summary) => throw new NotSupportedException();

    /// <summary>The second pass.</summary>
    /// <param name="summary">The scrubbed summary.</param>
    /// <param name="candidates">The proposed concepts.</param>
    /// <param name="found">What the searches found.</param>
    /// <returns>The prompt.</returns>
    public static string Conclude(string summary, IReadOnlyList<ConceptCandidate> candidates, IReadOnlyList<ConceptSearchResult> found) => throw new NotSupportedException();
}
