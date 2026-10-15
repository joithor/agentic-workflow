using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>The guards on a CLI call, each refusal AW-EVIDENCE-019: where the program lives, the words it is given, the variables it inherits.</summary>
public static class CliGuard
{
    /// <summary>The environment variables every call gets besides the ones a script declares.</summary>
    public static IReadOnlyList<string> BaseEnvironment { get; } = ["PATH", "HOME", "TMPDIR", "LANG"];

    /// <summary>Vets argv[0]: a path with a '/', resolved against the repo with every symlink followed, inside a build output or equal to an allowed program.</summary>
    /// <param name="call">The call.</param>
    /// <param name="repo">The repository (absolute).</param>
    /// <param name="targets">The targets config.</param>
    /// <param name="locator">Resolves paths on disk.</param>
    /// <returns>The real path of the program, or AW-EVIDENCE-019 saying why.</returns>
    public static Result<string> ResolveTarget(CliStep call, string repo, ResolvedTargets targets, IExecutableLocator locator) => throw new NotSupportedException();

    /// <summary>Refuses an argument that reads as a write verb the script did not declare.</summary>
    /// <param name="call">The call.</param>
    /// <param name="declaredWrites">The script's declared write words.</param>
    /// <returns>Success, or AW-EVIDENCE-019 naming the word.</returns>
    public static Result<Unit> CheckWrites(CliStep call, IReadOnlyCollection<string> declaredWrites) => throw new NotSupportedException();

    /// <summary>Refuses a pass-through name that reads as a secret.</summary>
    /// <param name="names">The names the script and the config pass through.</param>
    /// <returns>Success, or AW-EVIDENCE-019 naming the variable.</returns>
    public static Result<Unit> CheckPassEnv(IEnumerable<string> names) => throw new NotSupportedException();

    /// <summary>The call as a step target: the program and its words, with every value that is not a word hidden.</summary>
    /// <param name="call">The call.</param>
    /// <returns>For example 'out/notes list --limit &lt;value&gt;'.</returns>
    public static string TargetText(CliStep call) => throw new NotSupportedException();
}
