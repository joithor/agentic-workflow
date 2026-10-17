namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The PR hand-off's links.</summary>
public sealed record HandoffLinks : ILineLinks
{
    /// <summary>pr.json.</summary>
    public required string Pr { get; init; }

    /// <summary>body.md.</summary>
    public required string Body { get; init; }

    /// <summary>green.json.</summary>
    public required string Green { get; init; }

    /// <summary>index.json.</summary>
    public required string Index { get; init; }

    /// <inheritdoc />
    IReadOnlyDictionary<string, IReadOnlyList<string>> ILineLinks.Entries() => LinkMap.Of(("pr", Pr), ("body", Body), ("green", Green), ("index", Index));
}
