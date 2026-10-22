using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>'step.scope.evidence' on the block runtime (scoping spec §8.2, SC7): every check, and the packet assembled in the instance's run area.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeEvidenceStepTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ScopingFacts Gold() => ScopingFacts.Expiry().Without("rounds").Without("scopePacket");

    private const string Head = "3288642cd47359fff2642b8bf429976915ca4894";

    private static readonly RepoName _web = RepoName.From("web");

    private static Task<BlockStepResult> Run(IRunArea area, ScopingFacts facts, IPackets? packets = null)
    {
        var diff = new FakeRunDiff();
        diff.Add("/work/web", Head);
        return new ScopeEvidenceStep(area, packets ?? new InMemoryPackets(), new InMemoryRepoCheckouts().With("web", "/work/web"), diff)
            .Run(ScopingStepContexts.Of("step.scope.evidence", facts) with { Repo = _web }, Token).AsTask();
    }

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

    [Fact]
    public async Task The_scope_map_carries_the_checkouts_head_as_baseSha_and_the_change_slug_of_the_briefs_subject()
    {
        var area = new InMemoryRunArea();

        _ = await Run(area, Gold());

        var map = JsonNode.Parse((await area.Read(ScopingStepContexts.Address, "scope-packet/scope-map.json", Token)).Value)!;
        Assert.Equal(Head, (string?)map["baseSha"]);
        Assert.Equal("shared-links-expire-after-a-chosen-time", (string?)map["change"]);
    }

    [Fact]
    public async Task A_model_written_baseSha_in_the_facts_never_reaches_the_scope_map()
    {
        var area = new InMemoryRunArea();

        _ = await Run(area, Gold().Edit("scopeMap", n => n["baseSha"] = "deadbeef"));

        var map = JsonNode.Parse((await area.Read(ScopingStepContexts.Address, "scope-packet/scope-map.json", Token)).Value)!;
        Assert.Equal(Head, (string?)map["baseSha"]);
    }

    [Fact]
    public async Task A_checkout_with_no_head_fails_the_Step()
    {
        var result = await new ScopeEvidenceStep(new InMemoryRunArea(), new InMemoryPackets(), new InMemoryRepoCheckouts().With("web", "/work/web"), new FakeRunDiff())
            .Run(ScopingStepContexts.Of("step.scope.evidence", Gold()) with { Repo = _web }, Token);

        Assert.Null(result.Result);
        Assert.Contains("/work/web", result.Failure, StringComparison.Ordinal);
    }
}
