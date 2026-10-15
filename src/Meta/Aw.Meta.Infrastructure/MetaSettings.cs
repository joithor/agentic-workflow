namespace Aw.Meta.Infrastructure;

/// <summary>Meta's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Meta keeps its state: $AW_STATE_DIR/meta, or ~/.agentic-workflow/meta.</param>
internal sealed record MetaSettings(string StateDirectory);
