using System.Text.RegularExpressions;
using Aw.Evidence.Core.Guard;
using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>The guards on a CLI call, each refusal AW-EVIDENCE-019: where the program lives, the words it is given, the variables it inherits.</summary>
public static partial class CliGuard
{
    private static readonly string[] _secretParts = ["TOKEN", "SECRET", "PASSWORD", "CREDENTIAL", "API_KEY", "PRIVATE_KEY"];
    private static readonly string[] _secretPrefixes = ["AWS_", "AZURE_", "GCP_", "GOOGLE_", "SSH_", "GITHUB_"];

    /// <summary>The environment variables every call gets besides the ones a script declares.</summary>
    public static IReadOnlyList<string> BaseEnvironment { get; } = ["PATH", "HOME", "TMPDIR", "LANG"];

    /// <summary>Vets argv[0]: a path with a '/', resolved against the repo with every symlink followed, inside a build output or equal to an allowed program.</summary>
    /// <param name="call">The call.</param>
    /// <param name="repo">The repository (absolute).</param>
    /// <param name="targets">The targets config.</param>
    /// <param name="locator">Resolves paths on disk.</param>
    /// <returns>The real path of the program, or AW-EVIDENCE-019 saying why.</returns>
    public static Result<string> ResolveTarget(CliStep call, string repo, ResolvedTargets targets, IExecutableLocator locator)
    {
        var program = call.Argv[0];
        if (!program.Contains('/', StringComparison.Ordinal))
        {
            return EvidenceErrors.CliRefused.With($"'{program}' is a bare name: a program is named by a path such as out/tool and is never looked up on PATH");
        }

        if (targets.AllowsNoProgram)
        {
            return EvidenceErrors.CliRefused.With(
                $"no CLI program is configured: add cli.buildOutputs or cli.allow to {repo.TrimEnd('/')}/.agentic-workflow/evidence.json (or pass --config FILE) to run '{program}'");
        }

        var real = locator.RealExecutable(Normalize(program.StartsWith('/') ? program : repo.TrimEnd('/') + "/" + program));
        if (real is null)
        {
            return EvidenceErrors.CliRefused.With($"'{program}' is not an executable file");
        }

        return IsAllowed(real, repo, targets, locator)
            ? real
            : EvidenceErrors.CliRefused.With($"'{program}' resolves to {real}, which is outside the build outputs ({string.Join(", ", targets.BuildOutputs)}) and the allowlist");
    }

    /// <summary>Refuses an argument that reads as a write verb the script did not declare.</summary>
    /// <param name="call">The call.</param>
    /// <param name="declaredWrites">The script's declared write words.</param>
    /// <returns>Success, or AW-EVIDENCE-019 naming the word.</returns>
    public static Result<Unit> CheckWrites(CliStep call, IReadOnlyCollection<string> declaredWrites)
    {
        foreach (var argument in call.Argv.Skip(1))
        {
            foreach (var word in WriteWords.CliWritesIn(argument))
            {
                if (!declaredWrites.Any(d => string.Equals(d, word, StringComparison.OrdinalIgnoreCase)))
                {
                    return EvidenceErrors.CliRefused.With($"an argument reads as the write verb '{word}'; name it in declaredWrites if the script means to write");
                }
            }
        }

        return Unit.Value;
    }

    /// <summary>Refuses a pass-through name that reads as a secret.</summary>
    /// <param name="names">The names the script and the config pass through.</param>
    /// <returns>Success, or AW-EVIDENCE-019 naming the variable.</returns>
    public static Result<Unit> CheckPassEnv(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (ReadsAsSecret(name))
            {
                return EvidenceErrors.CliRefused.With($"{name} reads as a secret and is not passed through to a CLI call");
            }
        }

        return Unit.Value;
    }

    /// <summary>The call as a step target: the program and its words, with every value that is not a word hidden.</summary>
    /// <param name="call">The call.</param>
    /// <returns>For example 'out/notes list --limit &lt;value&gt;'.</returns>
    public static string TargetText(CliStep call) =>
        string.Join(' ', call.Argv.Take(1).Concat(call.Argv.Skip(1).Select(static a => Word.IsMatch(a) ? a : "<value>")));

    private static bool IsAllowed(string real, string repo, ResolvedTargets targets, IExecutableLocator locator)
    {
        if (targets.Allow.Any(allowed => string.Equals(locator.RealExecutable(Normalize(allowed)), real, StringComparison.Ordinal)))
        {
            return true;
        }

        return targets.BuildOutputs.Select(output => locator.RealDirectory(Normalize(repo.TrimEnd('/') + "/" + output)))
            .Any(directory => directory is not null && real.StartsWith(directory.TrimEnd('/') + "/", StringComparison.Ordinal));
    }

    private static bool ReadsAsSecret(string name)
    {
        var upper = name.ToUpperInvariant();
        return upper == "DATABASE_URL"
            || _secretParts.Any(part => upper.Contains(part, StringComparison.Ordinal))
            || _secretPrefixes.Any(prefix => upper.StartsWith(prefix, StringComparison.Ordinal));
    }

    // Collapses '.' and '..' segments of an absolute path without touching the disk.
    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (segment != ".")
            {
                parts.Add(segment);
            }
        }

        return "/" + string.Join('/', parts);
    }

    [GeneratedRegex("^-{0,2}[a-z][a-z0-9-]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Word { get; }
}
