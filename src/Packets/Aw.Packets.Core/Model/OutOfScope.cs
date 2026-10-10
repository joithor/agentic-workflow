using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>The explicit out-of-scope list the scope-expansion check reads.</summary>
/// <param name="SchemaVersion">The file's schema version; this build reads version 1.</param>
/// <param name="Items">What the packet deliberately doesn't do.</param>
/// <param name="Schema">An editor hint ('$schema'); not checked.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OutOfScope(int SchemaVersion, IReadOnlyList<string> Items, [property: JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Schema = null);
