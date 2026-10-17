using Aw.Context.Core.Linking;

namespace Aw.Context.Core.Lenses;

/// <summary>The rows of <see cref="LensSearched.Sources"/>.</summary>
public static class SearchedSources
{
    /// <summary>The row of an intent source: not-configured when the profile doesn't name it, error with the last run's code when it failed, unavailable ('not-synced') before the first sync, else ok at its last sync.</summary>
    /// <param name="id">The source ('onyx').</param>
    /// <param name="configured">Whether the profile names it.</param>
    /// <param name="state">What its sync stored, or null when it has no state yet.</param>
    /// <returns>The row.</returns>
    public static SearchedSource Intent(Sources.SourceId id, bool configured, IntentSourceState? state) =>
        throw new NotSupportedException("red");
}
