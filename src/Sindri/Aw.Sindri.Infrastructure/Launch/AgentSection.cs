using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// An agent entry or an override of a built-in one. Fields where "absent" and "null" mean different things when an override is merged
/// over a built-in one are kept as <see cref="JsonElement"/>: <see langword="default"/> (undefined) is absent.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AgentSection
{
    public bool? Override { get; init; }

    public string? Label { get; init; }

    public string? Bin { get; init; }

    public IReadOnlyList<string>? Open { get; init; }

    public JsonElement Resume { get; init; }

    public string? SessionId { get; init; }

    public JsonElement SessionIdPattern { get; init; }

    public JsonElement ReportedId { get; init; }

    public JsonElement ProcessName { get; init; }

    public JsonElement Transcripts { get; init; }

    public string? Usage { get; init; }

    public string? Mcp { get; init; }

    /// <summary>The per-run model form (scoping-station spec L-17): read and checked here, applied by the launcher; a null is not absent.</summary>
    public JsonElement Model { get; init; }

    /// <summary>The per-launch policy form (scoping-station spec L-17): read and checked here, applied by the launcher.</summary>
    public JsonElement Policy { get; init; }
}
