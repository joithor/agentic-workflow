#:project ../../../tests/Aw.Kernel.Testing/Aw.Kernel.Testing.csproj
#:property RootNamespace=Aw.Ci.LaneCap

// The per-test lane cap (spec §4b.5) as a CI command. The check itself is Aw.Kernel.Testing's LaneCap; this file-based
// app only gives it a Main, so CI tooling never needs its own project. Run each lane with xUnit's CTRF reporter, then:
//   dotnet run scripts/ci/lane-cap/LaneCapCli.cs -- TestResults/lanes/unit.json [more reports...]
// Exit 0: every test is within its lane's cap and carries exactly one lane; 1: LANE-TEST-001/002; 2: usage or input error.
namespace Aw.Ci.LaneCap;

internal static class LaneCapCli
{
    private static int Main(string[] args) => Aw.Kernel.Testing.LaneCap.Run(args, Console.Out, Console.Error);
}
