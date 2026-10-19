using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.extract.graphify' as written: numbers are read as doubles so a fraction is refused with its key path rather than a parser message.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record GraphifySection(double? TimeoutMinutes = null, double? MaxSnapshotMB = null, double? MaxFileMB = null, double? MaxGraphMB = null);
