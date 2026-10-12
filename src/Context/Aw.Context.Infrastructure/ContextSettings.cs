namespace Aw.Context.Infrastructure;

/// <summary>Context's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Context keeps its state: $AW_STATE_DIR/context, or ~/.agentic-workflow/context.</param>
internal sealed record ContextSettings(string StateDirectory);
