using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>A question deferred past the desk: who owns it and what the build may assume meanwhile.</summary>
/// <param name="Owner">Who answers it later.</param>
/// <param name="BuildMayAssume">The default the build may assume until then.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Deferral(string Owner, string BuildMayAssume);
