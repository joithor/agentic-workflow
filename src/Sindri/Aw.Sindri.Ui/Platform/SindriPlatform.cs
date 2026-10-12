using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Ui.Platform;

/// <summary>The platform adapters a head hands the app (one per port), so a new port doesn't change <see cref="Views.SindriApp.Create(SindriPlatform, TimeProvider)"/>'s signature.</summary>
/// <param name="Records">Where the control plane recorded its endpoint.</param>
/// <param name="Preferences">The local preferences.</param>
/// <param name="Motion">The OS reduce-motion setting.</param>
/// <param name="Instance">The single-instance claim and its messages.</param>
/// <param name="Tray">The tray icon.</param>
public sealed record SindriPlatform(
    IEndpointRecordSource Records,
    IPreferencesStore Preferences,
    IMotionPreference Motion,
    ISingleInstance Instance,
    ITray Tray)
{
    /// <summary>The app's log (the desktop head's writes JSON lines on standard error through Aw.Kernel.Telemetry); none by default.</summary>
    public ILogger Log { get; init; } = NullLogger.Instance;
}
