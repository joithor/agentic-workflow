using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The reportedId object of an agent entry.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ReportedIdSection
{
    public bool? Hook { get; init; }

    public string? NewestFile { get; init; }

    public string? Capture { get; init; }
}
