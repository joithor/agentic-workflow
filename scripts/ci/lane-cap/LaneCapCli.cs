#:project ../../../tests/Aw.Kernel.Testing/Aw.Kernel.Testing.csproj
#:property RootNamespace=Aw.Ci.LaneCap

// The per-test lane cap (spec §4b.5) as a CI command. The check itself is Aw.Kernel.Testing's LaneCap; this file-based
// app only gives it a Main, so CI tooling never needs its own project. Run a lane with xUnit's CTRF reporter, then
// point lane-cap at the results directory (one report per test project; don't pass --report-xunit-ctrf-filename with
// --solution, or every project writes the same file and only the last report survives):
//   dotnet test --solution AgenticWorkflow.slnx --filter-trait "Lane=unit" --ignore-exit-code 8 \
//     --report-xunit-ctrf --results-directory TestResults/lanes/unit
//   dotnet run scripts/ci/lane-cap/LaneCapCli.cs -- TestResults/lanes/unit
// The default report names carry the OS user and host name: never commit or upload them outside CI.
// Exit 0: every test is within its lane's cap and carries exactly one lane; 1: LANE-TEST-001/002; 2: usage or input error.
namespace Aw.Ci.LaneCap;

internal static class LaneCapCli
{
    private static int Main(string[] args) => Aw.Kernel.Testing.LaneCap.Run(args, Console.Out, Console.Error);
}
