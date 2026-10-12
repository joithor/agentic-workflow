using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The API goldens (UI spec E-17, API spec §12): each route's response body per scenario, at
/// 'Golden/Api/&lt;route-slug&gt;/&lt;scenario&gt;.json' in this project. The body is re-indented as served (same keys,
/// order and values; only whitespace changes) and normalized only where a field can't be fixed: ProblemDetails'
/// 'traceId', which AddProblemDetails draws at random (plan G18), and the temp home's path (status's 'profile.source'),
/// written '&lt;home&gt;'. Everything else is fixed by the GoldenHost's clock, ids and pid. AW_GOLDEN_UPDATE=1 rewrites
/// a file from the current answer (then review the diff).
/// </summary>
internal static class ApiGolden
{
    private static readonly NormalizationRule _traceId = new("traceId", "\"traceId\": \"[^\"]*\"", "\"traceId\": \"<trace>\"");

    private static readonly JsonWriterOptions _indented = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // keep the served text readable: no ' for a quote
    };

    /// <summary>The golden file of <paramref name="slug"/> and <paramref name="scenario"/>, relative to the test project.</summary>
    public static string PathOf(string slug, string scenario) => $"Golden/Api/{slug}/{scenario}.json";

    /// <summary>
    /// The route slug (E-17): the path after '/sindri/', '/' → '-', each '{…}' segment → 'id' whatever its name
    /// ('/sindri/items/{itemId}' → 'items-id', UI spec §18.1; '/sindri/profile/approve' → 'profile-approve').
    /// </summary>
    public static string Slug(string route)
    {
        const string Prefix = "/sindri/";
        if (!route.StartsWith(Prefix, StringComparison.Ordinal) || route.Length == Prefix.Length)
        {
            throw new ArgumentException($"'{route}' isn't a route under {Prefix}", nameof(route));
        }

        var parts = route[Prefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(static p => p.StartsWith('{') && p.EndsWith('}') ? "id" : p);
        return string.Join('-', parts);
    }

    /// <summary>Checks <paramref name="body"/> (JSON as served by the host in <paramref name="home"/>) against its golden file.</summary>
    public static void Match(string body, string home, string slug, string scenario) =>
        Golden.Match(
            Reindent(body),
            typeof(ApiGolden).Assembly,
            PathOf(slug, scenario),
            new Normalization(_traceId, new NormalizationRule("home", System.Text.RegularExpressions.Regex.Escape(home), "<home>")));

    /// <summary>The JSON re-indented with two spaces and a final newline; keys, order and values as served.</summary>
    public static string Reindent(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, _indented))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }
}
