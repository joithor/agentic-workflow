using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'code-index' view fold through the projector's pages (code index spec §8): the newest 'codeindex.built' per repo.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeIndexProjectionTests
{
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CodeIndexBuilt Built(string repo, char commit, int files, bool incremental = false) =>
        new(repo, new string(commit, 40), [new CodeIndexBuiltLayer("structure", "ok", Code: null), new CodeIndexBuiltLayer("graph", "unavailable", "SND-CODEIDX-007")], files, files * 10, files * 20, 0.25, 0.125, 0, 1200, incremental);

    private async Task<CodeIndexRow?> Row(string repo) => (await _v.Views.Get<CodeIndexRow>(ViewNames.CodeIndex, repo, Token)).Value;

    [Fact]
    public async Task A_build_makes_the_repos_row_with_the_events_numbers_and_sequence()
    {
        await _v.Fold(EventTypes.CodeIndexBuilt, Built("demo", 'a', files: 3));

        var row = (await Row("demo"))!;

        Expect.Equal(("demo", new string('a', 40), 3, 30, 60, 1L), (row.Repo, row.Commit, row.Files, row.Symbols, row.Edges, row.Sequence));
        Expect.Equal(("structure", "graph", "SND-CODEIDX-007"), (row.Layers[0].Name, row.Layers[1].Name, row.Layers[1].Code));
    }

    [Fact]
    public async Task A_later_build_replaces_the_row_and_another_repo_keeps_its_own()
    {
        await _v.Fold(EventTypes.CodeIndexBuilt, Built("demo", 'a', files: 3));
        await _v.Fold(EventTypes.CodeIndexBuilt, Built("other", 'c', files: 1));
        await _v.Fold(EventTypes.CodeIndexBuilt, Built("demo", 'b', files: 4, incremental: true));

        Expect.Equal((new string('b', 40), 4, true, 3L), ((await Row("demo"))!.Commit, (await Row("demo"))!.Files, (await Row("demo"))!.Incremental, (await Row("demo"))!.Sequence));
        Expect.Equal((new string('c', 40), 2L), ((await Row("other"))!.Commit, (await Row("other"))!.Sequence));
    }

    [Fact]
    public async Task A_repo_that_was_never_built_has_no_row()
    {
        await _v.Fold(EventTypes.CodeIndexBuilt, Built("demo", 'a', files: 3));

        Assert.Null(await Row("other"));
    }
}
