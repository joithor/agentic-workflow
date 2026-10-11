using Aw.Evidence.Infrastructure.Locks;

namespace Aw.Evidence.Infrastructure;

/// <summary>
/// Evidence's infrastructure settings, read from the environment (SettingsFromEnvironment). Evidence keeps no state of
/// its own: run files go where the caller's run directory points, and the locks are box-wide.
/// </summary>
/// <param name="Locks">The machine locks.</param>
/// <remarks>The deployment settings the use cases read (user agent, visual model, seed policy, app title) are EnvironmentEvidenceSettings.</remarks>
internal sealed record EvidenceSettings(LockSettings Locks);
