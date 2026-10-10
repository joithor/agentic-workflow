namespace Aw.Readiness.App.Infrastructure;

/// <summary>Readiness's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Readiness keeps its state: $AW_STATE_DIR/readiness, or ~/.agentic-workflow/readiness.</param>
internal sealed record ReadinessSettings(string StateDirectory);
