namespace Aw.Kernel.Testing;

/// <summary>
/// The per-test lane cap (spec §4b.5): each test's own time in xUnit's CTRF report ('--report-xunit-ctrf') is checked
/// against its lane's budget (<see cref="Lanes.BudgetOf"/>: unit 10 s, integration 60 s, heavy none), and every test
/// must carry exactly one known 'Lane' trait. The per-package wall clock of a lane run is the CI lane job's check; this
/// is the extra per-test cap. Run it through 'scripts/ci/lane-cap.cs' (see <see cref="Run"/>).
/// </summary>
public static class LaneCap
{
    /// <summary>A test ran longer than its lane's per-test cap.</summary>
    public const string OverCap = "LANE-TEST-001";

    /// <summary>A test has no 'Lane' trait, several, or one that isn't a known lane.</summary>
    public const string NotOneLane = "LANE-TEST-002";

    /// <summary>Checks one CTRF report.</summary>
    /// <param name="ctrfJson">The report's text.</param>
    /// <returns>The test count, the slowest test and every violation.</returns>
    /// <exception cref="InvalidDataException">The text isn't a CTRF report (no results.tests array, or a test without name or duration).</exception>
    public static LaneCapReport Check(string ctrfJson) => throw new NotSupportedException("red: LaneCap.Check");

    /// <summary>
    /// The command-line check: 'lane-cap &lt;ctrf.json&gt;...'. Prints each violation (rule id, package, test, measured
    /// seconds, limit, fix) and one summary line per report, plain text with no colour. Exit codes: 0 every report
    /// passes, 1 a test breaks a lane rule, 2 a usage or input error (missing file, not CTRF).
    /// </summary>
    /// <param name="args">The CTRF report paths.</param>
    /// <param name="output">Where results go.</param>
    /// <param name="error">Where usage and input errors go.</param>
    /// <returns>The exit code.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error) => throw new NotSupportedException("red: LaneCap.Run");
}
