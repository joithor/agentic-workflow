using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Line;

/// <summary>A packet fact of the line: the canonical hash and the directory the files are in ('{ "hash": "sha256:…", "dir": "/…" }', the shape the spec's phase and line facts use).</summary>
/// <param name="Hash">The canonical hash, 'sha256:…'.</param>
/// <param name="Directory">The packet directory.</param>
internal sealed record PacketFact(string Hash, string Directory)
{
    /// <summary>Reads a packet fact; a bare hash string has no directory, so its files cannot be read.</summary>
    /// <param name="fact">The fact's tree.</param>
    /// <param name="key">The fact's key, for the message.</param>
    /// <returns>The packet; an error naming the fix when the fact holds no hash or no directory.</returns>
    internal static Result<PacketFact> Of(JsonNode? fact, string key)
    {
        var hash = fact is JsonValue value && value.TryGetValue<string>(out var text) ? text : LineJson.Str(fact, "hash");
        if (string.IsNullOrWhiteSpace(hash))
        {
            return SindriErrors.InvalidInput.With($"fact:{key} holds no packet hash (fix: the station that made it must write {{ \"hash\": \"sha256:…\", \"dir\": … }})");
        }

        return LineJson.Str(fact, "dir") is { Length: > 0 } directory
            ? new PacketFact(hash, directory)
            : SindriErrors.InvalidInput.With($"fact:{key} names no packet directory, so its files cannot be read (fix: the station that froze or assembled it must write {{ \"hash\": \"{hash}\", \"dir\": … }})");
    }

    /// <summary>The fact's JSON.</summary>
    /// <returns>The object.</returns>
    internal JsonObject ToJson() => new() { ["hash"] = Hash, ["dir"] = Directory };
}
