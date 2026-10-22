using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Review;

namespace Aw.Sindri.Tests.Workflow.Review;

/// <summary>The review result packet's files as pure functions of the review loop's facts (line spec §3.2 row 6).</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewPackTests
{
    private const string Head = "abcabcabcabcabcabcabcabcabcabcabcabcabca";
    private static readonly string _phaseHash = "sha256:" + new string('b', 64);
    private static readonly string _acceptanceHash = "sha256:" + new string('c', 64);

    private static ReviewPackOrigin Origin => new("line-p1-review", "owner/name", "1111111111111111111111111111111111111111", _phaseHash, _acceptanceHash);

    private static ReviewFacts Facts(string verdict = "clean", string counts = "\"blocking\":0,\"issue\":0,\"suggestions\":1,\"nits\":0", string? rounds = null) => new(
        Head,
        JsonNode.Parse($$"""{"round":2,"verdict":"{{verdict}}",{{counts}},"findings":[{"lens":"correctness","severity":"suggestion","text":"s","resolution":"kept: the name is the domain's"}]}""")!,
        JsonNode.Parse(rounds ?? """[{"round":1,"lens":"correctness","findings":[{"id":"f1","severity":"blocking","text":"b"}],"closes":true},{"round":2,"lens":"correctness","findings":[{"severity":"suggestion","text":"s"}],"closes":true}]"""));

    private static JsonNode File(string name, ReviewFacts facts) => JsonNode.Parse(ReviewPack.Files(facts, Origin)[name])!;

    [Fact]
    public void The_packet_has_the_review_kinds_three_files_in_link_order()
    {
        var files = ReviewPack.Files(Facts(), Origin);

        Assert.Equal(["packet.json", "index.json", "review.json"], files.Keys, StringComparer.Ordinal);
        Assert.Equal(ReviewPack.FileNames, files.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void The_packet_json_names_the_review_files_and_is_a_draft_with_no_hash()
    {
        var packet = File("packet.json", Facts());

        Assert.Equal(("review", "line-p1-review", "owner/name", "draft"), ((string?)packet["kind"], (string?)packet["id"], (string?)packet["repo"], (string?)packet["status"]));
        Assert.Equal("1111111111111111111111111111111111111111", (string?)packet["baseSha"]);
        Assert.Null(packet["hash"]);
        Assert.Equal(("review.json", "index.json"), ((string?)packet["links"]!["review"], (string?)packet["links"]!["index"]));
        Assert.Equal(2, packet["links"]!.AsObject().Count);
    }

    [Fact]
    public void The_index_names_the_phase_packet_as_its_parent_and_carries_its_acceptance_hash_without_one_of_its_own()
    {
        var index = File("index.json", Facts());

        Assert.Equal("review", (string?)index["kind"]);
        Assert.Equal([_phaseHash], index["parents"]!.AsArray().Select(static p => (string?)p), StringComparer.Ordinal);
        Assert.Null(index["acceptance"]!["self"]);
        Assert.Equal(_acceptanceHash, (string?)index["acceptance"]!["extends"]);
    }

    [Fact]
    public void The_review_json_names_the_head_the_verdict_the_counts_and_the_last_runs_findings_with_their_resolution()
    {
        var review = File("review.json", Facts());

        Assert.Equal((1, Head, "clean", 2), (review["schemaVersion"]!.GetValue<int>(), (string?)review["head"], (string?)review["verdict"], review["round"]!.GetValue<int>()));
        Assert.Equal((0, 0, 1, 0), (review["counts"]!["blocking"]!.GetValue<int>(), review["counts"]!["issue"]!.GetValue<int>(), review["counts"]!["suggestions"]!.GetValue<int>(), review["counts"]!["nits"]!.GetValue<int>()));
        var finding = Assert.Single(review["findings"]!.AsArray())!;
        Assert.Equal(("suggestion", "kept: the name is the domain's"), ((string?)finding["severity"], (string?)finding["resolution"]));
    }

    [Fact]
    public void The_rounds_group_the_lens_log_by_round_with_each_finding_and_its_class_and_a_null_resolution_when_unanswered()
    {
        var rounds = File("review.json", Facts())["rounds"]!.AsArray();

        Assert.Equal([1, 2], rounds.Select(static r => r!["round"]!.GetValue<int>()));
        var first = Assert.Single(rounds[0]!["lenses"]!.AsArray())!;
        Assert.Equal("correctness", (string?)first["lens"]);
        var finding = Assert.Single(first["findings"]!.AsArray())!;
        Assert.Equal(("f1", "blocking"), ((string?)finding["id"], (string?)finding["severity"]));
        Assert.True(finding.AsObject().ContainsKey("resolution") && finding["resolution"] is null);
    }

    [Fact]
    public void Several_lenses_of_one_round_share_the_round_in_log_order()
    {
        const string log = """[{"round":1,"lens":"a","findings":[]},{"round":1,"lens":"b","notApplicable":"no sql","findings":[],"closes":true}]""";

        var round = Assert.Single(File("review.json", Facts(rounds: log))["rounds"]!.AsArray())!;

        Assert.Equal(["a", "b"], round["lenses"]!.AsArray().Select(static l => (string?)l!["lens"]), StringComparer.Ordinal);
        Assert.Equal("no sql", (string?)round["lenses"]![1]!["notApplicable"]);
    }

    [Fact]
    public void A_lens_log_that_is_not_an_array_gives_no_rounds()
    {
        var facts = Facts() with { LensRounds = null };

        Assert.Empty(File("review.json", facts)["rounds"]!.AsArray());
    }

    [Theory]
    [InlineData("clean", "clean")]
    [InlineData("behaviourWrong", "behaviourWrong")]
    [InlineData("findings", null)]
    [InlineData("", null)]
    public void Only_a_clean_or_behaviourWrong_review_has_a_verdict_to_record(string verdict, string? expected) =>
        Assert.Equal(expected, ReviewPack.Verdict(Facts(verdict).Review));

    [Fact]
    public void A_review_with_no_verdict_has_none_to_record() =>
        Assert.Null(ReviewPack.Verdict(JsonNode.Parse("""{"round":1}""")!));

    [Fact]
    public void A_clean_review_with_nothing_left_has_no_problem() =>
        Assert.Empty(ReviewPack.Problems(Facts()));

    [Fact]
    public void A_verdict_nothing_can_be_recorded_under_is_a_problem_that_names_it()
    {
        var problem = Assert.Single(ReviewPack.Problems(Facts("findings")));

        Assert.Contains("findings", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"blocking\":1,\"issue\":0")]
    [InlineData("\"blocking\":0,\"issue\":2")]
    public void A_clean_verdict_with_a_blocking_or_issue_finding_left_is_a_problem(string counts) =>
        Assert.Contains("clean", Assert.Single(ReviewPack.Problems(Facts(counts: counts))), StringComparison.Ordinal);

    [Fact]
    public void A_behaviourWrong_review_may_keep_its_blocking_findings() =>
        Assert.Empty(ReviewPack.Problems(Facts("behaviourWrong", "\"blocking\":1,\"issue\":0")));
}
