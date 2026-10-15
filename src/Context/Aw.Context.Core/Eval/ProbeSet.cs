using System.Text.Json.Serialization;

namespace Aw.Context.Core.Eval;

/// <summary>A probe file (spec §9.2 item 3): questions the owner confirmed once, each with what a right answer names. Unknown keys are refused.</summary>
/// <param name="Probes">The probes, in file order.</param>
/// <param name="SchemaVersion">The file format's version: 1, or null (read as 1).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProbeSet(IReadOnlyList<ProbeCase> Probes, int? SchemaVersion = null);
