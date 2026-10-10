namespace Aw.Evidence.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static EvidenceSettings Read(Func<string, string?> environment)
        => new(StateDirectory: environment("AW_STATE_DIR") is { Length: > 0 } root
            ? Path.Combine(root, "evidence")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow", "evidence"));
}
