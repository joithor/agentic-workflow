using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>A citation's state from its version check (spec §8.4).</summary>
public static class CitationStates
{
    /// <summary>The state of a reference cited at <paramref name="cited"/>.</summary>
    /// <param name="cited">The version the result cites.</param>
    /// <param name="checkedVersion">The source's answer: true with the current version (null when the record is gone), false when the check didn't run in time.</param>
    /// <param name="blocked">True when the scrubber withheld the record's text.</param>
    /// <returns>The state, and the current version when it is stale.</returns>
    public static (CitationState State, SourceVersion? Current) Of(SourceVersion cited, (bool Ran, SourceVersion? Current) checkedVersion, bool blocked) =>
        (blocked, checkedVersion) switch
        {
            (true, _) => (CitationState.ScrubBlocked, null),
            (_, (false, _)) => (CitationState.Unchecked, null),
            (_, (true, { } now)) when now == cited => (CitationState.Current, null),
            (_, (true, var now)) => (CitationState.Stale, now),
        };
}
