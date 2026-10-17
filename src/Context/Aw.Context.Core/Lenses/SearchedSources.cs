using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Lenses;

/// <summary>The rows of <see cref="LensSearched.Sources"/>.</summary>
public static class SearchedSources
{
    /// <summary>The row of an intent source: not-configured when the profile doesn't name it, error with the last run's code when it failed, unavailable ('not-synced') before the first sync, else ok at its last sync.</summary>
    /// <param name="id">The source ('onyx').</param>
    /// <param name="configured">Whether the profile names it.</param>
    /// <param name="state">What its sync stored, or null when it has no state yet.</param>
    /// <returns>The row.</returns>
    public static SearchedSource Intent(SourceId id, bool configured, IntentSourceState? state)
    {
        if (!configured)
        {
            return new SearchedSource(id, SearchedState.NotConfigured, NotConfigured, Code: null, AsOf: null);
        }

        if (state is null || (state.LastSync is null && state.LastError is null))
        {
            return new SearchedSource(id, SearchedState.Unavailable, "not-synced", Code: null, AsOf: null);
        }

        // A capped run (059) or held-back extraction (042) is partial, not a failure: the source still answers.
        var failed = state.LastError is { } code && code != ContextErrors.SyncCapped.Code && code != ContextErrors.LabelingUnavailable.Code;
        return new SearchedSource(id, failed ? SearchedState.Error : SearchedState.Ok, Reason: null, state.LastError, state.LastSync);
    }

    /// <summary>The map's row: ok, as of the version's creation.</summary>
    internal static SearchedSource Map(DateTimeOffset created) => new(SourceId.From("map"), SearchedState.Ok, Reason: null, Code: null, created);

    /// <summary>The code index's row: not built until the code stage ships it.</summary>
    internal static SearchedSource CodeIndex { get; } = new(SourceId.From("code-index"), SearchedState.Unavailable, "index-not-built", Code: null, AsOf: null);

    private const string NotConfigured = "not-configured";
}
