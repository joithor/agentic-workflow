namespace Aw.Readiness.App.Infrastructure;

/// <summary>Readiness's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Readiness keeps its state: $AW_STATE_DIR/readiness, or ~/.agentic-workflow/readiness.</param>
/// <param name="GhBinary">The gh CLI the host adapter runs: $AW_READINESS_GH, or 'gh' on PATH.</param>
/// <param name="GhSearchPath">The PATH a bare 'gh' is looked up on (only its absolute entries count).</param>
/// <param name="GhTimeout">How long one gh call may take.</param>
internal sealed record ReadinessSettings(string StateDirectory, string GhBinary, string GhSearchPath, TimeSpan GhTimeout);
