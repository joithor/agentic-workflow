using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The 'briefSource' fact (scoping spec §8.2): the brief file as 'read-brief' takes it, built the same way at the desk and on the block runtime.</summary>
internal static class BriefSource
{
    /// <summary>The path the scope input names, or null when it names none.</summary>
    internal static string? PathOf(JsonNode? scopeInput) => ScopeJson.Str(scopeInput, "path") is { Length: > 0 } path ? path : null;

    /// <summary>The fact's JSON text: the path, the text, the version and when it was read.</summary>
    internal static string Json(BriefFile file, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new JsonObject
        {
            ["path"] = file.Path,
            ["text"] = file.Text,
            ["version"] = file.Version,
            ["fetchedAt"] = at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        }.ToJsonString();
    }
}
