using Aw.BugFix.Core.Ports;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class ScriptedCheckRunnerContractTests : CheckRunnerContract
{
    protected override ICheckRunner Runner { get; } = new ScriptedCheckRunner();

    protected override string Directory => "/repo";

    protected override ICheckRunner HangingRunner { get; } = new ScriptedCheckRunner().Hangs("sleep");

    protected override IReadOnlyList<string> HangingCommand => ["sleep", "5"];
}
