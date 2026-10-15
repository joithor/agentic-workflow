using System.Net;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// What the API answers on the workflow reads (API §3.8), taken from its goldens on disk. Every golden is found by
/// enumerating the directory, never listed here, so one the API lane adds or renames is served the moment it lands.
/// The instance list is the golden of the whole root set (and, unless root=true, the child instances the goldens show in
/// full), narrowed by the query the way the API narrows it (the counts stay root-wide); an instance and a recipe are the
/// golden whose own id or name matches (a recipe also its hash); a fact is the golden whose key the instance lists; anything
/// else is the same not-found refusal the API writes.
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
        var rows = table["instances"]!.AsArray().Select(r => r!).Concat(query.Root ? [] : ChildRows()).Where(r => Keeps(r, query)).Select(r => r.DeepClone()).ToArray(); // why: the golden has rows
        table["instances"] = new JsonArray(rows);
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
        var listed = Served("workflow-instance").Any(g => IdOf(g.Body) == id && g.Body["facts"]!.AsArray().Any(f => Is(f!["key"], key))); // why: an instance lists its facts
        if (!listed)
        {
            return NotFound("workflow-instance-fact", "not-found");
        }

        return Served("workflow-instance-fact").FirstOrDefault(g => Is(g.Body["key"], key)) is { Body: { } body }
            ? new RouteAnswer(HttpStatusCode.OK, body.ToJsonString())
            : NotFound("workflow-instance-fact", "not-found");
    }

    /// <summary>GET /workflow/recipes/{recipe}[@hash]: the golden whose scenario is the recipe id with dashes for dots; a hash must be one an instance runs.</summary>
    /// <param name="recipe">The recipe id.</param>
    /// <param name="hash">The hash the caller names, or null for the current one.</param>
    /// <returns>The answer.</returns>
    internal static RouteAnswer Recipe(string recipe, string? hash) =>
        Served("workflow-recipe").FirstOrDefault(g => string.Equals(g.Scenario, recipe.Replace('.', '-'), StringComparison.Ordinal)) is { Body: { } body } && HashKnown(recipe, hash)
            ? new RouteAnswer(HttpStatusCode.OK, body.ToJsonString())
            : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-ITEM-404", hash is null ? $"No recipe {recipe} in this repo." : $"No recipe {recipe}@{hash} in this repo.");

    // A recipe's hash is whatever an instance of it reports running; none named means the current one.
    private static bool HashKnown(string recipe, string? hash) =>
        string.IsNullOrEmpty(hash)
        || Served("workflow-instance").Select(g => g.Body["recipe"]).Concat(Rows().Select(r => r["recipe"]))
            .Any(r => Is(r?["id"], recipe) && Is(r?["hash"], hash));

    private static IEnumerable<JsonNode> Rows() => ((JsonObject)JsonNode.Parse(GoldenFiles.Api("workflow-instances", AllRoots))!)["instances"]!.AsArray().Select(r => r!); // why: as above

    // The child instances the goldens show in full (an instance with a parent), as list rows. The list row's chip and its
    // other summary members are not in an instance's own document, so they are derived: the chip from the current state.
    private static IEnumerable<JsonNode> ChildRows() =>
        Served("workflow-instance").Where(g => g.Body["parent"] is not null).Select(g => ChildRow(g.Body));

    private static JsonObject ChildRow(JsonObject detail)
    {
        var terminal = !IsOpen(detail);
        return new JsonObject
        {
            ["address"] = detail["address"]!.DeepClone(),
            ["id"] = detail["id"]!.DeepClone(),
            ["itemId"] = detail["itemId"]?.DeepClone(),
            ["recipe"] = detail["recipe"]!.DeepClone(),
            ["state"] = detail["state"]!.DeepClone(),
            ["chip"] = new JsonObject { ["word"] = terminal ? "DONE" : "RUNNING", ["tone"] = terminal ? "neutral" : "ok" },
            ["since"] = detail["since"]!.DeepClone(),
            ["engineOwned"] = detail["engineOwned"]?.DeepClone(),
            ["subject"] = detail["subject"]!.DeepClone(),
            ["inputs"] = new JsonArray(),
            ["states"] = detail["states"]!.DeepClone(),
            ["now"] = detail["asOf"]!.DeepClone(),
            ["answer"] = null,
            ["actions"] = detail["actions"]!.DeepClone(),
        };
    }

    private static string? IdOf(JsonNode node) => (string?)node["id"];

    // An empty filter narrows nothing, as an absent one does. An address is the instance's address, its item id or its id (the CLI's forms).
    private static bool Keeps(JsonNode row, InstanceQuery query) =>
        (!query.Open || IsOpen(row))
        && Narrows(query.Recipe, row["recipe"]!["id"])
        && Narrows(query.Subject, row["subject"]!["label"], row["subject"]!["key"])
        && Narrows(query.Address, row["address"], row["itemId"], row["id"]);

    // A filter keeps a row when it is empty or null (no filter), or equals any of the row's members it may name.
    private static bool Narrows(string? filter, params JsonNode?[] members) => string.IsNullOrEmpty(filter) || members.Any(m => Is(m, filter));

    // Open is "not terminal": the row's own state cell says.
    private static bool IsOpen(JsonNode row) =>
        FixtureInstances.IsOpen((string)row["state"]!, row["states"]!.AsArray().Select(c => ((string)c!["id"]!, c["terminal"]!.GetValue<bool>())));

    private static bool Is(JsonNode? node, string? expected) => string.Equals((string?)node, expected, StringComparison.Ordinal);
}
