using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// The part of Onyx's DocumentSearchRequest the fake reads: 'message', and under 'retrieval_options' 'limit', 'offset'
/// and 'filters' with 'source_type' and 'time_cutoff' (documents updated at or after it). A field it doesn't read is a
/// 422 naming it, so a client never relies on a filter the fake would silently ignore.
/// </summary>
internal sealed record OnyxSearchRequest(string Message, IReadOnlyList<string>? SourceTypes, DateTimeOffset? TimeCutoff, int Limit, int Offset)
{
    private const int DefaultLimit = 10;
    private static readonly string[] _top = ["message", "search_type", "retrieval_options"];
    private static readonly string[] _options = ["filters", "limit", "offset", "run_search", "real_time", "enable_auto_detect_filters"];
    private static readonly string[] _filters = ["source_type", "time_cutoff"];

    public static OnyxSearchRequest Parse(JsonObject body)
    {
        Known(body, _top, "body");
        var message = body["message"] is JsonValue m && m.GetValueKind() == JsonValueKind.String && m.GetValue<string>().Trim().Length > 0
            ? m.GetValue<string>()
            : throw Unprocessable(["body", "message"], "Field required: a non-empty string.", "missing");
        var options = Section(body, "retrieval_options", _options, "body");
        var filters = Section(options, "filters", _filters, "body", "retrieval_options");
        return new OnyxSearchRequest(
            message,
            filters?["source_type"]?.AsArray().Select(s => (string?)s ?? "").ToList(),
            Cutoff(filters?["time_cutoff"]),
            Number(options?["limit"], DefaultLimit),
            Number(options?["offset"], 0));
    }

    public static FakeRequestException Unprocessable(string[] location, string message, string type) =>
        new(422, type, string.Join('.', location) + ": " + message)
        {
            Detail = new JsonArray(new JsonObject { ["loc"] = new JsonArray([.. location.Select(l => JsonValue.Create(l))]), ["msg"] = message, ["type"] = type }),
        };

    private static JsonObject? Section(JsonObject? parent, string name, string[] allowed, params string[] path)
    {
        if (parent?[name] is null)
        {
            return null;
        }

        var section = parent[name] as JsonObject ?? throw Unprocessable([.. path, name], "Must be an object.", "object_type");
        Known(section, allowed, [.. path, name]);
        return section;
    }

    private static void Known(JsonObject section, string[] allowed, params string[] path)
    {
        var unknown = section.Select(p => p.Key).FirstOrDefault(k => !allowed.Contains(k, StringComparer.Ordinal));
        if (unknown is not null)
        {
            throw Unprocessable([.. path, unknown], $"The fake Onyx server doesn't read '{unknown}'; it reads {string.Join(", ", allowed)}.", "extra_forbidden");
        }
    }

    private static DateTimeOffset? Cutoff(JsonNode? node) =>
        node is null ? null
        : DateTimeOffset.TryParse((string?)node, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at
        : throw Unprocessable(["body", "retrieval_options", "filters", "time_cutoff"], "Must be an ISO-8601 datetime.", "datetime_parsing");

    private static int Number(JsonNode? node, int fallback) =>
        node is null ? fallback
        : node.GetValueKind() == JsonValueKind.Number && node.GetValue<int>() >= 0 ? node.GetValue<int>()
        : throw Unprocessable(["body", "retrieval_options"], "limit and offset are non-negative integers.", "int_type");
}
