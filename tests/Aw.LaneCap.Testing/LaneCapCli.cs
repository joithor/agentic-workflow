namespace Aw.LaneCap.Testing;

/// <summary>
/// 'dotnet run --project tests/Aw.LaneCap.Testing -- &lt;ctrf-report-or-directory&gt;...'. Run a lane with xUnit's CTRF
/// reporter into a results directory, then point this at it (one report per test project; don't pass
/// --report-xunit-ctrf-filename with --solution, or every project writes the same file and only the last survives):
///   dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8 --report-xunit-ctrf --results-directory TestResults/lanes/unit
///   dotnet run --project tests/Aw.LaneCap.Testing -- TestResults/lanes/unit
/// Exit 0: every test is within its lane's cap and carries exactly one lane; 1: LANE-TEST-001/002; 2: usage or input
/// error. The default report names carry the OS user and host name: never commit reports from a developer machine or
/// share them (on CI the names are the runner's, not personal, so CI uploads its own on failure).
/// </summary>
internal static class LaneCapCli
{
    private static int Main(string[] args) => Kernel.Testing.LaneCap.Run(args, Console.Out, Console.Error);
}
