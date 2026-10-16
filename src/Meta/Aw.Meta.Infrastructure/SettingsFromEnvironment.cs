using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static MetaSettings Read(Func<string, string?> environment)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = environment("AW_STATE_DIR") is { Length: > 0 } configured ? configured : Path.Combine(home, ".agentic-workflow");
        var metaRoot = Path.Combine(root, "meta");
        var (state, layout) = TeamResolver.Resolve(metaRoot, environment("AW_META_TEAM"));
        var heavy = environment("AW_HEAVY") is { Length: > 0 } wrapper ? wrapper : Path.Combine(root, "bin-r1b", "heavy");
        return new MetaSettings(
            state,
            environment("AW_META_CONFIG") is { Length: > 0 } config ? config : Path.Combine(state, "team.json"),
            environment("AW_CLAUDE_SESSIONS_DIR") is { Length: > 0 } sessions ? sessions : Path.Combine(home, ".claude", "sessions"),
            environment("AW_HEAVY_JOB_LOCK") is { Length: > 0 } heavyLock ? heavyLock : Path.Combine(root, "locks", "heavy-job.lock"),
            Path.Combine(state, "lane-sessions"),
            environment("AW_TMUX") is { Length: > 0 } tmux ? tmux : "tmux",
            environment("AW_META_TMUX_SOCKET") is { Length: > 0 } socket ? socket : null,
            new MetaPaths(state, Path.Combine(state, "gates"), File.Exists(heavy) ? heavy : null, environment("AW_GH") is { Length: > 0 } gh ? gh : "gh", home),
            layout);
    }
}
