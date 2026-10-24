using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>The prove station's intake and fail-log Steps on the block runtime (line spec §7.3, Appendix C.4).</summary>
[Lane(TestLane.Unit)]
public sealed class ProveIntakeFailLogTests
{
    private const string Dir = "/packets/p1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private static InMemoryBriefFiles Packet() => new InMemoryBriefFiles()
        .With("acceptance.json", """{"schemaVersion":1,"parent":"sha256:aa","items":[{"id":"AC-1","text":"first","status":"failing","evidence":[]},{"id":"AC-2","text":"second","status":"failing","evidence":[]},{"id":"AC-3","text":"old","status":"superseded","supersededBy":"AC-2","evidence":[]}]}""")
        .With("signals.json", """{"schemaVersion":1,"signals":[{"item":"AC-1","id":"SG-1a","kind":"test","statement":"s","runs":"head"},{"item":"AC-1","id":"SG-1b","kind":"api-transcript","statement":"t","runs":"base+head"}]}""");

    private static Dictionary<string, string> IntakeFacts() => new(StringComparer.Ordinal)
    {
        ["phasePacket"] = $$"""{"hash":"sha256:aa","dir":"{{Dir}}"}""",
        ["built"] = """{"head":"abc","commits":[]}""",
        ["workspace"] = "\"/work/p1\"",
        ["redTests"] = "{}",
    };

    [Fact]
    public async Task Intake_seeds_the_four_facts_from_the_phase_packet_and_the_built_head()
    {
        var result = await new ProveIntakeStep(Packet(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.prove.intake", IntakeFacts()), Token);

        Assert.Equal("ready", result.Result!.Outcome.Value);
        Assert.Equal(["beforeAfter", "findings", "holdoutResult", "proof"], result.Result.Outputs.Keys.Select(static k => k.Value[5..]).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Empty(Out(result, "findings").AsArray());
        Assert.Equal("abc", (string?)Out(result, "holdoutResult")["commit"]);
        Assert.Empty(Out(result, "holdoutResult")["results"]!.AsArray());
        Assert.Equal("abc", (string?)Out(result, "beforeAfter")["commit"]);
        Assert.Empty(Out(result, "beforeAfter")["results"]!.AsArray());
    }

    [Fact]
    public async Task Intake_proof_lists_each_live_item_with_its_signals_and_no_artifacts()
    {
        var result = await new ProveIntakeStep(Packet(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.prove.intake", IntakeFacts()), Token);

        var proof = Out(result, "proof");
        Assert.Equal("abc", (string?)proof["head"]);
        var items = proof["items"]!.AsArray();
        Assert.Equal(["AC-1", "AC-2"], items.Select(static i => (string?)i!["id"]), StringComparer.Ordinal);   // the superseded AC-3 is not proved
        Assert.Equal(["SG-1a", "SG-1b"], items[0]!["signals"]!.AsArray().Select(static s => (string?)s!["id"]), StringComparer.Ordinal);
        Assert.Empty(items[1]!["signals"]!.AsArray());
        Assert.All(items, static i => Assert.Empty(i!["artifacts"]!.AsArray()));
    }

    [Theory]
    [InlineData("phasePacket")]
    [InlineData("built")]
    public async Task Intake_without_a_needed_fact_fails_the_Step_and_names_it(string missing)
    {
        var facts = IntakeFacts();
        _ = facts.Remove(missing);

        var result = await new ProveIntakeStep(Packet(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.prove.intake", facts), Token);

        Assert.Null(result.Result);
        Assert.Contains(missing, result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Intake_with_a_built_fact_that_has_no_head_fails_the_Step()
    {
        var facts = IntakeFacts();
        facts["built"] = "{}";

        var result = await new ProveIntakeStep(Packet(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.prove.intake", facts), Token);

        Assert.Contains("head", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Intake_with_no_acceptance_file_fails_the_Step_with_the_reason()
    {
        var result = await new ProveIntakeStep(new InMemoryBriefFiles(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.prove.intake", IntakeFacts()), Token);

        Assert.Null(result.Result);
        Assert.Contains("acceptance.json", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Intake_reads_a_relative_packet_dir_under_the_repos_checkout_and_an_absolute_one_as_it_is()
    {
        var relative = new RootRecordingFiles(Packet());
        var absolute = new RootRecordingFiles(Packet());
        var checkouts = new InMemoryRepoCheckouts().With("name", "/checkouts/name");
        var facts = IntakeFacts();
        facts["phasePacket"] = """{"hash":"sha256:aa","dir":"packets/p1"}""";
        var context = BuildShipContexts.Of("step.prove.intake", facts) with { Repo = RepoName.From("name") };

        var fromRelative = await new ProveIntakeStep(relative, checkouts).Run(context, Token);
        var fromAbsolute = await new ProveIntakeStep(absolute, checkouts).Run(BuildShipContexts.Of("step.prove.intake", IntakeFacts()) with { Repo = RepoName.From("name") }, Token);

        Assert.Equal("ready", fromRelative.Result!.Outcome.Value);
        Assert.Equal(["/checkouts/name/packets/p1"], relative.Roots.Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal("ready", fromAbsolute.Result!.Outcome.Value);
        Assert.Equal([Dir], absolute.Roots.Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Intake_with_a_relative_packet_dir_and_no_checkout_fails_the_Step_and_never_reads_from_the_working_directory()
    {
        var files = new RootRecordingFiles(Packet());
        var facts = IntakeFacts();
        facts["phasePacket"] = """{"hash":"sha256:aa","dir":"packets/p1"}""";

        var result = await new ProveIntakeStep(files, new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.prove.intake", facts) with { Repo = RepoName.From("name") }, Token);

        Assert.Null(result.Result);
        Assert.Contains("checkout", result.Failure, StringComparison.Ordinal);
        Assert.Empty(files.Roots);
    }

    private static Dictionary<string, string> FailFacts(string findings, string beforeAfter, string holdout) => new(StringComparer.Ordinal)
    {
        ["findings"] = findings,
        ["beforeAfter"] = beforeAfter,
        ["holdoutResult"] = holdout,
    };

    private static JsonArray Logged(BlockStepResult result)
    {
        Assert.Equal("logged", result.Result!.Outcome.Value);
        return Out(result, "findings").AsArray();
    }

    [Fact]
    public async Task Fail_log_turns_a_before_after_that_did_not_fail_at_base_into_a_finding_for_the_item()
    {
        var facts = FailFacts("[]", """{"commit":"abc","results":[{"item":"AC-4","signal":"SG-4a","base":"pass","head":"pass"},{"item":"AC-5","signal":"SG-5a","base":"fail","head":"pass"}]}""", """{"commit":"abc","results":[]}""");

        var findings = Logged(await new ProveFailLogStep().Run(BuildShipContexts.Of("step.prove.fail-log", facts), Token));

        var finding = Assert.Single(findings);
        Assert.Equal(("PRV-BA-001", "AC-4", "fail"), ((string?)finding!["rule"], (string?)finding["item"], (string?)finding["severity"]));
        Assert.Contains("base", (string?)finding["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fail_log_turns_a_check_that_failed_at_head_into_a_finding()
    {
        var facts = FailFacts("[]", """{"commit":"abc","results":[{"item":"AC-4","signal":"SG-4a","base":"fail","head":"fail"}]}""", """{"commit":"abc","results":[]}""");

        var finding = Assert.Single(Logged(await new ProveFailLogStep().Run(BuildShipContexts.Of("step.prove.fail-log", facts), Token)));

        Assert.Contains("head", (string?)finding!["message"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fail_log_reports_a_failed_hidden_scenario_by_its_item_and_what_the_app_did_never_its_id()
    {
        var holdout = """{"commit":"abc","results":[{"scenario":"P2-H1","item":"AC-2","verdict":"fail","category":"boundary","observed":"Status 200 with the note title."},{"scenario":"P2-H2","item":"AC-5","verdict":"pass","category":"x","observed":"ok"}]}""";

        var finding = Assert.Single(Logged(await new ProveFailLogStep().Run(BuildShipContexts.Of("step.prove.fail-log", FailFacts("[]", """{"results":[]}""", holdout)), Token)));

        Assert.Equal(("PRV-HOLD-001", "AC-2"), ((string?)finding!["rule"], (string?)finding["item"]));
        Assert.Contains("Status 200 with the note title.", (string?)finding["message"], StringComparison.Ordinal);
        Assert.DoesNotContain("P2-H1", finding.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fail_log_keeps_the_findings_it_was_given_and_does_not_log_the_same_one_twice()
    {
        var held = """[{"rule":"PRV-RED-001","item":null,"message":"m","fix":"f","severity":"fail"},{"rule":"PRV-BA-001","item":"AC-4","message":"already","fix":"f","severity":"fail"}]""";
        var facts = FailFacts(held, """{"commit":"abc","results":[{"item":"AC-4","signal":"SG-4a","base":"pass","head":"pass"}]}""", """{"commit":"abc","results":[]}""");

        var findings = Logged(await new ProveFailLogStep().Run(BuildShipContexts.Of("step.prove.fail-log", facts), Token));

        Assert.Equal(["PRV-RED-001", "PRV-BA-001"], findings.Select(static f => (string?)f!["rule"]), StringComparer.Ordinal);
        Assert.Equal("already", (string?)findings[1]!["message"]);
    }

    [Fact]
    public async Task Fail_log_with_nothing_recorded_still_says_the_proof_failed()
    {
        var findings = Logged(await new ProveFailLogStep().Run(BuildShipContexts.Of("step.prove.fail-log", FailFacts("[]", """{"results":[]}""", """{"results":[]}""")), Token));

        Assert.Equal("PRV-FAIL-001", (string?)Assert.Single(findings)!["rule"]);
    }
}
