using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.plan.evidence' on the block runtime (line spec §6.1): the plan and design checks at the evidence stage, then the plan packet and one packet per phase assembled, checked by aw-packets and hashed.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanEvidenceStepTests
{
    private static string PlanLocation => "/run/" + LineStation.PlanAddress + "/plan-packet";

    private static string PhaseLocation => "/run/" + LineStation.PlanAddress + "/phase-packets/p1";

    [Fact]
    public async Task A_sound_plan_completes_with_the_plan_packet_and_its_phase_packets_named()
    {
        var station = new LineStation();

        var result = await station.PlanEvidenceRun();

        Assert.Equal("complete", result.Result!.Outcome.Value);
        Assert.Equal(["planPacket", "recommendation"], ScopingStepContexts.Keys(result));
        Assert.Equal("approve", (string?)JsonNode.Parse(LineStation.Fact(result, "recommendation"))!["outcome"]);   // complete evidence: every check passed
        var packet = JsonNode.Parse(LineStation.Fact(result, "planPacket"))!;
        Assert.Equal(InMemoryPackets.HashOf(new PacketRef(PacketKind.Plan, PlanLocation)), (string?)packet["hash"]);
        Assert.Equal(PlanLocation, (string?)packet["dir"]);
        Assert.Equal(("P1", "expiry-p1", PhaseLocation), ((string?)packet["phases"]![0]!["phase"], (string?)packet["phases"]![0]!["id"], (string?)packet["phases"]![0]!["dir"]));
        Assert.Equal(InMemoryPackets.HashOf(new PacketRef(PacketKind.Phase, PhaseLocation)), (string?)packet["phases"]![0]!["hash"]);
        var kinds = station.Packets.Calls.Where(static c => c.Verb == "validate" && c.Packet.Kind != PacketKind.Design).Select(static c => (c.Packet.Kind, c.Packet.Repo)).ToList();
        Assert.Equal([(PacketKind.Plan, LineStation.Checkout), (PacketKind.Phase, LineStation.Checkout)], kinds);
    }

    [Fact]
    public async Task The_plan_packet_is_PKTs_task_plan_a_derived_graph_the_holdout_sets_and_an_index_chaining_to_the_design()
    {
        var station = new LineStation();

        _ = await station.PlanEvidenceRun();

        var tasks = JsonNode.Parse(await station.PlanFile("plan-packet/tasks.json"))!;
        var task = tasks["tasks"]![0]!;
        Assert.Equal(("P1", "w", "S"), ((string?)task["id"], (string?)task["workstream"], (string?)task["size"]));
        Assert.Equal(["src/a.ts"], task["writeScope"]!.AsArray().Select(static p => (string?)p), StringComparer.Ordinal);
        Assert.Equal(["kind:feature", "test-change:tests/a.test.ts:the call changes"], task["rules"]!.AsArray().Select(static p => (string?)p), StringComparer.Ordinal);
        Assert.Equal("w", (string?)tasks["workstreams"]![0]!["id"]);
        var graph = JsonNode.Parse(await station.PlanFile("plan-packet/graph.json"))!["phases"]![0]!;
        Assert.Equal((1, 1, "default-branch", "main", false), ((int?)graph["stack"], (int?)graph["wave"], (string?)graph["base"]!["rule"], (string?)graph["base"]!["ref"], (bool?)graph["waitsForMerge"]));
        var holdout = JsonNode.Parse(await station.PlanFile("plan-packet/holdout.json"))!["phases"]![0]!;
        Assert.Equal(("P1", LineStation.SetHash, 3, 1), ((string?)holdout["id"], (string?)holdout["setHash"], (int?)holdout["count"], (int?)holdout["canaries"]));
        var index = JsonNode.Parse(await station.PlanFile("plan-packet/index.json"))!;
        Assert.Equal(2, index["parents"]!.AsArray().Count);
        Assert.Null(index["acceptance"]!["self"]);
        var packet = JsonNode.Parse(await station.PlanFile("plan-packet/packet.json"))!;
        Assert.Equal(("plan", "plan-expiry", LineStation.BaseSha), ((string?)packet["kind"], (string?)packet["id"], (string?)packet["baseSha"]));
    }

    [Fact]
    public async Task A_phase_packet_holds_its_goal_base_items_signals_hidden_set_and_design_sections()
    {
        var station = new LineStation();

        _ = await station.PlanEvidenceRun();

        var phase = JsonNode.Parse(await station.PlanFile("phase-packets/p1/phase.json"))!;
        Assert.Equal(("Ship b", "feature", "S"), ((string?)phase["goal"], (string?)phase["kind"], (string?)phase["size"]));
        Assert.Equal(("default-branch", "main"), ((string?)phase["base"]!["rule"], (string?)phase["base"]!["ref"]));
        Assert.Equal(["src/a.ts"], phase["allowedFiles"]!.AsArray().Select(static p => (string?)p), StringComparer.Ordinal);
        Assert.Equal(("tests/a.test.ts", "the call changes"), ((string?)phase["testChange"]![0]!["path"], (string?)phase["testChange"]![0]!["reason"]));
        var acceptance = JsonNode.Parse(await station.PlanFile("phase-packets/p1/acceptance.json"))!;
        Assert.Equal(["AC-1"], acceptance["items"]!.AsArray().Select(static i => (string?)i!["id"]), StringComparer.Ordinal);
        var signals = JsonNode.Parse(await station.PlanFile("phase-packets/p1/signals.json"))!;
        Assert.Equal("api-transcript", (string?)signals["signals"]![0]!["kind"]);
        var holdout = JsonNode.Parse(await station.PlanFile("phase-packets/p1/holdout.json"))!;
        Assert.Equal((LineStation.SetHash, 3), ((string?)holdout["setHash"], (int?)holdout["count"]));
        var design = await station.PlanFile("phase-packets/p1/design.md");
        Assert.Contains("## Summary", design, StringComparison.Ordinal);
        Assert.Contains("B holds.", design, StringComparison.Ordinal);
        Assert.DoesNotContain("More.", design, StringComparison.Ordinal);
        var index = JsonNode.Parse(await station.PlanFile("phase-packets/p1/index.json"))!;
        Assert.Equal(InMemoryPackets.HashOf(new PacketRef(PacketKind.Plan, PlanLocation)), (string?)index["parents"]![0]);
        var packet = JsonNode.Parse(await station.PlanFile("phase-packets/p1/packet.json"))!;
        Assert.Equal(("phase", "expiry-p1"), ((string?)packet["kind"], (string?)packet["id"]));
    }

    [Fact]
    public async Task A_phase_with_two_dependencies_waits_for_merge_and_is_based_on_its_first()
    {
        var station = new LineStation();

        var run = await station.PlanEvidenceRun(plan =>
        {
            var p1 = (JsonObject)plan["phases"]![0]!.DeepClone();
            var p2 = (JsonObject)p1.DeepClone();
            var p3 = (JsonObject)p1.DeepClone();
            p2["id"] = "P2";
            p2["deps"] = new JsonArray("P1");
            p2["acceptance"] = new JsonArray();
            p2["kind"] = "docs";
            p2["reason"] = "docs only";
            p3["id"] = "P3";
            p3["deps"] = new JsonArray("P1", "P2");
            p3["waitsForMerge"] = true;
            p3["acceptance"] = new JsonArray();
            p3["kind"] = "docs";
            p3["reason"] = "docs only";
            plan["phases"] = new JsonArray(p1, p2, p3);
            plan["execReview"] = JsonNode.Parse("""[{"phase":"P1","verdict":"executable"},{"phase":"P2","verdict":"executable"},{"phase":"P3","verdict":"executable"}]""");
            return plan;
        }, holdout: $$"""[{"phase":"P1","count":3,"hash":"{{LineStation.SetHash}}","canary":true},{"phase":"P2","count":3,"hash":"{{LineStation.SetHash}}","canary":true},{"phase":"P3","count":3,"hash":"{{LineStation.SetHash}}","canary":true}]""");

        Assert.True(run.Result?.Outcome.Value == "complete", run.Failure ?? (run.Result is null || run.Result.Outcome.Value == "complete" ? "" : LineStation.Fact(run, "findings")));
        var graph = JsonNode.Parse(await station.PlanFile("plan-packet/graph.json"))!["phases"]!.AsArray();
        Assert.Equal([1, 2, 3], graph.Select(static g => (int)g!["wave"]!));
        Assert.Equal([1, 1, 2], graph.Select(static g => (int)g!["stack"]!));   // P3 follows P1, which P2 already continues, so it opens the next stack
        Assert.Equal(("dependency-head", "P1", true), ((string?)graph[2]!["base"]!["rule"], (string?)graph[2]!["base"]!["ref"], (bool?)graph[2]!["waitsForMerge"]));
    }

    [Fact]
    public async Task A_second_child_of_a_phase_opens_its_own_stack()
    {
        var station = new LineStation();

        var run = await station.PlanEvidenceRun(plan =>
        {
            var p1 = (JsonObject)plan["phases"]![0]!.DeepClone();
            var p2 = (JsonObject)p1.DeepClone();
            var p3 = (JsonObject)p1.DeepClone();
            p2["id"] = "P2";
            p2["deps"] = new JsonArray("P1");
            p2["acceptance"] = new JsonArray();
            p2["kind"] = "docs";
            p2["reason"] = "docs only";
            p3["id"] = "P3";
            p3["deps"] = new JsonArray("P1");
            p3["acceptance"] = new JsonArray();
            p3["kind"] = "docs";
            p3["reason"] = "docs only";
            plan["phases"] = new JsonArray(p1, p2, p3);
            plan["execReview"] = JsonNode.Parse("""[{"phase":"P1","verdict":"executable"},{"phase":"P2","verdict":"executable"},{"phase":"P3","verdict":"executable"}]""");
            return plan;
        }, holdout: $$"""[{"phase":"P1","count":3,"hash":"{{LineStation.SetHash}}","canary":true},{"phase":"P2","count":3,"hash":"{{LineStation.SetHash}}","canary":true},{"phase":"P3","count":3,"hash":"{{LineStation.SetHash}}","canary":true}]""");

        Assert.True(run.Result?.Outcome.Value == "complete", run.Failure ?? (run.Result is null || run.Result.Outcome.Value == "complete" ? "" : LineStation.Fact(run, "findings")));
        var graph = JsonNode.Parse(await station.PlanFile("plan-packet/graph.json"))!["phases"]!.AsArray();
        Assert.Equal([1, 1, 2], graph.Select(static g => (int)g!["stack"]!));
    }

    [Fact]
    public async Task A_plan_check_that_fails_is_incomplete_with_the_findings_and_writes_no_packet()
    {
        var station = new LineStation();

        var result = await station.PlanEvidenceRun(holdout: "[]");

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        Assert.Equal(["findings"], ScopingStepContexts.Keys(result));
        Assert.Equal("PLN-HOLD-003", (string?)JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!["rule"]);
        Assert.Equal(string.Empty, await station.PlanFile("plan-packet/packet.json"));
    }

    [Fact]
    public async Task A_design_check_that_fails_again_at_plan_evidence_is_incomplete()
    {
        var station = new LineStation();
        var facts = await station.PlanFacts();
        var design = (JsonObject)JsonNode.Parse(facts["design"])!;
        design["signals"] = new JsonArray();
        facts["design"] = design.ToJsonString();

        var result = await station.PlanEvidence().Run(LineStation.Context("step.plan.evidence", LineStation.PlanAddress, facts), TestContext.Current.CancellationToken);

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        Assert.Contains("DSN-ACC-001", JsonNode.Parse(LineStation.Fact(result, "findings"))!.AsArray().Select(static f => (string?)f!["rule"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_plan_packet_aw_packets_refuses_is_incomplete_with_its_rule()
    {
        var station = new LineStation();
        _ = station.Packets.Fails(PlanLocation, new PacketFailure("PKT-GRAPH-001", "task P1", "a cycle", "Break it."));

        var result = await station.PlanEvidenceRun();

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        Assert.Equal("PKT-GRAPH-001", (string?)JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!["rule"]);
    }

    [Fact]
    public async Task A_phase_packet_aw_packets_refuses_is_incomplete_with_its_rule()
    {
        var station = new LineStation();
        _ = station.Packets.Fails(PhaseLocation, new PacketFailure("PKT-PHS-008", "phase.json", "no allowedFiles", "Add some."));

        var result = await station.PlanEvidenceRun();

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        Assert.Equal("PKT-PHS-008", (string?)JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!["rule"]);
    }

    [Fact]
    public async Task A_phase_with_no_holdout_set_hash_is_incomplete_not_a_packet_aw_packets_must_refuse()
    {
        var station = new LineStation();

        var result = await station.PlanEvidenceRun(holdout: """[{"phase":"P1","count":3,"hash":"","canary":true}]""");

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        Assert.Contains("PLN-HOLD-003", JsonNode.Parse(LineStation.Fact(result, "findings"))!.AsArray().Select(static f => (string?)f!["rule"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_packet_aw_packets_cannot_read_fails_the_Step_rather_than_completing_it()
    {
        var station = new LineStation();
        _ = station.Packets.Unreadable(PlanLocation);

        var result = await station.PlanEvidenceRun();

        Assert.Null(result.Result);
        Assert.Contains("could not check the plan packet", result.Failure, StringComparison.Ordinal);
    }
}
