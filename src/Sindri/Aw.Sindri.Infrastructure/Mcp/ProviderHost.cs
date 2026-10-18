using System.Diagnostics;
using System.Globalization;
using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Infrastructure.Mcp;

/// <summary>The real machine: PATH lookups, the providers' config files under the settings' home, and git for the tracked check.</summary>
/// <param name="settings">Where to look.</param>
internal sealed class ProviderHost(McpSettings settings) : IProviderHost
{
    public bool Installed(ProviderFile provider) => provider switch
    {
        ProviderFile.Claude => OnPath(settings.ClaudeBin),
        ProviderFile.Codex => OnPath("codex"),
        ProviderFile.Cursor => OnPath("cursor-agent") || Directory.Exists(Path.Combine(settings.Home, ".cursor")),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Not a provider"),
    };

    public string UserConfigPath(ProviderFile provider) => provider switch
    {
        ProviderFile.Claude => Path.Combine(settings.ClaudeConfigDir ?? settings.Home, ".claude.json"),
        ProviderFile.Codex => Path.Combine(settings.CodexHome ?? Path.Combine(settings.Home, ".codex"), "config.toml"),
        ProviderFile.Cursor => Path.Combine(settings.Home, ".cursor", "mcp.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Not a provider"),
    };

    public async ValueTask<string?> Read(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : null;

    public async ValueTask Write(string path, string text, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full) ?? full;
        _ = Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(full) + "." + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, text, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, full, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public async ValueTask<bool> Tracked(string directory, string relativePath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        var start = new ProcessStartInfo("git", ["-C", directory, "ls-files", "--error-unmatch", "--", relativePath])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        try
        {
            using var git = Process.Start(start);
            if (git is null)
            {
                return false;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            _ = git.StandardOutput.ReadToEndAsync(timeout.Token);
            _ = git.StandardError.ReadToEndAsync(timeout.Token);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return git.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;   // no git: nothing is tracked as far as we can tell
        }
    }

    private bool OnPath(string name) =>
        name.Contains('/', StringComparison.Ordinal)
            ? File.Exists(name)
            : settings.SearchPath.Any(directory => File.Exists(Path.Combine(directory, name)));
}
