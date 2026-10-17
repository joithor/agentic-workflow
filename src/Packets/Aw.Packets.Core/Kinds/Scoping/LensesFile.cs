using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>The scope packet's 'lenses.json': the four lenses' graph parts, frozen at the owner's approval.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LensesFile
{
    /// <summary>An editor hint ('$schema'); not checked and not part of the hash.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The file's schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The code lens.</summary>
    public required LensEntry Code { get; init; }

    /// <summary>The product lens.</summary>
    public required LensEntry Product { get; init; }

    /// <summary>The related lens.</summary>
    public required LensEntry Related { get; init; }

    /// <summary>The history lens.</summary>
    public required LensEntry History { get; init; }
}
