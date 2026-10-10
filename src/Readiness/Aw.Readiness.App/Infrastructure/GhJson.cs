using System.Globalization;
using System.Text.Json;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>Reads the few fields the host adapter needs from gh's JSON (reflection-free: JsonDocument).</summary>
internal static class GhJson
{
    internal static bool TryRepo(string json, out string name, out string branch)
    {
        name = branch = "";
        if (!TryParse(json, out var doc))
        {
            return false;
        }

        using (doc)
        {
            var root = doc.RootElement;
            name = Text(root, "nameWithOwner") ?? "";
            branch = root.TryGetProperty("defaultBranchRef", out var b) ? Text(b, "name") ?? "" : "";
            return name.Length > 0 && branch.Length > 0;
        }
    }

    internal static bool TryRuleTypes(string json, out List<string> types)
    {
        types = [];
        if (!TryParse(json, out var doc))
        {
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            types = [.. doc.RootElement.EnumerateArray().Select(r => Text(r, "type")).OfType<string>().Distinct(StringComparer.Ordinal)];
            return true;
        }
    }

    /// <summary>(admin, (scanning, push)): admin is null when gh didn't say; the pair is null when the repo reports
    /// no security_and_analysis.</summary>
    internal static (bool? Admin, (string Scanning, string Push)? Statuses) Scanning(string json)
    {
        if (!TryParse(json, out var doc))
        {
            return (null, null);
        }

        using (doc)
        {
            var root = doc.RootElement;
            bool? admin = root.TryGetProperty("permissions", out var p) && p.TryGetProperty("admin", out var a)
                && a.ValueKind is JsonValueKind.True or JsonValueKind.False ? a.GetBoolean() : null;
            if (!root.TryGetProperty("security_and_analysis", out var sa) || sa.ValueKind != JsonValueKind.Object)
            {
                return (admin, null);
            }

            return (admin, (Status(sa, "secret_scanning"), Status(sa, "secret_scanning_push_protection")));
        }
    }

    internal static bool TryNames(string json, out List<string> names)
    {
        names = [];
        if (!TryParse(json, out var doc))
        {
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            names = [.. doc.RootElement.EnumerateArray().Select(r => Text(r, "name")).OfType<string>()];
            return true;
        }
    }

    internal static bool TryPublished(string json, out List<DateTimeOffset> published)
    {
        published = [];
        if (!TryParse(json, out var doc))
        {
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            published = [.. doc.RootElement.EnumerateArray()
                .Select(r => Text(r, "published_at"))
                .Where(t => t is not null && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                .Select(t => DateTimeOffset.Parse(t!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal))];   // why: the Where above keeps only parseable, non-null text
            return true;
        }
    }

    private static string Status(JsonElement sa, string feature) =>
        sa.TryGetProperty(feature, out var f) ? Text(f, "status") ?? "unreported" : "unreported";

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static bool TryParse(string json, out JsonDocument doc)
    {
        try
        {
            doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            doc = null!;   // why: callers read doc only when this returns true
            return false;
        }
    }
}
