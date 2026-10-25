namespace Aw.Sindri.Core.Mcp;

/// <summary>The machine the registration touches: which providers are installed, and their config files (never a shell).</summary>
public interface IProviderHost
{
    /// <summary>Whether the provider's CLI (or, for Cursor, its config directory) is there.</summary>
    /// <param name="provider">Which provider.</param>
    /// <returns>True when installed.</returns>
    public bool Installed(ProviderFile provider);

    /// <summary>The provider's user-level config file (Claude's .claude.json, Codex's config.toml, Cursor's mcp.json).</summary>
    /// <param name="provider">Which provider.</param>
    /// <returns>The path.</returns>
    public string UserConfigPath(ProviderFile provider);

    /// <summary>Reads a file.</summary>
    /// <param name="path">The path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, or null when the file doesn't exist.</returns>
    public ValueTask<string?> Read(string path, CancellationToken cancellationToken);

    /// <summary>Writes a file whole (creating its directory), atomically.</summary>
    /// <param name="path">The path.</param>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    public ValueTask Write(string path, string text, CancellationToken cancellationToken);

    /// <summary>Whether git tracks a file in a checkout.</summary>
    /// <param name="directory">The checkout.</param>
    /// <param name="relativePath">The file, relative to it.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>True when tracked.</returns>
    public ValueTask<bool> Tracked(string directory, string relativePath, CancellationToken cancellationToken);
}
