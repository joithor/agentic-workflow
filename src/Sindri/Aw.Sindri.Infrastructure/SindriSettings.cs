namespace Aw.Sindri.Infrastructure;

/// <summary>Sindri's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Sindri keeps its state: $AW_STATE_DIR/sindri, or ~/.agentic-workflow/sindri.</param>
internal sealed record SindriSettings(string StateDirectory);
