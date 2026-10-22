namespace Aw.CodeIndex.Infrastructure;

/// <summary>CodeIndex's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where CodeIndex keeps its own state: $AW_STATE_DIR/codeindex, or ~/.agentic-workflow/codeindex.</param>
/// <param name="IndexDirectory">Where the per-repo index files live (spec §4.1): $AW_SINDRI_HOME/code-index, which defaults to the state directory's sindri-app/code-index.</param>
internal sealed record CodeIndexStateSettings(string StateDirectory, string IndexDirectory);
