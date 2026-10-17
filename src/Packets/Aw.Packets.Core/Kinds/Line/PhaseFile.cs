using System.Text.Json.Serialization;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>phase.json (line spec §3.2 row 4, §6.2): one phase's contract. An amended phase packet adds 'amends'.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PhaseFile
{
    /// <summary>An editor hint ('$schema'); not checked and not part of the hash.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The file's schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>What the phase achieves.</summary>
    public required string Goal { get; init; }

    /// <summary>The phase kind.</summary>
    public required PhaseKind Kind { get; init; }

    /// <summary>The phases it builds on (their plan ids).</summary>
    public required IReadOnlyList<string> Deps { get; init; }

    /// <summary>Its size class.</summary>
    public required SizeClass Size { get; init; }

    /// <summary>How its base is chosen.</summary>
    public required PhaseBase Base { get; init; }

    /// <summary>Repo-relative globs it may write, or 'new: &lt;path&gt;' for a file it creates.</summary>
    public required IReadOnlyList<string> AllowedFiles { get; init; }

    /// <summary>Existing tests it may change, each with a reason.</summary>
    public required IReadOnlyList<TestChange> TestChange { get; init; }

    /// <summary>Product nodes, files and refs the builder starts from.</summary>
    public required IReadOnlyList<string> ReadSeeds { get; init; }

    /// <summary>Its budget.</summary>
    public required TaskBudget Budget { get; init; }

    /// <summary>Set only on an amended phase packet.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PhaseAmends? Amends { get; init; }
}
