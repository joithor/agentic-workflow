using Aw.Context.Core.Graph;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Review;

/// <summary>The review file store's contract (S-4): the fake and the file store pass the same tests.</summary>
public abstract class ReviewFileStoreContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    private static ReviewFile File => new("v14@7c3e1a2",
    [
        new ReviewFileEntry("Tab groups", ReviewGroup.Model, 0.62, NodeKey.From("feature:tabs.groups"), Decision: ReviewVerb.Rename),
        new ReviewFileEntry("TabManager.ts implements Tab manager", ReviewGroup.Model, 0.74, Link: LinkId.From("edge:0123456789abcdef"), Decision: ReviewVerb.Accept),
        new ReviewFileEntry("Ctrl+T", ReviewGroup.Deterministic, 1.0, NodeKey.From("surface:keybinding/ctrl+t")),
    ]);

    /// <summary>A file written to the exports folder reads back as written; a relative name lands under the folder.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_written_review_file_lands_in_the_exports_folder_and_reads_back()
    {
        const string test = nameof(A_written_review_file_lands_in_the_exports_folder_and_reads_back);
        var store = CreateStore();

        var named = Expect.Ok(await store.Write(path: null, "review-v14.json", File, None), test);
        var relative = Expect.Ok(await store.Write("mine/copy.json", "unused.json", File, None), test);
        var read = Expect.Ok(await store.Read(named, None), test);

        Expect.That(named == Path.Combine(Folder, "review-v14.json"), test, $"wrote {named}");
        Expect.That(relative == Path.Combine(Folder, "mine", "copy.json"), test, $"wrote {relative}");
        Expect.That(read.Draft == File.Draft && read.Entries.SequenceEqual(File.Entries), test, "the file read back differently");
    }

    /// <summary>Outside the exports folder is SND-CONTEXT-033, inside a worktree SND-CONTEXT-005; reading a worktree file is SND-CONTEXT-005 and a missing one SND-CONTEXT-031.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Paths_outside_the_folder_or_in_a_worktree_are_refused()
    {
        const string test = nameof(Paths_outside_the_folder_or_in_a_worktree_are_refused);
        var store = CreateStore();

        var outside = await store.Write("../elsewhere.json", "x.json", File, None);
        var writeInWorktree = await store.Write(Path.Combine(Worktree, "out.json"), "x.json", File, None);
        var readInWorktree = await store.Read(Path.Combine(Worktree, "review.json"), None);
        var missing = await store.Read(Path.Combine(Folder, "nothing-here.json"), None);

        Expect.That(outside.IsError && outside.Error.Code == "SND-CONTEXT-033", test, "a path outside the folder wasn't SND-CONTEXT-033");
        Expect.That(writeInWorktree.IsError && writeInWorktree.Error.Code == "SND-CONTEXT-005", test, "a worktree write wasn't SND-CONTEXT-005");
        Expect.That(readInWorktree.IsError && readInWorktree.Error.Code == "SND-CONTEXT-005", test, "a worktree read wasn't SND-CONTEXT-005");
        Expect.That(missing.IsError && missing.Error.Code == "SND-CONTEXT-031", test, "a missing file wasn't SND-CONTEXT-031");
    }

    /// <summary>The exports folder of the store <see cref="CreateStore"/> makes.</summary>
    protected abstract string Folder { get; }

    /// <summary>A folder inside a git worktree.</summary>
    protected abstract string Worktree { get; }

    /// <summary>A fresh store with an empty folder.</summary>
    /// <returns>The store.</returns>
    protected abstract IReviewFileStore CreateStore();
}
