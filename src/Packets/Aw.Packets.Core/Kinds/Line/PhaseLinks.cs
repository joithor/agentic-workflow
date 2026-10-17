namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The phase packet's links.</summary>
public sealed record PhaseLinks : ILineLinks
{
    /// <summary>phase.json.</summary>
    public required string Phase { get; init; }

    /// <summary>acceptance.json.</summary>
    public required string Acceptance { get; init; }

    /// <summary>signals.json.</summary>
    public required string Signals { get; init; }

    /// <summary>holdout.json.</summary>
    public required string Holdout { get; init; }

    /// <summary>design.md.</summary>
    public required string Design { get; init; }

    /// <summary>index.json.</summary>
    public required string Index { get; init; }

    /// <inheritdoc />
    IReadOnlyDictionary<string, IReadOnlyList<string>> ILineLinks.Entries() => LinkMap.Of(
        ("phase", Phase), ("acceptance", Acceptance), ("signals", Signals), ("holdout", Holdout), ("design", Design), ("index", Index));
}
