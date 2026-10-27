using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Judge;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The model pick in the launch activity (model-pick spec 4, 5, 6): the candidates, Jev's answer or the rules fallback, and the pick stored before the session starts so a retry never asks twice.</summary>
[Lane(TestLane.Integration)]
public sealed class ModelPickLaunchTests : IDisposable
{
    private const string Repo = "app";
    private const string Item = "ITM-00000000000000000000000000000009";
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly ModelFacts[] _three =
    [
        new("haiku", "Haiku", ModelSource.Catalog, 0),
        new("sonnet", "Sonnet", ModelSource.Catalog, 1),
        new("opus", "Opus", ModelSource.Catalog, 2),
    ];

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Pick_off_runs_the_agents_default_and_asks_no_judge()
    {
        await using var rig = await Rig(new ModelPickSettings(PickOn: false), TestContext.Current.CancellationToken);

        _ = await rig.Launch(Run(1), floor: null, TestContext.Current.CancellationToken);

        Assert.Null(rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
        Expect.Equal("default/", await rig.Picked("by", "model", TestContext.Current.CancellationToken));
        Expect.Equal("default/pick-off", await rig.Picked("by", "reason", TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 120_000)]
    public async Task One_candidate_is_the_pick_and_asks_no_judge()
    {
        await using var rig = await Rig(new ModelPickSettings(PickOn: true, Pin: "sonnet"), TestContext.Current.CancellationToken);

        _ = await rig.Launch(Run(1), floor: null, TestContext.Current.CancellationToken);

        Expect.Equal("sonnet", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
        Expect.Equal("constraint/sonnet", await rig.Picked("by", "model", TestContext.Current.CancellationToken));
        Expect.Equal("constraint/pin", await rig.Picked("by", "reason", TestContext.Current.CancellationToken));
    }

    /// <summary>A Step that names its model keeps it (issue 966): the recipe author's explicit choice, so no judge call.</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_step_that_names_its_model_keeps_it_and_asks_no_judge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);

        _ = await rig.Launch(Run(1), floor: null, ct, model: "opus");

        Expect.Equal("opus", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
        Expect.Equal("constraint/opus", await rig.Picked("by", "model", ct));
        Expect.Equal("constraint/step", await rig.Picked("by", "reason", ct));
    }

    [Fact(Timeout = 120_000)]
    public async Task A_strong_floor_leaves_one_candidate()
    {
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), TestContext.Current.CancellationToken);

        _ = await rig.Launch(Run(1), floor: "strong", TestContext.Current.CancellationToken);

        Expect.Equal("opus", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal(0, rig.Judge.Asks.Count);
    }

    [Fact(Timeout = 120_000)]
    public async Task Jevs_answer_is_the_model_and_picking_comes_before_picked()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m2");

        _ = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal("sonnet", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal("m1=haiku,m2=sonnet,m3=opus", string.Join(',', rig.Judge.Asks[0].Candidates.Select(c => c.Key + "=" + c.Model)));
        Expect.Equal("jev/sonnet", await rig.Picked("by", "model", ct));
        var launch = AgentLaunches.LaunchIdFor(Run(1)).Value;
        Expect.Equal($"model.picking:{launch},model.pick:{launch}", string.Join(',', (await rig.Rows(ct)).Where(e => e.Envelope.Type.StartsWith("model.pick", StringComparison.Ordinal)).Select(e => e.IdempotencyKey)));
    }

    [Fact(Timeout = 120_000)]
    public async Task Launch_started_carries_the_pick()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m1");

        _ = await rig.Launch(Run(1), floor: null, ct);

        var started = (await rig.Rows(ct)).First(e => e.Envelope.Type == "launch.started");
        using var document = JsonDocument.Parse(started.Envelope.Payload);
        Expect.Equal("haiku", document.RootElement.GetProperty("model").GetString());
    }

    [Fact(Timeout = 120_000)]
    public async Task A_retry_reads_the_stored_pick_and_asks_the_judge_once()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Chooses("m2");

        _ = await rig.Launch(Run(1), floor: null, ct);
        _ = rig.Judge.Chooses("m3");   // a second ask would pick opus
        _ = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal(1, rig.Judge.Asks.Count);
        Expect.Equal("sonnet,sonnet", string.Join(',', rig.Launcher.Requests.Select(r => r.Step?.Model)));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("below-threshold", "below-threshold")]
    [InlineData("unavailable", "judge-unavailable")]
    [InlineData("rules-no-key", "no-api-key")]
    [InlineData("unknown-key", "unknown-key")]
    public async Task Each_rules_reason_runs_the_cheapest_allowed_model(string scenario, string reason)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(new ModelPickSettings(PickOn: true), ct);
        _ = rig.Judge.Answers(Answer(scenario));

        _ = await rig.Launch(Run(1), floor: null, ct);

        Expect.Equal("haiku", rig.Launcher.Requests[0].Step?.Model);
        Expect.Equal($"rules/{reason}", await rig.Picked("by", "reason", ct));
    }

    private static ChooseAnswer Answer(string scenario) => scenario switch
    {
        "below-threshold" => new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "below-threshold", DecisionRef: "decision-2"),
        "unavailable" => new ChooseAnswer(ChooseStatus.Failed, Choice: null, "judge model-pick did not finish (judge: TimedOut)"),
        "rules-no-key" => new ChooseAnswer(ChooseStatus.Decided, "m1", "no-api-key", Confidence: 1.0, Provider: "rules", DecisionRef: "decision-3"),
        _ => new ChooseAnswer(ChooseStatus.Decided, "m9", "model", Confidence: 0.9, Provider: "jev", DecisionRef: "decision-4"),
    };

    private static AgentStepRun Run(int attempt)
    {
        var address = InstanceAddress.From("T-9/step/" + attempt.ToString(CultureInfo.InvariantCulture));
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From(attempt.ToString(CultureInfo.InvariantCulture) + ".1"), new StepRef("questions", "step.interview.questions"), WriteScope: false);
    }

    private async Task<PickRig> Rig(ModelPickSettings settings, CancellationToken ct)
    {
        var launcher = new FakeLauncher();
        var judge = new FakeModelPickJudge();
        var sindri = _harness.Sindri(services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ILauncher>(launcher));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickJudge>(judge));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickSettings>(new FakeModelPickSettings { Settings = settings }));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelPickHistory>(new FakeModelPickHistory()));
            _ = services.Replace(ServiceDescriptor.Singleton<IModelCatalog>(new FakeModelCatalog().With(_claude, _three)));
        });
        await WorkflowsHarness.HoldLease(sindri, ct);
        var declared = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Repos: [new RepoEntry(Repo, Path.Combine(_harness.Home, "checkout"))], Workflow: new WorkflowSettings(Agent: new WorkflowAgentSettings(_claude)));
        var profile = ProfileRules.Check(declared).Value;
        var saved = await sindri.GetRequiredService<IApprovals>().Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), ct);
        Assert.False(saved.IsError, saved.IsError ? saved.Error.Message : null);
        var item = _harness.Fact(JsonSerializer.Serialize(new { id = Item, repo = Repo, spec = (string?)null }));
        return new PickRig(sindri, item, launcher, judge);
    }

    private sealed class PickRig(ServiceProvider sindri, FactValue item, FakeLauncher launcher, FakeModelPickJudge judge) : IAsyncDisposable
    {
        public FakeLauncher Launcher { get; } = launcher;

        public FakeModelPickJudge Judge { get; } = judge;

        public Task<AgentLaunched> Launch(AgentStepRun run, string? floor, CancellationToken ct, string? model = null) =>
            sindri.GetRequiredService<AgentLaunching>().Launch(new AgentLaunchRun(run, item, Model: model, Floor: floor), ct);

        public async Task<IReadOnlyList<LedgerEvent>> Rows(CancellationToken ct) => (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 500, ct)).Value;

        /// <summary>Two fields of the 'model.picked' payload.</summary>
        public async Task<string> Picked(string first, string second, CancellationToken ct)
        {
            var row = (await Rows(ct)).First(e => e.Envelope.Type == "model.picked");
            using var document = JsonDocument.Parse(row.Envelope.Payload);
            return Text(document.RootElement, first) + "/" + Text(document.RootElement, second);
        }

        public ValueTask DisposeAsync() => sindri.DisposeAsync();

        private static string Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }
}
