namespace Aw.Evidence.Infrastructure;

/// <summary>Evidence's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Evidence keeps its state: $AW_STATE_DIR/evidence, or ~/.agentic-workflow/evidence.</param>
internal sealed record EvidenceSettings(string StateDirectory);
