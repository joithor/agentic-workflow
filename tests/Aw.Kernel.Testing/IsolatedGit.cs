namespace Aw.Kernel.Testing;

/// <summary>Git arguments that keep a test's repo independent of the box's git config. A global <c lang="text">init.templateDir</c> (the sindri pre-commit hook, say) copies hooks into every fresh repo, and a hook that runs a scrub can overrun a short probe budget under load. Callers keep their own runner; this only supplies the arguments.</summary>
public static class IsolatedGit
{
    /// <summary>Arguments of <c lang="text">git init</c> with an empty template (no hooks are copied), then <paramref name="extra"/> (<c lang="text">-b main</c>, say).</summary>
    /// <param name="extra">Further init arguments.</param>
    /// <returns>The arguments after <c lang="text">git</c>.</returns>
    public static string[] Init(params string[] extra) => ["init", "-q", "--template=", .. extra];

    /// <summary>The global options that turn hooks off for one command: <c lang="text">-c core.hooksPath=/dev/null</c>. Put them before the subcommand.</summary>
    public static readonly IReadOnlyList<string> NoHooks = ["-c", "core.hooksPath=/dev/null"];
}
