using Aw.Evidence.Infrastructure.Locks;

namespace Aw.Evidence.Infrastructure;

/// <summary>Evidence's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Evidence keeps its state: $AW_STATE_DIR/evidence, or ~/.agentic-workflow/evidence.</param>
/// <param name="Locks">The machine locks.</param>
/// <remarks>The UI_EVIDENCE_* values (user agent, app title, seed policy) are read per use through ISecretsSource.</remarks>
internal sealed record EvidenceSettings(string StateDirectory, LockSettings Locks);
