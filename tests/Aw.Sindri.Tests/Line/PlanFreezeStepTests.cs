using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.plan.freeze' on the block runtime (line spec §6.1, LC-5): after the approve, freezes the design packet, the plan packet and each phase packet, as the owner, in that order; anything that moved or was refused is mismatch.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanFreezeStepTests
{
    private static string PlanLocation => "/run/" + LineStation.PlanAddress + "/plan-packet";

    private static string PhaseLocation => "/run/" + LineStation.PlanAddress + "/phase-packets/p1";

    private static string DesignLocation => "/run/" + LineStation.DesignAddress + "/design-packet";

    private static async Task<(ScopingFacts Facts, LineStation Station)> Approved(Func<JsonObject, JsonObject>? editPlan = null)
    {
        var station = new LineStation();
        var facts = await station.PlanFacts(editPlan);
        var evidence = await station.PlanEvidence().Run(LineStation.Context("step.plan.evidence", LineStation.PlanAddress, facts), TestContext.Current.CancellationToken);
        facts["planPacket"] = LineStation.Fact(evidence, "planPacket");
        return (facts, station);
    }

    private static async Task<Core.Workflow.Blocks.BlockStepResult> Freeze(LineStation station, ScopingFacts facts) =>
        await station.PlanFreeze().Run(LineStation.Context("step.plan.freeze", LineStation.PlanAddress, facts), TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_design_plan_and_phase_packets_freeze_in_order_as_the_owner_and_the_Step_is_frozen()
    {
        var (facts, station) = await Approved();
        var before = station.Packets.Calls.Count;

        var result = await Freeze(station, facts);

        Assert.Equal("frozen", result.Result!.Outcome.Value);
        Assert.Equal(["phasePackets", "planPacket"], ScopingStepContexts.Keys(result));
        var freezes = station.Packets.Calls.Skip(before).Where(static c => c.Verb == "freeze").ToList();
        Assert.Equal([(PacketKind.Design, DesignLocation), (PacketKind.Plan, PlanLocation), (PacketKind.Phase, PhaseLocation)], freezes.Select(static f => (f.Packet.Kind, f.Packet.Directory)));
        Assert.All(freezes, f => Assert.Equal((PacketFreezer.Owner, LineStation.Checkout), (f.By, f.Packet.Repo)));
        var plan = JsonNode.Parse(LineStation.Fact(result, "planPacket"))!;
        Assert.Equal((true, PlanLocation, InMemoryPackets.HashOf(new PacketRef(PacketKind.Plan, PlanLocation))), ((bool?)plan["frozen"], (string?)plan["dir"], (string?)plan["hash"]));
        var phase = JsonNode.Parse(LineStation.Fact(result, "phasePackets"))![0]!;
        Assert.Equal(("P1", "expiry-p1", PhaseLocation, InMemoryPackets.HashOf(new PacketRef(PacketKind.Phase, PhaseLocation))), ((string?)phase["phase"], (string?)phase["id"], (string?)phase["dir"], (string?)phase["hash"]));
    }

    [Fact]
    public async Task A_stored_hidden_set_is_sealed_to_the_plan_packet_just_frozen()
    {
        var (facts, station) = await Approved();
        var set = """{ "schemaVersion": 1, "phase": "expiry-p1", "scenarios": [ { "id": "H1", "item": "AC-1", "kind": "api", "steps": ["s"], "expected": "e" }, { "id": "C", "item": null, "kind": "canary", "steps": [], "expected": "never", "phrase": "canary-abc" } ] }""";
        _ = (await station.Holdouts.Save("T-1", "expiry-p1", set, TestContext.Current.CancellationToken)).Value;

        var result = await Freeze(station, facts);

        Assert.Equal("frozen", result.Result!.Outcome.Value);
        var sealedSet = await station.Holdouts.Seal("T-1", "expiry-p1", TestContext.Current.CancellationToken);
        Assert.Equal((Core.Prove.HoldoutFiles.Hash(set), InMemoryPackets.HashOf(new PacketRef(PacketKind.Plan, PlanLocation))), (sealedSet.Value.SetHash, sealedSet.Value.PlanHash));
    }

    [Fact]
    public async Task A_second_freeze_of_the_same_packets_is_still_frozen()
    {
        var (facts, station) = await Approved();
        _ = await Freeze(station, facts);

        var again = await Freeze(station, facts);

        Assert.Equal("frozen", again.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_plan_packet_that_hashes_to_something_else_than_the_evidence_checked_is_mismatch_and_nothing_is_frozen()
    {
        var (facts, station) = await Approved();
        var packet = JsonNode.Parse(facts["planPacket"])!;
        packet["hash"] = "sha256:" + new string('0', 64);
        facts["planPacket"] = packet.ToJsonString();

        var result = await Freeze(station, facts);

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        var finding = JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!;
        Assert.Equal("SND-LINE-001", (string?)finding["rule"]);
        Assert.Contains("plan packet", (string?)finding["message"], StringComparison.Ordinal);
        Assert.DoesNotContain(station.Packets.Calls, static c => c.Verb == "freeze" && c.Packet.Kind == PacketKind.Plan);
    }

    [Fact]
    public async Task A_phase_packet_that_hashes_to_something_else_than_the_evidence_checked_is_mismatch_and_nothing_is_frozen()
    {
        var (facts, station) = await Approved();
        var packet = JsonNode.Parse(facts["planPacket"])!;
        packet["phases"]![0]!["hash"] = "sha256:" + new string('0', 64);
        facts["planPacket"] = packet.ToJsonString();

        var result = await Freeze(station, facts);

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        var finding = JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!;
        Assert.Equal("SND-LINE-001", (string?)finding["rule"]);
        Assert.Contains("phase packet P1", (string?)finding["message"], StringComparison.Ordinal);
        Assert.DoesNotContain(station.Packets.Calls, static c => c.Verb == "freeze");
    }

    [Fact]
    public async Task A_freeze_that_reports_frozen_with_no_hash_fails_the_Step_instead_of_recording_an_empty_hash()
    {
        var (facts, station) = await Approved();

        var result = await new PlanFreezeStep(new HashlessFreezes(station.Packets), station.Checkouts, station.Holdouts).Run(LineStation.Context("step.plan.freeze", LineStation.PlanAddress, facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("no hash", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_packet_that_fails_its_checks_at_the_freeze_is_mismatch_with_the_rule()
    {
        var (facts, station) = await Approved();
        _ = station.Packets.Fails(PhaseLocation, new PacketFailure("PKT-PHS-008", "phase.json", "no allowedFiles", "Add some."));

        var result = await Freeze(station, facts);

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        var finding = JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!;
        Assert.Equal(("PKT-PHS-008", "Add some."), ((string?)finding["rule"], (string?)finding["fix"]));
    }

    [Fact]
    public async Task A_packet_a_machine_froze_first_is_mismatch_because_the_owners_freeze_was_not_recorded()
    {
        var (facts, station) = await Approved();
        _ = await station.Packets.Freeze(new PacketRef(PacketKind.Plan, PlanLocation, LineStation.Checkout), PacketFreezer.Machine, TestContext.Current.CancellationToken);

        var result = await Freeze(station, facts);

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        Assert.Equal("PKT-FREEZE-002", (string?)JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!["rule"]);
    }

    [Fact]
    public async Task A_plan_check_that_fails_again_at_the_freeze_is_mismatch_and_nothing_is_frozen()
    {
        var (facts, station) = await Approved();
        facts["holdoutSets"] = "[]";

        var result = await Freeze(station, facts);

        Assert.Equal("mismatch", result.Result!.Outcome.Value);
        Assert.Equal("PLN-HOLD-003", (string?)JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!["rule"]);
        Assert.DoesNotContain(station.Packets.Calls, static c => c.Verb == "freeze");
    }

    [Fact]
    public async Task A_packet_aw_packets_cannot_read_fails_the_Step_rather_than_freezing_it()
    {
        var (facts, station) = await Approved();
        _ = station.Packets.Unreadable(DesignLocation);

        var result = await Freeze(station, facts);

        Assert.Null(result.Result);
        Assert.Contains("could not hash the design packet", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_design_with_no_packet_fails_the_Step_naming_the_fix()
    {
        var (facts, station) = await Approved();
        var design = (JsonObject)JsonNode.Parse(facts["design"])!;
        _ = design.Remove("packet");
        facts["design"] = design.ToJsonString();

        var result = await Freeze(station, facts);

        Assert.Null(result.Result);
        Assert.Contains("design.packet", result.Failure, StringComparison.Ordinal);
    }

    private sealed class HashlessFreezes(IPackets inner) : IPackets
    {
        public ValueTask<Result<PacketValidation>> Validate(PacketRef packet, CancellationToken cancellationToken) => inner.Validate(packet, cancellationToken);

        public ValueTask<Result<string>> Hash(PacketRef packet, CancellationToken cancellationToken) => inner.Hash(packet, cancellationToken);

        public async ValueTask<Result<PacketFreeze>> Freeze(PacketRef packet, PacketFreezer by, CancellationToken cancellationToken) =>
            (await inner.Freeze(packet, by, cancellationToken).ConfigureAwait(false)).Map(static f => f with { Hash = null });
    }
}
