using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>The workstreams and tasks file.</summary>
/// <param name="SchemaVersion">The file's schema version; this build reads version 1.</param>
/// <param name="Workstreams">The workstreams.</param>
/// <param name="Tasks">The tasks; each names its workstream.</param>
/// <param name="Schema">An editor hint ('$schema'); not checked.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TaskPlan(int SchemaVersion, IReadOnlyList<Workstream> Workstreams, IReadOnlyList<PacketTask> Tasks, [property: JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Schema = null);
