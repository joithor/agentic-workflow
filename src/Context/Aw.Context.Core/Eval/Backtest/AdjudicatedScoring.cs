using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Which predicted nodes the adjudicator may judge, and the precision once it has (spec §9.1). Pure.</summary>
public static class AdjudicatedScoring
{
    /// <summary>The surfaces to judge: predicted nodes outside the truth. Truth nodes are never judged: the adjudicator only labels surfaces no PR touched.</summary>
    /// <param name="predicted">The predicted node keys.</param>
    /// <param name="truth">The truth node keys.</param>
    /// <param name="graph">The graph the labels come from.</param>
    /// <returns>The surfaces, in key order.</returns>
    public static IReadOnlyList<Surface> Surfaces(IReadOnlyList<string> predicted, IReadOnlyList<string> truth, GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(predicted);
        ArgumentNullException.ThrowIfNull(truth);
        ArgumentNullException.ThrowIfNull(graph);
        return [.. predicted.Where(k => !truth.Contains(k, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(k => new Surface(k, graph.Nodes.FirstOrDefault(n => string.Equals(n.Key.Value, k, StringComparison.Ordinal))?.Label ?? k))];
    }

    /// <summary>Precision with the judged-relevant surfaces counted as hits.</summary>
    /// <param name="predicted">The predicted node keys.</param>
    /// <param name="truth">The truth node keys.</param>
    /// <param name="verdicts">The verdicts by key.</param>
    /// <returns>(truth hits + relevant surfaces) over predicted; null when nothing was predicted or any surface is unjudged (not measured).</returns>
    public static double? Precision(IReadOnlyList<string> predicted, IReadOnlyList<string> truth, IReadOnlyDictionary<string, Verdict> verdicts)
    {
        ArgumentNullException.ThrowIfNull(predicted);
        ArgumentNullException.ThrowIfNull(truth);
        ArgumentNullException.ThrowIfNull(verdicts);
        if (predicted.Count == 0 || predicted.Any(k => !truth.Contains(k, StringComparer.Ordinal) && (!verdicts.TryGetValue(k, out var v) || v == Verdict.Unjudged)))
        {
            return null;
        }

        var hits = predicted.Count(k => truth.Contains(k, StringComparer.Ordinal) || verdicts[k] == Verdict.Relevant);
        return Math.Round((double)hits / predicted.Count, 4, MidpointRounding.ToEven);
    }
}
