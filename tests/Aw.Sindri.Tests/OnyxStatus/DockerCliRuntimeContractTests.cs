using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// The same contract the fake is held to, with the adapter over a runner scripted from the facts.
[Lane(TestLane.Unit)]
public sealed class DockerCliRuntimeContractTests : ContainerRuntimeContract
{
    protected override IContainerRuntime Create(ContainerRuntimeFacts facts) => new DockerCliRuntime(new FakeToolRunner((_, arguments) => facts switch
    {
        { CliFound: false } => ToolRun.Missing,
        _ => arguments[0] switch
        {
            "version" => facts.DaemonAnswers ? new ToolRun(ToolOutcome.Exited, 0, facts.ServerVersion + "\n") : new ToolRun(ToolOutcome.Exited, 1, ""),
            "context" => facts.Endpoint is null ? new ToolRun(ToolOutcome.Exited, 1, "") : new ToolRun(ToolOutcome.Exited, 0, facts.Endpoint + "\n"),
            "compose" => facts.ComposeV2 ? new ToolRun(ToolOutcome.Exited, 0, facts.ComposeVersion + "\n") : new ToolRun(ToolOutcome.Exited, 1, ""),
            _ => facts.MemoryBytes is null && facts.DataRoot is null
                ? new ToolRun(ToolOutcome.Exited, 1, "")
                : new ToolRun(ToolOutcome.Exited, 0, facts.MemoryBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + facts.DataRoot + "\n"),
        },
    }), static _ => null);
}