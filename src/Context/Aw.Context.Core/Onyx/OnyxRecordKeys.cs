using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Onyx;

/// <summary>The canonical key, kind and alias of an Onyx document (spec §5.1, D16).</summary>
public static class OnyxRecordKeys
{
    /// <summary>Identifies one Onyx document.</summary>
    /// <param name="document">Its Onyx document id.</param>
    /// <param name="sourceType">Its connector ('linear', 'slack', …).</param>
    /// <param name="semanticIdentifier">Its title as the connector built it.</param>
    /// <param name="link">Its link, if any.</param>
    /// <param name="teams">The Linear team keys an alias may name.</param>
    /// <returns>The identity, or null when the id can't form a key.</returns>
    public static OnyxRecordIdentity? Identify(ExternalId document, string sourceType, string semanticIdentifier, string? link, IReadOnlySet<string> teams)
    {
        _ = (document, sourceType, semanticIdentifier, link, teams);
        throw new NotSupportedException("red: Onyx record keys");
    }

    /// <summary>The kind of the record a canonical key from <see cref="Identify"/> names.</summary>
    /// <param name="canonical">The key.</param>
    /// <returns>Its kind.</returns>
    public static RecordKind KindOf(RecordKey canonical)
    {
        _ = canonical;
        throw new NotSupportedException("red: Onyx record kinds");
    }
}
