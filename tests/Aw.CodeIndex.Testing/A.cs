namespace Aw.CodeIndex.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A temp git repo builder: files, symlinks, a submodule entry, one commit.</summary>
    /// <returns>A new builder.</returns>
    public static GitRepoBuilder GitRepo() => new();
}
