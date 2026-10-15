using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.sources.linear' as this build reads it: the team keys an Onyx identifier alias may name.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record LinearSection(IReadOnlyList<string>? Teams = null);
