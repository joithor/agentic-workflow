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
    /// The finished line's figure ("4 done today"). The API serves a root-wide counts.done and no per-day figure
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

    private static T Read<T>(string body)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(body), SindriApiClient.Json).Value;
}
