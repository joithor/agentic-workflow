using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>What a desk step asks the person to read (engine q65): one "Open the ..." button per review, the agent session behind the step, and the reason code in words. Every field is optional.</summary>
[Lane(TestLane.Unit)]
public sealed class DeskReviewTests
{
    private readonly FakeLauncher _launcher = new();
    private readonly List<string> _shown = [];
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private IReadOnlyList<DeskReviewViewModel> Of(params DeskReview[] reviews) => DeskReviewViewModel.Of(reviews, _launcher, _shown.Add);

    private static InstanceDetail Served(Action<JsonNode> edit)
    {
        var node = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        edit(node);
        return AwJson.Deserialize<InstanceDetail>(Encoding.UTF8.GetBytes(node.ToJsonString()), SindriApiClient.Json).Value;
    }

    [Theory]
    [InlineData("design", "Open the design")]
    [InlineData("scope", "Open the scope")]
    [InlineData("plan", "Open the plan")]
    [InlineData("packet", "Open the packet")]
    [InlineData("pr", "Open the PR")]
    public void Each_kind_has_its_own_button_words(string kind, string words) =>
        Assert.Equal(words, Assert.Single(Of(new DeskReview("fact:x", kind, Path: "/a/b", Url: null))).Label);

    [Fact]
    public void A_kind_this_build_does_not_know_reads_as_served()
    {
        var button = Assert.Single(Of(new DeskReview("fact:x", "diagram", Path: null, Url: null)));

        Assert.Equal("Open the diagram", button.Label);
    }

    [Fact]
    public async Task A_link_opens_in_the_browser()
    {
        var button = Assert.Single(Of(new DeskReview("fact:pr", "pr", Path: null, Url: "https://example.test/pull/7")));

        await button.Open();

        Assert.Equal(["https://example.test/pull/7"], _launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_path_opens_as_a_file()
    {
        _launcher.AddFile("/work/design.md");
        var button = Assert.Single(Of(new DeskReview("fact:design", "design", Path: "/work/design.md", Url: null)));

        await button.Open();

        Assert.Equal(["/work/design.md"], _launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_review_with_neither_takes_the_person_to_its_fact()
    {
        var button = Assert.Single(Of(new DeskReview("fact:scope", "scope")));

        await button.Open();

        Assert.Equal(["fact:scope"], _shown, StringComparer.Ordinal);
        Assert.Empty(_launcher.Opened);
    }

    [Fact]
    public async Task A_link_that_is_not_a_web_address_is_never_launched()
    {
        var button = Assert.Single(Of(new DeskReview("fact:x", "plan", Path: null, Url: "file:///etc/passwd")));

        await button.Open();

        Assert.Empty(_launcher.Opened);
        Assert.Equal(["fact:x"], _shown, StringComparer.Ordinal);
    }

    [Fact]
    public void Questions_with_nothing_to_open_stay_in_the_desk_form_and_get_no_button()
    {
        Assert.Empty(Of(new DeskReview("fact:escalation", "questions")));
        _ = Assert.Single(Of(new DeskReview("fact:escalation", "questions", Path: "/work/q.md")));
    }

    [Fact]
    public void Reviews_keep_the_order_served_and_a_missing_list_is_no_button() =>
        Assert.Equal(["Open the design", "Open the PR"], Of(new DeskReview("fact:d", "design"), new DeskReview("fact:p", "pr")).Select(b => b.Label), StringComparer.Ordinal);

    [Fact]
    public void A_missing_list_changes_nothing()
    {
        var detail = Served(_ => { });
        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock) { Launcher = _launcher };

        Assert.Equal((false, false, string.Empty), (expanded.HasReviews, expanded.HasReasonLine, expanded.ReasonLine));
        Assert.Null(detail.Current.Status.Run?.Session);
    }

    [Fact]
    public void The_served_reviews_read_into_the_desk_and_draw_as_buttons()
    {
        var detail = Served(n => n["current"]!["desk"]!["reviews"] = JsonNode.Parse("""[{"fact":"fact:design","kind":"design","path":"/work/design.md"},{"fact":"fact:pr","kind":"pr","url":"https://example.test/pull/7"}]"""));

        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock) { Launcher = _launcher };

        Assert.Equal(["Open the design", "Open the PR"], expanded.Reviews.Select(b => b.Label), StringComparer.Ordinal);
        Assert.True(expanded.HasReviews);
    }

    [Fact]
    public void The_frames_fact_request_reaches_its_listener()
    {
        var detail = Served(n => n["current"]!["desk"]!["reviews"] = JsonNode.Parse("""[{"fact":"fact:scope","kind":"scope"}]"""));
        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock) { Launcher = _launcher };
        var asked = new List<string>();
        expanded.FactRequested += (_, e) => asked.Add(e.Fact);

        _ = expanded.Reviews[0].Open();

        Assert.Equal(["fact:scope"], asked, StringComparer.Ordinal);
    }

    [Fact]
    public void The_served_session_reads_into_the_run()
    {
        var detail = Served(n => n["current"]!["status"]!["run"] = JsonNode.Parse("""{"runtime":"agent","launch":"LCH-1","session":{"provider":"claude","id":"abc123"}}"""));

        var session = detail.Current.Status.Run!.Session!;

        Assert.Equal(("claude", "abc123", "claude/abc123"), (session.Provider, session.Id, session.Ref));
    }

    [Theory]
    [InlineData("SND-WF-010", "The step's dispatch was refused")]
    [InlineData("SND-WF-999", "SND-WF-999")]
    public void The_reason_code_reads_in_words_and_an_unknown_one_as_itself(string code, string words)
    {
        var detail = Served(n => n["current"]!["status"]!["reasonCode"] = code);

        var expanded = new ExpandedStateViewModel(detail.Current, detail.Address, new FakeClipboard(), _clock);

        Assert.Equal((words, true), (expanded.ReasonLine, expanded.HasReasonLine));
    }

    [Fact]
    public async Task The_desk_target_carries_the_reviews_session_and_reason_code_for_the_bar()
    {
        var server = new FakeSindriApi();
        var node = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        node["current"]!["desk"]!["reviews"] = JsonNode.Parse("""[{"fact":"fact:design","kind":"design","path":"/work/design.md"}]""");
        node["current"]!["status"]!["run"] = JsonNode.Parse("""{"runtime":"agent","launch":"LCH-1","session":{"provider":"claude","id":"abc123"}}""");
        node["current"]!["status"]!["reasonCode"] = "SND-WF-010";
        server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, node.ToJsonString());
        var reporter = new ViewModels.NeedsYou.DeskReporter(server, () => new Uri("http://127.0.0.1:7410/"));

        var target = await reporter.Load("WFI-x");

        Assert.NotNull(target);
        Assert.Equal((1, "claude/abc123", "SND-WF-010"), (target.Reviews.Count, target.SessionRef, target.ReasonCode));
    }
}
