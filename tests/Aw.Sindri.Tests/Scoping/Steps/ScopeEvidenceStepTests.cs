using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>'step.scope.evidence' on the block runtime (scoping spec §8.2, SC7): every check, and the packet assembled in the instance's run area.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeEvidenceStepTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ScopingFacts Gold() => ScopingFacts.Expiry().Without("rounds").Without("scopePacket");

    private static Task<BlockStepResult> Run(IRunArea area, ScopingFacts facts, IPackets? packets = null) =>
        new ScopeEvidenceStep(area, packets ?? new InMemoryPackets()).Run(ScopingStepContexts.Of("step.scope.evidence", facts), Token).AsTask();

    [Fact]
    public async Task Gold_facts_complete_and_the_packet_files_land_in_the_run_area()
    {
        var area = new InMemoryRunArea();

        var result = await Run(area, Gold());

        Assert.Equal("complete", result.Result!.Outcome.Value);
        Assert.Equal(["acceptance", "coverage", "openQuestions", "scopeMap", "scopePacket"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
        var expected = ScopePacket.Build(new ScopeFacts(Gold()));
        Assert.NotEmpty(expected);
        foreach (var file in expected)
        {
            Assert.Equal(file.Content, (await area.Read(ScopingStepContexts.Address, file.Path, Token)).Value);
        }
    }

    // The gold's two challenger rounds, the last one found something new.
    private static ScopingFacts FoundLast() => Gold().Edit("challenge", n => n["rounds"]![1]!["verdict"] = "found");

    private static Task<BlockStepResult> RunWithCap(ScopingFacts facts, int? cap) =>
        new ScopeEvidenceStep(new InMemoryRunArea(), new InMemoryPackets())
            .Run(ScopingStepContexts.Of("step.scope.evidence", facts, loopCaps: cap is { } n ? new Dictionary<string, int>(StringComparer.Ordinal) { ["challenge"] = n } : null), Token).AsTask();

    [Fact]
    public async Task A_found_last_round_is_spent_and_only_warns_when_the_recipes_challenge_cap_is_two()
    {
        var result = await RunWithCap(FoundLast(), cap: 2);

        Assert.Equal("complete", result.Result!.Outcome.Value);
        Assert.Contains(result.Result.Outputs.Keys, k => k.Value == "fact:scopePacket");
    }

    [Fact]
    public async Task The_same_rounds_still_fail_when_the_recipes_challenge_cap_is_three()
    {
        var result = await RunWithCap(FoundLast(), cap: 3);

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Without_a_recipe_cap_the_challenge_cap_falls_back_to_three()
    {
        var result = await RunWithCap(FoundLast(), cap: null);

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task An_acceptance_list_with_a_gap_is_incomplete_and_writes_no_packet()
    {
        var area = new InMemoryRunArea();

        var result = await Run(area, Gold().Edit("acceptance", n => n["items"]!.AsArray().Clear()));

        Assert.NotEqual("complete", result.Result!.Outcome.Value, StringComparer.Ordinal);
        Assert.Equal(["findings"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
        Assert.Equal(string.Empty, (await area.Read(ScopingStepContexts.Address, ScopePacket.Build(new ScopeFacts(Gold()))[0].Path, Token)).Value);
    }

    [Fact]
    public async Task A_run_area_that_cannot_be_written_fails_the_Step()
    {
        var result = await Run(new InMemoryRunArea { Unwritable = true }, Gold());

        Assert.Null(result.Result);
        Assert.Contains("cannot be written", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_records_fact_scopePacket_with_the_hash_aw_packets_gives_the_run_areas_scope_packet()
    {
        var area = new InMemoryRunArea();
        var dir = area.PathFor(ScopingStepContexts.Address, "scope-packet").Value;

        var result = await Run(area, Gold());

        var recorded = JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:scopePacket")].GetRawText())!;
        Assert.Equal(InMemoryPackets.HashOf(new PacketRef(PacketKind.Scoping, dir)), recorded["hash"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_packet_that_cannot_be_hashed_fails_the_Step()
    {
        var area = new InMemoryRunArea();
        var dir = area.PathFor(ScopingStepContexts.Address, "scope-packet").Value;

        var result = await Run(area, Gold(), new InMemoryPackets().Unreadable(dir));

        Assert.Null(result.Result);
        Assert.Contains("not a readable packet", result.Failure, StringComparison.Ordinal);
    }
}
