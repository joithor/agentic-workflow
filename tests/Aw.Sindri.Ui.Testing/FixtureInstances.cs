using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The one instance table: the API's own golden of the root set, read through the app's records, and the instances it
/// shows in full. The nav count, the Home strip head, the Workflows head and the children counts are all held to it; no
/// test or view has a table of its own. The goldens are enumerated from disk.
/// </summary>
internal static class FixtureInstances
{
    /// <summary>
    /// The finished line's figure ("4 finished"). The API serves a root-wide counts.done and no per-day figure
    /// (Q-API-3, P11's list contract), so this is a UI extra kept equal to the golden's until P11.
    /// </summary>
    internal const int DoneToday = 4;

    private static readonly Lazy<InstanceList> _root = new(() => Read<InstanceList>(GoldenFiles.Api("workflow-instances", "root")));

    private static readonly Lazy<IReadOnlyList<(string Scenario, InstanceDetail Detail)>> _details = new(() =>
        [.. GoldenFiles.All()
            .Where(g => g.Kind == "Api" && g.Slug == "workflow-instance" && !g.Body.Contains("\"traceId\"", StringComparison.Ordinal))
            .Select(g => (g.Scenario, Read<InstanceDetail>(g.Body)))]);

    /// <summary>The root list as served: every root row and the root-wide counts.</summary>
    internal static InstanceList Root => _root.Value;

    /// <summary>One list golden of the instance list route, read through the app's records.</summary>
    /// <param name="scenario">The scenario ('open', 'recipe-filter', 'preview-missing-input').</param>
    /// <returns>The list as served.</returns>
    internal static InstanceList List(string scenario) => Read<InstanceList>(GoldenFiles.Api("workflow-instances", scenario));

    /// <summary>The root rows.</summary>
    internal static IReadOnlyList<InstanceSummary> Rows => Root.Instances;

    /// <summary>The counts as served.</summary>
    internal static ListCounts Counts => Root.Counts;

    /// <summary>Every instance golden in full, with the scenario it came from.</summary>
    internal static IReadOnlyList<(string Scenario, InstanceDetail Detail)> Details => _details.Value;

    /// <summary>Every child row of an instance, from its workstreams and its plain rows.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The rows.</returns>
    internal static IEnumerable<ChildRow> ChildRows(ChildGroup group) => group.Workstreams.SelectMany(w => w.Rows).Concat(group.Rows);

    /// <summary>
    /// The one open rule: a row is open unless its current state's cell is terminal. The API computes counts.open by it, the
    /// fake's open=true filter applies it to the golden's JSON, and the checks hold the rows to it.
    /// </summary>
    /// <param name="state">The row's current state.</param>
    /// <param name="cells">The row's state cells as (state, terminal).</param>
    /// <returns>Whether the row is open.</returns>
    internal static bool IsOpen(string state, IEnumerable<(string State, bool Terminal)> cells) =>
        !cells.Any(c => c.Terminal && string.Equals(c.State, state, StringComparison.Ordinal));

    /// <summary>The open rule on one row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Whether it is open.</returns>
    internal static bool IsOpen(InstanceSummary row) => IsOpen(row.State, row.States.Select(s => (s.State, s.Terminal)));

    /// <summary>One instance golden in full, read through the app's records.</summary>
    /// <param name="scenario">The scenario ('project-build', 'incident-loop').</param>
    /// <returns>The instance as served.</returns>
    internal static InstanceDetail Detail(string scenario) => Read<InstanceDetail>(GoldenFiles.Api("workflow-instance", scenario));

    /// <summary>One recipe golden in full, read through the app's records.</summary>
    /// <param name="id">The recipe id ('workflow.project'); a recipe golden is named by the id its body carries.</param>
    /// <returns>The recipe as served.</returns>
    internal static RecipeView Recipe(string id) => Read<RecipeView>(GoldenFiles.Api("workflow-recipe", id));

    /// <summary>Every recipe golden's id (the not-found refusal is not one).</summary>
    internal static IReadOnlyList<string> RecipeIds => [.. WorkflowAnswers.NamedRecipeGoldens.Values.Order(StringComparer.Ordinal)];

    /// <summary>One status golden, read through the app's records (a state the instance goldens don't show in full, such as taken-over).</summary>
    /// <param name="scenario">The scenario ('taken-over', 'parked-drift').</param>
    /// <returns>The status as served.</returns>
    internal static StatusFields Status(string scenario) => Read<StatusFields>(GoldenFiles.Api("workflow-status", scenario));

    /// <summary>One instance summary as the layout goldens record it (the escalated-child and parked-mirrored-budget rows carry answer.via).</summary>
    /// <param name="scenario">The scenario ('escalated-child', 'parked-mirrored-budget').</param>
    /// <returns>The summary as served.</returns>
    internal static InstanceSummary Summary(string scenario) => Read<InstanceSummary>(File.ReadAllText(Path.Combine(GoldenFiles.Root, "Layout", "instance-summary-" + scenario + ".json")));

    /// <summary>A golden changed by a test (a member removed or nulled), read through the app's records.</summary>
    /// <typeparam name="T">The record.</typeparam>
    /// <param name="node">The edited body.</param>
    /// <returns>The record.</returns>
    internal static T Parse<T>(System.Text.Json.Nodes.JsonNode node)
        where T : class => Read<T>(node.ToJsonString());

    private static T Read<T>(string body)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(body), SindriApiClient.Json).Value;
}
