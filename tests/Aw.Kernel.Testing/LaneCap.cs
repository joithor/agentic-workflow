using System.Globalization;
using System.Text.Json;

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
    public static LaneCapReport Check(string ctrfJson)
    {
        ArgumentNullException.ThrowIfNull(ctrfJson);
        using var document = Parse(ctrfJson);
        var results = Property(document.RootElement, "results", JsonValueKind.Object);
        var packages = SuitePackages(results);
        var violations = new List<LaneCapViolation>();
        (string Test, double Seconds, string Lane)? slowest = null;
        var count = 0;
        foreach (var test in Property(results, "tests", JsonValueKind.Array).EnumerateArray())
        {
            count++;
            var name = Property(test, "name", JsonValueKind.String).GetString()!; // why: ValueKind checked as String
            var seconds = Property(test, "duration", JsonValueKind.Number).GetDouble() / 1000.0;
            var package = PackageOf(test, packages);
            var lanes = LanesOf(test);
            if (lanes.Count != 1 || !TryLane(lanes[0], out var lane))
            {
                violations.Add(new(NotOneLane, package, name, NotOneLaneMessage(package, name, lanes)));
                continue;
            }

            if (slowest is null || seconds > slowest.Value.Seconds)
            {
                slowest = (name, seconds, lanes[0]);
            }

            var cap = Lanes.BudgetOf(lane);
            if (cap != Timeout.InfiniteTimeSpan && seconds > cap.TotalSeconds)
            {
                violations.Add(new(OverCap, package, name, OverCapMessage(package, name, seconds, lanes[0], cap)));
            }
        }

        return new(count, slowest, violations);
    }

    /// <summary>
    /// The command-line check: 'lane-cap &lt;ctrf.json&gt;...'. Prints each violation (rule id, package, test, measured
    /// seconds, limit, fix) and one summary line per report, plain text with no colour. Exit codes: 0 every report
    /// passes, 1 a test breaks a lane rule, 2 a usage or input error (missing file, not CTRF).
    /// </summary>
    /// <param name="args">The CTRF report paths.</param>
    /// <param name="output">Where results go.</param>
    /// <param name="error">Where usage and input errors go.</param>
    /// <returns>The exit code.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Count == 0)
        {
            error.WriteLine("usage: lane-cap <ctrf.json>...   (a report from dotnet test --report-xunit-ctrf)");
            return 2;
        }

        var failed = false;
        foreach (var path in args)
        {
            LaneCapReport report;
            try
            {
                report = Check(File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                error.WriteLine($"lane-cap: {path}: {e.Message}");
                return 2;
            }

            foreach (var violation in report.Violations)
            {
                output.WriteLine(violation.Message);
            }

            output.WriteLine(Summary(path, report));
            failed |= !report.Passed;
        }

        output.WriteLine(failed ? "FAIL: a test breaks a lane rule (spec §4b.5)" : "OK");
        return failed ? 1 : 0;
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"not JSON: {e.Message}", e);
        }
    }

    private static JsonElement Property(JsonElement element, string name, JsonValueKind kind) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == kind
            ? value
            : throw new InvalidDataException($"not a CTRF report: expected '{name}' ({kind}) in {Shorten(element)}");

    private static string Shorten(JsonElement element)
    {
        var text = element.GetRawText();
        return text.Length <= 80 ? text : string.Concat(text.AsSpan(0, 80), "…");
    }

    private static Dictionary<string, string> SuitePackages(JsonElement results)
    {
        var packages = new Dictionary<string, string>(StringComparer.Ordinal);
        if (results.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object
            && extra.TryGetProperty("suites", out var suites) && suites.ValueKind == JsonValueKind.Array)
        {
            foreach (var suite in suites.EnumerateArray())
            {
                if (suite.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                    && suite.TryGetProperty("filePath", out var file) && file.ValueKind == JsonValueKind.String)
                {
                    packages[id.GetString()!] = Path.GetFileNameWithoutExtension(file.GetString()!); // why: ValueKind checked as String
                }
            }
        }

        return packages;
    }

    private static string PackageOf(JsonElement test, Dictionary<string, string> packages) =>
        test.TryGetProperty("suite", out var suite) && suite.ValueKind == JsonValueKind.Array && suite.GetArrayLength() > 0
            && suite[0].ValueKind == JsonValueKind.String && packages.TryGetValue(suite[0].GetString()!, out var package) // why: ValueKind checked as String
            ? package
            : "unknown-package";

    private static List<string> LanesOf(JsonElement test)
    {
        if (!test.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Object
            || !labels.TryGetProperty(Lanes.Key, out var lane))
        {
            return [];
        }

        if (lane.ValueKind == JsonValueKind.Array)
        {
            return [.. lane.EnumerateArray().Select(Text)];
        }

        return [Text(lane)];
    }

    private static string Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText(); // why: ValueKind checked as String

    private static bool TryLane(string value, out TestLane lane)
    {
        lane = value switch
        {
            Lanes.Unit => TestLane.Unit,
            Lanes.Integration => TestLane.Integration,
            Lanes.Heavy => TestLane.Heavy,
            _ => default,
        };
        return lane != default;
    }

    private static string Seconds(double seconds) => seconds.ToString("0.000", CultureInfo.InvariantCulture);

    private static string OverCapMessage(string package, string test, double seconds, string lane, TimeSpan cap) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{OverCap} {package} {test}: {Seconds(seconds)} s in lane {lane}, limit {cap.TotalSeconds:0} s per test. Fix: make the test faster, or if it does real I/O move it to a slower lane ([Lane(TestLane.Integration)] or Heavy).");

    private static string NotOneLaneMessage(string package, string test, List<string> lanes) =>
        $"{NotOneLane} {package} {test}: lane traits [{string.Join(", ", lanes)}], expected exactly one of {Lanes.Unit}, {Lanes.Integration}, {Lanes.Heavy}. "
        + "Fix: put one [Lane(TestLane.<Lane>)] on the test or on its class, not both.";

    private static string Summary(string path, LaneCapReport report) =>
        report.Slowest is { } slowest
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"lane-cap {path}: {report.Tests} tests, slowest {slowest.Test} {Seconds(slowest.Seconds)} s ({slowest.Lane}), {report.Violations.Count} violation(s)")
            : string.Create(CultureInfo.InvariantCulture, $"lane-cap {path}: {report.Tests} tests, {report.Violations.Count} violation(s)");
}
