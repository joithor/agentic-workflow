using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The red-check Step of the line's build station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildRedCheckStepContract : BlockContract
{
    protected override Type Block => typeof(BuildRedCheckStep);

    protected override BlockDescriptor Descriptor => BuildSteps.RedCheck;

    protected override string ExpectedHash => "sha256:77854e9a2e2639a9c6b1bc0dfc3a8855f7681efe288badff22f2487867202463";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var rig = new Workflow.BuildShip.StepRig();
        _ = await new BuildRedCheckStep(rig.Diff, rig.Runner).Run(BuildShipContexts.Of("step.build.red-check", []), cancellationToken);
    }
}
