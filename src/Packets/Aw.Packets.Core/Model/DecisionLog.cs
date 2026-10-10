using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>The decision log file.</summary>
/// <param name="SchemaVersion">The file's schema version; this build reads version 1.</param>
/// <param name="Questions">Every question, answered or deferred.</param>
/// <param name="Schema">An editor hint ('$schema'); not checked.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecisionLog(int SchemaVersion, IReadOnlyList<Question> Questions, [property: JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Schema = null);
