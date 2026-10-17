using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The viewer section: accepted and not read (SL-2 reads it).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ViewerSection(string Kind)
{
    public string? Name { get; init; }

    public IReadOnlyList<string>? Argv { get; init; }
}
