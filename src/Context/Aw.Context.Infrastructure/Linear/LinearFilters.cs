using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The variables of the adapter's operations: the filters (flat objects, whose keys Linear ANDs) and the ids and counts around them.</summary>
internal static class LinearFilters
{
    /// <summary>Records whose <paramref name="field"/> contains any of the terms (case-insensitive), changed since a time, in some projects or teams.</summary>
    /// <param name="field">'title', 'name' or 'body'.</param>
    /// <param name="terms">The terms; none leaves the title or body unconstrained.</param>
    /// <param name="since">Only records updated at or after this time.</param>
    public static JsonObject Matching(string field, IReadOnlyList<string> terms, DateTimeOffset? since)
    {
        var filter = new JsonObject();
        if (terms.Count > 0)
        {
            filter["or"] = new JsonArray([.. terms.Select(t => (JsonNode)new JsonObject { [field] = new JsonObject { ["containsIgnoreCase"] = t } })]);
        }

        if (since is { } at)
        {
            filter["updatedAt"] = new JsonObject { ["gte"] = at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) };
        }

        return filter;
    }

    /// <summary>The filter limited to records of these projects.</summary>
    public static JsonObject InProjects(JsonObject filter, IEnumerable<string> projectIds)
    {
        filter["project"] = new JsonObject { ["id"] = new JsonObject { ["in"] = Strings(projectIds) } };
        return filter;
    }

    /// <summary>The filter limited to issues of these teams.</summary>
    public static JsonObject InTeams(JsonObject filter, IEnumerable<string> teams)
    {
        filter["team"] = new JsonObject { ["key"] = new JsonObject { ["in"] = Strings(teams) } };
        return filter;
    }

    /// <summary>A filter for records with these ids.</summary>
    public static JsonObject WithIds(IEnumerable<string> ids) => new() { ["id"] = new JsonObject { ["in"] = Strings(ids) } };

    /// <summary>The variables of a filtered, counted read.</summary>
    public static JsonObject Variables(JsonObject? filter, int first)
    {
        var variables = new JsonObject { ["first"] = first };
        if (filter is not null)
        {
            variables["filter"] = filter;
        }

        return variables;
    }

    /// <summary>The variables of a read by id.</summary>
    public static JsonObject ById(string id) => new() { ["id"] = id };

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(v => (JsonNode?)JsonValue.Create(v))]);
}
