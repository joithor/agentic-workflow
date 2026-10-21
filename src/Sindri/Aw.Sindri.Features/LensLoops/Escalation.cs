using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// The one way a lens loop reaches a human (owner, 2026-10-19): a stopping finding carrying a typed
/// <c language="text">escalate: { kind: "ambiguousFix" | "productQuestion", question, options? }</c> (schemas/line/escalation.json). Anything else, a finding that only says
/// "please decide", an unknown kind, a missing question or an option that is not text, is not an escalation and stays in the loop.
/// </summary>
internal static class Escalation
{
    private static readonly string[] _kinds = ["ambiguousFix", "productQuestion"];

    /// <summary>The first typed escalation among the findings, as 'fact:escalation': the kind, the question, the options when there are any and the finding's lens and id.</summary>
    /// <param name="stopping">The round's findings that stop the loop, each with its lens.</param>
    /// <returns>The escalation; null when no finding carries a typed one.</returns>
    internal static JsonObject? First(IEnumerable<JsonObject> stopping)
    {
        ArgumentNullException.ThrowIfNull(stopping);
        foreach (var finding in stopping)
        {
            if (Typed(finding["escalate"]) is { } typed)
            {
                typed["lens"] = LoopJson.Str(finding, "lens");
                if (LoopJson.Str(finding, "id") is { Length: > 0 } id)
                {
                    typed["finding"] = id;
                }

                return typed;
            }
        }

        return null;
    }

    private static JsonObject? Typed(JsonNode? node)
    {
        if (node is not JsonObject escalate
            || LoopJson.Str(escalate, "kind") is not { } kind || !_kinds.Contains(kind, StringComparer.Ordinal)
            || LoopJson.Str(escalate, "question") is not { Length: > 0 } question)
        {
            return null;
        }

        var typed = new JsonObject { ["kind"] = kind, ["question"] = question };
        if (escalate["options"] is null)
        {
            return typed;
        }

        if (escalate["options"] is not JsonArray { Count: > 0 } options || options.Any(static o => o is not JsonValue v || !v.TryGetValue<string>(out _)))
        {
            return null;
        }

        typed["options"] = options.DeepClone();
        return typed;
    }
}
