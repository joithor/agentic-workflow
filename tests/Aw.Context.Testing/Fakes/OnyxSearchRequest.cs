using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// Onyx v4.9.0's AdminSearchRequest (onyx/server/query_and_chat/models.py: 'query: str', 'filters: BaseFilters', both
/// required) as the fake reads it. Pydantic's default ignores fields the models don't have, so the fake ignores them
/// too (there is no 'limit' or 'offset'). Of BaseFilters (onyx/context/search/models.py) it applies 'source_type'
/// (DocumentSource values; an empty list is no filter), 'updated_at_range' {start, end} and its legacy alias
/// 'time_cutoff' (= updated_at_range.start; an explicit range wins). A body the real models reject is Onyx's generic
/// 422 (<see cref="ValidationCode"/>); a real filter the fake doesn't model ('document_set', 'tags',
/// 'created_at_range') with a value is refused with <see cref="UnsupportedCode"/> naming it, so a client never relies
/// on a filter the fake would silently drop.
/// </summary>
internal sealed record OnyxSearchRequest(string Query, IReadOnlyList<string>? SourceTypes, DateTimeOffset? UpdatedFrom, DateTimeOffset? UpdatedTo)
{
    /// <summary>NUM_RETURNED_HITS (onyx/configs/chat_configs.py): admin search returns at most this many documents.</summary>
    public const int MaxHits = 50;

    public const string ValidationCode = "VALIDATION_ERROR";
    public const string UnsupportedCode = "FAKE_UNSUPPORTED";

    private static readonly string[] _unmodeled = ["document_set", "tags", "created_at_range"];

    /// <summary>The DocumentSource values of v4.9.0 (onyx/configs/constants.py).</summary>
    private static readonly HashSet<string> _sources = new(StringComparer.Ordinal)
    {
        "ingestion_api", "slack", "web", "google_drive", "gmail", "github", "gitbook", "gitlab", "guru", "bookstack", "outline",
        "confluence", "jira", "slab", "productboard", "file", "coda", "canvas", "notion", "zulip", "linear", "hubspot", "gong",
        "google_sites", "zendesk", "loopio", "box", "dropbox", "sharepoint", "onedrive", "teams", "outlook", "salesforce",
        "discourse", "axero", "clickup", "mediawiki", "wikipedia", "asana", "google_cloud_storage", "oci_storage", "xenforo",
        "not_applicable", "discord", "freshdesk", "fireflies", "zoom", "egnyte", "airtable", "highspot", "drupal_wiki", "imap",
        "bitbucket", "testrail", "braintrust", "lumapps", "mock_connector", "user_file", "craft_file", "federated_slack",
    };

    public static OnyxSearchRequest Parse(string body)
    {
        var root = Json(body) as JsonObject ?? throw Invalid();
        var query = root["query"] is JsonValue q && q.GetValueKind() == JsonValueKind.String ? q.GetValue<string>() : throw Invalid();
        var filters = root["filters"] as JsonObject ?? throw Invalid();
        var unmodeled = _unmodeled.FirstOrDefault(name => filters[name] is not null);
        if (unmodeled is not null)
        {
            throw new FakeRequestException(422, UnsupportedCode,
                $"The fake Onyx server doesn't model the '{unmodeled}' filter (real Onyx applies it); send it as null or leave it out.");
        }

        var cutoff = Time(filters["time_cutoff"]);
        var range = filters["updated_at_range"];
        return range is null
            ? new OnyxSearchRequest(query, Sources(filters["source_type"]), cutoff, UpdatedTo: null)
            : new OnyxSearchRequest(query, Sources(filters["source_type"]), Time(Bound(range, "start")), Time(Bound(range, "end")));
    }

    /// <summary>A request the real models reject: Onyx's 422 body names no field.</summary>
    public static FakeRequestException Invalid() => new(422, ValidationCode, "Request validation failed.");

    private static JsonNode? Json(string body)
    {
        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            throw Invalid();
        }
    }

    private static JsonNode? Bound(JsonNode range, string name) => range is JsonObject window ? window[name] : throw Invalid();

    private static List<string>? Sources(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonArray { Count: 0 } => null,
            JsonArray list when list.All(x => x is JsonValue v && v.GetValueKind() == JsonValueKind.String && _sources.Contains(v.GetValue<string>())) =>
                [.. list.Select(x => x!.GetValue<string>())], // why: All checked every element is a string value
            _ => throw Invalid(),
        };

    /// <summary>Pydantic's datetime: an ISO-8601 string (naive means UTC) or a Unix time in seconds (milliseconds past 2e10).</summary>
    private static DateTimeOffset? Time(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonValue v when v.GetValueKind() == JsonValueKind.String
                && DateTimeOffset.TryParse(v.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) => at,
            JsonValue v when v.GetValueKind() == JsonValueKind.Number && v.TryGetValue<long>(out var unix) =>
                unix > 20_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(unix) : DateTimeOffset.FromUnixTimeSeconds(unix),
            _ => throw Invalid(),
        };
}
