namespace Aw.Sindri.Infrastructure.Mcp;

/// <summary>
/// Where the MCP adapters look: the claude CLI (AW_SINDRI_CLAUDE_BIN, else 'claude' on PATH) and the providers' config
/// files under HOME (Claude Code's CLAUDE_CONFIG_DIR and Codex's CODEX_HOME honoured, as those tools honour them).
/// Tests and proofs point HOME at a scratch directory and AW_SINDRI_CLAUDE_BIN at a recording stub, so the owner's real
/// provider configs are never read or written.
/// </summary>
/// <param name="ClaudeBin">The claude executable.</param>
/// <param name="Home">The home directory.</param>
/// <param name="ClaudeConfigDir">CLAUDE_CONFIG_DIR, or null.</param>
/// <param name="CodexHome">CODEX_HOME, or null.</param>
internal sealed record McpSettings(string ClaudeBin, string Home, string? ClaudeConfigDir, string? CodexHome)
{
    /// <summary>Reads the settings from the environment Composition passes.</summary>
    /// <param name="environment">Reads a variable.</param>
    /// <returns>The settings.</returns>
    internal static McpSettings Read(Func<string, string?> environment) => new(
        environment("AW_SINDRI_CLAUDE_BIN") is { Length: > 0 } bin ? bin : "claude",
        environment("HOME") is { Length: > 0 } home ? home : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        environment("CLAUDE_CONFIG_DIR") is { Length: > 0 } claude ? claude : null,
        environment("CODEX_HOME") is { Length: > 0 } codex ? codex : null);
}
