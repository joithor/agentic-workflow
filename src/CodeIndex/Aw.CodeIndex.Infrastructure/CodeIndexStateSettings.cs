namespace Aw.CodeIndex.Infrastructure;

/// <summary>CodeIndex's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where CodeIndex keeps its state: $AW_STATE_DIR/codeindex, or ~/.agentic-workflow/codeindex.</param>
internal sealed record CodeIndexStateSettings(string StateDirectory);
