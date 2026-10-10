namespace Aw.Readiness.App.Infrastructure;

/// <summary>Reads this package's settings from the environment that Composition passes in.</summary>
internal static class SettingsFromEnvironment
{
    internal static readonly TimeSpan DefaultGhTimeout = TimeSpan.FromSeconds(30);

    internal static ReadinessSettings Read(Func<string, string?> environment)
        => new(
            StateDirectory: environment("AW_STATE_DIR") is { Length: > 0 } root
                ? Path.Combine(root, "readiness")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow", "readiness"),
            GhBinary: environment("AW_READINESS_GH") is { Length: > 0 } gh ? gh : "gh",
            GhSearchPath: "",
            GhTimeout: DefaultGhTimeout);
}
