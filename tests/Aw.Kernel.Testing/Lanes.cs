namespace Aw.Kernel.Testing;

/// <summary>
/// The lane trait's key and values, and each lane's time budget (spec §4b.5). Each budget covers one package's whole
/// run of that lane, and is also the cap on each single test, which <see cref="LaneCap"/> checks from xUnit's CTRF
/// report. The per-package wall clock is the planned CI lane job's check (gap — R1b): it times
/// 'dotnet test --filter-trait Lane=&lt;lane&gt;' per test project against these values.
/// </summary>
public static class Lanes
{
    /// <summary>The trait key: 'dotnet test --filter-trait "Lane=unit"'.</summary>
    public const string Key = "Lane";

    /// <summary>The trait value of <see cref="TestLane.Unit"/>.</summary>
    public const string Unit = "unit";

    /// <summary>The trait value of <see cref="TestLane.Integration"/>.</summary>
    public const string Integration = "integration";

    /// <summary>The trait value of <see cref="TestLane.Heavy"/>.</summary>
    public const string Heavy = "heavy";

    /// <summary>The trait value for a lane.</summary>
    /// <param name="lane">The lane.</param>
    /// <returns>Its trait value.</returns>
    public static string NameOf(TestLane lane) => lane switch
    {
        TestLane.Unit => Unit,
        TestLane.Integration => Integration,
        TestLane.Heavy => Heavy,
        _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Not a test lane."),
    };

    /// <summary>
    /// The budget for one package's whole run of a lane (unit 10 s, integration 60 s; heavy has none and returns
    /// <see cref="Timeout.InfiniteTimeSpan"/>). <see cref="LaneCap"/> applies it to each test; the planned CI lane job
    /// (gap — R1b) applies it to a package's whole lane run.
    /// </summary>
    /// <param name="lane">The lane.</param>
    /// <returns>The budget.</returns>
    public static TimeSpan BudgetOf(TestLane lane) => lane switch
    {
        TestLane.Unit => TimeSpan.FromSeconds(10),
        TestLane.Integration => TimeSpan.FromSeconds(60),
        TestLane.Heavy => Timeout.InfiniteTimeSpan,
        _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Not a test lane."),
    };
}
