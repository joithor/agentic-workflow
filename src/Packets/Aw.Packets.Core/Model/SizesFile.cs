using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>
/// The sizes file 'validate --sizes' reads (line spec LN-PKT3, PKT-SIZE-001): what each size class allows a task.
/// Sindri writes it from its profile's workflow.plan.sizes, so tier-1 aw-packets never reads the profile: this
/// package owns the shape, the profile the values.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SizesFile
{
    /// <summary>An editor hint ('$schema'); not checked.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The file's schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The limits of every size class.</summary>
    public required SizeLimits Sizes { get; init; }
}
