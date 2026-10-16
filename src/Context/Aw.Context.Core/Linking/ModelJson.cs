using System.Text.Json;

namespace Aw.Context.Core.Linking;

/// <summary>Reads the JSON object a model answered with, even when it wrapped it in prose.</summary>
internal static class ModelJson
{
    internal static JsonDocument? Parse(string answer)
    {
        var start = answer.IndexOf('{', StringComparison.Ordinal);
        var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(answer.AsMemory(start, end - start + 1));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
