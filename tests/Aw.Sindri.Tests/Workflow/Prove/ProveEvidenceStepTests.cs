using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>'step.prove.evidence' on the block runtime (line spec §7.3): the proof rules, the pack in the run area and the machine freeze.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveEvidenceStepTests
{
    private const string Head = "abcabcabcabcabcabc";
    private const string Address = "T-1/prove/1";
    private const string Dir = "/packets/p1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Rig
    {
        public InMemoryBriefFiles Files { get; } = new InMemoryBriefFiles()
            .With("acceptance.json", """{"schemaVersion":1,"parent":"sha256:aa","items":[{"id":"AC-1","text":"one","status":"failing","evidence":[]}]}""")
            .With("signals.json", """{"schemaVersion":1,"signals":{"AC-1":[{"id":"SG-1a","kind":"test","statement":"s1","runsAt":"head"}]}}""")
            .With("phase.json", """{"kind":"feature"}""")
            .With("holdout.json", """{"schemaVersion":1,"setHash":"sha256:aa","count":2}""")
            .With("packet.json", """{"schemaVersion":1,"kind":"phase","id":"line-p1","repo":"owner/name","baseSha":"1111111111111111111111111111111111111111","status":"frozen","hash":null,"links":{}}""");

        public InMemoryRunArea Area { get; } = new();

        public InMemoryPackets Packets { get; } = new();

        public InMemoryRepoCheckouts Checkouts { get; } = new InMemoryRepoCheckouts().With("name", "/checkouts/name");

        public ProveEvidenceStep Step() => new(Files, Area, Packets, Checkouts);
    }

    private static Dictionary<string, string> Facts(string? proof = null, string? holdout = null)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["phasePacket"] = $$"""{"hash":"sha256:{{new string('b', 64)}}","dir":"{{Dir}}"}""",
            ["built"] = $$"""{"head":"{{Head}}"}""",
            ["redTests"] = """{"tests":[{"id":"T1","outcome":"failed"}]}""",
            ["beforeAfter"] = $$"""{"commit":"{{Head}}","results":[]}""",
            ["holdoutResult"] = holdout ?? $$"""{"commit":"{{Head}}","canary":"clean","results":[{"scenario":"H1","item":"AC-1","verdict":"pass"},{"scenario":"H2","item":"AC-1","verdict":"pass"}]}""",
        };
        if (proof is not null)
        {
            facts["proof"] = proof;
        }

        return facts;
    }

    private static StepContext Context(Dictionary<string, string> facts, bool address = true, bool repo = true) =>
        BuildShipContexts.Of("step.prove.evidence", facts) with { Address = address ? Address : null, Repo = repo ? RepoName.From("name") : null };

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private static string Passing => $$"""{"id":"x","items":[{"id":"AC-1","artifacts":[{"kind":"test","path":"evidence/a.txt","sha256":"sha256:{{new string('a', 64)}}","commit":"{{Head}}","at":"t","passing":true,"signal":"SG-1a"}]}]}""";

    [Fact]
    public async Task A_proof_that_holds_writes_the_pack_to_the_run_area_and_machine_freezes_it()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(Facts(Passing)), Token);

        Assert.Equal("complete", result.Result!.Outcome.Value);
        var pack = Out(result, "proofPack");
        var written = await rig.Area.Read(Address, "proof-pack/proof.json", Token);
        Assert.Contains(Head, written.Value, StringComparison.Ordinal);
        foreach (var name in Aw.Sindri.Core.Prove.ProofPack.FileNames)
        {
            Assert.NotEmpty((await rig.Area.Read(Address, "proof-pack/" + name, Token)).Value);
        }

        var (_, packet, by) = Assert.Single(rig.Packets.Calls, static c => c.Verb == "freeze");
        Assert.Equal((PacketKind.Proof, PacketFreezer.Machine, "/checkouts/name"), (packet.Kind, by, packet.Repo));
        Assert.Equal(packet.Directory, (string?)pack["dir"]);
        Assert.Equal(InMemoryPackets.HashOf(packet), (string?)pack["hash"]);
    }

    [Fact]
    public async Task A_proof_that_holds_with_rules_not_run_still_completes_and_records_them_in_the_pack()
    {
        var rig = new Rig();

        var result = await rig.Step().Run(Context(Facts()), Token);   // no fact:proof: the artifact rules cannot run

        Assert.Equal("complete", result.Result!.Outcome.Value);
        Assert.Contains("PRV-ACC-001", (await rig.Area.Read(Address, "proof-pack/proof.json", Token)).Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_proof_that_breaks_a_rule_is_incomplete_with_findings_and_writes_nothing()
    {
        var rig = new Rig();
        var failed = $$"""{"commit":"{{Head}}","canary":"clean","results":[{"scenario":"H1","item":"AC-1","verdict":"fail"},{"scenario":"H2","item":"AC-1","verdict":"pass"}]}""";

        var result = await rig.Step().Run(Context(Facts(Passing, failed)), Token);

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        var finding = Assert.Single(Out(result, "findings").AsArray())!;
        Assert.Equal(("PRV-HOLD-001", "AC-1", "fail"), ((string?)finding["rule"], (string?)finding["item"], (string?)finding["severity"]));
        Assert.Empty(rig.Packets.Calls);
        Assert.Equal(string.Empty, (await rig.Area.Read(Address, "proof-pack/proof.json", Token)).Value);
        Assert.False(result.Result.Outputs.ContainsKey(FactKey.From("fact:proofPack")));
    }

    [Fact]
    public async Task A_pack_that_aw_packets_refuses_is_incomplete_and_the_failed_checks_are_the_findings()
    {
        var rig = new Rig();
        _ = rig.Packets.Fails("/run/T-1/prove/1/proof-pack", new PacketFailure("PKT-PRF-008", "proof.json", "an item is passing without evidence", "add the evidence"));

        var result = await rig.Step().Run(Context(Facts(Passing)), Token);

        Assert.Equal("incomplete", result.Result!.Outcome.Value);
        Assert.Equal("PKT-PRF-008", (string?)Assert.Single(Out(result, "findings").AsArray())!["rule"]);
    }

    [Fact]
    public async Task A_run_area_that_cannot_be_written_fails_the_Step()
    {
        var rig = new Rig();
        rig.Area.Unwritable = true;

        var result = await rig.Step().Run(Context(Facts(Passing)), Token);

        Assert.Null(result.Result);
        Assert.Empty(rig.Packets.Calls);
    }

    [Fact]
    public async Task A_packet_tool_that_cannot_run_fails_the_Step()
    {
        var rig = new Rig();
        _ = rig.Packets.Unreadable("/run/T-1/prove/1/proof-pack");

        var result = await rig.Step().Run(Context(Facts(Passing)), Token);

        Assert.Null(result.Result);
        Assert.Contains("aw-packets", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Step_without_an_address_a_repo_or_a_checkout_fails_and_says_which()
    {
        var rig = new Rig();

        Assert.Contains("address", (await rig.Step().Run(Context(Facts(Passing), address: false), Token)).Failure, StringComparison.Ordinal);
        Assert.Contains("repo", (await rig.Step().Run(Context(Facts(Passing), repo: false), Token)).Failure, StringComparison.Ordinal);
        var bare = new Rig();
        _ = bare.Checkouts.With("other", "/x");
        var noCheckout = new ProveEvidenceStep(bare.Files, bare.Area, bare.Packets, new InMemoryRepoCheckouts());
        Assert.Contains("checkout", (await noCheckout.Run(Context(Facts(Passing)), Token)).Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_phase_packet_that_cannot_be_read_fails_the_Step()
    {
        var rig = new Rig();
        var step = new ProveEvidenceStep(new InMemoryBriefFiles(), rig.Area, rig.Packets, rig.Checkouts);

        var result = await step.Run(Context(Facts(Passing)), Token);

        Assert.Null(result.Result);
        Assert.Contains(Dir, result.Failure, StringComparison.Ordinal);
    }
}
