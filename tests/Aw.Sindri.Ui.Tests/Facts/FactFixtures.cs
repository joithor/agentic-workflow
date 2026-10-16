using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>The fact rows of the instance goldens, and the one link fixture (Q-API-17: no golden has a link-typed fact yet).</summary>
internal static class FactFixtures
{
    internal static readonly Uri Api = new("http://127.0.0.1:7410/");

    /// <summary>One fact row of an instance golden.</summary>
    /// <param name="scenario">The instance golden.</param>
    /// <param name="key">The fact's key.</param>
    /// <returns>The row as served.</returns>
    internal static FactRow Row(string scenario, string key) => FixtureInstances.Parse<FactRow>(RowNode(scenario, key));

    /// <summary>A row of an instance golden, retyped (a type the app has no viewer for, or does not know).</summary>
    /// <param name="scenario">The instance golden.</param>
    /// <param name="key">The fact's key.</param>
    /// <param name="type">The type word.</param>
    /// <returns>The row.</returns>
    internal static FactRow Retyped(string scenario, string key, string type)
    {
        var node = RowNode(scenario, key);
        node["type"] = type;
        return FixtureInstances.Parse<FactRow>(node);
    }

    /// <summary>The link fact: a row of the project golden, typed link, carrying the link fixture's url, host and title.</summary>
    /// <returns>The row.</returns>
    internal static FactRow Link()
    {
        var node = RowNode("project-build", "fact:project");
        node["type"] = "link";
        node["ref"] = JsonNode.Parse(Fixture()["body"]!.ToJsonString()); // why: the fixture has a body
        node["ref"]!["name"] = null;
        node["ref"]!["size"] = null;
        node["ref"]!["hash"] = null;
        node["ref"]!["path"] = null;
        node["ref"]!["itemId"] = null;
        node["ref"]!["factKey"] = null;
        return FixtureInstances.Parse<FactRow>(node);
    }

    /// <summary>The link fixture's body.</summary>
    /// <returns>The body (url, host, title).</returns>
    internal static JsonNode Fixture() => JsonNode.Parse(GoldenFiles.SpecShaped("workflow-instance-fact", "extra-link"))!;

    private static JsonNode RowNode(string scenario, string key) =>
        GoldenFiles.Node("workflow-instance", scenario)["facts"]!.AsArray().Single(f => (string?)f!["key"] == key)!; // why: the golden lists the key
}
