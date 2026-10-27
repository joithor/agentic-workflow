namespace Aw.Sindri.Infrastructure.Sandbox;

#pragma warning disable MA0182 // until the watchdog (SB-3a, next builder) uses it
/// <summary>
/// The scope's loose RuntimeMaxSec (sandbox spec §3.3): systemd counts real time, parks included, so after a park the watchdog moves it out.
/// It is a backstop, never the clock.
/// </summary>
internal static class RuntimeCeiling
{
    /// <summary>Gets the next ceiling, counted from the scope's start.</summary>
    /// <param name="elapsedReal">Real time since the scope started.</param>
    /// <param name="wallLeft">Unsuspended wall time still left (the wall already holds the grace).</param>
    /// <param name="parkMax">The longest single suspension to allow for.</param>
    /// <returns>The ceiling.</returns>
    internal static TimeSpan Next(TimeSpan elapsedReal, TimeSpan wallLeft, TimeSpan parkMax) => throw new NotSupportedException();

    /// <summary>Builds the systemctl command that moves the ceiling.</summary>
    /// <param name="systemctl">The systemctl program.</param>
    /// <param name="unit">The scope unit.</param>
    /// <param name="ceiling">The new ceiling.</param>
    /// <returns>The argv.</returns>
    internal static IReadOnlyList<string> Argv(string systemctl, string unit, TimeSpan ceiling) => throw new NotSupportedException();
}
#pragma warning restore MA0182
