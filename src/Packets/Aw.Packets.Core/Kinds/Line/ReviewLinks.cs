namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The review result's links.</summary>
public sealed record ReviewLinks : ILineLinks
{
    /// <summary>review.json.</summary>
    public required string Review { get; init; }

    /// <summary>index.json.</summary>
    public required string Index { get; init; }

    /// <inheritdoc />
    IReadOnlyDictionary<string, IReadOnlyList<string>> ILineLinks.Entries() => LinkMap.Of(("review", Review), ("index", Index));
}
