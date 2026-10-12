using Aw.Context.Core.Map;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>The map file reader's contract: the fake and the file reader pass the same tests.</summary>
public abstract class MapFileReaderContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>A placed file reads back with a 64-hex content hash that changes with its content.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_file_reads_with_a_hash_of_its_content()
    {
        const string test = nameof(A_file_reads_with_a_hash_of_its_content);
        var reader = CreateReader();
        var path = Place("map.json", "first");

        var first = Expect.Ok(await reader.Read(path, None), test);
        _ = Place("map.json", "second");
        var second = Expect.Ok(await reader.Read(path, None), test);

        Expect.That(first.Document.Repo == "first" && second.Document.Repo == "second", test, "the document isn't the file's");
        Expect.That(first.Hash.Length == 64 && first.Hash != second.Hash, test, "the hash doesn't follow the content");
    }

    /// <summary>A missing file is SND-CONTEXT-031; a file in a worktree SND-CONTEXT-005.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Missing_and_worktree_files_are_refused()
    {
        const string test = nameof(Missing_and_worktree_files_are_refused);
        var reader = CreateReader();

        var missing = await reader.Read(Place("map.json", "x") + ".gone", None);
        var worktree = await reader.Read(InWorktree("map.json"), None);

        Expect.That(missing.IsError && missing.Error.Code == "SND-CONTEXT-031", test, "a missing file wasn't SND-CONTEXT-031");
        Expect.That(worktree.IsError && worktree.Error.Code == "SND-CONTEXT-005", test, "a worktree file wasn't SND-CONTEXT-005");
    }

    /// <summary>A fresh reader.</summary>
    /// <returns>The reader.</returns>
    protected abstract IMapFileReader CreateReader();

    /// <summary>Writes a one-node authored map for <paramref name="repo"/> at a fresh place outside every worktree.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="repo">The repo it names (so two contents differ).</param>
    /// <returns>Its path.</returns>
    protected abstract string Place(string name, string repo);

    /// <summary>Writes a one-node map inside a git worktree.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>Its path.</returns>
    protected abstract string InWorktree(string name);
}
