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
        Assert.Equal(["acceptance", "coverage", "openQuestions", "recommendation", "scopeMap", "scopePacket"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
        var expected = ScopePacket.Build(new ScopeFacts(Gold()));
        Assert.NotEmpty(expected);
        foreach (var file in expected)
        {
            Assert.Equal(file.Content, (await area.Read(ScopingStepContexts.Address, file.Path, Token)).Value);
        }
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
    public async Task A_complete_packet_recommends_approve_when_no_question_is_open_and_revise_when_one_is()
    {
        var result = await Run(new InMemoryRunArea(), Gold());

        var open = JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:openQuestions")].GetRawText())!.AsArray().Count;
        var recommended = JsonNode.Parse(result.Result.Outputs[FactKey.From("fact:recommendation")].GetRawText())!;

        Assert.Equal(open == 0 ? "approve" : "revise", (string?)recommended["outcome"]);
        Assert.False(string.IsNullOrWhiteSpace((string?)recommended["why"]));
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
