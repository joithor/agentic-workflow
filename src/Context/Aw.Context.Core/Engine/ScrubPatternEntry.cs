using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>One 'context.scrub.extraPatterns' entry as written.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ScrubPatternEntry(string? Id = null, string? Pattern = null);
