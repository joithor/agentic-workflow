namespace Aw.Context.Core.Sources;

/// <summary>One search against one source.</summary>
/// <param name="Terms">Plain terms; an adapter quotes each as a phrase and never passes it as query syntax (spec §5.1).</param>
/// <param name="AsOf">Cut the answer to this time, or null for now.</param>
/// <param name="Limit">At most this many hits (the engine caps it at 200).</param>
/// <param name="Deadline">The query's remaining deadline; missing it counts as unavailable for this query.</param>
public sealed record SourceQuery(IReadOnlyList<string> Terms, DateTimeOffset? AsOf, int Limit, TimeSpan Deadline)
{
    /// <summary>Keep only records updated at or after this time, applied by the source before its cap; a source that can't filter by update time ignores it.</summary>
    public DateTimeOffset? UpdatedSince { get; init; }
}
