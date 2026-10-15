namespace Aw.Context.Infrastructure.Extract;

/// <summary>Wraps a command so it runs with the network denied and writes confined to given folders (spec §6.2); it fails closed: no sandbox, no command.</summary>
internal static class GraphifySandbox
{
    // The sandbox binaries by absolute path, never through PATH: one a tool install dropped earlier on PATH would run the command unsandboxed.
    private const string SandboxExec = "/usr/bin/sandbox-exec";
    private static readonly string[] _bwrap = ["/usr/bin/bwrap", "/bin/bwrap"];

    // Credential and token stores under the home: unreadable inside the sandbox.
    private static readonly string[] _secrets =
        [".ssh", ".aws", ".gnupg", ".agentic-workflow", ".config/gh", ".docker", ".kube", ".codex", ".claude", ".claude.json", ".netrc", ".git-credentials", ".npmrc", ".pypirc"];

    /// <summary>Wraps <paramref name="command"/>.</summary>
    /// <param name="host">The machine.</param>
    /// <param name="command">The command and its arguments.</param>
    /// <param name="writable">The only folders it may write.</param>
    /// <returns>The argument vector, or null when this machine has no sandbox.</returns>
    public static IReadOnlyList<string>? Argv(SandboxHost host, IReadOnlyList<string> command, IReadOnlyList<string> writable)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(writable);
        if (host.IsMac && host.IsRootOwned(SandboxExec))
        {
            return [SandboxExec, "-p", MacProfile(host, writable), .. command];
        }

        var bwrap = host.IsLinux ? _bwrap.FirstOrDefault(host.IsRootOwned) : null;
        return bwrap is null
            ? null
            : [bwrap, "--unshare-all", "--new-session", "--die-with-parent", "--ro-bind", "/", "/", "--dev", "/dev", "--proc", "/proc", "--tmpfs", "/tmp",
                .. LinuxMasks(host), .. writable.SelectMany(w => (string[])["--bind", w, w]), .. command];
    }

    // An empty tmpfs over each credential directory and /dev/null over each credential file, at their real paths (bwrap can't mount over a missing
    // path on the read-only root, and a missing one holds no secrets). /run, /var/run and the runtime directory get a tmpfs too: a read-only bind
    // still lets connect() reach the session D-Bus or docker.sock, each a way out.
    private static List<string> LinuxMasks(SandboxHost host)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var masks = new List<string>();
        string[] runtime = ["/run", "/var/run", .. host.RuntimeDir is { } dir && !dir.StartsWith("/run/", StringComparison.Ordinal) ? [dir] : Array.Empty<string>()];
        foreach (var path in runtime.Concat(_secrets.Select(d => $"{host.Home}/{d}")))
        {
            MaskPath(host, path, seen, masks);
        }

        return masks;
    }

    private static void MaskPath(SandboxHost host, string path, HashSet<string> seen, List<string> masks)
    {
        var kind = host.Kind(path);
        var real = host.RealPath(path);
        if (kind != PathKind.Missing && seen.Add(real))
        {
            masks.AddRange(kind == PathKind.Directory ? ["--tmpfs", real] : ["--ro-bind", "/dev/null", real]);
        }
    }

    // No network, no LaunchServices opens or Apple events, no launchctl (a launchd job runs outside the sandbox); no writes except the writable
    // folders and /dev; no reads of the credential stores, each by name and by real path. In SBPL the last matching rule wins.
    private static string MacProfile(SandboxHost host, IReadOnlyList<string> writable)
    {
        static string Subpath(string path) => $"(subpath \"{path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\")";
        var home = host.RealPath(host.Home);
        var hidden = _secrets.Append("Library/Keychains").SelectMany(d => (string[])[$"{home}/{d}", host.RealPath($"{home}/{d}")]).Distinct(StringComparer.Ordinal).Select(Subpath);
        var writes = writable.Select(Subpath).Append(Subpath("/dev"));
        return "(version 1)(allow default)(deny network*)(deny lsopen)(deny appleevent-send)"
            + "(deny mach-lookup (global-name \"com.apple.coreservices.launchservicesd\"))(deny process-exec (literal \"/bin/launchctl\"))"
            + $"(deny file-write*)(allow file-write* {string.Join(' ', writes)})(deny file-read* {string.Join(' ', hidden)})";
    }
}
