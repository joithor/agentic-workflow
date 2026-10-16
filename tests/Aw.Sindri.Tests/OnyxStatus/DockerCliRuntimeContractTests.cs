using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Secrets;
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
            "compose" => facts.ComposeV2 ? new ToolRun(ToolOutcome.Exited, 0, facts.ComposeVersion + "\n") : new ToolRun(ToolOutcome.Exited, 1, ""),
            "info" when arguments[2] == "{{.DockerRootDir}}" => facts.DataRoot is { } root ? new ToolRun(ToolOutcome.Exited, 0, root + "\n") : new ToolRun(ToolOutcome.Exited, 1, ""),
            _ => facts.MemoryBytes is { } bytes ? new ToolRun(ToolOutcome.Exited, 0, bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n") : new ToolRun(ToolOutcome.Exited, 1, ""),
        },
    }));
}
