namespace Aw.Packets.Core.Model;

/// <summary>The files a packet manifest links to, each relative to the packet directory.</summary>
public sealed record PacketLinks
{
    /// <summary>The decision log (JSON, <see cref="DecisionLog"/>).</summary>
    public required string Decisions { get; init; }

    /// <summary>The out-of-scope list (JSON, <see cref="OutOfScope"/>).</summary>
    public required string OutOfScope { get; init; }

    /// <summary>The workstreams and tasks (JSON, <see cref="TaskPlan"/>).</summary>
    public required string Tasks { get; init; }

    /// <summary>Design docs, ADRs, product-graph and data-model deltas (any format; hashed by content).</summary>
    public required IReadOnlyList<string> Design { get; init; }

    /// <summary>Generated plan files (any format; hashed by content).</summary>
    public required IReadOnlyList<string> Plans { get; init; }

    /// <summary>The frozen workflow (addendum §17.7), once workflow generation exists; hashed by content.</summary>
    public string? Workflow { get; init; }
}
