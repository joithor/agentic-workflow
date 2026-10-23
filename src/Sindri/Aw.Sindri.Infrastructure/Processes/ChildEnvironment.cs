using System.Collections;
using System.Diagnostics;

#if AW_WORKFLOWS_COPY
namespace Aw.Sindri.Infrastructure.Workflows;   // Aw.Sindri.Workflows compiles this file too (its csproj); a namespace of its own keeps the two internal types apart for the tests that see both
#else
namespace Aw.Sindri.Infrastructure.Processes;
#endif

/// <summary>
/// The one place a <see cref="ProcessStartInfo"/> is made in src/Sindri (ARCH-PROC-001 fails any other), so the pod's child environment cannot be bypassed
/// (deployable-context spec A2d, decision K9). On the desktop (AW_SINDRI_DEPLOY unset) a child inherits this process's environment, as it always did.
/// In pod mode (AW_SINDRI_DEPLOY=pod) it gets only <see cref="Allowed"/> variables, so the secrets the pod holds as environment variables
/// (the repo token, the forge token, the cloud and cluster variables) reach no child. Either way the extra variables are applied last: a value sets a variable
/// and null removes it. A call that needs a credential passes it there, for that call only.
/// </summary>
/// <remarks>
/// What each pod-mode spawner needs, and so why the allowlist is what it is: git (GitProcess, GitRepoLocator, ProviderHost) PATH, HOME for its config,
/// LANG and LC_*, TMPDIR, SSL_CERT_* for https remotes; the detached Temporal server (DevServerProcesses, through /bin/sh) PATH, HOME, TMPDIR and TZ;
/// the NativeAOT judge and aw-packets AW_STATE_DIR and the AW_* paths below (a call adds its own, e.g. AW_JUDGE_STATE_DIR); graphify (a code-index child)
/// XDG_CACHE_HOME and UV_CACHE_DIR under HOME; .NET children DOTNET_*. gh (GitChangeSource) needs a token in pod mode, which its caller passes as
/// the extra variables. The desktop-only spawners (tmux, viewers, the browser, the notifier, the agent launcher, wrap) run unchanged: they are not started in a pod.
/// </remarks>
internal static class ChildEnvironment
{
    private const string DeployVariable = "AW_SINDRI_DEPLOY";

    private static readonly HashSet<string> _allowed = new(StringComparer.Ordinal)
    {
        // process basics
        "PATH", "HOME", "LANG", "LANGUAGE", "TMPDIR", "TZ", "USER", "LOGNAME",
        // https remotes for git
        "SSL_CERT_FILE", "SSL_CERT_DIR",
        // caches under HOME (the root is read-only)
        "XDG_CACHE_HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "UV_CACHE_DIR",
        // Sindri's own non-secret configuration (the image sets these; .env.example lists them)
        "AW_SINDRI_DEPLOY", "AW_SINDRI_HOME", "AW_SINDRI_PROFILE", "AW_STATE_DIR", "AW_LOG_LEVEL",
        "AW_SINDRI_JUDGE", "AW_PACKETS_BIN", "AW_CODEINDEX_GRAPHIFY", "AW_CODE_INDEX_LOCK",
    };

    private static readonly string[] _allowedPrefixes = ["LC_", "DOTNET_"];

    /// <summary>Whether a value of AW_SINDRI_DEPLOY selects pod mode: "pod" in any case.</summary>
    /// <param name="value">The variable's value, or null when unset.</param>
    /// <returns>True for pod mode.</returns>
    internal static bool IsPod(string? value) => string.Equals(value, "pod", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether pod mode lets the variable reach a child.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>True for an allowlisted name.</returns>
    internal static bool Allowed(string name) =>
        _allowed.Contains(name) || _allowedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    /// <summary>A start for <paramref name="fileName"/>: no shell, the arguments one by one, the child environment of this process's mode.</summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">Its arguments, or none.</param>
    /// <param name="extra">Variables to set (a value) or remove (null) for this call, applied after the mode's environment: where a pod-mode call passes its credential.</param>
    /// <returns>The start info; the caller sets redirects and the working directory and starts it.</returns>
    internal static ProcessStartInfo Start(string fileName, IEnumerable<string>? arguments = null, IReadOnlyDictionary<string, string?>? extra = null) =>
        Build(fileName, arguments, extra, Current());

    /// <summary>The same as <see cref="Start"/> over a given snapshot of the parent's variables (for tests).</summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">Its arguments, or none.</param>
    /// <param name="extra">Variables to set or remove for this call.</param>
    /// <param name="parent">The parent's variables.</param>
    /// <returns>The start info.</returns>
    internal static ProcessStartInfo Build(string fileName, IEnumerable<string>? arguments, IReadOnlyDictionary<string, string?>? extra, IReadOnlyDictionary<string, string> parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var start = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (var argument in arguments ?? [])
        {
            start.ArgumentList.Add(argument);
        }

        if (IsPod(parent.GetValueOrDefault(DeployVariable)))
        {
            start.Environment.Clear();
            foreach (var (name, value) in parent.Where(p => Allowed(p.Key)))
            {
                start.Environment[name] = value;
            }
        }

        foreach (var (name, value) in extra ?? new Dictionary<string, string?>(StringComparer.Ordinal))
        {
            if (value is null)
            {
                _ = start.Environment.Remove(name);
            }
            else
            {
                start.Environment[name] = value;
            }
        }

        return start;
    }

    private static Dictionary<string, string> Current() =>
        Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
            .Select(static e => (Name: (string)e.Key, Value: e.Value as string))
            .Where(static e => e.Value is not null)
            .ToDictionary(static e => e.Name, static e => e.Value ?? string.Empty, StringComparer.Ordinal);
}
