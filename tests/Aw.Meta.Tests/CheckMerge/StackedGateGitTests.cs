using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Merge;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Tests.Adapters;
using Aw.Meta.Tests.Cli;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.CheckMerge;

/// <summary>The stacked-gate validity rule against a real repository: the tree a gate tested versus what merging its head onto main gives once the pull requests under it have merged.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class StackedGateGitTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string Repo => System.IO.Path.Combine(_directory.Path, "repo");

    private static void Git(string directory, params string[] args) => WorktreeLab.Git(directory, args);

    private static string GitOut(string directory, params string[] args) => WorktreeLab.GitOut(directory, args);

    private async Task Commit(string file, string text, string message)
    {
        await File.WriteAllTextAsync(System.IO.Path.Combine(Repo, file), text, TestContext.Current.CancellationToken);
        Git(Repo, "add", ".");
        Git(Repo, "commit", "--quiet", "-m", message);
    }

    private async Task<string> Branch(string name, string file)
    {
        Git(Repo, "checkout", "--quiet", "-b", name, "main");
        await Commit(file, name, name);
        return GitOut(Repo, "rev-parse", "HEAD");
    }

    // The gate of B stacked on A: a detached tree of main with A's head and then B's head merged in, as the train builds it.
    private GateRecord StackedGate(string mainSha, string aHead, string bHead)
    {
        Git(Repo, "checkout", "--quiet", "--detach", mainSha);
        Git(Repo, "merge", "--quiet", "--no-edit", aHead);
        Git(Repo, "merge", "--quiet", "--no-edit", bHead);
        var tree = GitOut(Repo, "rev-parse", "HEAD^{tree}");
        return new GateRecord(2, bHead, mainSha, ["repo"], [], GateVerdict.Pass, A.At(1)) { Tree = tree, Stack = [new StackedPr(1, aHead)] };
    }

    private async Task<(RepoCommands Commands, string Main)> Init()
    {
        _ = Directory.CreateDirectory(Repo);
        Git(Repo, "init", "--quiet");
        await Commit("base.txt", "base", "base");
        Git(Repo, "branch", "-M", "main");
        return (new RepoCommands(new SystemCommandRunner(), new MetaPaths(_directory.Path, _directory.Path, Heavy: null, "gh")), GitOut(Repo, "rev-parse", "HEAD"));
    }

    private static MergeCheck MainCheck(GateRecord gate, string currentMain, string? mergedTree) =>
        MergeGate.Evaluate(new MergeFacts { Pr = 2, Gate = gate, CurrentHead = gate.Head, CurrentMain = currentMain, MergedTree = mergedTree, PlannedScopes = ["repo"], OpenPrs = [] })
            .Single(check => check.Name == "main");

    [Fact, Trait("Lane", "integration")]
    public async Task A_gate_stacked_on_a_pull_request_becomes_valid_once_that_one_merges_with_the_same_head()
    {
        var (commands, main0) = await Init();
        var aHead = await Branch("a", "a.txt");
        var bHead = await Branch("b", "b.txt");
        var gate = StackedGate(main0, aHead, bHead);
        Git(Repo, "checkout", "--quiet", "main");
        Assert.False(MainCheck(gate, main0, await commands.MergeTree(Repo, main0, bHead)).Ok);   // A is not on main yet: B alone is a different tree

        Git(Repo, "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge a", aHead);
        var main1 = GitOut(Repo, "rev-parse", "HEAD");

        Assert.NotEqual(main0, main1, StringComparer.Ordinal);
        var check = MainCheck(gate, main1, await commands.MergeTree(Repo, main1, bHead));
        Assert.True(check.Ok, check.Detail);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_gate_stacked_on_a_pull_request_is_invalid_when_that_pull_request_merged_with_another_head()
    {
        var (commands, main0) = await Init();
        var aHead = await Branch("a", "a.txt");
        var bHead = await Branch("b", "b.txt");
        var gate = StackedGate(main0, aHead, bHead);
        Git(Repo, "checkout", "--quiet", "a");
        await Commit("a2.txt", "later work", "a again");
        Git(Repo, "checkout", "--quiet", "main");
        Git(Repo, "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge a", "a");
        var main1 = GitOut(Repo, "rev-parse", "HEAD");

        var check = MainCheck(gate, main1, await commands.MergeTree(Repo, main1, bHead));

        Assert.False(check.Ok);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_conflicting_merge_has_no_tree()
    {
        var (commands, main0) = await Init();
        var aHead = await Branch("a", "clash.txt");
        var bHead = await Branch("b", "clash.txt");
        Git(Repo, "checkout", "--quiet", "main");
        Git(Repo, "merge", "--quiet", "--no-ff", "--no-edit", "-m", "Merge a", aHead);

        Assert.Null(await commands.MergeTree(Repo, GitOut(Repo, "rev-parse", "HEAD"), bHead));
        Assert.NotNull(await commands.MergeTree(Repo, main0, bHead));
    }

    public void Dispose() => _directory.Dispose();
}
