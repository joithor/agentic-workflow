namespace Aw.Kernel.Testing;

/// <summary>What <see cref="LaneCap.Check"/> found in one CTRF report.</summary>
/// <param name="Tests">How many tests the report holds.</param>
/// <param name="Slowest">The slowest test's name, its seconds and its lane, or null for an empty report.</param>
/// <param name="Violations">Every test that breaks a lane rule.</param>
public sealed record LaneCapReport(int Tests, (string Test, double Seconds, string Lane)? Slowest, IReadOnlyList<LaneCapViolation> Violations)
{
    /// <summary>True when no test breaks a lane rule.</summary>
    public bool Passed => Violations.Count == 0;
}
