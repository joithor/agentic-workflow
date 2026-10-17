namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The proof pack's links: its files, and the artifacts under 'app-run/' and 'evidence/' one by one.</summary>
public sealed record ProofLinks : ILineLinks
{
    /// <summary>proof.json.</summary>
    public required string Proof { get; init; }

    /// <summary>red.json.</summary>
    public required string Red { get; init; }

    /// <summary>before-after.json.</summary>
    public required string BeforeAfter { get; init; }

    /// <summary>holdout.json.</summary>
    public required string Holdout { get; init; }

    /// <summary>not-proven.md.</summary>
    public required string NotProven { get; init; }

    /// <summary>acceptance.json.</summary>
    public required string Acceptance { get; init; }

    /// <summary>index.json.</summary>
    public required string Index { get; init; }

    /// <summary>Every file under 'app-run/' (any format; hashed by content).</summary>
    public required IReadOnlyList<string> AppRun { get; init; }

    /// <summary>Every file under 'evidence/' (any format; hashed by content).</summary>
    public required IReadOnlyList<string> Evidence { get; init; }

    /// <inheritdoc />
    IReadOnlyDictionary<string, IReadOnlyList<string>> ILineLinks.Entries() => LinkMap.OfLists(
        [("proof", Proof), ("red", Red), ("beforeAfter", BeforeAfter), ("holdout", Holdout), ("notProven", NotProven), ("acceptance", Acceptance), ("index", Index)],
        ("appRun", AppRun), ("evidence", Evidence));
}
