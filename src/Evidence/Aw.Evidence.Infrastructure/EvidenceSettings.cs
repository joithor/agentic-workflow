using Aw.Evidence.Infrastructure.Locks;

namespace Aw.Evidence.Infrastructure;

/// <summary>Evidence's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Evidence keeps its state: $AW_STATE_DIR/evidence, or ~/.agentic-workflow/evidence.</param>
/// <param name="Locks">The machine locks.</param>
/// <remarks>The deployment settings the use cases read (user agent, visual model, seed policy, app title) are EnvironmentEvidenceSettings.</remarks>
internal sealed record EvidenceSettings(string StateDirectory, LockSettings Locks);
