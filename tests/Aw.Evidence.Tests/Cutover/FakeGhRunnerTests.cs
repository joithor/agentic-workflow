using System.Text;
using System.Text.Json;
using Aw.Evidence.Core.Publishing;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>The in-memory repository behind FakeGhRunner answers the git data API as GitHub does, so gh-publish's tests mean something.</summary>
public sealed class FakeGhRunnerTests
{
    private readonly FakeGhRunner _gh = new FakeGhRunner().WithGitData("o/n");

    private async Task<JsonElement> Ok(params string[] args) => await Ok(body: null, args);

    private async Task<JsonElement> Ok(object? body, params string[] args)
    {
        var outcome = await Call(body, args);
        Assert.Equal(0, outcome.ExitCode);
        return JsonDocument.Parse(outcome.Stdout).RootElement;
    }

    private async Task<GhOutcome> Call(object? body, params string[] args) =>
        (await _gh.RunAsync(new GhCall(body is null ? args : [.. args, "--input", "-"], body is null ? null : JsonSerializer.Serialize(body)), CancellationToken.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_branch_is_a_404_in_stderr_with_a_non_zero_exit()
    {
        var outcome = await Call(body: null, "api", "repos/o/n/git/ref/heads/ui-evidence/pr-7");

        Assert.Equal(1, outcome.ExitCode);
        Assert.Contains("HTTP 404", outcome.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_blob_tree_commit_and_ref_build_a_branch_and_a_second_commit_keeps_the_first_runs_files()
    {
        var blob1 = (await Ok(new { content = Convert.ToBase64String("one"u8), encoding = "base64" }, "api", "--method", "POST", "repos/o/n/git/blobs")).GetProperty("sha").GetString()!;
        var tree1 = (await Ok(new { tree = new[] { new { path = "pr-7/a.png", mode = "100644", type = "blob", sha = blob1 } } }, "api", "--method", "POST", "repos/o/n/git/trees")).GetProperty("sha").GetString()!;
        var commit1 = (await Ok(new { message = "m1", tree = tree1, parents = Array.Empty<string>() }, "api", "--method", "POST", "repos/o/n/git/commits")).GetProperty("sha").GetString()!;
        _ = await Ok(new { @ref = "refs/heads/ui-evidence/pr-7", sha = commit1 }, "api", "--method", "POST", "repos/o/n/git/refs");

        var head = (await Ok("api", "repos/o/n/git/ref/heads/ui-evidence/pr-7")).GetProperty("object").GetProperty("sha").GetString();
        var baseTree = (await Ok("api", $"repos/o/n/git/commits/{head}")).GetProperty("tree").GetProperty("sha").GetString();
        var blob2 = (await Ok(new { content = Convert.ToBase64String("two"u8), encoding = "base64" }, "api", "--method", "POST", "repos/o/n/git/blobs")).GetProperty("sha").GetString()!;
        var tree2 = (await Ok(new { base_tree = baseTree, tree = new[] { new { path = "pr-7/b.png", mode = "100644", type = "blob", sha = blob2 } } }, "api", "--method", "POST", "repos/o/n/git/trees")).GetProperty("sha").GetString()!;
        var commit2 = (await Ok(new { message = "m2", tree = tree2, parents = new[] { commit1 } }, "api", "--method", "POST", "repos/o/n/git/commits")).GetProperty("sha").GetString()!;
        _ = await Ok(new { sha = commit2, force = false }, "api", "--method", "PATCH", "repos/o/n/git/refs/heads/ui-evidence/pr-7");

        Assert.Equal(commit2, _gh.Head("ui-evidence/pr-7"));
        Assert.Equal(["pr-7/a.png", "pr-7/b.png"], [.. _gh.FilesAt(commit2).Keys.Order(StringComparer.Ordinal)]);
        Assert.Equal("one", Encoding.UTF8.GetString(_gh.FilesAt(commit2)["pr-7/a.png"]));
        Assert.Equal([commit1], _gh.ParentsOf(commit2));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ref_update_that_is_not_a_fast_forward_is_refused_unless_forced()
    {
        var earlier = _gh.Seed("b", new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["x"] = [1] });
        var unrelated = _gh.Seed("other", new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["y"] = [2] });

        var refused = await Call(new { sha = unrelated, force = false }, "api", "--method", "PATCH", "repos/o/n/git/refs/heads/b");

        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("fast forward", refused.Stderr, StringComparison.Ordinal);
        Assert.Equal(earlier, _gh.Head("b"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_for_another_repository_is_a_404_and_a_failing_fake_fails_every_call()
    {
        Assert.Equal(1, (await Call(body: null, "api", "repos/x/y/git/ref/heads/b")).ExitCode);

        _gh.FailWith = "gh: Bad Gateway (HTTP 502)";
        var failed = await Call(body: null, "api", "repos/o/n/git/ref/heads/b");

        Assert.Equal((1, "gh: Bad Gateway (HTTP 502)"), (failed.ExitCode, failed.Stderr));
    }
}
