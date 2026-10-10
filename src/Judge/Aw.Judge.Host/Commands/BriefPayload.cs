using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Json;
using Aw.Judge.Features.SaveBrief;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// The 'brief save' stdin schema (TS commands.ts BriefSaveInputSchema, zod): an object whose eight fields are all
/// present, each a string (empty allowed) and dispatchName a string or null; other keys are ignored. Read as plain
/// strings first, then made typed ids.
/// </summary>
internal static class BriefPayload
{
    private static readonly string[] _required = ["toolUseId", "sessionId", "promptId", "subagentType", "goal", "acceptanceCriteria", "proofCommand"];

    /// <summary>The request, or null when the payload misses the schema.</summary>
    internal static SaveBriefRequest? Read(JsValue payload)
    {
        if (payload is not JsObject o || !o.TryGet("dispatchName", out var name) || name is not (JsString or JsNull))
        {
            return null;
        }

        if (Strings(o) is not { } text)
        {
            return null;
        }

        // A typed id refuses only text past its 4096-character cap, which no hook sends.
        return ToolUseId.TryFrom(text["toolUseId"], out var toolUseId)
            && SessionId.TryFrom(text["sessionId"], out var sessionId)
            && PromptId.TryFrom(text["promptId"], out var promptId)
            ? new SaveBriefRequest(toolUseId, sessionId, promptId, (name as JsString)?.Value, text["subagentType"], text["goal"], text["acceptanceCriteria"], text["proofCommand"])
            : null;
    }

    private static Dictionary<string, string>? Strings(JsObject payload)
    {
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in _required)
        {
            if (!payload.TryGet(key, out var value) || value is not JsString s)
            {
                return null;
            }

            text[key] = s.Value;
        }

        return text;
    }
}
