namespace Aw.Packets.Core.Kinds.Line;

/// <summary>The plan graph's links; 'tasks' is PKT's own links.tasks (LN10), the task plan, one task per phase.</summary>
public sealed record PlanLinks : ILineLinks
{
    /// <summary>tasks.json: PKT's task plan.</summary>
    public required string Tasks { get; init; }

    /// <summary>graph.json.</summary>
    public required string Graph { get; init; }

    /// <summary>exec-review.json.</summary>
    public required string ExecReview { get; init; }

    /// <summary>holdout.json.</summary>
    public required string Holdout { get; init; }

    /// <summary>answers.jsonl.</summary>
    public required string Answers { get; init; }

    /// <summary>index.json.</summary>
    public required string Index { get; init; }

    /// <inheritdoc />
    IReadOnlyDictionary<string, IReadOnlyList<string>> ILineLinks.Entries() => LinkMap.Of(
        ("tasks", Tasks), ("graph", Graph), ("execReview", ExecReview), ("holdout", Holdout), ("answers", Answers), ("index", Index));
}
