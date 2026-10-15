using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Infrastructure.Mcp;

/// <summary>Reads the providers' user-wide MCP files from the settings' home. A missing file is no finding; an unreadable one says why, never what it holds.</summary>
/// <param name="settings">Where to look.</param>
internal sealed class ProviderConfigFiles(McpSettings settings) : IProviderFiles
{
    public async ValueTask<ProviderFileRead> Read(ProviderFile file, CancellationToken cancellationToken)
    {
        var path = PathOf(file);
        var shown = path.StartsWith(settings.Home.TrimEnd('/') + "/", StringComparison.Ordinal) ? "~" + path[settings.Home.TrimEnd('/').Length..] : path;
        try
        {
            return File.Exists(path)
                ? new ProviderFileRead(file, shown, await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), Problem: null)
                : new ProviderFileRead(file, shown, Text: null, Problem: null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new ProviderFileRead(file, shown, Text: null, e is UnauthorizedAccessException ? "permission denied" : "couldn't be read");
        }
    }

    private string PathOf(ProviderFile file) => file switch
    {
        ProviderFile.Claude => Path.Combine(settings.ClaudeConfigDir ?? settings.Home, ".claude.json"),
        ProviderFile.Codex => Path.Combine(settings.CodexHome ?? Path.Combine(settings.Home, ".codex"), "config.toml"),
        ProviderFile.Cursor => Path.Combine(settings.Home, ".cursor", "mcp.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(file), file, "not a provider file"),
    };
}
