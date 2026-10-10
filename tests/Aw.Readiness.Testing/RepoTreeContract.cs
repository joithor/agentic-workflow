using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The contract every <see cref="IRepoTree"/> passes: the in-memory fake and the file-system adapter run the
/// same tests (spec §4b.5), so the fake can't drift. A derived class builds a tree holding the given files.</summary>
public abstract class RepoTreeContract
{
    /// <summary>A tree holding exactly these files (repo-relative path → text).</summary>
    /// <param name="files">The files.</param>
    /// <returns>The tree.</returns>
    protected abstract IRepoTree TreeWith(IReadOnlyDictionary<string, string> files);

    private IRepoTree Sample() => TreeWith(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["README.md"] = "hello",
        ["src/A/A.csproj"] = "<Project />",
        ["src/A/b/B.cs"] = "class B;",
        ["src/A/bin/Debug/X.cs"] = "built",
        ["src/A/obj/Y.cs"] = "built",
        ["src/.hidden/Z.cs"] = "hidden",
        ["web/node_modules/m/M.cs"] = "dependency",
    });

    /// <summary>Files and the directories above them exist; anything else doesn't.</summary>
    [Fact]
    public void Exists_finds_files_and_their_directories()
    {
        var tree = Sample();

        Expect.True(tree.Exists("README.md"), "Exists(README.md) is false for a file");
        Expect.True(tree.Exists("src/A"), "Exists(src/A) is false for a directory holding files");
        Expect.True(tree.Exists("src/A/b/B.cs"), "Exists is false for a nested file");
        Expect.True(!tree.Exists("missing.md"), "Exists is true for a missing file");
        Expect.True(!tree.Exists("src/B"), "Exists is true for a missing directory");
    }

    /// <summary>ReadText returns a file's text, or null for a missing file or a directory.</summary>
    [Fact]
    public void ReadText_returns_text_or_null()
    {
        var tree = Sample();

        Expect.Equal("hello", tree.ReadText("README.md"), "ReadText(README.md)");
        Expect.Equal(expected: null, tree.ReadText("missing.md"), "ReadText of a missing file");
        Expect.Equal(expected: null, tree.ReadText("src/A"), "ReadText of a directory");
    }

    /// <summary>Files lists by suffix, recursively, sorted, skipping build output, dependencies and dot-directories.</summary>
    [Fact]
    public void Files_lists_by_suffix_and_skips_build_output()
    {
        var tree = Sample();

        Expect.Sequence(["src/A/b/B.cs"], tree.Files("src", ".cs"), "Files(src, .cs) skips bin, obj and dot-directories");
        Expect.Sequence(["src/A/A.csproj"], tree.Files("src", ".csproj"), "Files(src, .csproj)");
        Expect.Sequence([], tree.Files("web", ".cs"), "Files skips node_modules");
        Expect.Sequence([], tree.Files("missing", ".cs"), "Files of a missing directory");
        Expect.Sequence(["README.md", "src/A/A.csproj", "src/A/b/B.cs"], tree.Files("", ""), "Files(root) sorted ordinally");
    }
}
