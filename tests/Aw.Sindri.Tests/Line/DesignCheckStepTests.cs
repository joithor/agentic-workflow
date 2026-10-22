using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.design.check' on the block runtime (line spec §4.1): pass, or gaps with the findings; a fact it needs and the instance lacks fails the Step.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignCheckStepTests
{
    private const string Scope = """{"scopeMap":{"surfaces":[{"id":"S1"}]},"acceptance":{"items":[{"id":"AC-1","text":"WHEN a THEN b."}]},"openQuestions":[],"answerLog":[]}""";

    private const string Design = """{"sections":[{"id":"sec-1","covers":["S1"],"cites":["S1"]}],"signals":[{"item":"AC-1","kind":"test","statement":"b holds","runs":"head"}],"decisions":[]}""";

    private static async Task<Core.Workflow.Blocks.BlockStepResult> Run(ScopingFacts facts) =>
        await new DesignCheckStep(new FakeStylePacks()).Run(ScopingStepContexts.Of("step.design.check", facts), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_sound_draft_passes_and_writes_nothing()
    {
        var result = await Run(new ScopingFacts { ["scope"] = Scope, ["packs"] = "{}", ["design"] = Design });

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
    }

    [Fact]
    public async Task A_draft_with_a_gap_reports_gaps_with_the_findings()
    {
        var result = await Run(new ScopingFacts { ["scope"] = Scope, ["packs"] = "{}", ["design"] = Design.Replace("\"covers\":[\"S1\"]", "\"covers\":[]", StringComparison.Ordinal) });

        Assert.Equal("gaps", result.Result!.Outcome.Value);
        var findings = JsonNode.Parse(result.Result.Outputs[FactKey.From("fact:findings")].GetRawText())!.AsArray();
        Assert.Equal("DSN-COV-001", (string)findings[0]!["rule"]!);
    }

    [Fact]
    public async Task A_missing_fact_fails_the_Step_rather_than_passing_it()
    {
        var result = await Run(new ScopingFacts { ["scope"] = Scope, ["packs"] = "{}" });

        Assert.Null(result.Result);
        Assert.Contains("fact:design", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_spec_that_breaks_the_pinned_style_pack_gets_gaps_with_DSN_STYLE_001()
    {
        var styles = new FakeStylePacks().With(new StylePack(LensPackId.From("plain"), "sha256:" + new string('a', 64), [new StyleSection("Summary", Required: true, MaxWords: null)], new StyleRules([], MaxLines: null)));
        var design = JsonNode.Parse(Design)!;
        design["spec"] = "# Design\n\n## Detail\nNo summary here.\n";
        var facts = new ScopingFacts { ["scope"] = Scope, ["packs"] = """{"style":{"id":"plain","hash":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}""", ["design"] = design.ToJsonString() };

        var result = await new DesignCheckStep(styles).Run(ScopingStepContexts.Of("step.design.check", facts), TestContext.Current.CancellationToken);

        Assert.Equal("gaps", result.Result!.Outcome.Value);
        Assert.Contains("DSN-STYLE-001", result.Result.Outputs.Single().Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pinned_style_pack_whose_hash_differs_fails_the_Step_with_SND_PACK_002()
    {
        var styles = new FakeStylePacks().With(new StylePack(LensPackId.From("plain"), "sha256:" + new string('c', 64), [], new StyleRules([], MaxLines: null)));
        var facts = new ScopingFacts { ["scope"] = Scope, ["packs"] = """{"style":{"id":"plain","hash":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}""", ["design"] = Design };

        var result = await new DesignCheckStep(styles).Run(ScopingStepContexts.Of("step.design.check", facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("SND-PACK-002", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pinned_style_pack_that_no_longer_exists_fails_the_Step_with_SND_PACK_001()
    {
        var facts = new ScopingFacts { ["scope"] = Scope, ["packs"] = """{"style":{"id":"gone","hash":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}""", ["design"] = Design };

        var result = await new DesignCheckStep(new FakeStylePacks()).Run(ScopingStepContexts.Of("step.design.check", facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("SND-PACK-001", result.Failure, StringComparison.Ordinal);
    }
}
