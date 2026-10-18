namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The design packet's links: each names the file of that name (spec.md, sections.json, and so on).</summary>
public sealed record DesignLinks : ILineLinks
{
    /// <summary>spec.md.</summary>
    public required string Spec { get; init; }

    /// <summary>sections.json.</summary>
    public required string Sections { get; init; }

    /// <summary>coverage.json.</summary>
    public required string Coverage { get; init; }

    /// <summary>decisions.json.</summary>
    public required string Decisions { get; init; }

    /// <summary>acceptance.json.</summary>
    public required string Acceptance { get; init; }

    /// <summary>signals.json.</summary>
    public required string Signals { get; init; }

    /// <summary>contracts.json.</summary>
    public required string Contracts { get; init; }

    /// <summary>lens-rounds.jsonl.</summary>
    public required string LensRounds { get; init; }

    /// <summary>style.json.</summary>
    public required string Style { get; init; }

    /// <summary>escapes.json.</summary>
    public required string Escapes { get; init; }

    /// <summary>runs.json.</summary>
    public required string Runs { get; init; }

    /// <summary>index.json.</summary>
    public required string Index { get; init; }

    /// <inheritdoc />
    IReadOnlyDictionary<string, IReadOnlyList<string>> ILineLinks.Entries() => LinkMap.Of(
        ("spec", Spec), ("sections", Sections), ("coverage", Coverage), ("decisions", Decisions), ("acceptance", Acceptance),
        ("signals", Signals), ("contracts", Contracts), ("lensRounds", LensRounds), ("style", Style), ("escapes", Escapes),
        ("runs", Runs), ("index", Index));
}
