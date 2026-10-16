using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Extract;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>The <see cref="ChangeWorld"/> the contract tests ask of a reader, built in a throwaway git repo or described to the fake.</summary>
internal static class ChangeWorlds
{
    /// <summary>Builds the history in <paramref name="temp"/> (which holds <see cref="RepoReaderContract.Committed"/> at its first commit).</summary>
    internal static ChangeWorld Build(TempGitRepo temp)
    {
        var root = temp.Head;
        _ = temp.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(temp.Path, "README.md"), "# Split panes, edited\n");
        File.Delete(Path.Combine(temp.Path, "docs", "guide.md"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "new.ts"), "export const added = 1;\n");
        _ = temp.Run("add", "-A");
        _ = temp.Run("commit", "-q", "-m", "feature work");
        var head = temp.Run("rev-parse", "HEAD");
        _ = temp.Run("mv", "src/commands/registry.ts", "src/commands/index.ts");
        _ = temp.Run("commit", "-q", "-m", "rename");
        var renameHead = temp.Run("rev-parse", "HEAD");
        _ = temp.Run("checkout", "-q", "main");
        File.WriteAllText(Path.Combine(temp.Path, "main-only.txt"), "main moved on\n");
        _ = temp.Run("add", "-A");
        _ = temp.Run("commit", "-q", "-m", "main moves on");
        var main = temp.Run("rev-parse", "HEAD");
        var orphan = temp.Run("commit-tree", "4b825dc642cb6eb9a060e54bf8d69288fbee4904", "-m", "unrelated");
        return new ChangeWorld(main, head, root,
            [new RepoChange("README.md", ChangeKind.Modified), new RepoChange("docs/guide.md", ChangeKind.Deleted), new RepoChange("src/new.ts", ChangeKind.Added)],
            orphan, renameHead,
            [new RepoChange("src/commands/index.ts", ChangeKind.Added), new RepoChange("src/commands/registry.ts", ChangeKind.Deleted)]);
    }

    /// <summary>The same world on made-up shas, with the answers the git reader gives.</summary>
    internal static (ChangeWorld World, FakeRepoReader Reader) Fake(ContextRepo repo)
    {
        var world = new ChangeWorld("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "cccccccccccccccccccccccccccccccccccccccc",
            [new RepoChange("README.md", ChangeKind.Modified), new RepoChange("docs/guide.md", ChangeKind.Deleted), new RepoChange("src/new.ts", ChangeKind.Added)],
            "dddddddddddddddddddddddddddddddddddddddd", "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
            [new RepoChange("src/commands/index.ts", ChangeKind.Added), new RepoChange("src/commands/registry.ts", ChangeKind.Deleted)]);
        var reader = new FakeRepoReader(repo, world.MergeBase, RepoReaderContract.Committed).WithHistory(
            [world.Base, world.Head, world.MergeBase, world.Unrelated, world.RenameHead],
            [new RepoChanges(world.Base, world.Head, world.MergeBase, world.Expected),
             new RepoChanges(world.Head, world.RenameHead, world.Head, world.RenameExpected),
             new RepoChanges(world.Head, world.Head, world.Head, [])]);
        return (world, reader);
    }
}
