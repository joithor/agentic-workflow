using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Line;
using Aw.Sindri.Tests.Design;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.GetInstanceDesignStop;

/// <summary>'GetInstanceDesignStop' (line spec LN-API2): the stop-2 view over a workflow.plan instance's facts, scoped like every instance read.</summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceDesignStopServiceTests
{
    private const string Item = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly (string Member, string File)[] _designFiles =
    [
        ("spec", "spec.md"), ("sections", "sections.json"), ("coverage", "coverage.json"), ("decisions", "decisions.json"), ("acceptance", "acceptance.json"), ("signals", "signals.json"),
        ("contracts", "contracts.json"), ("lensRounds", "lens-rounds.jsonl"), ("style", "style.json"), ("escapes", "escapes.json"), ("runs", "runs.json"),
    ];

    private sealed class Contents(IReadOnlyDictionary<string, string> bySha) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(bySha[fact.Sha256])));
    }

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("sk-live-123", "[redacted]", StringComparison.Ordinal);
    }

    /// <summary>The instance's facts as the plan station's Steps write them: fact:design an object with one member per design packet file, fact:plan the task plan, fact:holdoutSets the hidden sets by hash.</summary>
    internal static Dictionary<string, string> Facts(LineFixture gold)
    {
        var design = new JsonObject();
        foreach (var (member, file) in _designFiles)
        {
            var text = gold["design/" + file];
            design[member] = file.EndsWith(".md", StringComparison.Ordinal) ? JsonValue.Create(text)
                : file.EndsWith(".jsonl", StringComparison.Ordinal) ? new JsonArray([.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => JsonNode.Parse(l))])
                : JsonNode.Parse(text);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal) { ["design"] = design.ToJsonString(), ["plan"] = gold["tasks.json"], ["holdoutSets"] = gold["holdout.json"] };
    }

    private static (InstanceView View, Contents Contents) Instance(string recipe, Dictionary<string, string> facts)
    {
        var view = StatusCases.View("desk-task");
        var shas = new Dictionary<string, string>(StringComparer.Ordinal);
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        var n = 0;
        foreach (var (key, body) in facts)
        {
            var sha = (++n).ToString("x64", CultureInfo.InvariantCulture);
            shas["fact:" + key] = sha;
            text[sha] = body;
        }

        return (view with { ItemId = ItemKey.From(Item), Recipe = view.Recipe with { Id = RecipeId.From(recipe) }, FactShas = shas }, new Contents(text));
    }

    private static async Task<Result<DesignStopView>> Read(string recipe, Dictionary<string, string> facts, CallerContext? caller = null, FixedRepos? repos = null)
    {
        var (view, contents) = Instance(recipe, facts);
        var instances = new SomeInstances(view);
        var service = new GetInstanceDesignStopService(new PlanInstances(instances, instances, repos ?? new FixedRepos(), new ListInstances.FixedCaller(caller ?? new CallerContextBuilder().Human().Build())), contents, new Redacting());
        return await service.Handle(new GetInstanceDesignStopRequest(view.Id), CancellationToken.None);
    }

    [Fact]
    public async Task A_plan_instances_facts_become_the_same_view_as_the_files_do()
    {
        var gold = LineFixture.Plan();

        var result = await Read("workflow.plan", Facts(gold));

        Assert.True(result.IsSuccess);
        Assert.Equal(DesignStopViewTests.Serialized(await DesignStopViewTests.Show(gold)), DesignStopViewTests.Serialized(result.Value));
        Golden.Match(DesignStopViewTests.Serialized(result.Value), typeof(GetInstanceDesignStopServiceTests).Assembly, "Golden/Api/workflow-instance-design-stop/gold-stop.json", Normalization.None);
    }

    [Fact]
    public async Task The_view_is_live_before_evidence_with_what_the_instance_lacks_named_in_notYet()
    {
        var result = await Read("workflow.plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["plan"] = LineFixture.Plan()["tasks.json"] });

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.NotYet, s => s == "decisions");
        Assert.Contains(result.Value.NotYet, s => s == "lensRounds");
        Assert.NotEmpty(result.Value.Graph.Phases);
    }

    [Fact]
    public async Task A_freeze_refusal_the_instance_holds_is_the_banner()
    {
        var facts = Facts(LineFixture.Plan());
        facts["freezeRefused"] = "{\"rule\":\"SND-LINE-001\",\"message\":\"the design packet changed after the approve\"}";

        var result = await Read("workflow.plan", facts);

        Assert.Equal("SND-LINE-001", result.Value.FreezeRefused!.Rule);
    }

    [Fact]
    public async Task An_instance_of_another_recipe_is_SND_LINE_003()
    {
        var result = await Read("workflow.design", Facts(LineFixture.Plan()));

        Assert.Equal(LineErrors.WrongRecipe.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_session_of_another_repo_gets_item_404_and_one_of_the_same_repo_reads()
    {
        var repos = new FixedRepos((Item, "alpha"));

        var own = await Read("workflow.plan", Facts(LineFixture.Plan()), new CallerContextBuilder().Session().InRepo("alpha").Build(), repos);
        var other = await Read("workflow.plan", Facts(LineFixture.Plan()), new CallerContextBuilder().Session().InRepo("beta").Build(), repos);

        Assert.True(own.IsSuccess);
        Assert.Equal(SindriErrors.ItemNotFound.Code, other.Error.Code);
    }

    [Fact]
    public async Task Secrets_in_a_fact_are_scrubbed_before_the_view_is_built()
    {
        var gold = LineFixture.Plan().EditText("design/spec.md", t => t.Replace("A member who shares a note", "A member who shares a note with sk-live-123", StringComparison.Ordinal));

        var result = await Read("workflow.plan", Facts(gold));

        Assert.DoesNotContain("sk-live-123", result.Value.Spec.Text, StringComparison.Ordinal);
        Assert.Contains("[redacted]", result.Value.Spec.Text, StringComparison.Ordinal);
    }
}
