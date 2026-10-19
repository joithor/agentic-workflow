using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>'step.scope.read-brief' on the block runtime (scoping spec §7, §8.2): the brief file from the item's checkout, the text shape's anchors, then the desk helper's code.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopingReadBriefStepTests
{
    private const string BriefText = "# Shared links expire after a chosen time\n\n## 1. Problem\n\nA shared link stays open for ever.\n\n## 2. What we want\n\n- Pick a time after which the link stops working.\n";

    private static readonly RepoName _web = RepoName.From("web");

    private static ScopeReadBriefStep Step(InMemoryBriefFiles files, InMemoryScopingTextShape shape) =>
        new(files, new InMemoryRepoCheckouts().With("web", "/work/web"), shape, new FakeTimeProvider(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero)));

    private static StepContext Context(string scopeInput, RepoName? repo = null) =>
        ScopingStepContexts.Of("step.scope.read-brief", new ScopingFacts { ["scopeInput"] = scopeInput }) with { Repo = repo ?? _web, Address = "T-1/scoping/1" };

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private const string Input = """{"kind":"brief","repo":"web","path":"docs/expiry.md"}""";

    [Fact]
    public async Task It_reads_the_brief_from_the_repos_checkout_and_records_it_with_the_text_shapes_anchors()
    {
        var files = new InMemoryBriefFiles().With("docs/expiry.md", BriefText);
        var shape = new InMemoryScopingTextShape(["feature:links"]);

        var result = await Step(files, shape).Run(Context(Input), TestContext.Current.CancellationToken);

        Assert.Equal("read", result.Result!.Outcome.Value);
        var brief = Out(result, "brief");
        Assert.Equal(BriefText, brief["text"]!.GetValue<string>());
        Assert.Equal("Shared links expire after a chosen time", brief["subject"]!.GetValue<string>());
        Assert.Equal("2026-10-19T09:00:00Z", brief["refs"]![0]!["fetchedAt"]!.GetValue<string>());
        Assert.Equal(["feature:links"], Out(result, "anchors").AsArray().Select(static a => a!.GetValue<string>()), StringComparer.Ordinal);
        var (repo, asked) = Assert.Single(shape.Asked);
        Assert.Equal((_web, BriefText), (repo!.Value, asked));
    }

    [Fact]
    public async Task A_named_section_is_the_brief_and_the_question_the_text_shape_is_asked()
    {
        var files = new InMemoryBriefFiles().With("docs/expiry.md", BriefText);
        var shape = new InMemoryScopingTextShape([]);

        var result = await Step(files, shape).Run(Context("""{"kind":"brief","repo":"web","path":"docs/expiry.md","section":"2"}"""), TestContext.Current.CancellationToken);

        Assert.StartsWith("## 2. What we want", Out(result, "brief")["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.StartsWith("## 2. What we want", Assert.Single(shape.Asked).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_brief_with_nothing_in_the_named_section_is_empty_and_never_asks_the_engine()
    {
        var files = new InMemoryBriefFiles().With("docs/expiry.md", BriefText);
        var shape = new InMemoryScopingTextShape(["feature:x"]);

        var result = await Step(files, shape).Run(Context("""{"kind":"brief","repo":"web","path":"docs/expiry.md","section":"9"}"""), TestContext.Current.CancellationToken);

        Assert.Equal("empty", result.Result!.Outcome.Value);
        Assert.Empty(shape.Asked);
    }

    [Theory]
    [InlineData("""{"kind":"brief","repo":"web"}""", "path")]
    [InlineData("""{"kind":"tracker","repo":"web","key":"NTS-1"}""", "brief")]
    public async Task A_scope_input_that_is_not_a_brief_with_a_path_fails_the_Step(string input, string mentions)
    {
        var result = await Step(new InMemoryBriefFiles(), new InMemoryScopingTextShape([])).Run(Context(input), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains(mentions, result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_path_fails_the_Step_with_the_refusal()
    {
        var files = new InMemoryBriefFiles().With(".env", "SECRET=1");

        var result = await Step(files, new InMemoryScopingTextShape([])).Run(Context("""{"kind":"brief","repo":"web","path":".env"}"""), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("refused", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_item_whose_repo_is_not_in_the_approved_profile_has_no_checkout_to_read()
    {
        var files = new InMemoryBriefFiles().With("docs/expiry.md", BriefText);

        var result = await Step(files, new InMemoryScopingTextShape([])).Run(Context(Input, RepoName.From("other")), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("no checkout", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_engine_unable_to_answer_fails_the_Step()
    {
        var files = new InMemoryBriefFiles().With("docs/expiry.md", BriefText);

        var result = await Step(files, new InMemoryScopingTextShape(SindriErrors.InvalidInput.With("no map"))).Run(Context(Input), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("no map", result.Failure, StringComparison.Ordinal);
    }
}
