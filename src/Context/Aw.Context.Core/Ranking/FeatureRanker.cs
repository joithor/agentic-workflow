namespace Aw.Context.Core.Ranking;

/// <summary>Stage 5 of the query (spec §8.2): re-scores the fused items with the features. Pure.</summary>
public static class FeatureRanker
{
    /// <summary>The weighted mean of the features of one record, 0 to 1; 0 when every weight is 0.</summary>
    /// <param name="signals">What the map says about the record.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="now">The query's time.</param>
    /// <returns>The mean.</returns>
    public static double Boost(RecordSignals signals, FeatureWeights weights, DateTimeOffset now) => throw new NotSupportedException();

    /// <summary>Multiplies each item's reciprocal-rank-fusion score by 1 + its <see cref="Boost"/> (so features can at most double a score and never reorder items by more than the fusion's own spread allows), and sorts again.</summary>
    /// <param name="items">The fused items, best first.</param>
    /// <param name="signals">The signals by the item's canonical key; an item with none counts as <see cref="RecordSignals.None"/>.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="now">The query's time.</param>
    /// <returns>The items, best score first (ties keep the fusion's order).</returns>
    public static IReadOnlyList<FusedItem> Apply(IReadOnlyList<FusedItem> items, IReadOnlyDictionary<string, RecordSignals> signals, FeatureWeights weights, DateTimeOffset now) => throw new NotSupportedException();
}
