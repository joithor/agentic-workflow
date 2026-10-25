using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Line;

namespace Aw.Sindri.Tests.Line;

/// <summary>The plan station's checks (line spec §6.3): one pure function over the facts, run at the draft stage by check and at the evidence stage by evidence.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanChecksTests
{
    private const string Design = """{"acceptance":{"items":[{"id":"AC-1","text":"a"},{"id":"AC-2","text":"b"}]},"signals":[{"item":"AC-1","kind":"api-transcript","statement":"returns 410","runs":"head"},{"item":"AC-2","kind":"ui-browser","statement":"shows 3 rows","runs":"head"}]}""";

    private const string Plan = """{"phases":[{"id":"P1","title":"first","workstream":"api","deps":[],"size":"S","kind":"feature","acceptance":[{"id":"AC-1","evidence":["api-transcript"]},{"id":"H1","holdout":true}]},{"id":"P2","title":"second","workstream":"api","deps":["P1"],"size":"M","kind":"feature","acceptance":[{"id":"AC-2","evidence":[{"kind":"ui-browser"}]}]}],"execReview":[{"phase":"P1","verdict":"executable"},{"phase":"P2","verdict":"executable"}]}""";

    private const string Holdouts = """[{"phase":"P1","count":3,"hash":"sha256:aa","canary":true},{"phase":"P2","count":2,"hash":"sha256:bb","canary":true}]""";

    private static LineReport Check(Action<JsonObject, JsonObject>? edit = null, bool evidence = false, string holdouts = Holdouts)
    {
        var design = (JsonObject)JsonNode.Parse(Design)!;
        var plan = (JsonObject)JsonNode.Parse(Plan)!;
        edit?.Invoke(design, plan);
        return PlanChecks.Run(design, plan, JsonNode.Parse(holdouts), evidence);
    }

    private static IEnumerable<string> Failed(LineReport report) => report.Failures.Select(static f => f.Rule).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    [Fact]
    public void The_gold_plan_passes_the_draft_and_completes_the_evidence_stage_with_the_packet_warning()
    {
        var draft = Check();
        var evidence = Check(evidence: true);

        Assert.Equal("pass", draft.Outcome);
        Assert.Equal("complete", evidence.Outcome);
        Assert.Equal(["PLN-PKT-001"], draft.Findings.Where(static f => f.Warn).Select(static f => f.Rule), StringComparer.Ordinal);
    }

    [Fact]
    public void Every_live_item_is_in_exactly_one_phase_and_a_superseded_one_is_not_needed()
    {
        var none = Check((_, p) => p["phases"]![1]!["acceptance"] = new JsonArray());
        var twice = Check((_, p) => p["phases"]![1]!["acceptance"]!.AsArray().Add(JsonNode.Parse("""{"id":"AC-1","evidence":["test"]}""")));
        var superseded = Check((d, p) =>
        {
            p["phases"]![1]!["acceptance"] = new JsonArray();
            d["acceptance"]!["items"]![1]!["status"] = "superseded";
        });

        Assert.Contains("PLN-ACC-001", Failed(none), StringComparer.Ordinal);
        Assert.Equal(["PLN-ACC-001"], Failed(twice), StringComparer.Ordinal);
        Assert.DoesNotContain("PLN-ACC-001", Failed(superseded), StringComparer.Ordinal);
    }

    [Fact]
    public void A_phase_above_the_largest_allowed_size_is_refused_and_large_is_never_allowed()
    {
        var large = Check((_, p) => p["phases"]![0]!["size"] = "L");
        var largeAllowed = Check((_, p) =>
        {
            p["phases"]![0]!["size"] = "L";
            p["maxPhaseSize"] = "XL";
        });
        var small = Check((_, p) => p["maxPhaseSize"] = "S");

        Assert.Equal(["PLN-SIZE-001"], Failed(large), StringComparer.Ordinal);
        Assert.Equal(["PLN-SIZE-001"], Failed(largeAllowed), StringComparer.Ordinal);
        Assert.Equal(["PLN-SIZE-001"], Failed(small), StringComparer.Ordinal);
    }

    [Fact]
    public void A_fix_phase_needs_base_and_head_signals_and_every_phase_a_real_app_kind()
    {
        var fix = Check((_, p) => p["phases"]![0]!["kind"] = "fix");
        var fixed_ = Check((d, p) =>
        {
            p["phases"]![0]!["kind"] = "fix";
            d["signals"]![0]!["runs"] = "base+head";
        });
        var onlyTests = Check((_, p) => p["phases"]![1]!["acceptance"]![0]!["evidence"] = JsonNode.Parse("""["test"]"""));
        var docs = Check((_, p) =>
        {
            p["phases"]![1]!["acceptance"]![0]!["evidence"] = JsonNode.Parse("""["test"]""");
            p["phases"]![1]!["kind"] = "docs";
            p["phases"]![1]!["reason"] = "prose only";
        });
        var docsNoReason = Check((_, p) =>
        {
            p["phases"]![1]!["acceptance"]![0]!["evidence"] = JsonNode.Parse("""["test"]""");
            p["phases"]![1]!["kind"] = "config";
        });

        Assert.Equal(["PLN-SIG-001"], Failed(fix), StringComparer.Ordinal);
        Assert.Empty(Failed(fixed_));
        Assert.Equal(["PLN-SIG-001"], Failed(onlyTests), StringComparer.Ordinal);
        Assert.Empty(Failed(docs));
        Assert.Equal(["PLN-SIG-001"], Failed(docsNoReason), StringComparer.Ordinal);
    }

    [Fact]
    public void A_phase_that_allows_an_existing_test_file_must_name_it_in_its_test_change()
    {
        var unnamed = Check((_, p) => p["phases"]![0]!["allowedFiles"] = JsonNode.Parse("""["scripts/x.sh","scripts/tests/x.test.sh"]"""));
        var named = Check((_, p) =>
        {
            p["phases"]![0]!["allowedFiles"] = JsonNode.Parse("""["scripts/x.sh","scripts/tests/x.test.sh"]""");
            p["phases"]![0]!["testChange"] = JsonNode.Parse("""[{"path":"scripts/tests/x.test.sh","reason":"new cases"}]""");
        });
        var viaRule = Check((_, p) =>
        {
            p["phases"]![0]!["writeScope"] = JsonNode.Parse("""["scripts/tests/x.test.sh"]""");
            p["phases"]![0]!["rules"] = JsonNode.Parse("""["test-change:scripts/tests/x.test.sh:new cases"]""");
        });
        var created = Check((_, p) => p["phases"]![0]!["allowedFiles"] = JsonNode.Parse("""["scripts/x.sh","new: scripts/tests/y.test.sh","scripts/**"]"""));

        Assert.Equal(["PLN-TEST-001"], Failed(unnamed), StringComparer.Ordinal);
        Assert.Contains("scripts/tests/x.test.sh", unnamed.Failures.Single(static f => f.Rule == "PLN-TEST-001").Message, StringComparison.Ordinal);
        Assert.Empty(Failed(named));
        Assert.Empty(Failed(viaRule));
        Assert.Empty(Failed(created));   // a file the phase creates, or a glob, is not an existing test the plan must name
    }

    [Fact]
    public void More_than_one_dependency_waits_for_a_merge_and_a_stack_is_limited()
    {
        var join = Check((_, p) => p["phases"]!.AsArray().Add(JsonNode.Parse("""{"id":"P3","deps":["P1","P2"],"size":"S","kind":"feature","acceptance":[{"id":"AC-3","evidence":["cli-transcript"]}]}""")));
        var marked = Check((_, p) => p["phases"]!.AsArray().Add(JsonNode.Parse("""{"id":"P3","deps":["P1","P2"],"waitsForMerge":true,"size":"S","kind":"feature","acceptance":[{"id":"AC-3","evidence":["cli-transcript"]}]}""")));
        var deep = Check((_, p) => p["maxStack"] = 1);

        Assert.Contains("PLN-STACK-001", Failed(join), StringComparer.Ordinal);
        Assert.DoesNotContain("PLN-STACK-001", Failed(marked), StringComparer.Ordinal);
        Assert.Equal(["PLN-STACK-001"], Failed(deep), StringComparer.Ordinal);
    }

    [Fact]
    public void The_evidence_rules_do_not_run_at_the_draft_stage()
    {
        Assert.Empty(Failed(Check(holdouts: "[]")));
        Assert.Equal(["PLN-HOLD-003"], Failed(Check(evidence: true, holdouts: "[]")), StringComparer.Ordinal);
    }

    [Fact]
    public void The_last_executability_review_must_report_every_phase_executable()
    {
        var objection = Check((_, p) => p["execReview"] = JsonNode.Parse("""[{"phase":"P1","verdict":"executable"},{"phase":"P2","verdict":"objection"}]"""), evidence: true);
        var recovered = Check((_, p) => p["execReview"] = JsonNode.Parse("""[{"phase":"P1","verdict":"executable"},{"phase":"P2","verdict":"objection"},{"phase":"P2","verdict":"executable"}]"""), evidence: true);
        var never = Check((_, p) => p["execReview"] = new JsonArray(), evidence: true);

        Assert.Equal(["PLN-EXEC-001"], Failed(objection), StringComparer.Ordinal);
        Assert.Empty(Failed(recovered));
        Assert.Equal(["PLN-EXEC-001"], Failed(never), StringComparer.Ordinal);
    }

    [Fact]
    public void Every_phase_needs_a_holdout_set_of_the_minimum_size_with_a_canary_by_hash()
    {
        var few = Check(evidence: true, holdouts: """[{"phase":"P1","count":1,"hash":"sha256:aa","canary":true},{"phase":"P2","count":2,"hash":"sha256:bb","canary":true}]""");
        var noCanary = Check(evidence: true, holdouts: """[{"phase":"P1","count":3,"hash":"sha256:aa"},{"phase":"P2","count":2,"hash":"sha256:bb","canary":true}]""");
        var missing = Check(evidence: true, holdouts: """[{"phase":"P1","count":3,"hash":"sha256:aa","canary":true}]""");
        var raised = Check((_, p) => p["holdoutMin"] = 3, evidence: true);

        Assert.Equal(["PLN-HOLD-003"], Failed(few), StringComparer.Ordinal);
        Assert.Equal(["PLN-HOLD-003"], Failed(noCanary), StringComparer.Ordinal);
        Assert.Equal(["PLN-HOLD-003"], Failed(missing), StringComparer.Ordinal);
        Assert.Equal(["PLN-HOLD-003"], Failed(raised), StringComparer.Ordinal);
    }
}
