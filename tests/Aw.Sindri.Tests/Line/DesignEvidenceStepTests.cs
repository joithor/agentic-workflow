using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>'step.design.evidence' on the block runtime (line spec §4.1, §4.3): the evidence-stage checks, then the design packet assembled in the run area, checked by aw-packets against the checkout and hashed.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignEvidenceStepTests
{
    private static readonly string[] _files =
    [
        "acceptance.json", "contracts.json", "coverage.json", "decisions.json", "escapes.json", "index.json", "lens-rounds.jsonl", "packet.json", "runs.json", "sections.json", "signals.json", "spec.md", "style.json",
    ];

    private static string Location => "/run/" + LineStation.DesignAddress + "/design-packet";

    [Fact]
    public async Task A_sound_design_completes_with_the_candidate_packet_the_files_and_a_checked_hash()
    {
        var station = new LineStation();

        var result = await station.DesignEvidenceRun();

        Assert.Equal("complete", result.Result!.Outcome.Value);
        Assert.Equal(["design", "designPacket", "lensRounds"], ScopingStepContexts.Keys(result));
        var packet = JsonNode.Parse(LineStation.Fact(result, "designPacket"))!;
        Assert.Equal(InMemoryPackets.HashOf(new PacketRef(PacketKind.Design, Location)), (string?)packet["hash"]);
        Assert.Equal(Location, (string?)packet["dir"]);
        foreach (var file in _files)
        {
            Assert.NotEqual(string.Empty, await station.DesignFile("design-packet/" + file), StringComparer.Ordinal);
        }

        var (_, validated, _) = Assert.Single(station.Packets.Calls, static c => c.Verb == "validate");
        Assert.Equal((PacketKind.Design, Location, LineStation.Checkout), (validated.Kind, validated.Directory, validated.Repo));
    }

    [Fact]
    public async Task The_packet_files_carry_the_design_and_chain_the_acceptance_list_to_the_scope_packet()
    {
        var station = new LineStation();
        var facts = await station.DesignFacts();

        _ = await station.DesignEvidence().Run(LineStation.Context("step.design.evidence", LineStation.DesignAddress, facts), TestContext.Current.CancellationToken);

        var scope = JsonNode.Parse(facts["scope"])!;
        var index = JsonNode.Parse(await station.DesignFile("design-packet/index.json"))!;
        var acceptanceText = await station.DesignFile("design-packet/acceptance.json");
        var acceptance = JsonNode.Parse(acceptanceText)!;
        var packet = JsonNode.Parse(await station.DesignFile("design-packet/packet.json"))!;
        Assert.Equal([(string?)scope["hash"]], index["parents"]!.AsArray().Select(static p => (string?)p), StringComparer.Ordinal);
        Assert.Equal((string?)scope["acceptanceHash"], (string?)index["acceptance"]!["extends"]);
        Assert.Equal((string?)scope["acceptanceHash"], (string?)acceptance["parent"]);
        Assert.Equal(LinePackets.Sha256(acceptanceText), (string?)index["acceptance"]!["self"]);
        Assert.Equal(("design", "design-expiry", "sindri/web", LineStation.BaseSha, "draft"), ((string?)packet["kind"], (string?)packet["id"], (string?)packet["repo"], (string?)packet["baseSha"], (string?)packet["status"]));
        Assert.Equal("AC-1", (string?)acceptance["items"]![0]!["id"]);
        Assert.StartsWith("# Design", await station.DesignFile("design-packet/spec.md"), StringComparison.Ordinal);
        Assert.Equal("{\"round\":1", (await station.DesignFile("design-packet/lens-rounds.jsonl"))[..10]);
    }

    [Fact]
    public async Task An_escape_the_design_adds_is_listed_in_escapes_json_as_a_scope_escape()
    {
        var station = new LineStation();

        var result = await station.DesignEvidenceRun(design =>
        {
            design["escapes"] = JsonNode.Parse("""[{"kind":"surface","id":"S9","why":"export"}]""");
            return design;
        });

        Assert.Equal("complete", result.Result!.Outcome.Value);
        var escape = JsonNode.Parse(await station.DesignFile("design-packet/escapes.json"))!["escapes"]![0]!;
        Assert.Equal(("scoping", "design", "surface", "S9"), ((string?)escape["from"], (string?)escape["station"], (string?)escape["kind"], (string?)escape["id"]));
    }

    [Fact]
    public async Task A_check_that_fails_is_incomplete_with_the_findings_and_writes_no_packet()
    {
        var station = new LineStation();

        var result = await station.DesignEvidenceRun(design =>
        {
            design["signals"] = new JsonArray();
            return design;
        });

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        Assert.Equal(["findings"], ScopingStepContexts.Keys(result));
        Assert.Equal("DSN-ACC-001", (string?)JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!["rule"]);
        Assert.Equal(string.Empty, await station.DesignFile("design-packet/packet.json"));
        Assert.DoesNotContain(station.Packets.Calls, static c => c.Packet.Kind == PacketKind.Design);
    }

    [Fact]
    public async Task A_design_with_no_spec_text_is_incomplete_and_names_the_fix()
    {
        var station = new LineStation();
        var facts = await station.DesignFacts(design =>
        {
            _ = design.Remove("spec");
            return design;
        });
        facts["packs"] = "{}"; // no pinned style pack, so the style check doesn't stop the run before the packet is assembled

        var result = await station.DesignEvidence().Run(LineStation.Context("step.design.evidence", LineStation.DesignAddress, facts), TestContext.Current.CancellationToken);

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        var finding = JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!;
        Assert.Equal("DSN-PKT-001", (string?)finding["rule"]);
        Assert.Contains("design.spec", (string?)finding["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_packet_aw_packets_refuses_is_incomplete_with_its_rule_and_fix()
    {
        var station = new LineStation();
        _ = station.Packets.Fails(Location, new PacketFailure("PKT-DSN-007", "acceptance.json", "item AC-1 has no text", "Give it text."));

        var result = await station.DesignEvidenceRun();

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        var finding = JsonNode.Parse(LineStation.Fact(result, "findings"))![0]!;
        Assert.Equal(("PKT-DSN-007", "Give it text."), ((string?)finding["rule"], (string?)finding["fix"]));
    }

    [Fact]
    public async Task A_packet_aw_packets_cannot_read_fails_the_Step_rather_than_completing_it()
    {
        var station = new LineStation();
        _ = station.Packets.Unreadable(Location);

        var result = await station.DesignEvidenceRun();

        Assert.Null(result.Result);
        Assert.Contains("could not check the design packet", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unwritable_run_area_fails_the_Step()
    {
        var station = new LineStation();
        var facts = await station.DesignFacts();
        station.Area.Unwritable = true;

        var result = await station.DesignEvidence().Run(LineStation.Context("step.design.evidence", LineStation.DesignAddress, facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("can't write the design packet", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_scope_fact_fails_the_Step_before_any_check_runs()
    {
        var station = new LineStation();
        var facts = await station.DesignFacts();
        _ = facts.Remove("scope");

        var result = await station.DesignEvidence().Run(LineStation.Context("step.design.evidence", LineStation.DesignAddress, facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("fact:scope", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repo_with_no_checkout_fails_the_Step()
    {
        var station = new LineStation();
        var facts = await station.DesignFacts();

        var result = await new DesignEvidenceStep(station.Packets, station.Area, new InMemoryRepoCheckouts(), station.Styles).Run(
            LineStation.Context("step.design.evidence", LineStation.DesignAddress, facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("checkout", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Packet_files_round_trip_into_the_design_fact_the_plan_station_reads()
    {
        var station = new LineStation();
        var evidence = await station.DesignEvidenceRun();
        var intake = await station.PlanIntake().Run(
            LineStation.Context("step.plan.intake", LineStation.PlanAddress, new ScopingFacts { ["scopePacket"] = LineStation.ScopePacket, ["designPacket"] = LineStation.Fact(evidence, "designPacket") }),
            TestContext.Current.CancellationToken);

        var design = JsonNode.Parse(LineStation.Fact(intake, "design"))!;
        var original = JsonNode.Parse(LineStation.Design)!;

        Assert.Equal((string?)original["spec"], (string?)design["spec"]);
        Assert.Equal(original["sections"]!.ToJsonString(), design["sections"]!.ToJsonString());
        Assert.Equal(original["signals"]!.ToJsonString(), design["signals"]!.ToJsonString());
        Assert.Equal(original["acceptance"]!["items"]!.ToJsonString(), design["acceptance"]!["items"]!.ToJsonString());
        Assert.Equal(original["deferred"]!.ToJsonString(), design["deferred"]!.ToJsonString());
        Assert.Equal(LineStation.LensRounds, design["lensRounds"]!.ToJsonString());
        Assert.Equal(Location, (string?)design["packet"]!["dir"]);
    }

    [Fact]
    public async Task A_style_pack_that_changed_since_the_intake_pinned_it_fails_the_Step_with_SND_PACK_002()
    {
        var station = new LineStation();
        var facts = await station.DesignFacts();
        _ = station.Styles.With(LineStation.Style("plain", maxWords: 5) with { Hash = "sha256:" + new string('c', 64) });

        var result = await station.DesignEvidence().Run(LineStation.Context("step.design.evidence", LineStation.DesignAddress, facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("SND-PACK-002", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_design_whose_spec_breaks_the_pinned_style_packs_word_limit_is_incomplete_with_DSN_STYLE_001()
    {
        var station = new LineStation();
        _ = station.Styles.With(LineStation.Style("plain", maxWords: 1));

        var result = await station.DesignEvidenceRun();

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        Assert.Contains("DSN-STYLE-001", LineStation.Fact(result, "findings"), StringComparison.Ordinal);
    }
}
