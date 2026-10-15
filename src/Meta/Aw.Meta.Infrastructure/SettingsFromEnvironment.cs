namespace Aw.Meta.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static MetaSettings Read(Func<string, string?> environment)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var state = environment("AW_STATE_DIR") is { Length: > 0 } root
            ? Path.Combine(root, "meta")
            : Path.Combine(home, ".agentic-workflow", "meta");
        return new MetaSettings(
            state,
            environment("AW_META_CONFIG") is { Length: > 0 } config ? config : Path.Combine(state, "team.json"),
            environment("AW_CLAUDE_SESSIONS_DIR") is { Length: > 0 } sessions ? sessions : Path.Combine(home, ".claude", "sessions"));
    }
}
