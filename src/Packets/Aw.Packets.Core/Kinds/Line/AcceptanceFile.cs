using System.Text.Json.Serialization;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>acceptance.json of a design, phase or proof packet (line spec LC-2): the items, and the hash of the acceptance.json it extends.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcceptanceFile
{
    /// <summary>An editor hint ('$schema'); not checked and not part of the hash.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The file's schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The 'sha256:' hash of the previous acceptance.json in the chain.</summary>
    public required PacketHash Parent { get; init; }

    /// <summary>The items.</summary>
    public required IReadOnlyList<AcceptanceItem> Items { get; init; }
}
