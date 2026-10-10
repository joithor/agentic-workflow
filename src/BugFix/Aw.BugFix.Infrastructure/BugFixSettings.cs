namespace Aw.BugFix.Infrastructure;

/// <summary>BugFix's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where BugFix keeps its state: $AW_STATE_DIR/bugfix, or ~/.agentic-workflow/bugfix.</param>
internal sealed record BugFixSettings(string StateDirectory);
