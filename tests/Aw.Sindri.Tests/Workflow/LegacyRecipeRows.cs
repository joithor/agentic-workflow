using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The legacy recipe rows (stored before the declarations of P2b and P4c), shared by the registry tests here and the Temporal suites of Aw.Sindri.Engine.Tests.</summary>
internal static class LegacyRecipeRows
{
    private static Recipe Bind(string yaml)
    {
        var reading = new YamlRecipeReader(SliceHarness.Json).Read(yaml, "test.yaml");
        Assert.Empty(reading.Findings.Select(f => f.ToLine()));
        return reading.Document!.Recipe;
    }

    /// <summary>The long form of the legacy recipe as the registry stored it: today's JSON (with its null 'hash') without the declarations P2b added (urgency, outputs, kind), the two P4c added (name, generate) and the plain-words name SO-E1 added (displayName).</summary>
    internal static JsonObject Json(string yaml)
    {
        var node = JsonNode.Parse(AwJson.Serialize(Bind(yaml) with { Hash = null }, SliceHarness.Json))!.AsObject();
        _ = node.Remove("urgency");
        _ = node.Remove("outputs");
        _ = node.Remove("name");
        _ = node.Remove("displayName");
        foreach (var input in node["inputs"]!.AsArray())
        {
            _ = input!.AsObject().Remove("kind");
            _ = input.AsObject().Remove("generate");
        }

        return node;
    }

    /// <summary>The hash the legacy recipe was registered under: sha256 over the canonical legacy form without 'hash'.</summary>
    internal static Core.Workflow.Ids.RecipeHash Hash(string yaml)
    {
        var node = Json(yaml);
        _ = node.Remove("hash");
        return Core.Workflow.Ids.RecipeHash.Of(CanonicalJson.Of(node));
    }
}
