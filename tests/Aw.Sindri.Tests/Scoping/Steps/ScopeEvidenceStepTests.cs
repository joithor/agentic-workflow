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

    private static ScopingFacts WithHead(ScopingFacts facts)
    {
        facts["baseSha"] = $"\"{Head}\"";
        return facts;
    }

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
        Assert.Equal(["acceptance", "coverage", "openQuestions", "recommendation", "scopeMap", "scopePacket"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
        var expected = ScopePacket.Build(new ScopeFacts(WithHead(Gold())));
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
    public void A_model_written_baseSha_in_the_scope_map_fact_is_replaced_by_the_stamped_one_or_dropped()
    {
        var stamped = WithHead(Gold().Edit("scopeMap", n => n["baseSha"] = "deadbeef"));
        var bare = Gold().Edit("scopeMap", n => n["baseSha"] = "deadbeef");

        Assert.Equal(Head, (string?)JsonNode.Parse(ScopePacket.Build(new ScopeFacts(stamped))[0].Content)!["baseSha"]);
        Assert.Null(JsonNode.Parse(ScopePacket.Build(new ScopeFacts(bare))[0].Content)!["baseSha"]);
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
