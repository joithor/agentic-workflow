namespace Aw.Meta.Infrastructure.Processes;

/// <summary>
/// A fresh HOME and AW_STATE_DIR for one command, as scripts/ci/bash-tests.sh makes for each suite: what the real home holds for tools
/// stays reachable through explicit pointers (git identity, the .NET SDK, the NuGet and npm caches). The directory is removed on dispose.
/// </summary>
internal sealed class Sandbox : IDisposable
{
    // The hook and provider overrides the install helpers honour: a caller's must not steer a suite at real files.
    internal static readonly string[] Unset =
    [
        "CLAUDE_SETTINGS_FILE", "CLAUDE_HOOKS_DIR", "CLAUDE_DIR", "CODEX_HOOKS_FILE", "CODEX_HOME", "CURSOR_HOOKS_FILE", "AW_HOOKS_DIR",
        "AW_HOOK_ADAPTER_FLAGS", "AW_META_CONFIG", "AW_SINDRI_HOME", "CLAUDE_LOCAL_BIN", "TMUX",
    ];

    private readonly DirectoryInfo _root;

    private Sandbox(DirectoryInfo root)
    {
        _root = root;
    }

    internal static Sandbox Create()
    {
        var root = Directory.CreateTempSubdirectory("aw-meta-iso-");
        _ = root.CreateSubdirectory("home");
        _ = root.CreateSubdirectory("state");
        return new Sandbox(root);
    }

    internal Dictionary<string, string> Variables()
    {
        var realHome = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = Path.Combine(_root.FullName, "home"),
            ["AW_STATE_DIR"] = Path.Combine(_root.FullName, "state"),
        };
        if (realHome.Length == 0)
        {
            return variables;
        }

        Keep(variables, "GIT_CONFIG_GLOBAL", File.Exists(Path.Combine(realHome, ".gitconfig")) ? Path.Combine(realHome, ".gitconfig") : null);
        if (Directory.Exists(Path.Combine(realHome, ".dotnet")))
        {
            Keep(variables, "DOTNET_ROOT", Path.Combine(realHome, ".dotnet"));
            variables["PATH"] = Path.Combine(realHome, ".dotnet") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        }

        Keep(variables, "NUGET_PACKAGES", Path.Combine(realHome, ".nuget", "packages"));
        Keep(variables, "npm_config_cache", Path.Combine(realHome, ".npm"));
        return variables;
    }

    // An explicit setting in our own environment wins over the default under the real home.
    private static void Keep(Dictionary<string, string> variables, string name, string? fallback)
    {
        if ((Environment.GetEnvironmentVariable(name) ?? fallback) is { Length: > 0 } value)
        {
            variables[name] = value;
        }
    }

    public void Dispose()
    {
        try
        {
            _root.Delete(recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp directory is harmless; the command's result stands.
        }
    }
}
