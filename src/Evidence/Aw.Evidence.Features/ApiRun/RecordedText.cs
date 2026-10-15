using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Text on its way into a summary: secret-named JSON members hidden, the rest scrubbed, then capped, so a cap never cuts a secret in half and leaves the rest unrecognisable.</summary>
internal static class RecordedText
{
    internal const int OutputLimit = 2000;
    internal const int ValueLimit = 200;
    internal const int HeadlineLimit = 200;

    /// <summary>What replaces the value of a secret-named member (Aw.Redaction's own text for it).</summary>
    internal const string SecretField = "[REDACTED:secret-field]";

    internal static string Of(ITextScrubber scrubber, string text, int limit)
    {
        var scrubbed = Scrubbed(scrubber, text);
        return scrubbed.Length <= limit ? scrubbed : string.Concat(scrubbed.AsSpan(0, limit - 1), "…");
    }

    /// <summary>A failed assertion with its quoted values hidden when the pointer runs through a secret-named member, and scrubbed and capped otherwise.</summary>
    internal static ApiFailure Of(ITextScrubber scrubber, ApiFailure failure)
    {
        var secret = failure.Location is not null && failure.Location.Split('/').Any(segment => scrubber.IsSecretName(segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)));
        return failure with
        {
            Location = failure.Location is null ? null : Of(scrubber, failure.Location, ValueLimit),
            Expected = secret ? SecretField : Of(scrubber, failure.Expected, ValueLimit),
            Actual = secret ? SecretField : Of(scrubber, failure.Actual, ValueLimit),
        };
    }

    private static string Scrubbed(ITextScrubber scrubber, string text) => scrubber.Scrub(HideSecretMembers(scrubber, text));

    private static string HideSecretMembers(ITextScrubber scrubber, string text)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return text;
        }

        try
        {
            var node = JsonNode.Parse(text);
            return node is not null && Hide(scrubber, node) ? node.ToJsonString() : text;
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static bool Hide(ITextScrubber scrubber, JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var entry in obj.ToList())
                {
                    if (scrubber.IsSecretName(entry.Key))
                    {
                        obj[entry.Key] = SecretField;
                        changed = true;
                    }
                    else if (entry.Value is not null)
                    {
                        changed |= Hide(scrubber, entry.Value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    changed |= item is not null && Hide(scrubber, item);
                }

                break;
            default:
                break;
        }

        return changed;
    }
}
