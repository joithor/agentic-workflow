using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The evidence parents' facts (DT-R2 plan U6): which fact each child's summary lands under, folded through the pure engine.</summary>
public sealed class EvidenceParentFactsTests
{
    private const string Start = """{"start":{"inputs":{"fact:phasePacket":{"phase":"1"},"fact:built":"https://example.test/diff/1"}}}""";

    private static InMemoryChildRecipes Siblings()
    {
        var children = InMemoryChildRecipes.None;
        foreach (var path in ShippedRecipes.Recipes.Where(r => r.StartsWith("sindri/", StringComparison.Ordinal)))
        {
            var recipe = EvalComposition.Recipe(path);
            _ = children.With(new ChildRecipe(recipe, recipe.Hash!.Value));
        }

        return children;
    }

    private static EvalSimulation Fold(string recipePath, params string[] lines)
    {
        var recipe = EvalComposition.Recipe(recipePath);
        var read = EvalCaseReader.Read("facts", string.Join('\n', [Start, .. lines]), EvalComposition.Options);
        var evalCase = Assert.IsType<EvalCase>(read.Case);
        var children = Siblings();
        var simulation = EvalSimulation.Start(evalCase, recipe, EvalComposition.Catalog, children).Value;
        for (var i = 0; i < evalCase.Steps.Count; i++)
        {
            var step = simulation.Step(evalCase.Steps[i], i + 2);
            Assert.False(step.IsError, step.IsError ? step.Error.Message : null);
            simulation = step.Value.Next;
            Assert.Null(simulation.Diverges(evalCase.Steps[i].Expect));
        }

        return simulation;
    }

    private static string Digest(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Child(string state, string outcome, string? summary = null) =>
        JsonSerializer.Serialize(new
        {
            trigger = new { kind = "wait.satisfied", outcome, outputs = summary is null ? null : new Dictionary<string, string> { ["fact:evidenceSummary"] = summary } },
            expect = new { state },
        });

    [Fact, Trait("Lane", "unit")]
    public void Evidence_all_keeps_each_childs_summary_under_its_own_key()
    {
        var simulation = Fold("sindri/workflow.evidence-all.yaml",
            Child("api", "resolved", "ui summary"), Child("db", "resolved", "api summary"), Child("combine", "resolved", "db summary"));

        var facts = simulation.Instance.Facts;
        Assert.Equal(Digest("ui summary"), facts[FactKey.From("fact:evidenceUi")].Sha256);
        Assert.Equal(Digest("api summary"), facts[FactKey.From("fact:evidenceApi")].Sha256);
        Assert.Equal(Digest("db summary"), facts[FactKey.From("fact:evidenceDb")].Sha256);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evidence_all_unresolved_carries_no_summary()
    {
        var simulation = Fold("sindri/workflow.evidence-all.yaml",
            Child("api", "resolved", "ui summary"), Child("failed", "unresolved", "api broke"));

        Assert.DoesNotContain(FactKey.From("fact:evidenceSummary"), simulation.Instance.Facts.Keys);
        Assert.DoesNotContain(FactKey.From("fact:evidenceApi"), simulation.Instance.Facts.Keys);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ui", "evidence-ui")]
    [InlineData("api", "evidence-api")]
    [InlineData("db", "evidence-db")]
    [InlineData("all", "evidence-all")]
    [InlineData("other", "evidence-other")]
    public void Evidence_routes_every_leaf_choice_and_reconverges_on_the_summary(string choice, string state)
    {
        var simulation = Fold("sindri/workflow.evidence.yaml",
            $$"""{"trigger":{"kind":"step.completed","outcome":"{{choice}}"},"expect":{"state":"{{state}}"}}""",
            Child("done", "resolved", "proof"));

        Assert.Equal("resolved", simulation.Instance.Outcome?.Value);
        Assert.Equal(Digest("proof"), simulation.Instance.Facts[FactKey.From("fact:evidenceSummary")].Sha256);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_leaf_carries_its_summary_to_the_route()
    {
        var simulation = Fold("sindri/workflow.evidence.yaml",
            """{"trigger":{"kind":"step.completed","outcome":"api"},"expect":{"state":"evidence-api"}}""",
            Child("failed", "unresolved", "api failed"));

        Assert.Equal(Digest("api failed"), simulation.Instance.Facts[FactKey.From("fact:evidenceSummary")].Sha256);
    }
}
