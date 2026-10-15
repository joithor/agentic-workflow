using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>A citation: one real-world record, every source reference that returned it, and whether it is still current.</summary>
/// <param name="Key">The canonical record key.</param>
/// <param name="Refs">Each source's reference, with its version.</param>
/// <param name="Permalink">A link, kept only for allowed schemes and hosts, or null.</param>
/// <param name="Trust">The trust of its text.</param>
/// <param name="State">Current, stale, unchecked, unstable or scrub-blocked.</param>
public sealed record Citation(RecordKey Key, IReadOnlyList<CitationRef> Refs, Uri? Permalink, Trust Trust, CitationState State)
{
    /// <summary>
    /// The record's other names, such as the Linear identifier 'SPL-103' of 'linear:issue/&lt;uuid&gt;' (spec §5.1, D16),
    /// merged across its refs, each scrubbed on its own like a title (a blocked one is left out); empty when none.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];
}
