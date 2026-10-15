namespace Aw.Context.Core.Sources;

/// <summary>One search hit. Source scores are discarded; only the rank is kept (spec §8.2).</summary>
/// <param name="Ref">The record it points at.</param>
/// <param name="Title">The record's raw title (the engine sanitizes and scrubs it).</param>
/// <param name="Rank">Its rank in this source's answer, 1 first.</param>
/// <param name="AsOf">When the source's answer was true.</param>
/// <param name="Leaky">True when the source couldn't cut to the query's as-of time.</param>
public sealed record SourceHit(SourceRef Ref, string Title, int Rank, DateTimeOffset AsOf, bool Leaky)
{
    /// <summary>
    /// Other names of the same record that resolve to <see cref="SourceRef.Canonical"/>, such as a Linear identifier 'ENG-482' for
    /// 'linear:issue/&lt;uuid&gt;' (spec §5.1, D16); empty when the source gives none it can vouch for.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];
}
