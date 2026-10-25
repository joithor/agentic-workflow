using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Testing;

/// <summary>The providers, their config files and git's tracked set in memory.</summary>
public sealed class FakeProviderHost : IProviderHost
{
    private readonly HashSet<ProviderFile> _missing = [];
    private readonly HashSet<string> _tracked = new(StringComparer.Ordinal);

    /// <summary>Every file by path.</summary>
    public IDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Paths whose write throws an IOException.</summary>
    public ISet<string> Unwritable { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Marks a provider not installed.</summary>
    /// <param name="provider">Which provider.</param>
    /// <returns>This host.</returns>
    public FakeProviderHost WithoutProvider(ProviderFile provider)
    {
        _ = _missing.Add(provider);
        return this;
    }

    /// <summary>Marks a file tracked by git.</summary>
    /// <param name="path">The full path.</param>
    /// <returns>This host.</returns>
    public FakeProviderHost WithTracked(string path)
    {
        _ = _tracked.Add(path);
        return this;
    }

    /// <inheritdoc/>
    public bool Installed(ProviderFile provider) => !_missing.Contains(provider);

    /// <inheritdoc/>
    public string UserConfigPath(ProviderFile provider) => provider switch
    {
        ProviderFile.Claude => "/home/u/.claude.json",
        ProviderFile.Codex => "/home/u/.codex/config.toml",
        ProviderFile.Cursor => "/home/u/.cursor/mcp.json",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Not a provider"),
    };

    /// <inheritdoc/>
    public ValueTask<string?> Read(string path, CancellationToken cancellationToken) => ValueTask.FromResult(Files.TryGetValue(path, out var text) ? text : null);

    /// <inheritdoc/>
    public ValueTask Write(string path, string text, CancellationToken cancellationToken)
    {
        if (Unwritable.Contains(path))
        {
            throw new IOException("disk full");
        }

        Files[path] = text;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> Tracked(string directory, string relativePath, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_tracked.Contains(Path.Combine(directory, relativePath)));
}
