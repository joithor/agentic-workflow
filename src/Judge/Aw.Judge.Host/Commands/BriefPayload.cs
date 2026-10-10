using System.Text.Json;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.SaveBrief;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// The 'brief save' stdin schema (TS commands.ts BriefSaveInputSchema): an object whose eight fields are all present,
/// each a string (empty allowed) and dispatchName a string or null; other keys are ignored. Read as plain strings
/// first (the typed ids' converters would refuse an empty id), then made typed ids.
/// </summary>
internal static class BriefPayload
{
    private static readonly string[] _required = ["toolUseId", "sessionId", "promptId", "subagentType", "goal", "acceptanceCriteria", "proofCommand"];

    /// <summary>The request, or null when the payload misses the schema.</summary>
    internal static SaveBriefRequest? Read(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("dispatchName", out var name)
            || name.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
            || Strings(payload) is not { } text)
        {
            return null;
        }

        // A typed id refuses only text past its 4096-character cap, which no hook sends.
        return ToolUseId.TryFrom(text["toolUseId"], out var toolUseId)
            && SessionId.TryFrom(text["sessionId"], out var sessionId)
            && PromptId.TryFrom(text["promptId"], out var promptId)
            ? new SaveBriefRequest(toolUseId, sessionId, promptId, name.GetString(), text["subagentType"], text["goal"], text["acceptanceCriteria"], text["proofCommand"])
            : null;
    }

    private static Dictionary<string, string>? Strings(JsonElement payload)
    {
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in _required)
        {
            if (!payload.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            text[key] = value.GetString() ?? "";
        }

        return text;
    }
}
