using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The meta section: accepted and not read.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MetaSection
{
    public string? LanesAgent { get; init; }

    public Dictionary<string, string>? Lanes { get; init; }
}
