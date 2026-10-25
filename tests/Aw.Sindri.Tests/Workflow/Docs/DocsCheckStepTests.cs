using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Docs;

/// <summary>'step.docs.check' on the block runtime (docs-sync spec §7.2, API17): doc paths only, links resolve, the markdown parses.</summary>
[Lane(TestLane.Unit)]
public sealed class DocsCheckStepTests
{
    private const string Tree = "/work/docs";

    private static string Scope(string candidates = "[]") => $$"""{"base":"abc","paths":["docs/**","*.md"],"candidates":{{candidates}}}""";

    private static async Task<BlockStepResult> Check(FakeRunDiff diff, InMemoryBriefFiles files, string? scope = null) =>
        await new DocsCheckStep(diff, files).Run(
            BuildShipContexts.Of("step.docs.check", new Dictionary<string, string>(StringComparer.Ordinal) { ["docsScope"] = scope ?? Scope(), ["workspace"] = $"\"{Tree}\"" }),
            TestContext.Current.CancellationToken);

    private static FakeRunDiff Changed(params string[] paths)
    {
        var diff = new FakeRunDiff();
        diff.Add(Tree);
        diff.Touch(Tree, paths);
        return diff;
    }

    private static string[] Rules(BlockStepResult result) => [.. JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:findings")].GetRawText())!.AsArray().Select(static f => (string)f!["rule"]!)];   // why: the Step writes every finding with a rule

    [Fact]
    public async Task Docs_with_resolving_links_and_balanced_fences_pass_and_write_no_findings()
    {
        var files = new InMemoryBriefFiles().With("docs/a.md", "# A\nSee [b](b.md#top), [site](https://example.test/x), [here](#a) and [root](../README.md).\n```sh\n[x](nowhere.md)\n```\n").With("docs/b.md", "# B\n").With("README.md", "# R\n");

        var result = await Check(Changed("docs/a.md"), files);

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
    }

    [Fact]
    public async Task A_changed_path_outside_the_doc_globs_fails_with_the_path()
    {
        var result = await Check(Changed("docs/a.md", "src/App.cs", "sub/notes.md"), new InMemoryBriefFiles().With("docs/a.md", "# A\n"));

        Assert.Equal("fail", result.Result!.Outcome.Value);
        Assert.Equal(["DOCS-PATH-001", "DOCS-PATH-001"], Rules(result));
        Assert.Contains("src/App.cs", result.Result.Outputs[FactKey.From("fact:findings")].GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_root_markdown_file_matches_the_default_globs()
    {
        var result = await Check(Changed("README.md"), new InMemoryBriefFiles().With("README.md", "# R\n"));

        Assert.Equal("pass", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_doc_the_design_names_is_allowed_outside_the_globs()
    {
        var result = await Check(Changed("planning/ARCH.txt"), new InMemoryBriefFiles(), Scope("""[{"path":"planning/ARCH.txt","designNamed":true}]"""));

        Assert.Equal("pass", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_relative_link_to_a_file_that_is_not_there_fails()
    {
        var result = await Check(Changed("docs/a.md"), new InMemoryBriefFiles().With("docs/a.md", "See [gone](missing.md) and [up](../../../out.md).\n"));

        Assert.Equal("fail", result.Result!.Outcome.Value);
        Assert.Equal(["DOCS-LINK-001", "DOCS-LINK-001"], Rules(result));
    }

    [Fact]
    public async Task An_unclosed_code_fence_fails_as_markdown_that_does_not_parse()
    {
        var result = await Check(Changed("docs/a.md"), new InMemoryBriefFiles().With("docs/a.md", "# A\n```sh\necho\n"));

        Assert.Equal("fail", result.Result!.Outcome.Value);
        Assert.Equal(["DOCS-MD-001"], Rules(result));
    }

    [Fact]
    public async Task A_docs_worktree_with_no_change_fails_because_there_is_nothing_to_open()
    {
        var result = await Check(Changed(), new InMemoryBriefFiles());

        Assert.Equal("fail", result.Result!.Outcome.Value);
        Assert.Equal(["DOCS-CHANGE-001"], Rules(result));
    }

    [Fact]
    public async Task A_deleted_doc_is_not_read()
    {
        var result = await Check(Changed("docs/gone.md"), new InMemoryBriefFiles());

        Assert.Equal("pass", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_worktree_git_cannot_read_fails_the_Step_with_the_reason()
    {
        var result = await Check(new FakeRunDiff(), new InMemoryBriefFiles());

        Assert.Null(result.Result);
        Assert.Contains(Tree, result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("docsScope")]
    [InlineData("workspace")]
    public async Task A_missing_fact_fails_the_Step_naming_it(string missing)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal) { ["docsScope"] = Scope(), ["workspace"] = $"\"{Tree}\"" };
        _ = facts.Remove(missing);

        var result = await new DocsCheckStep(new FakeRunDiff(), new InMemoryBriefFiles()).Run(BuildShipContexts.Of("step.docs.check", facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("fact:" + missing, result.Failure, StringComparison.Ordinal);
    }
}
