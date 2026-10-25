using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>'step.scope.read-change' on the block runtime (scoping spec §4, §8.2): the change through the change port, scrubbed, the text shape's anchors, then the desk helper's code.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopingReadChangeStepTests
{
    private static readonly RepoName _web = RepoName.From("web");

    private static InMemoryChangeSource Source() => new(
        new Dictionary<string, string>(StringComparer.Ordinal) { ["README.md"] = "# App\n" },
        [
            new ChangeSeedCommit("Add the expiry field", new Dictionary<string, string>(StringComparer.Ordinal) { ["src/links.txt"] = "expires_at is new\nkey=GHP-SECRET-9\n" }),
            new ChangeSeedCommit("Keep an env around", new Dictionary<string, string>(StringComparer.Ordinal) { [".env"] = "TOKEN=hunter2\n" }),
        ]);

    private static ScopeReadChangeStep Step(IChangeSource source, InMemoryScopingTextShape shape, IRepoCheckouts? checkouts = null) =>
        new(source, checkouts ?? new InMemoryRepoCheckouts().With("web", "/work/web"), shape, new MaskingScrubber(), new FakeTimeProvider(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero)));

    private static StepContext Context(string scopeInput, RepoName? repo = null) =>
        ScopingStepContexts.Of("step.scope.read-change", new ScopingFacts { ["scopeInput"] = scopeInput }) with { Repo = repo ?? _web, Address = "T-1/scoping/1" };

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private const string Input = """{"kind":"change","repo":"web","ref":"feat/expiry"}""";

    [Fact]
    public async Task A_branch_is_the_brief_with_its_commits_and_diff_scrubbed_and_the_text_shapes_anchors()
    {
        var shape = new InMemoryScopingTextShape(["feature:links"]);

        var result = await Step(Source(), shape).Run(Context(Input), TestContext.Current.CancellationToken);

        Assert.Equal("read", result.Result!.Outcome.Value);
        var brief = Out(result, "brief");
        var text = brief["text"]!.GetValue<string>();
        Assert.Contains("Add the expiry field", text, StringComparison.Ordinal);
        Assert.Contains("src/links.txt", text, StringComparison.Ordinal);
        Assert.Contains("expires_at is new", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GHP-SECRET-9", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        var reference = brief["refs"]![0]!;
        Assert.Equal(("R1", "change", "feat/expiry", "untrusted", "2026-10-20T09:00:00Z"),
            (reference["id"]!.GetValue<string>(), reference["source"]!.GetValue<string>(), reference["key"]!.GetValue<string>(), reference["trust"]!.GetValue<string>(), reference["fetchedAt"]!.GetValue<string>()));
        Assert.Matches("^[0-9a-f]+\\.\\.[0-9a-f]+$|^[^ ]+$", reference["version"]!.GetValue<string>());
        Assert.Equal(["feature:links"], Out(result, "anchors").AsArray().Select(static a => a!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(text, Assert.Single(shape.Asked).Text);
    }

    [Fact]
    public async Task A_denied_path_is_named_and_its_hunk_never_reaches_the_brief()
    {
        var result = await Step(Source(), new InMemoryScopingTextShape([])).Run(Context(Input), TestContext.Current.CancellationToken);

        var text = Out(result, "brief")["text"]!.GetValue<string>();
        Assert.Contains("denied", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".env", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_change_with_no_diff_and_no_text_is_empty_and_never_asks_the_engine()
    {
        var shape = new InMemoryScopingTextShape(["feature:x"]);
        var nothing = new InMemoryChangeSource(new Dictionary<string, string>(StringComparer.Ordinal), []);

        var result = await Step(nothing, shape).Run(Context(Input), TestContext.Current.CancellationToken);

        Assert.Equal("empty", result.Result!.Outcome.Value);
        Assert.Empty(shape.Asked);
    }

    [Theory]
    [InlineData("""{"kind":"change","repo":"web"}""", "ref")]
    [InlineData("""{"kind":"brief","repo":"web","path":"a.md"}""", "change")]
    public async Task A_scope_input_that_is_not_a_change_with_a_ref_fails_the_Step(string input, string mentions)
    {
        var result = await Step(Source(), new InMemoryScopingTextShape([])).Run(Context(input), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains(mentions, result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_reference_that_names_nothing_fails_the_Step()
    {
        var result = await Step(Source(), new InMemoryScopingTextShape([])).Run(Context("""{"kind":"change","repo":"web","ref":"feat/absent"}"""), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("feat/absent", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_item_whose_repo_is_not_in_the_approved_profile_has_no_checkout_to_read()
    {
        var result = await Step(Source(), new InMemoryScopingTextShape([])).Run(Context(Input, RepoName.From("other")), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("checkout", result.Failure, StringComparison.Ordinal);
    }

    private sealed class MaskingScrubber : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("GHP-SECRET-9", "[redacted]", StringComparison.Ordinal);
    }
}
