#pragma warning disable MA0041, CA1822, MA0182 // until the watchdog (SB-3a, next builder) uses it
namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The wall clock of one run (sandbox spec §3.3): time counts only while the run was not suspended. An interval between two samples counts
/// when the run was unsuspended at both ends, so a park costs nothing and the tick that ends one does not count the park.
/// </summary>
/// <param name="wall">The unsuspended time allowed.</param>
internal sealed class WallBudget(TimeSpan wall)
{
    /// <summary>Gets the wall.</summary>
    internal TimeSpan Wall { get; } = wall;

    /// <summary>Gets the unsuspended time used so far.</summary>
    internal TimeSpan Used => throw new NotSupportedException();

    /// <summary>Gets the unsuspended time left (never below zero).</summary>
    internal TimeSpan Left => throw new NotSupportedException();

    /// <summary>Gets a value indicating whether the wall is used up.</summary>
    internal bool Exhausted => throw new NotSupportedException();

    /// <summary>Records a sample.</summary>
    /// <param name="at">When the sample was taken.</param>
    /// <param name="suspended">Whether the run was suspended then.</param>
    /// <returns>True when this sample ends a suspension (the watchdog then moves the ceiling).</returns>
    internal bool Tick(DateTimeOffset at, bool suspended) => throw new NotSupportedException();
}
#pragma warning restore MA0041, CA1822, MA0182
