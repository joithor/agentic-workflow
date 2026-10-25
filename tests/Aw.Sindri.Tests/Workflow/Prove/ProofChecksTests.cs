using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>The proof rules over facts (line spec §7.3, PRV-*): each rule on its own, and the not-run list.</summary>
[Lane(TestLane.Unit)]
public sealed class ProofChecksTests
{
    private const string Head = "abcabcabcabcabcabc";

    private static JsonNode Acceptance() => JsonNode.Parse("""{"schemaVersion":1,"parent":"sha256:aa","items":[{"id":"AC-1","text":"one","status":"failing","evidence":[]},{"id":"AC-2","text":"two","status":"failing","evidence":[]},{"id":"AC-0","text":"old","status":"superseded","supersededBy":"AC-1","evidence":[]}]}""")!;

    private static JsonNode Signals() => JsonNode.Parse("""{"schemaVersion":1,"signals":[{"item":"AC-1","id":"SG-1a","kind":"test","statement":"s1","runs":"head"},{"item":"AC-2","id":"SG-2a","kind":"api-transcript","statement":"s2","runs":"base+head"}]}""")!;

    private static string Artifact(string signal, string commit, string extra = "") => $$"""{"kind":"test","path":"evidence/{{signal}}.txt","sha256":"sha256:{{new string('a', 64)}}","commit":"{{commit}}","at":"2026-10-19T10:00:00Z","passing":true,"signal":"{{signal}}"{{extra}}}""";

    private static JsonNode Proof(string one, string two) => JsonNode.Parse($$"""{"head":"{{Head}}","items":[{"id":"AC-1","artifacts":[{{one}}]},{"id":"AC-2","artifacts":[{{two}}]}]}""")!;

    private static ProofFacts Good(Func<ProofFacts, ProofFacts>? change = null)
    {
        var facts = new ProofFacts(
            Head,
            Acceptance(),
            Signals(),
            "feature",
            HoldoutCount: 3,
            Proof(Artifact("SG-1a", Head), Artifact("SG-2a", Head)),
            Red: JsonNode.Parse("""{"AC-1":["T1"],"AC-2":{"tests":[{"id":"T2","path":"tests/b.cs"}],"commit":"c0ffee","output":"1 failed"}}"""),
            BeforeAfter: JsonNode.Parse($$"""{"commit":"{{Head}}","results":[{"item":"AC-2","signal":"SG-2a","base":"fail","head":"pass","baseScript":"s.sh","headScript":"s.sh"}]}"""),
            Holdout: JsonNode.Parse($$"""{"commit":"{{Head}}","canary":"clean","results":[{"scenario":"H1","item":"AC-1","verdict":"pass"},{"scenario":"H2","item":"AC-2","verdict":"pass"},{"scenario":"H3","item":"AC-1","verdict":"pass"},{"scenario":"C","kind":"canary","verdict":"pass"}]}"""));
        return change is null ? facts : change(facts);
    }

    private static string[] Rules(ProofReport report) => [.. report.Failures.Select(static f => f.Rule + (f.Item is null ? string.Empty : "@" + f.Item))];

    [Fact]
    public void A_phase_with_every_artifact_on_the_head_holds_and_only_the_application_run_is_not_run()
    {
        var report = ProofChecks.Run(Good());

        Assert.Empty(report.Failures);
        Assert.True(report.Holds);
        Assert.Equal("PRV-APP-001", Assert.Single(report.NotRun)[..11]);
    }

    [Fact]
    public void Without_fact_proof_the_artifact_rules_cannot_run_and_the_report_says_so_instead_of_holding_them()
    {
        var report = ProofChecks.Run(Good(static f => f with { Proof = null }));

        Assert.Empty(report.Failures);
        Assert.Equal(["PRV-ACC-001", "PRV-SHA-001"], report.Unrunnable.Select(static n => n[..11]), StringComparer.Ordinal);
        Assert.DoesNotContain(report.NotRun, static n => n.StartsWith("PRV-ACC-001", StringComparison.Ordinal) || n.StartsWith("PRV-SHA-001", StringComparison.Ordinal));
        Assert.Empty(ProofChecks.Run(Good()).Unrunnable);
    }

    [Fact]
    public void An_item_with_no_passing_artifact_on_the_head_fails_acc_001_and_a_superseded_item_is_not_asked_for()
    {
        var report = ProofChecks.Run(Good(static f => f with { Proof = Proof(Artifact("SG-1a", Head), Artifact("SG-2a", "old", ",\"superseded\":true")) }));

        Assert.Equal(["PRV-ACC-001@AC-2"], Rules(report));
    }

    [Fact]
    public void An_artifact_of_an_earlier_head_that_is_not_marked_superseded_fails_sha_001()
    {
        var report = ProofChecks.Run(Good(static f => f with { Proof = Proof(Artifact("SG-1a", Head) + "," + Artifact("SG-1a", "old"), Artifact("SG-2a", Head)) }));

        Assert.Equal(["PRV-SHA-001@AC-1"], Rules(report));
    }

    [Fact]
    public void A_docs_or_config_phase_needs_no_application_run()
    {
        Assert.Empty(ProofChecks.Run(Good(static f => f with { PhaseKind = "docs" })).NotRun);
        Assert.Empty(ProofChecks.Run(Good(static f => f with { PhaseKind = "config" })).NotRun);
    }

    [Theory]
    [InlineData("""{"commit":"abcabcabcabcabcabc","results":[]}""", "has no before/after result")]
    [InlineData("""{"results":[{"item":"AC-2","signal":"SG-2a","base":"pass","head":"pass"}]}""", "did not fail at the base")]
    [InlineData("""{"results":[{"item":"AC-2","signal":"SG-2a","base":"fail","head":"fail"}]}""", "did not pass at the head")]
    [InlineData("""{"results":[{"item":"AC-2","signal":"SG-2a","base":"fail","head":"pass","baseScript":"a.sh","headScript":"b.sh"}]}""", "different script")]
    public void A_base_and_head_signal_needs_one_script_failing_at_base_and_passing_at_head(string beforeAfter, string problem)
    {
        var report = ProofChecks.Run(Good(f => f with { BeforeAfter = JsonNode.Parse(beforeAfter) }));

        var finding = Assert.Single(report.Failures);
        Assert.Equal(("PRV-BA-001", "AC-2"), (finding.Rule, finding.Item));
        Assert.Contains(problem, finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"AC-1":["T1"],"AC-2":["T2"]}""")]
    [InlineData("""{"AC-1":[{"id":"T1","path":"a.cs"}],"AC-2":{"tests":["T2"]}}""")]
    [InlineData("""{"AC-1":{"appOnly":{"signal":"SG-1a"}},"AC-2":{"tests":[],"appOnly":{"signal":"SG-2a"},"commit":"c","output":"o"}}""")]
    [InlineData("""{"AC-1":["T1"],"AC-2":["T2"],"AC-0":[]}""")]
    public void Every_live_item_with_a_test_or_an_app_only_entry_holds_in_both_shapes_of_the_spec(string red) =>
        Assert.Empty(ProofChecks.Run(Good(f => f with { Red = JsonNode.Parse(red) })).Failures);

    [Fact]
    public void An_item_with_no_test_and_no_app_only_entry_is_one_finding_naming_the_item()
    {
        Assert.Equal(["PRV-RED-001@AC-2"], Rules(ProofChecks.Run(Good(static f => f with { Red = JsonNode.Parse("""{"AC-1":["T1"]}""") }))));
        Assert.Equal(["PRV-RED-001@AC-1", "PRV-RED-001@AC-2"], Rules(ProofChecks.Run(Good(static f => f with { Red = JsonNode.Parse("""{"AC-1":[],"AC-2":{"commit":"c"}}""") }))));
        Assert.Equal(["PRV-RED-001@AC-1", "PRV-RED-001@AC-2"], Rules(ProofChecks.Run(Good(static f => f with { Red = JsonNode.Parse("{}") }))));
    }

    [Theory]
    [InlineData("""{"AC-1":"T1","AC-2":["T2"]}""", "AC-1")]
    [InlineData("""{"AC-1":["T1"],"AC-2":{"tests":"T2"}}""", "AC-2")]
    [InlineData("""{"AC-1":[7],"AC-2":["T2"]}""", "AC-1")]
    [InlineData("""{"AC-1":["T1"],"AC-2":{"appOnly":"yes"}}""", "AC-2")]
    public void An_entry_of_the_wrong_shape_is_a_finding_that_names_the_item_and_the_expected_shape_never_no_tests(string red, string item)
    {
        var finding = Assert.Single(ProofChecks.Run(Good(f => f with { Red = JsonNode.Parse(red) })).Failures);

        Assert.Equal(("PRV-RED-001", item), (finding.Rule, finding.Item));
        Assert.Contains("expected", finding.Message, StringComparison.Ordinal);
        Assert.Contains("tests", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_red_tests_fact_that_is_not_an_object_is_one_finding_and_a_docs_phase_needs_no_red_tests()
    {
        Assert.Equal(["PRV-RED-001"], Rules(ProofChecks.Run(Good(static f => f with { Red = JsonNode.Parse("""[{"id":"T1","outcome":"failed"}]""") }))));
        Assert.Empty(ProofChecks.Run(Good(static f => f with { Red = JsonNode.Parse("{}"), PhaseKind = "docs" })).Failures);
    }

    [Fact]
    public void The_canary_is_not_counted_among_the_scenarios_that_ran_nor_judged_nor_spent()
    {
        static string Holdout(string canary) => $$"""{"commit":"{{Head}}","canary":"clean","results":[{"scenario":"H1","item":"AC-1","verdict":"pass"},{"scenario":"H2","item":"AC-2","verdict":"pass"},{{canary}}]}""";
        var onlyTwoAndCanary = ProofChecks.Run(Good(f => f with { Holdout = JsonNode.Parse(Holdout("""{"scenario":"C","kind":"canary","verdict":"pass"}""")) }));
        var noItemCanary = ProofChecks.Run(Good(f => f with { Holdout = JsonNode.Parse(Holdout("""{"scenario":"C","verdict":"fail"}""")), HoldoutCount = 2 }));
        var spentCanary = ProofChecks.Run(Good(f => f with { Holdout = JsonNode.Parse(Holdout("""{"scenario":"C","kind":"canary","verdict":"fail","spent":true}""")), HoldoutCount = 2, HoldoutMin = 2 }));

        Assert.Equal(["PRV-HOLD-001"], Rules(onlyTwoAndCanary));   // count 3, two hidden scenarios ran: the canary does not make the third
        Assert.Empty(noItemCanary.Failures);
        Assert.Empty(spentCanary.Failures);
    }

    [Fact]
    public void Every_hidden_scenario_must_have_run_on_the_head_and_passed()
    {
        var fewer = ProofChecks.Run(Good(static f => f with { HoldoutCount = 4 }));
        var elsewhere = ProofChecks.Run(Good(static f => f with { Holdout = JsonNode.Parse("""{"commit":"old","canary":"clean","results":[{"item":"AC-1","verdict":"pass"},{"item":"AC-2","verdict":"pass"},{"item":"AC-1","verdict":"pass"}]}""") }));
        var failed = ProofChecks.Run(Good(static f => f with { Holdout = JsonNode.Parse($$"""{"commit":"{{Head}}","canary":"clean","results":[{"scenario":"H9","item":"AC-1","verdict":"fail"},{"item":"AC-2","verdict":"pass"},{"item":"AC-1","verdict":"pass"}]}""") }));

        Assert.Equal(["PRV-HOLD-001"], Rules(fewer));
        Assert.Equal(["PRV-HOLD-001"], Rules(elsewhere));
        Assert.Equal(["PRV-HOLD-001@AC-1"], Rules(failed));
        Assert.DoesNotContain("H9", failed.Failures[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_hidden_set_size_is_not_run()
    {
        var report = ProofChecks.Run(Good(static f => f with { HoldoutCount = null }));

        Assert.Empty(report.Failures);
        Assert.Contains(report.NotRun, static n => n.StartsWith("PRV-HOLD-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Too_many_spent_scenarios_fail_hold_002_against_the_minimum()
    {
        var spent = $$"""{"commit":"{{Head}}","canary":"clean","results":[{"item":"AC-1","verdict":"pass","spent":true},{"item":"AC-2","verdict":"pass","spent":true},{"item":"AC-1","verdict":"pass"}]}""";

        Assert.Equal(["PRV-HOLD-002"], Rules(ProofChecks.Run(Good(f => f with { Holdout = JsonNode.Parse(spent) }))));
        Assert.Empty(ProofChecks.Run(Good(f => f with { Holdout = JsonNode.Parse(spent), HoldoutMin = 1 })).Failures);
    }

    [Fact]
    public void The_canary_scan_must_be_clean_and_a_missing_scan_is_not_run()
    {
        var leaked = ProofChecks.Run(Good(static f => f with { Holdout = JsonNode.Parse($$"""{"commit":"{{Head}}","canary":"leaked","results":[{"item":"AC-1","verdict":"pass"},{"item":"AC-2","verdict":"pass"},{"item":"AC-1","verdict":"pass"}]}""") }));
        var unscanned = ProofChecks.Run(Good(static f => f with { Holdout = JsonNode.Parse($$"""{"commit":"{{Head}}","results":[{"item":"AC-1","verdict":"pass"},{"item":"AC-2","verdict":"pass"},{"item":"AC-1","verdict":"pass"}]}""") }));

        Assert.Equal(["PRV-HOLD-003"], Rules(leaked));
        Assert.Contains(unscanned.NotRun, static n => n.StartsWith("PRV-HOLD-003", StringComparison.Ordinal));
    }

    [Fact]
    public void The_pack_acceptance_flips_only_an_item_a_passing_head_artifact_backs_and_carries_its_evidence()
    {
        var acceptance = ProofPack.Acceptance(Good(static f => f with { Proof = Proof(Artifact("SG-1a", Head), Artifact("SG-2a", "old")) }));

        var items = acceptance["items"]!.AsArray();
        Assert.Equal(["passing", "failing", "superseded"], items.Select(static i => (string?)i!["status"]), StringComparer.Ordinal);
        var evidence = Assert.Single(items[0]!["evidence"]!.AsArray())!;
        Assert.Equal(Head, (string?)evidence["commit"]);
        Assert.Equal(["at", "commit", "kind", "path", "sha256"], evidence.AsObject().Select(static p => p.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void An_acceptance_that_changes_more_than_status_or_flips_without_evidence_fails_acc_002()
    {
        var before = Acceptance();
        var edited = Acceptance();
        edited["items"]![0]!["text"] = "reworded";
        var bare = Acceptance();
        bare["items"]![1]!["status"] = "passing";

        Assert.Equal(["PRV-ACC-002@AC-1"], ProofPack.AcceptanceFlips(before, edited).Select(static f => f.Rule + "@" + f.Item), StringComparer.Ordinal);
        Assert.Equal(["PRV-ACC-002@AC-2"], ProofPack.AcceptanceFlips(before, bare).Select(static f => f.Rule + "@" + f.Item), StringComparer.Ordinal);
        Assert.Empty(ProofPack.AcceptanceFlips(before, Acceptance()));
    }

    [Fact]
    public void The_pack_lists_its_eight_files_ties_its_index_to_the_phase_and_keeps_scenario_text_out()
    {
        var facts = Good(static f => f with { Holdout = JsonNode.Parse($$"""{"commit":"{{Head}}","canary":"clean","results":[{"scenario":"H1","item":"AC-1","verdict":"pass","observed":"secret words","spent":false}]}""") });
        var origin = new ProofPackOrigin("line-p1-proof", "owner/name", new string('1', 40), "sha256:" + new string('b', 64), "sha256:" + new string('c', 64));

        var files = ProofPack.Files(facts, ProofChecks.Run(facts), origin);

        Assert.Equal(ProofPack.FileNames.Order(StringComparer.Ordinal), files.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var index = JsonNode.Parse(files["index.json"])!;
        Assert.Equal(origin.PacketHash, (string?)index["parents"]![0]);
        Assert.Equal(origin.AcceptanceHash, (string?)index["acceptance"]!["extends"]);
        Assert.Equal(ProofPack.Sha256(files["acceptance.json"]), (string?)index["acceptance"]!["self"]);
        Assert.Equal(origin.AcceptanceHash, (string?)JsonNode.Parse(files["acceptance.json"])!["parent"]);
        Assert.DoesNotContain("secret words", files["holdout.json"], StringComparison.Ordinal);
        Assert.Equal("proof", (string?)JsonNode.Parse(files["packet.json"])!["kind"]);
    }

    [Fact]
    public void Not_proven_lists_each_signal_no_passing_head_artifact_names_and_says_nothing_skipped_when_none()
    {
        var all = Good();
        var origin = new ProofPackOrigin("line-p1-proof", "owner/name", new string('1', 40), "sha256:" + new string('b', 64), "sha256:" + new string('c', 64));
        var partial = all with { Proof = Proof(Artifact("SG-1a", Head), Artifact("SG-2a", "old")) };

        Assert.Equal("nothing skipped\n", ProofPack.Files(all, ProofChecks.Run(all), origin)["not-proven.md"]);
        var text = ProofPack.Files(partial, ProofChecks.Run(partial), origin)["not-proven.md"];
        Assert.Contains("AC-2 / SG-2a", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SG-1a", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_proof_file_keeps_an_artifact_of_an_earlier_head_marked_superseded_and_records_the_rules_that_did_not_run()
    {
        var facts = Good(static f => f with { Proof = Proof(Artifact("SG-1a", Head) + "," + Artifact("SG-1a", "old"), Artifact("SG-2a", Head)) });
        var origin = new ProofPackOrigin("line-p1-proof", "owner/name", new string('1', 40), "sha256:" + new string('b', 64), "sha256:" + new string('c', 64));

        var proof = JsonNode.Parse(ProofPack.Files(facts, ProofChecks.Run(facts), origin)["proof.json"])!;

        var artifacts = proof["items"]![0]!["artifacts"]!.AsArray();
        Assert.Equal([false, true], artifacts.Select(static a => (bool?)a!["superseded"] ?? false));
        Assert.NotEmpty(proof["notRun"]!.AsArray());
    }
}
