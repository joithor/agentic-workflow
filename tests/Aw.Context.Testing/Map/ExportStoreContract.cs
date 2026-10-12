using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>The exports folder's contract: the fake and the file store pass the same tests.</summary>
public abstract class ExportStoreContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    private static MapDocument Document => new(1, MapDocumentKind.Export, ContextRepoName.From("aw-split-panes"), []);

    /// <summary>With no path the default name lands in the folder; a relative name too.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Exports_land_in_the_exports_folder()
    {
        const string test = nameof(Exports_land_in_the_exports_folder);
        var store = CreateStore();

        var named = Expect.Ok(await store.Write(path: null, "map-v1.json", Document, None), test);
        var relative = Expect.Ok(await store.Write("mine/copy.json", "unused.json", Document, None), test);

        Expect.That(named == Path.Combine(Folder, "map-v1.json"), test, $"wrote {named}");
        Expect.That(relative == Path.Combine(Folder, "mine", "copy.json"), test, $"wrote {relative}");
        var listed = Expect.Ok(await store.List(None), test).Select(f => f.Path).Order(StringComparer.Ordinal);
        Expect.That(listed.SequenceEqual([named, relative], StringComparer.Ordinal), test, "List doesn't return the written files");
    }

    /// <summary>Outside the folder is SND-CONTEXT-033; inside a worktree SND-CONTEXT-005; nothing is written.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Paths_outside_the_folder_or_in_a_worktree_are_refused()
    {
        const string test = nameof(Paths_outside_the_folder_or_in_a_worktree_are_refused);
        var store = CreateStore();

        var outside = await store.Write("../elsewhere.json", "x.json", Document, None);
        var worktree = await store.Write(Path.Combine(Worktree, "out.json"), "x.json", Document, None);

        Expect.That(outside.IsError && outside.Error.Code == "SND-CONTEXT-033", test, "a path outside the folder wasn't SND-CONTEXT-033");
        Expect.That(worktree.IsError && worktree.Error.Code == "SND-CONTEXT-005", test, "a worktree path wasn't SND-CONTEXT-005");
        Expect.That(Expect.Ok(await store.List(None), test).Count == 0, test, "a refused export was written");
    }

    /// <summary>Delete removes one listed file.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Delete_removes_a_listed_file()
    {
        const string test = nameof(Delete_removes_a_listed_file);
        var store = CreateStore();
        var path = Expect.Ok(await store.Write(path: null, "map-v1.json", Document, None), test);

        Expect.That(Expect.Ok(await store.Delete(path, None), test), test, "Delete returned false");
        Expect.That(!Expect.Ok(await store.Delete(path, None), test), test, "a second Delete returned true");
        Expect.That(Expect.Ok(await store.List(None), test).Count == 0, test, "the file is still listed");
    }

    /// <summary>The exports folder of the store <see cref="CreateStore"/> makes.</summary>
    protected abstract string Folder { get; }

    /// <summary>A folder inside a git worktree.</summary>
    protected abstract string Worktree { get; }

    /// <summary>A fresh store with an empty folder.</summary>
    /// <returns>The store.</returns>
    protected abstract IExportStore CreateStore();
}
