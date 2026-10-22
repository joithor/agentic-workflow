namespace Aw.Context.Core.Ranking;

/// <summary>The rerank arm's answer (spec §8.2 step 6): a model's ordering of the top items, parsed defensively. Pure.</summary>
public static class RerankOrder
{
    /// <summary>The most items the model is asked to order.</summary>
    public const int DefaultTop = 30;

    /// <summary>Reads a JSON array of the numbers 1..<paramref name="asked"/>, best first, e.g. [3,1,2].</summary>
    /// <param name="answer">The model's text (untrusted).</param>
    /// <param name="asked">How many items were listed.</param>
    /// <returns>The zero-based positions, best first: numbers outside 1..asked and repeats are skipped and the items the model left out follow in their old order; null when the text is not a JSON array of numbers or names none of the items.</returns>
    public static IReadOnlyList<int>? Parse(string answer, int asked) => throw new NotSupportedException();

    /// <summary>Puts the first items in the model's order and leaves the rest where they were.</summary>
    /// <param name="items">The items, best first.</param>
    /// <param name="top">How many were reranked.</param>
    /// <param name="order">The positions from <see cref="Parse"/>.</param>
    /// <returns>The reordered items.</returns>
    public static IReadOnlyList<FusedItem> Apply(IReadOnlyList<FusedItem> items, int top, IReadOnlyList<int> order) => throw new NotSupportedException();
}
