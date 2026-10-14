using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.scrub': the profile's extra scrub patterns (spec §8.8, D14).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ScrubSection(IReadOnlyList<ScrubPatternEntry?>? ExtraPatterns = null);
