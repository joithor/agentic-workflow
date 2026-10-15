using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// What the API answers on the workflow reads (API §3.8), taken from its goldens on disk. Every golden is found by
/// enumerating the directory, never listed here, so one the API lane adds or renames is served the moment it lands.
/// The instance list is the golden of the whole root set, narrowed by the query the way the API narrows it (the counts
/// stay root-wide); an instance, a fact and a recipe are the golden whose own id, key or name matches; anything else is
/// the same not-found refusal the API writes.
/// </summary>
internal static class WorkflowAnswers
{
    // The list golden that holds every root row: the one served with no filter.
    private const string AllRoots = "root";

    private static readonly Lazy<Dictionary<string, List<(string Scenario, JsonObject Body)>>> _served = new(Load);

    private static Dictionary<string, List<(string Scenario, JsonObject Body)>> Load() =>
        GoldenFiles.All()
            .Where(g => g.Kind == "Api" && g.Slug.StartsWith("workflow-", StringComparison.Ordinal))
            .GroupBy(g => g.Slug, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => (x.Scenario, (JsonObject)JsonNode.Parse(x.Body)!)).ToList(), StringComparer.Ordinal); // why: the goldens are JSON objects

    private static IEnumerable<(string Scenario, JsonObject Body)> Served(string slug) =>
        _served.Value.TryGetValue(slug, out var all) ? all.Where(g => g.Body["traceId"] is null) : [];

    private static RouteAnswer NotFound(string slug, string scenario) => new(HttpStatusCode.NotFound, GoldenFiles.Api(slug, scenario));

    /// <summary>GET /workflow/instances.</summary>
    /// <param name="query">What the list is asked for.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer Instances(InstanceQuery query)
    {
        var table = (JsonObject)JsonNode.Parse(GoldenFiles.Api("workflow-instances", AllRoots))!; // why: the golden is a JSON object
        var kept = table["instances"]!.AsArray().Select(r => r!).Where(r => Keeps(r, query)).Select(r => r.DeepClone()).ToArray(); // why: the golden has rows
        table["instances"] = new JsonArray(kept);
        return new RouteAnswer(HttpStatusCode.OK, table.ToJsonString());
    }

    /// <summary>GET /workflow/instances/{id}.</summary>
    /// <param name="id">The opaque id.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer Instance(string id) =>
        Served("workflow-instance").FirstOrDefault(g => IdOf(g.Body) == id) is { Body: { } body }
            ? new RouteAnswer(HttpStatusCode.OK, body.ToJsonString())
            : NotFound("workflow-instance", "not-found");

    /// <summary>GET /workflow/instances/{id}/facts/{key}.</summary>
    /// <param name="id">The opaque id.</param>
    /// <param name="key">The fact's key.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer Fact(string id, string key)
    {
        var known = Served("workflow-instance").Select(g => IdOf(g.Body)).Concat(Rows().Select(IdOf)).Contains(id, StringComparer.Ordinal);
        if (!known)
        {
            return NotFound("workflow-instance", "not-found");
        }

        return Served("workflow-instance-fact").FirstOrDefault(g => string.Equals((string?)g.Body["key"], key, StringComparison.Ordinal)) is { Body: { } body }
            ? new RouteAnswer(HttpStatusCode.OK, body.ToJsonString())
            : NotFound("workflow-instance-fact", "not-found");
    }

    /// <summary>GET /workflow/recipes/{recipe}[@hash]: the golden whose scenario is the recipe id with dashes for dots.</summary>
    /// <param name="recipe">The recipe id.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer Recipe(string recipe) =>
        Served("workflow-recipe").FirstOrDefault(g => string.Equals(g.Scenario, recipe.Replace('.', '-'), StringComparison.Ordinal)) is { Body: { } body }
            ? new RouteAnswer(HttpStatusCode.OK, body.ToJsonString())
            : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-ITEM-404", $"No recipe {recipe} in this repo.");

    private static IEnumerable<JsonNode> Rows() => ((JsonObject)JsonNode.Parse(GoldenFiles.Api("workflow-instances", AllRoots))!)["instances"]!.AsArray().Select(r => r!); // why: as above

    private static string? IdOf(JsonNode node) => (string?)node["id"];

    private static bool Keeps(JsonNode row, InstanceQuery query) =>
        (!query.Open || IsOpen(row))
        && (query.Recipe is null || string.Equals((string?)row["recipe"]!["id"], query.Recipe, StringComparison.Ordinal))
        && (query.Subject is null || Is(row["subject"]!["label"], query.Subject) || Is(row["subject"]!["key"], query.Subject))
        && (query.Address is null || Is(row["address"], query.Address));

    // Open is "not terminal": the row's own state cell says.
    private static bool IsOpen(JsonNode row) =>
        FixtureInstances.IsOpen((string)row["state"]!, row["states"]!.AsArray().Select(c => ((string)c!["id"]!, c["terminal"]!.GetValue<bool>())));

    private static bool Is(JsonNode? node, string expected) => string.Equals((string?)node, expected, StringComparison.Ordinal);
}
