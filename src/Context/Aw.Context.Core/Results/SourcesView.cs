namespace Aw.Context.Core.Results;

/// <summary>The sources' state (spec §8.5 'context sources status').</summary>
/// <param name="Sources">One row per source and repo.</param>
/// <param name="SessionsEnabled">Whether session callers may read context ('context.sessions.enabled').</param>
/// <param name="AsOf">When it was assembled.</param>
public sealed record SourcesView(IReadOnlyList<SourceStatusRow> Sources, bool SessionsEnabled, DateTimeOffset AsOf);
