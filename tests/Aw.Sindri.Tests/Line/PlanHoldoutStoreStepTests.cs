using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.plan.holdout-store' on the block runtime (line spec §9.1, LN-WF4): the author's scenarios go into the holdout store under the phase packet's id, with a canary Sindri adds, and the facts carry ids, counts and hashes only.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanHoldoutStoreStepTests
{
    private const string Dir = "/author/run";

    private const string Sound = "{ \"sets\": [ { \"phase\": \"P1\", \"scenarios\": [ "
        + "{ \"id\": \"P1-H1\", \"item\": \"AC-1\", \"kind\": \"api\", \"steps\": [\"open an expired link\"], \"expected\": \"answers 410\" }, "
        + "{ \"id\": \"P1-H2\", \"item\": \"AC-1\", \"kind\": \"ui\", \"steps\": [\"click save twice\"], \"expected\": \"one record\" } ] } ] }";

    private static async Task<(BlockStepResult Step, LineStation Station)> Run(string? draft, Func<JsonObject, JsonObject>? editPlan = null)
    {
        var station = new LineStation();
        var facts = await station.PlanFacts(editPlan);
        facts["holdoutDraft"] = $$"""{"dir":"{{Dir}}"}""";
        var drafts = new InMemoryBriefFiles();
        if (draft is not null)
        {
            _ = drafts.With("holdout-sets.json", draft);
        }

        var result = await station.PlanHoldoutStore(drafts).Run(LineStation.Context("step.plan.holdout-store", LineStation.PlanAddress, facts), TestContext.Current.CancellationToken);
        return (result, station);
    }

    [Fact]
    public async Task A_sound_draft_is_stored_under_the_phase_packet_id_and_the_fact_carries_no_text()
    {
        var (run, station) = await Run(Sound);

        Assert.Equal("stored", run.Result!.Outcome.Value);
        var stored = await station.Holdouts.Set("T-1", "expiry-p1", TestContext.Current.CancellationToken);
        Assert.True(stored.IsSuccess);
        var entry = JsonNode.Parse(LineStation.Fact(run, "holdoutSets"))![0]!;
        Assert.Equal(("P1", 2, true, stored.Value.Hash), ((string?)entry["phase"], (int?)entry["count"], (bool?)entry["canary"], (string?)entry["hash"]));
        var text = LineStation.Fact(run, "holdoutSets");
        Assert.DoesNotContain("expired link", text, StringComparison.Ordinal);
        Assert.DoesNotContain(stored.Value.Canary!, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sindri_adds_the_canary_with_a_random_phrase_and_drops_any_the_author_wrote()
    {
        var withCanary = Sound.Replace("[ { \"id\": \"P1-H1\"", "[ { \"id\": \"P1-C\", \"item\": null, \"kind\": \"canary\", \"steps\": [], \"expected\": \"x\", \"phrase\": \"author-phrase\" }, { \"id\": \"P1-H1\"", StringComparison.Ordinal);
        var (_, first) = await Run(withCanary);
        var (_, second) = await Run(Sound);

        var a = (await first.Holdouts.Set("T-1", "expiry-p1", TestContext.Current.CancellationToken)).Value;
        var b = (await second.Holdouts.Set("T-1", "expiry-p1", TestContext.Current.CancellationToken)).Value;

        Assert.Equal(3, a.Scenarios.Count);
        Assert.Equal(1, a.Scenarios.Count(static s => s.IsCanary));
        Assert.NotEqual("author-phrase", a.Canary, StringComparer.Ordinal);
        Assert.NotEqual(a.Canary, b.Canary, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Too_few_scenarios_an_unknown_item_and_a_missing_phase_are_invalid_with_findings_and_nothing_is_stored()
    {
        var (few, station) = await Run("""{ "sets": [ { "phase": "P1", "scenarios": [ { "id": "H1", "item": "AC-9", "kind": "api", "steps": ["s"], "expected": "e" } ] } ] }""");

        Assert.Equal("invalid", few.Result!.Outcome.Value);
        var findings = LineStation.Fact(few, "findings");
        Assert.Contains("not the 2 it needs", findings, StringComparison.Ordinal);
        Assert.Contains("no visible acceptance item", findings, StringComparison.Ordinal);
        Assert.True((await station.Holdouts.Set("T-1", "expiry-p1", TestContext.Current.CancellationToken)).IsError);
        var (none, _) = await Run("""{ "sets": [] }""");
        Assert.Contains("no set for phase P1", LineStation.Fact(none, "findings"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_or_unparseable_draft_is_invalid_not_a_crash()
    {
        var (missing, _) = await Run(draft: null);
        var (garbage, _) = await Run("not json");

        Assert.Equal(("invalid", "invalid"), (missing.Result!.Outcome.Value, garbage.Result!.Outcome.Value));
    }

    [Fact]
    public async Task A_phase_that_is_already_sealed_is_not_overwritten_and_the_Step_fails()
    {
        var station = new LineStation();
        var facts = await station.PlanFacts();
        facts["holdoutDraft"] = $$"""{"dir":"{{Dir}}"}""";
        var drafts = new InMemoryBriefFiles().With("holdout-sets.json", Sound);
        var context = LineStation.Context("step.plan.holdout-store", LineStation.PlanAddress, facts);
        _ = await station.PlanHoldoutStore(drafts).Run(context, TestContext.Current.CancellationToken);
        _ = await station.Holdouts.SealWith("T-1", "expiry-p1", "sha256:plan", TestContext.Current.CancellationToken);

        var again = await station.PlanHoldoutStore(drafts).Run(context, TestContext.Current.CancellationToken);

        Assert.NotNull(again.Failure);
        Assert.Contains("sealed", again.Failure, StringComparison.Ordinal);
    }

}
